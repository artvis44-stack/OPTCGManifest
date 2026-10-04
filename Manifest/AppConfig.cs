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

    /// <summary>
    /// Headers on every response. A full Content-Security-Policy is not here on
    /// purpose: ui.html is one file of inline script and inline onerror= handlers, so
    /// a strict policy needs a build step to extract them first. frame-ancestors is
    /// the part that works today without one.
    /// </summary>
    public static readonly (string Key, string Value)[] SecurityHeaders =
    {
        ("X-Content-Type-Options", "nosniff"),
        ("Referrer-Policy", "same-origin"),
        ("Content-Security-Policy", "frame-ancestors 'none'"),
        ("X-Frame-Options", "DENY"),
    };

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

    public string WorkerMode { get; init; } =
        Env("MANIFEST_WORKER_MODE") ?? "inline";

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

        var mode = WorkerMode.Trim().ToLowerInvariant();
        if (mode is not ("inline" or "postgres" or "redis" or "disabled"))
            errors.Add("MANIFEST_WORKER_MODE must be inline, postgres, redis, or disabled.");

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
