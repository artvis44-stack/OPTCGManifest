using System.Net.Sockets;
using Manifest.Data;

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
        AppConfig config, Database database, CancellationToken cancel)
    {
        var checks = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase)
        {
            ["database"] = DatabaseCheck(database),
            ["redis"] = await Redis(config, cancel),
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

    static async Task<Component> Redis(AppConfig config, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(config.RedisUrl))
            return new Component("not_configured");

        var uri = ParseRedis(config.RedisUrl);
        if (uri is null)
            return new Component("fail", "MANIFEST_REDIS_URL is not a valid redis URL");

        var port = uri.Port > 0 ? uri.Port : 6379;
        return await Tcp(uri.Host, port, "Redis", cancel);
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
        return config.WorkerMode.Trim().ToLowerInvariant() switch
        {
            "disabled" => new Component("disabled"),
            "inline" => new Component("ok", "inline"),
            "postgres" => checks["database"].Status == "ok"
                ? new Component("ok", "database-backed queue")
                : new Component("fail", "database is not ready"),
            "redis" => checks["redis"].Status == "ok"
                ? new Component("ok", "redis-backed queue")
                : new Component("fail", "redis is not ready"),
            _ => new Component("fail", "unknown worker mode"),
        };
    }

    static Uri? ParseRedis(string value)
    {
        var raw = value.Contains("://", StringComparison.Ordinal)
            ? value
            : "redis://" + value;
        return Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : null;
    }

    static async Task<Component> Tcp(string host, int port, string name, CancellationToken cancel)
    {
        using var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(host, port, cancel).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(2), cancel);
            return new Component("ok");
        }
        catch (Exception e) when (e is SocketException or TimeoutException
                                     or OperationCanceledException)
        {
            return new Component("fail", $"{name} connection failed: {e.Message}");
        }
    }
}
