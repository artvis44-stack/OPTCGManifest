using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Manifest;
using Manifest.Data;
using Manifest.Services;
using Manifest.Services.Jobs;
using Manifest.Tools;
using Manifest.Web;

// The catalogue and price tools are subcommands of the same binary rather than
// separate scripts, so there is one thing to build and one thing to ship.
if (args.Length > 0 && args[0] == "worker")
    return await WorkerHost.Run(args[1..]);

if (args.Length > 0
    && args[0] is "scrape" or "refresh-catalog" or "refresh-prices" or "user" or "migrate-sqlite"
                  or "enqueue" or "print")
{
    var toolPaths = new AppPaths(Cli.RootFrom(args));
    return args[0] switch
    {
        "scrape" => await CatalogScraper.Run(args[1..], toolPaths),
        "refresh-catalog" => await CatalogRefresh.Run(toolPaths),
        "refresh-prices" => await PriceRefresh.Run(toolPaths, new Database(toolPaths)),
        "user" => UserAdmin.Run(args[1..], new Database(toolPaths)),
        "migrate-sqlite" => SqliteToPostgres.Run(args[1..], toolPaths),
        "enqueue" => WorkerHost.Enqueue(args[1..], toolPaths),
        "print" => await PrintAdmin.Run(args[1..], toolPaths),
        _ => 1,
    };
}

var config = Cli.Parse(args);
if (config is null) return 0;

var paths = new AppPaths(config.Root);
var database = new Database(paths, config.DatabaseUrl);
var mailSettings = MailSettings.FromEnvironment();

// Default to this machine only. Reaching the app from a phone is what --lan is for,
// so opening the app to the whole network is a decision someone makes rather than
// the state it starts in.
foreach (var name in Net.LocalNames()) config.AllowedHosts.Add(name);

var configErrors = config.ValidateForStartup(mailSettings);
if (configErrors.Count > 0)
{
    Console.Error.WriteLine("Manifest cannot start with this configuration:");
    foreach (var startupError in configErrors) Console.Error.WriteLine($"  - {startupError}");
    return 1;
}

if (database.Initialise(config.Reseed) is { } databaseError)
{
    Console.Error.WriteLine(databaseError);
    return 1;
}

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});
builder.Logging.SetMinimumLevel(config.Verbose || config.IsProduction
    ? LogLevel.Information
    : LogLevel.Warning);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = config.UploadLimitBytes;

    var address = config.Bind == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(config.Bind);
    options.Listen(address, config.Port, listen =>
    {
        if (!config.Https) return;
        if (!File.Exists(paths.CertPem) || !File.Exists(paths.KeyPem))
            throw new FileNotFoundException("cert.pem/key.pem not found. Run ./make_cert.sh first.");
        listen.UseHttps(X509Certificate2.CreateFromPemFile(paths.CertPem, paths.KeyPem));
    });
});

builder.Services.AddManifest(config, paths, database, mailSettings);
if (config.RunsJobsInline) builder.Services.AddHostedService<JobWorker>();

var app = builder.Build();

var requestLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Manifest.Requests");
app.Use(async (ctx, next) =>
{
    var requestId = RequestId(ctx.Request.Headers["X-Request-ID"].FirstOrDefault())
                    ?? ctx.TraceIdentifier;
    ctx.TraceIdentifier = requestId;
    ctx.Response.Headers["X-Request-ID"] = requestId;

    var started = Stopwatch.GetTimestamp();
    using (requestLogger.BeginScope(new Dictionary<string, object?>
           {
               ["request_id"] = requestId,
           }))
    {
        try
        {
            await next();
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Telemetry.Request(ctx.Request.Method, RouteLabel(ctx), ctx.Response.StatusCode,
                              elapsed / 1000);
            requestLogger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms "
                + "request_id={RequestId} user_id={UserId}",
                ctx.Request.Method,
                ctx.Request.Path.Value ?? "/",
                ctx.Response.StatusCode,
                elapsed,
                requestId,
                ctx.User()?.Id);
        }
    }
});

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (Exception e) when (e is JsonException or FormatException or ArgumentException
                                   or OverflowException)
    {
        await ctx.Fail(400, "That request could not be read.", e.Message);
    }
    catch (BadHttpRequestException e) when (e.StatusCode == 413)
    {
        await ctx.Fail(413,
            $"That request is too large. The limit is {config.UploadLimitBytes / (1024 * 1024)}MB.",
            e.Message);
    }
    catch (Exception e)
    {
        await ctx.Fail(500, "Something went wrong on the server.",
                       $"{e.GetType().Name}: {e.Message}");
    }
});
app.UseMiddleware<Guard>();
app.UseMiddleware<Auth>();
Endpoints.Map(app);

var sessions = app.Services.GetRequiredService<ISessionRepository>();
sessions.PurgeExpiredSessions();

var accounts = app.Services.GetRequiredService<IUserRepository>();
var requests = app.Services.GetRequiredService<IAccessRepository>();
requests.PurgeExpiredTokens();

var metrics = await MetricsServer.Start(config, app.Services.GetRequiredService<JobQueue>(),
                                        requestLogger);

var scheme = config.Https ? "https" : "http";
var ip = Net.LanIp();

if (config.Https && !Net.CertCoversIp(paths.CertPem, ip))
{
    Console.WriteLine($"\n  ! cert.pem does not cover this machine's current address ({ip}).");
    Console.WriteLine("    That's why HTTPS fails on the phone with a certificate error.");
    Console.WriteLine("    Run ./make_cert.sh again to reissue it, then restart with --https.");
}

// With https on, also serve the setup page over plain http, or the phone cannot
// reach the page that makes https work in the first place.
WebApplication? helper = null;
if (config.Https)
{
    try
    {
        helper = SetupHelper.Build(config, paths, $"{scheme}://{ip}:{config.Port}/");
        await helper.StartAsync();
    }
    catch (Exception e)
    {
        Console.WriteLine($"  (could not open the setup helper on port {config.Port + 1}: {e.Message})");
        helper = null;
    }
}

Console.WriteLine($"\n  Manifest {AppConfig.Version} is up.");
Console.WriteLine($"  This machine : {scheme}://localhost:{config.Port}");
if (config.Bind == "127.0.0.1")
{
    Console.WriteLine("  Your phone   : not reachable — this machine only.");
    Console.WriteLine("                 Add --lan to open it to your network.");
}
else
{
    Console.WriteLine($"  Your phone   : {scheme}://{ip}:{config.Port}");
}
Console.WriteLine($"  Database     : {database.Description}");
Console.WriteLine($"  Throttling   : {app.Services.GetRequiredService<ILoginThrottle>().Description}");
Console.WriteLine($"  Card art     : {app.Services.GetRequiredService<IImageStore>().Description}");
Console.WriteLine("  Jobs         : " + config.WorkerMode switch
{
    "inline" => $"run in this process, {config.WorkerConcurrency} at a time",
    "external" => "queued here, run by `manifest worker`",
    _ => "disabled - nothing runs queued work",
} + (config.ScansAsync ? "; scans are read in the background" : ""));
var accountCount = accounts.Count();
if (accountCount == 0 && AppConfig.RegistrationOpen)
    Console.WriteLine("  Accounts     : none yet - the first one created owns any "
                      + "existing collection");
else if (accountCount == 0)
    Console.WriteLine("  Accounts     : none, and registration is closed. Run "
                      + "`manifest user add <name>` first.");
else
    Console.WriteLine($"  Accounts     : {accountCount}, "
                      + (AppConfig.RegistrationOpen
                          ? "registration open with an invite code"
                          : "registration closed (no MANIFEST_INVITE_CODE set)"));

// The request-access flow is the half of this that talks to the outside world, and
// the ways it can be half-configured are all silent at runtime - a form that files
// requests nobody is told about, or approvals whose links point at localhost. So it
// is spelled out here, at the one moment somebody is definitely reading.
if (!AppConfig.RequestAccessOpen)
{
    Console.WriteLine("  Requests     : off - set MANIFEST_ADMIN_EMAIL to let people "
                      + "ask for an account");
}
else
{
    var pending = requests.PendingCount();
    var waiting = pending == 0 ? "none waiting" : $"{pending} waiting at /admin";
    Console.WriteLine($"  Requests     : on, to {AppConfig.AdminEmail} ({waiting})");

    if (!mailSettings.Configured)
        Console.WriteLine("                 ! no SMTP configured, so mail is printed to "
                          + "this console instead");
    else
        Console.WriteLine($"                 mail via {mailSettings.Host}:{mailSettings.Port} as {mailSettings.From}");

    if (AppConfig.PublicUrl is { } url)
        Console.WriteLine($"                 links point at {url}");
    else
        Console.WriteLine("                 ! MANIFEST_PUBLIC_URL is not set, so links "
                          + "use whatever Host the request carried");
}

if (metrics is not null && config.MetricsEndpoint is { } metricsAt)
    Console.WriteLine($"  Metrics      : {MetricsServer.Describe(metricsAt)}");

if (TesseractScanner.Binary is { } binary)
    Console.WriteLine($"  Scanning     : on, local OCR ({binary})");
else if (AppConfig.ApiKey is not null)
    Console.WriteLine("  Scanning     : on, via the API (costs money per scan)");
else
    Console.WriteLine("  Scanning     : off - install tesseract for free local scanning");

if (config.Https && helper is not null)
{
    Console.WriteLine($"  Camera setup : http://{ip}:{config.Port + 1}/setup");
    Console.WriteLine("                 ^ plain http on purpose, so the phone can reach it");
    Console.WriteLine("                 before it trusts the certificate. Open it there first.");
}
else if (config.Https)
{
    Console.WriteLine($"  Camera setup : {scheme}://{ip}:{config.Port}/setup");
}
else
{
    Console.WriteLine("  Camera       : needs --https. Photo upload and typing work as-is;");
    Console.WriteLine("                 run ./make_cert.sh then --https for the live viewfinder");
}
Console.WriteLine("  Ctrl-C to stop.\n");

await app.RunAsync();
if (helper is not null) await helper.StopAsync();
if (metrics is not null) await metrics.StopAsync();
Console.WriteLine(database.SqliteFile is { } file
    ? $"\n  Stopped. Counts are saved in {Path.GetFileName(file)}"
    : "\n  Stopped. Counts are saved in PostgreSQL");
return 0;

// The route's template rather than the path, so /api/decks/17 and /api/decks/18
// are one series and a scanner walking random URLs cannot mint new ones.
static string RouteLabel(HttpContext ctx) =>
    ctx.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } pattern }
    && !pattern.StartsWith("{*", StringComparison.Ordinal)
        ? (pattern.StartsWith('/') ? pattern : "/" + pattern)
        : "unmatched";

static string? RequestId(string? value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return null;
    foreach (var c in value)
        if (!char.IsLetterOrDigit(c) && c is not '-' and not '_' and not '.' and not ':')
            return null;
    return value;
}
