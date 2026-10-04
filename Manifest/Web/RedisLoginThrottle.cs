using StackExchange.Redis;

namespace Manifest.Web;

/// <summary>
/// The sign-in throttle with its counters in Redis, so every app container counts
/// the same failures. Two keys per caller: a failure counter that expires one
/// window after the first failure, and a lock that exists while the caller has to
/// wait. Redis expires both, so there is nothing to sweep.
/// </summary>
public sealed class RedisLoginThrottle : ILoginThrottle
{
    const string Prefix = "manifest:throttle:";

    /// <summary>
    /// Count, start the window on the first failure, and lock on reaching the
    /// allowance - in one step, so two containers failing the same key at once
    /// cannot both read 7 and both decide not to lock.
    /// </summary>
    static readonly LuaScript FailScript = LuaScript.Prepare("""
        local n = redis.call('INCR', @count)
        if n == 1 then redis.call('PEXPIRE', @count, @window) end
        if n >= tonumber(@allowance) then redis.call('SET', @lock, '1', 'PX', @window) end
        return n
        """);

    readonly IConnectionMultiplexer _redis;
    readonly ILogger<RedisLoginThrottle> _log;

    public RedisLoginThrottle(IConnectionMultiplexer redis, ILogger<RedisLoginThrottle> log)
    {
        _redis = redis;
        _log = log;
    }

    public string Description =>
        $"shared via Redis at {string.Join(", ", _redis.GetEndPoints().Select(e => e.ToString()))}";

    static RedisKey Count(string key) => Prefix + key + ":n";
    static RedisKey Lock(string key) => Prefix + key + ":lock";

    // Every call fails open: with Redis unreachable, nobody can sign in at all if a
    // missing answer counts as "locked", and that is a worse outage than a brief
    // stretch without a throttle. The readiness check reports Redis as down meanwhile,
    // and each miss is logged as an error so it is not silent. A connection already
    // known to be down is not even tried, or every sign-in would sit out a timeout.

    bool Down(string key, string what)
    {
        if (_redis.IsConnected) return false;
        _log.LogError("sign-in throttle unavailable (Redis not connected); {What} for {Key}", what, key);
        return true;
    }

    public async Task<int?> RetryAfter(string key)
    {
        if (Down(key, "letting it through")) return null;
        try
        {
            var left = await _redis.GetDatabase().KeyTimeToLiveAsync(Lock(key));
            return left is { } ttl && ttl > TimeSpan.Zero ? (int)Math.Ceiling(ttl.TotalSeconds) : null;
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _log.LogError(e, "sign-in throttle unavailable; letting {Key} through", key);
            return null;
        }
    }

    public async Task Failed(string key)
    {
        if (Down(key, "failure not counted")) return;
        try
        {
            await _redis.GetDatabase().ScriptEvaluateAsync(FailScript, new
            {
                count = Count(key),
                @lock = Lock(key),
                window = (long)ILoginThrottle.Window.TotalMilliseconds,
                allowance = ILoginThrottle.Allowance,
            });
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _log.LogError(e, "sign-in throttle unavailable; failure for {Key} not counted", key);
        }
    }

    public async Task Succeeded(string key)
    {
        if (Down(key, "could not clear")) return;
        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(new[] { Count(key), Lock(key) });
        }
        catch (Exception e) when (e is RedisException or TimeoutException)
        {
            _log.LogError(e, "sign-in throttle unavailable; could not clear {Key}", key);
        }
    }

    /// <summary>
    /// redis://[:password@]host[:port][/db] or rediss:// for TLS - the form hosting
    /// providers hand out - or StackExchange.Redis's own "host:port,password=..."
    /// string for anything that form cannot say.
    /// </summary>
    public static ConfigurationOptions Options(string url)
    {
        ConfigurationOptions options;
        if (!url.Contains("://", StringComparison.Ordinal))
        {
            options = ConfigurationOptions.Parse(url);
        }
        else
        {
            var uri = new Uri(url);
            if (uri.Scheme is not ("redis" or "rediss"))
                throw new ArgumentException("MANIFEST_REDIS_URL must start with redis:// or rediss://");

            options = new ConfigurationOptions { Ssl = uri.Scheme == "rediss" };
            options.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);

            if (uri.UserInfo.Length > 0)
            {
                var parts = uri.UserInfo.Split(':', 2);
                if (parts.Length == 2)
                {
                    if (parts[0].Length > 0) options.User = Uri.UnescapeDataString(parts[0]);
                    options.Password = Uri.UnescapeDataString(parts[1]);
                }
                else
                {
                    options.Password = Uri.UnescapeDataString(parts[0]);
                }
            }

            var db = uri.AbsolutePath.Trim('/');
            if (db.Length > 0) options.DefaultDatabase = int.Parse(db);
        }

        // Start even if Redis is not up yet; the multiplexer keeps trying in the
        // background, and the throttle fails open until it gets there.
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 2000;
        options.SyncTimeout = 2000;
        options.AsyncTimeout = 2000;
        return options;
    }
}
