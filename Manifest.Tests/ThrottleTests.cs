using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Manifest.Web;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Manifest.Tests;

/// <summary>
/// The sign-in throttle, held to the same rules in memory and in Redis, and then
/// shown doing the one thing Redis is there for: two app processes counting the
/// same failures. The Redis cases need MANIFEST_TEST_REDIS (e.g.
/// redis://127.0.0.1:56379) and skip without it.
/// </summary>
public class ThrottleTests
{
    static string? RedisUrl =>
        Environment.GetEnvironmentVariable("MANIFEST_TEST_REDIS") is { Length: > 0 } url ? url : null;

    sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>A key nobody else - another test, an earlier run - has touched.</summary>
    static string FreshKey() => "test:" + Guid.NewGuid().ToString("N");

    static RedisLoginThrottle Redis(string url) =>
        new(ConnectionMultiplexer.Connect(RedisLoginThrottle.Options(url)),
            NullLogger<RedisLoginThrottle>.Instance);

    static async Task LocksAfterTheAllowanceAndSuccessClears(ILoginThrottle throttle)
    {
        var key = FreshKey();
        for (var i = 0; i < ILoginThrottle.Allowance - 1; i++) await throttle.Failed(key);
        Assert.Null(await throttle.RetryAfter(key));

        await throttle.Failed(key);
        var wait = await throttle.RetryAfter(key);
        Assert.NotNull(wait);
        Assert.InRange(wait!.Value, 1, (int)ILoginThrottle.Window.TotalSeconds);

        Assert.Null(await throttle.RetryAfter(FreshKey()));   // others are unaffected

        await throttle.Succeeded(key);
        Assert.Null(await throttle.RetryAfter(key));
    }

    [Fact]
    public Task MemoryLocksAfterTheAllowance() =>
        LocksAfterTheAllowanceAndSuccessClears(new MemoryLoginThrottle(new FakeClock()));

    [SkippableFact]
    public Task RedisLocksAfterTheAllowance()
    {
        Skip.If(RedisUrl is null, "MANIFEST_TEST_REDIS is not set");
        return LocksAfterTheAllowanceAndSuccessClears(Redis(RedisUrl!));
    }

    [Fact]
    public async Task MemoryForgetsFailuresOlderThanTheWindow()
    {
        var clock = new FakeClock();
        var throttle = new MemoryLoginThrottle(clock);
        var key = FreshKey();

        for (var i = 0; i < ILoginThrottle.Allowance - 1; i++) await throttle.Failed(key);
        clock.UtcNow += ILoginThrottle.Window + TimeSpan.FromSeconds(1);
        await throttle.Failed(key);

        Assert.Null(await throttle.RetryAfter(key));
    }

    [SkippableFact]
    public async Task TwoConnectionsCountTheSameFailures()
    {
        Skip.If(RedisUrl is null, "MANIFEST_TEST_REDIS is not set");
        ILoginThrottle a = Redis(RedisUrl!), b = Redis(RedisUrl!);
        var key = FreshKey();

        for (var i = 0; i < ILoginThrottle.Allowance; i++)
            await (i % 2 == 0 ? a : b).Failed(key);

        Assert.NotNull(await a.RetryAfter(key));
        Assert.NotNull(await b.RetryAfter(key));
    }

    /// <summary>
    /// Nothing listens on port 1. An unreachable Redis must not lock everyone out of
    /// signing in, and must not turn a sign-in into an exception.
    /// </summary>
    [Fact]
    public async Task FailsOpenWhenRedisIsUnreachable()
    {
        ILoginThrottle throttle = Redis("redis://127.0.0.1:1");
        var key = FreshKey();
        for (var i = 0; i < ILoginThrottle.Allowance; i++) await throttle.Failed(key);
        Assert.Null(await throttle.RetryAfter(key));
        await throttle.Succeeded(key);
    }

    [Theory]
    [InlineData("redis://127.0.0.1:6380/2", "127.0.0.1:6380", null, null, 2, false)]
    [InlineData("rediss://:s%40cret@cache.internal", "cache.internal:6379", null, "s@cret", null, true)]
    [InlineData("redis://app:pw@cache:6379", "cache:6379", "app", "pw", null, false)]
    public void ReadsRedisUrls(string url, string endpoint, string? user, string? password,
                               int? db, bool ssl)
    {
        var o = RedisLoginThrottle.Options(url);
        Assert.Equal(endpoint, Assert.Single(o.EndPoints).ToString()!.Replace("Unspecified/", ""));
        Assert.Equal(user, o.User);
        Assert.Equal(password, o.Password);
        Assert.Equal(db, o.DefaultDatabase);
        Assert.Equal(ssl, o.Ssl);
    }

    /// <summary>
    /// The failure this stage exists to fix: behind a load balancer, each container
    /// used to keep its own count, so N containers meant N times the guesses. Two
    /// real servers on one Redis, the wrong password sent alternately to each.
    /// </summary>
    [SkippableFact]
    public async Task TwoServersShareOneAllowance()
    {
        Skip.If(RedisUrl is null, "MANIFEST_TEST_REDIS is not set");

        var work = Directory.CreateTempSubdirectory("manifest-throttle-").FullName;
        foreach (var f in new[] { "catalog.json" })
            File.Copy(Path.Combine(ServerFixture.RepoRoot, f), Path.Combine(work, f));

        // Different ports, and the same database file: two containers behind one proxy.
        var ports = new[] { 8491, 8492 };
        var servers = new List<Process>();
        try
        {
            foreach (var port in ports)
            {
                var info = new ProcessStartInfo(
                    Path.Combine(ServerFixture.RepoRoot, "Manifest", "bin", "Debug", "net8.0", "manifest"))
                {
                    WorkingDirectory = work,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (var a in new[] { "--port", port.ToString(), "--root", work, "--behind-proxy" })
                    info.ArgumentList.Add(a);
                info.Environment["MANIFEST_REDIS_URL"] = RedisUrl;
                info.Environment.Remove("MANIFEST_DATABASE_URL");
                info.Environment.Remove("DATABASE_URL");
                servers.Add(Process.Start(info)!);
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            foreach (var port in ports) await WaitFor(http, port);

            // A made-up client address, so no earlier run's counters can be in the way.
            var ip = $"10.{Random.Shared.Next(256)}.{Random.Shared.Next(256)}.{Random.Shared.Next(1, 255)}";

            async Task<HttpStatusCode> WrongPassword(int port)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"http://127.0.0.1:{port}/api/auth/login")
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { username = "nobody", password = "wrong-password" }),
                        Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-Forwarded-For", ip);
                return (await http.SendAsync(request)).StatusCode;
            }

            for (var i = 0; i < ILoginThrottle.Allowance; i++)
                Assert.Equal(HttpStatusCode.Unauthorized, await WrongPassword(ports[i % 2]));

            // Each server has seen only half the allowance, and both refuse.
            Assert.Equal(HttpStatusCode.TooManyRequests, await WrongPassword(ports[0]));
            Assert.Equal(HttpStatusCode.TooManyRequests, await WrongPassword(ports[1]));
        }
        finally
        {
            foreach (var s in servers)
                try { s.Kill(entireProcessTree: true); s.WaitForExit(10_000); } catch { /* gone */ }
            try { Directory.Delete(work, recursive: true); } catch { /* best effort */ }
        }
    }

    static async Task WaitFor(HttpClient http, int port)
    {
        for (var i = 0; i < 120; i++)
        {
            try
            {
                if ((await http.GetAsync($"http://127.0.0.1:{port}/api/health")).IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }
            await Task.Delay(500);
        }
        throw new TimeoutException($"server on {port} never came up");
    }
}
