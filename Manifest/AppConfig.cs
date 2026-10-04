using Manifest.Services;

namespace Manifest;

/// <summary>Parsed command line, plus the process-wide bits the handlers read.</summary>
public sealed class AppConfig
{
    public const string Version = "1.4";
    public const int DeckSize = 50;
    public const int MaxCopies = 4;

    /// <summary>
    /// A phone's scan payload is six corner crops plus one shrunk JPEG - about 2MB of
    /// base64 in the worst case measured. 8MB leaves generous headroom while stopping
    /// an unauthenticated caller from making the server buffer arbitrary memory.
    /// </summary>
    public const long DefaultMaxBody = 8 * 1024 * 1024;
    public const long MaxBody = DefaultMaxBody;

    public int Port { get; init; } = 8420;
    public string Bind { get; init; } = "127.0.0.1";
    public bool Https { get; init; }
    public bool Reseed { get; init; }
    public bool Verbose { get; init; }
    public string? Root { get; init; }

    /// <summary>
    /// Host header values this server will answer to. Anything else is refused, which
    /// is what stops DNS rebinding: an attacker's domain can be pointed at this
    /// machine's LAN address, but the browser still sends their hostname in Host, and
    /// a name we never bound to is not us. localhost always works so a bare run needs
    /// no configuration.
    /// </summary>
    public HashSet<string> AllowedHosts { get; } =
        new(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1", "::1" };

    /// <summary>Headers on every response, beside the Content-Security-Policy.</summary>
    public static readonly (string Key, string Value)[] SecurityHeaders =
    {
        ("X-Content-Type-Options", "nosniff"),
        ("Referrer-Policy", "same-origin"),
        ("X-Frame-Options", "DENY"),
    };

    /// <summary>
    /// Scripts and styles only from this origin, plus - for the few pages the server
    /// writes itself, with their script inline - the ones carrying this response's
    /// nonce, which an injected tag cannot know. No inline event handlers and no
    /// style="" attributes anywhere; images from here and from data:/blob: URLs the
    /// scanner makes; nothing may frame the app.
    /// </summary>
    public static string ContentSecurityPolicy(string nonce) =>
        "default-src 'none'; "
        + $"script-src 'self' 'nonce-{nonce}'; "
        + $"style-src 'self' 'nonce-{nonce}'; "
        + "img-src 'self' data: blob:; "
        + "connect-src 'self'; "
        + "media-src 'self' blob:; "
        + "font-src 'self'; "
        + "manifest-src 'self'; "
        + "form-action 'self'; "
        + "base-uri 'none'; "
        + "object-src 'none'; "
        + "frame-ancestors 'none'";

    /// <summary>
    /// Set when something else terminates TLS in front of this - Caddy, nginx, a
    /// load balancer. It makes the forwarded client address trustworthy enough to
    /// rate-limit sign-ins by, which it is not when anyone can send the header
    /// directly. Off by default, because a wrong guess here is a bypass.
    /// </summary>
    public bool BehindProxy { get; init; }

    public string EnvironmentName { get; init; } =
        Env("MANIFEST_ENV") ?? "Development";

    public bool IsProduction =>
        string.Equals(EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase);

    public long UploadLimitBytes { get; init; } = ReadUploadLimitBytes();

    public string DatabaseUrl { get; init; } = DatabaseUrlFromEnvironment();

    /// <summary>
    /// sqlite://manifest.db unless told otherwise, so a bare run on one machine still
    /// needs no configuration. The subcommands read this too, which is why it is not
    /// only an instance property.
    /// </summary>
    public static string DatabaseUrlFromEnvironment() =>
        Env("MANIFEST_DATABASE_URL") ?? Env("DATABASE_URL") ?? "sqlite://manifest.db";

    public string? RedisUrl { get; init; } =
        Env("MANIFEST_REDIS_URL") ?? Env("REDIS_URL");

    public string? ObjectStorageEndpoint { get; init; } =
        Env("MANIFEST_OBJECT_STORAGE_ENDPOINT") ?? Env("S3_ENDPOINT");

    public string? ObjectStorageBucket { get; init; } =
        Env("MANIFEST_OBJECT_STORAGE_BUCKET") ?? Env("S3_BUCKET");

    public string? ObjectStorageAccessKey { get; init; } =
        Env("MANIFEST_OBJECT_STORAGE_ACCESS_KEY") ?? Env("AWS_ACCESS_KEY_ID");

    public string? ObjectStorageSecretKey { get; init; } =
        Env("MANIFEST_OBJECT_STORAGE_SECRET_KEY") ?? Env("AWS_SECRET_ACCESS_KEY");

    /// <summary>
    /// Who runs background jobs. inline: this web process, the single-machine
    /// default. external: separate `manifest worker` processes, so the site only
    /// queues work ("postgres" is accepted as an older name for it). disabled:
    /// nobody, and queued work waits.
    /// </summary>
    public string WorkerMode { get; init; } =
        (Env("MANIFEST_WORKER_MODE") ?? "inline").Trim().ToLowerInvariant() switch
        {
            "postgres" => "external",
            var m => m,
        };

    public bool RunsJobsInline => WorkerMode == "inline";

    /// <summary>
    /// Jobs one worker process runs at once. Most are waits on another server - the
    /// card site, SMTP - so four keeps a screenful of new card art arriving about as
    /// fast as fetching it inside each request used to.
    /// </summary>
    public int WorkerConcurrency { get; init; } =
        int.TryParse(Env("MANIFEST_WORKER_CONCURRENCY"), out var n) && n > 0 ? Math.Min(n, 32) : 4;

    public TimeSpan? RefreshPricesEvery { get; init; } = Hours("MANIFEST_REFRESH_PRICES_HOURS");
    public TimeSpan? RefreshCatalogEvery { get; init; } = Hours("MANIFEST_REFRESH_CATALOG_HOURS");

    /// <summary>
    /// sync: a scan is read inside the request, as it always was. async: the request
    /// queues it and the page polls for the answer, so a slow OCR run never ties up
    /// a web worker. Async by default in Production, sync otherwise.
    /// </summary>
    public string ScanMode { get; init; } = (Env("MANIFEST_SCAN_MODE") ?? "").Trim().ToLowerInvariant();

    public bool ScansAsync => ScanMode switch
    {
        "async" => true,
        "sync" => false,
        _ => IsProduction,
    };

    public string ObjectStorageRegion { get; init; } =
        Env("MANIFEST_OBJECT_STORAGE_REGION") ?? Env("AWS_REGION") ?? "us-east-1";

    /// <summary>
    /// bucket.host/key or host/bucket/key. Path style is what MinIO and most
    /// self-hosted stores want, so it is the default; AWS itself prefers virtual.
    /// </summary>
    public bool ObjectStoragePathStyle { get; init; } =
        !string.Equals(Env("MANIFEST_OBJECT_STORAGE_PATH_STYLE"), "false", StringComparison.OrdinalIgnoreCase);

    public bool ObjectStorageConfigured =>
        !string.IsNullOrWhiteSpace(ObjectStorageEndpoint)
        && !string.IsNullOrWhiteSpace(ObjectStorageBucket);

    public static string? ApiKey => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    /// <summary>
    /// The code a new account has to quote. Unset means nobody can register at all
    /// and accounts are made with `manifest user add` on the server instead - which
    /// is the right state to leave a server in once everyone who needs an account
    /// has one.
    /// </summary>
    public static string? InviteCode
    {
        get
        {
            var code = Environment.GetEnvironmentVariable("MANIFEST_INVITE_CODE");
            return string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        }
    }

    public static bool RegistrationOpen => InviteCode is not null;

    /// <summary>
    /// Where this server is reachable from outside, e.g. https://manifest.example.com.
    /// It is what the links in outgoing mail are built from, and it has to be set
    /// explicitly because the Host header on the request that triggered the mail is
    /// whatever the sender wrote there - trusting it would let a stranger post a
    /// request that mails the admin a link to the stranger's own machine.
    /// </summary>
    public static string? PublicUrl
    {
        get
        {
            var url = Environment.GetEnvironmentVariable("MANIFEST_PUBLIC_URL");
            if (string.IsNullOrWhiteSpace(url)) return null;
            return url.Trim().TrimEnd('/');
        }
    }

    /// <summary>
    /// The one switch for the request-access flow: an address for the requests to go
    /// to. Unset means the "Ask for access" half of the sign-in page is not there at
    /// all, which is the right state for a server on a home network.
    /// </summary>
    public static string? AdminEmail
    {
        get
        {
            var to = Environment.GetEnvironmentVariable("MANIFEST_ADMIN_EMAIL");
            return string.IsNullOrWhiteSpace(to) ? null : to.Trim();
        }
    }

    public static bool RequestAccessOpen => AdminEmail is not null;

    public IReadOnlyList<string> ValidateForStartup(MailSettings mail)
    {
        var errors = new List<string>();

        if (!string.Equals(EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase))
            errors.Add("MANIFEST_ENV must be Development or Production.");

        if (Env("MANIFEST_UPLOAD_LIMIT_BYTES") is { } rawBytes
            && !long.TryParse(rawBytes, out _))
            errors.Add("MANIFEST_UPLOAD_LIMIT_BYTES must be an integer byte count.");

        if (Env("MANIFEST_UPLOAD_LIMIT_MB") is { } rawMb
            && !long.TryParse(rawMb, out _))
            errors.Add("MANIFEST_UPLOAD_LIMIT_MB must be an integer megabyte count.");

        if (UploadLimitBytes < 1024)
            errors.Add("MANIFEST_UPLOAD_LIMIT_BYTES must be at least 1024.");

        if (Data.Database.RejectUrl(DatabaseUrl) is { } badUrl)
            errors.Add(badUrl);

        if (RedisUrl is { } redis)
        {
            try { Web.RedisLoginThrottle.Options(redis); }
            catch (Exception e) when (e is ArgumentException or FormatException
                                         or UriFormatException or OverflowException)
            {
                errors.Add($"MANIFEST_REDIS_URL could not be read: {e.Message}");
            }
        }

        if (WorkerMode is not ("inline" or "external" or "disabled"))
            errors.Add("MANIFEST_WORKER_MODE must be inline, external or disabled.");

        if (ScanMode is not ("" or "sync" or "async"))
            errors.Add("MANIFEST_SCAN_MODE must be sync or async.");

        if (ScansAsync && WorkerMode == "disabled")
            errors.Add("Asynchronous scans need a worker: set MANIFEST_WORKER_MODE to inline or "
                       + "external, or MANIFEST_SCAN_MODE=sync.");

        foreach (var name in new[] { "MANIFEST_REFRESH_PRICES_HOURS", "MANIFEST_REFRESH_CATALOG_HOURS" })
            if (Env(name) is { } raw && !(double.TryParse(raw, out var h) && h > 0))
                errors.Add($"{name} must be a positive number of hours.");

        if (!IsProduction) return errors;

        if (PublicUrl is not { } url)
        {
            errors.Add("MANIFEST_PUBLIC_URL is required when MANIFEST_ENV=Production.");
        }
        else if (!Uri.TryCreate(url, UriKind.Absolute, out var publicUri)
                 || publicUri.Scheme != Uri.UriSchemeHttps
                 || string.IsNullOrWhiteSpace(publicUri.Host))
        {
            errors.Add("MANIFEST_PUBLIC_URL must be an absolute https URL in Production.");
        }

        if (!Https && !BehindProxy)
            errors.Add("Production must use --https or --behind-proxy with HTTPS terminated upstream.");

        if (RegistrationOpen && InviteCode is { Length: < 16 })
            errors.Add("MANIFEST_INVITE_CODE must be at least 16 characters in Production.");

        if (RequestAccessOpen && !mail.Configured)
            errors.Add("SMTP must be configured when MANIFEST_ADMIN_EMAIL is set in Production.");

        return errors;
    }

    static TimeSpan? Hours(string name) =>
        double.TryParse(Env(name), out var h) && h > 0 ? TimeSpan.FromHours(h) : null;

    static long ReadUploadLimitBytes()
    {
        if (long.TryParse(Env("MANIFEST_UPLOAD_LIMIT_BYTES"), out var bytes))
            return bytes;

        if (long.TryParse(Env("MANIFEST_UPLOAD_LIMIT_MB"), out var mb))
            return mb * 1024 * 1024;

        return DefaultMaxBody;
    }

    static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
