using Manifest.Data;
using StackExchange.Redis;

namespace Manifest.Web;

public static class HealthChecks
{
    public sealed record Component(string Status, string? Detail = null);

    public sealed record ReadyResponse(
        bool Ok,
        string Version,
        string Environment,
        IReadOnlyDictionary<string, Component> Checks);

    public static async Task<(int Status, ReadyResponse Body)> Ready(
        AppConfig config, Database database, IConnectionMultiplexer? redis,
        CancellationToken cancel)
    {
        var checks = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase)
        {
            ["database"] = DatabaseCheck(database),
            ["redis"] = await Redis(config, redis),
            ["object_storage"] = await ObjectStorage(config, cancel),
        };

        checks["worker_queue"] = WorkerQueue(config, checks);

        var ok = checks.Values.All(c => c.Status is "ok" or "not_configured" or "disabled");
        if (config.IsProduction)
            ok = checks.Values.All(c => c.Status is "ok" or "disabled");

        return (ok ? 200 : 503, new ReadyResponse(
            ok, AppConfig.Version, config.EnvironmentName, checks));
    }

    static Component DatabaseCheck(Database database)
    {
        try
        {
            using var conn = database.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1
                ? new Component("ok", database.Dialect.Name)
                : new Component("fail", "unexpected database probe result");
        }
        catch (Exception e)
        {
            return new Component("fail", $"{e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>
    /// A PING through the connection the app actually uses, so a wrong password or
    /// database number shows up here and not only as throttle errors in the log.
    /// </summary>
    static async Task<Component> Redis(AppConfig config, IConnectionMultiplexer? redis)
    {
        if (string.IsNullOrWhiteSpace(config.RedisUrl) || redis is null)
            return new Component("not_configured");

        try
        {
            var rtt = await redis.GetDatabase().PingAsync();
            return new Component("ok", $"{rtt.TotalMilliseconds:0.0} ms");
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            return new Component("fail", $"Redis: {e.Message}");
        }
    }

    static async Task<Component> ObjectStorage(AppConfig config, CancellationToken cancel)
    {
        if (!config.ObjectStorageConfigured)
            return new Component("not_configured");

        if (!Uri.TryCreate(config.ObjectStorageEndpoint, UriKind.Absolute, out var endpoint))
            return new Component("fail", "MANIFEST_OBJECT_STORAGE_ENDPOINT is not a valid URL");

        var bucket = Uri.EscapeDataString(config.ObjectStorageBucket!);
        var bucketUri = new Uri(endpoint.ToString().TrimEnd('/') + "/" + bucket + "/");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        using var request = new HttpRequestMessage(HttpMethod.Head, bucketUri);

        try
        {
            using var response = await http.SendAsync(request, cancel);
            return (int)response.StatusCode < 500
                ? new Component("ok", $"HTTP {(int)response.StatusCode}")
                : new Component("fail", $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                     or OperationCanceledException)
        {
            return new Component("fail", $"{e.GetType().Name}: {e.Message}");
        }
    }

    static Component WorkerQueue(AppConfig config, IReadOnlyDictionary<string, Component> checks)
    {
        if (config.WorkerMode == "disabled") return new Component("disabled");
        return checks["database"].Status == "ok"
            ? new Component("ok", $"{config.WorkerMode}, database-backed queue")
            : new Component("fail", "database is not ready");
    }
}
