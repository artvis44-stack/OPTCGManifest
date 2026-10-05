namespace Manifest;

public static class Cli
{
    const string Usage = """
        Manifest — a self-hosted One Piece TCG collection counter.

          --port N           port to listen on (default 8420)
          --host ADDRESS     address to bind (default 127.0.0.1; --lan opens it up)
          --lan              reachable from your phone: bind every interface
          --allow-host NAME  extra Host header to accept, e.g. your public domain
                             (repeatable; MANIFEST_HOSTS takes a comma-separated list)
          --behind-proxy     trust X-Forwarded-For/Proto, when Caddy or nginx is in front
          --https            serve TLS using cert.pem/key.pem (needed for the camera)
          --reseed           rebuild catalog from catalog.json
          --root PATH        where catalog.json and (with SQLite) manifest.db live
          --verbose          log every request
          -h, --help         this text

        Subcommands:
          user               add, list, delete accounts and change passwords
          scrape             rebuild catalog.json from the official card site
                             (--new: only add sets it does not have yet)
          refresh-catalog    rebuild catalog.json from the vegapull-records dataset
          refresh-prices     pull market prices into the database
          migrate-sqlite     copy a manifest.db into PostgreSQL, once
          worker             run background jobs (with MANIFEST_WORKER_MODE=external)
          enqueue            queue scrape-catalog, refresh-prices, refresh-catalog
                             or purge now

        Environment:
          MANIFEST_ENV          Development or Production (default Development)
          MANIFEST_INVITE_CODE  the code new accounts must quote; unset closes
                                registration and leaves `manifest user add` as the
                                only way in
          MANIFEST_PUBLIC_URL   canonical public https URL, used in mailed links
          MANIFEST_HOSTS        extra Host headers to accept, comma-separated
          MANIFEST_DATABASE_URL where the data lives: sqlite://manifest.db (the
                                default, relative to --root) or
                                postgres://user:pass@host:5432/db?sslmode=require
          MANIFEST_REDIS_URL    redis://[:password@]host:6379 (rediss:// for TLS);
                                when set, sign-in throttling is shared by every
                                app container instead of kept per process
          MANIFEST_OBJECT_STORAGE_ENDPOINT, _BUCKET, _ACCESS_KEY, _SECRET_KEY
                                an S3-compatible bucket for card art in place of
                                img-cache/ (also _REGION; _PATH_STYLE=false for AWS)
          MANIFEST_WORKER_MODE  inline (jobs run in this process, the default),
                                external (run by `manifest worker`), or disabled
          MANIFEST_WORKER_CONCURRENCY
                                jobs one process runs at once (default 4)
          MANIFEST_SCAN_MODE    sync or async (default: async in Production)
          MANIFEST_SCRAPE_CATALOG_HOURS, MANIFEST_REFRESH_PRICES_HOURS
                                how often new sets and prices are fetched
                                (default 24; off to never)
          MANIFEST_REFRESH_CATALOG_HOURS
                                rebuild from the GitHub dataset on a schedule;
                                unset means never
          MANIFEST_WEB_ROOT     serve the front end from this folder instead of
                                the copy built into the binary, for editing it
          MANIFEST_UPLOAD_LIMIT_BYTES
                                max request body size (default 8388608)
          ANTHROPIC_API_KEY     optional paid fallback for photos OCR cannot read
        """;

    /// <summary>The --root value if one was given, for the subcommands.</summary>
    public static string? RootFrom(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--root") return args[i + 1];
        return null;
    }

    /// <summary>Parsed options, or null when help was printed and the process should stop.</summary>
    public static AppConfig? Parse(string[] args)
    {
        var port = 8420;
        string? host = null;
        var lan = false;
        var https = false;
        var reseed = false;
        var verbose = false;
        var behindProxy = false;
        string? root = null;
        var allowHosts = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    Console.WriteLine(Usage);
                    return null;
                case "--port":
                    port = int.Parse(args[++i]);
                    break;
                case "--host":
                    host = args[++i];
                    break;
                case "--allow-host":
                    allowHosts.Add(args[++i]);
                    break;
                case "--root":
                    root = args[++i];
                    break;
                case "--lan":
                    lan = true;
                    break;
                case "--https":
                    https = true;
                    break;
                case "--reseed":
                    reseed = true;
                    break;
                case "--behind-proxy":
                    behindProxy = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                default:
                    Console.Error.WriteLine($"unknown option: {args[i]}\n");
                    Console.Error.WriteLine(Usage);
                    Environment.Exit(2);
                    break;
            }
        }

        var config = new AppConfig
        {
            Port = port,
            Bind = host ?? (lan ? "0.0.0.0" : "127.0.0.1"),
            Https = https,
            Reseed = reseed,
            Verbose = verbose,
            BehindProxy = behindProxy,
            Root = root,
        };
        foreach (var h in allowHosts) config.AllowedHosts.Add(h);

        // A container is configured with environment variables far more naturally
        // than with a command line, and the public domain is the one thing that has
        // to be set for a deployment to answer at all.
        var fromEnv = Environment.GetEnvironmentVariable("MANIFEST_HOSTS") ?? "";
        foreach (var h in fromEnv.Split(',', StringSplitOptions.RemoveEmptyEntries
                                             | StringSplitOptions.TrimEntries))
            config.AllowedHosts.Add(h);

        if (AppConfig.PublicUrl is { } url
            && Uri.TryCreate(url, UriKind.Absolute, out var publicUri)
            && publicUri.Host.Length > 0)
            config.AllowedHosts.Add(publicUri.Host);

        return config;
    }
}
