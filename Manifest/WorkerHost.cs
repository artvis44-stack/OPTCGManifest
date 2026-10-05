using Manifest.Data;
using Manifest.Services;
using Manifest.Services.Jobs;
using Manifest.Web;

namespace Manifest;

/// <summary>
/// `manifest worker`: the job runner on its own, with no web server, for when
/// MANIFEST_WORKER_MODE=external. Run as many as the queue needs; they share it.
/// </summary>
public static class WorkerHost
{
    const string Usage = """
        manifest worker [--root PATH] [--verbose]

        Runs background jobs - card art, mail, scans, refreshes, housekeeping - from
        the queue in the database named by MANIFEST_DATABASE_URL, until stopped.
        MANIFEST_WORKER_CONCURRENCY sets how many run at once (default 4).

        manifest enqueue <refresh-prices|refresh-catalog|purge>

        Queues one of the occasional jobs now, for whichever worker is running.
        """;

    public static async Task<int> Run(string[] args)
    {
        var verbose = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--root" when i + 1 < args.Length: i++; break;
                case "--verbose": verbose = true; break;
                case "-h" or "--help": Console.WriteLine(Usage); return 0;
                default:
                    Console.Error.WriteLine($"unknown option: {args[i]}\n\n{Usage}");
                    return 2;
            }
        }

        var config = new AppConfig { Root = Cli.RootFrom(args), Verbose = verbose };
        if ((Database.RejectUrl(config.DatabaseUrl) ?? config.ProductionDatabaseError()) is { } badUrl)
        {
            Console.Error.WriteLine(badUrl);
            return 1;
        }
        if (config.MetricsListen is not null && config.MetricsEndpoint is null)
        {
            Console.Error.WriteLine("MANIFEST_METRICS_LISTEN must be a port or address:port, e.g. 9464 or 0.0.0.0:9464.");
            return 1;
        }
        var paths = new AppPaths(config.Root);
        var database = new Database(paths, config.DatabaseUrl);
        if (database.Initialise(false) is { } error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.IncludeScopes = true;
            o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Services.AddManifest(config, paths, database, MailSettings.FromEnvironment());
        builder.Services.AddHostedService<JobWorker>();
        var host = builder.Build();
        var metrics = await MetricsServer.Start(config, host.Services.GetRequiredService<JobQueue>(),
                                                host.Services.GetRequiredService<ILoggerFactory>()
                                                    .CreateLogger("Manifest.Metrics"));

        Console.WriteLine($"\n  Manifest {AppConfig.Version} worker is up.");
        Console.WriteLine($"  Database     : {database.Description}");
        Console.WriteLine($"  Card art     : {host.Services.GetRequiredService<IImageStore>().Description}");
        Console.WriteLine($"  At a time    : {config.WorkerConcurrency}");
        if (metrics is not null && config.MetricsEndpoint is { } metricsAt)
            Console.WriteLine($"  Metrics      : {MetricsServer.Describe(metricsAt)}");
        if (config.WorkerMode == "inline")
            Console.WriteLine("  Note         : MANIFEST_WORKER_MODE is inline, so the web process runs "
                              + "jobs too. That works - they share the queue - but set it to "
                              + "external if this worker is meant to be the only one.");
        Console.WriteLine("  Ctrl-C to stop.\n");

        await host.RunAsync();
        if (metrics is not null) await metrics.StopAsync();
        return 0;
    }

    public static int Enqueue(string[] args, AppPaths paths)
    {
        var types = args.FirstOrDefault() switch
        {
            "refresh-prices" => new[] { JobTypes.RefreshPrices },
            "refresh-catalog" => new[] { JobTypes.RefreshCatalog },
            "purge" => new[] { JobTypes.PurgeExpiredSessions, JobTypes.PurgeExpiredAccessTokens,
                               JobTypes.PurgeFinishedJobs },
            _ => null,
        };
        if (types is null)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var database = new Database(paths);
        database.EnsureSchema();
        var queue = new JobQueue(database, SystemClock.Instance);
        foreach (var type in types)
            Console.WriteLine($"queued {type} as job {queue.Enqueue(type, new { }, dedupeKey: "manual:" + type)}");
        return 0;
    }
}
