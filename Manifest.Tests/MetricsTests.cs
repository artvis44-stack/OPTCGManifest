using System.Diagnostics;
using System.Net;
using System.Text;
using Manifest.Services;

namespace Manifest.Tests;

/// <summary>
/// /metrics on MANIFEST_METRICS_LISTEN: what it reports after real traffic, that
/// routes are counted by template so the series cannot multiply, and that the
/// site's own port does not hand the numbers out.
/// </summary>
public sealed class MetricsTests : IAsyncLifetime
{
    const int Port = 8491;
    const int MetricsPort = 8492;
    readonly string _work = Directory.CreateTempSubdirectory("manifest-metrics-").FullName;
    Process _server = null!;
    string? _database;
    string? _databaseUrl;
    HttpClient _http = null!;
    readonly HttpClient _scraper = new() { BaseAddress = new Uri($"http://127.0.0.1:{MetricsPort}") };

    public async Task InitializeAsync()
    {
        File.Copy(Path.Combine(ServerFixture.RepoRoot, "catalog.json"), Path.Combine(_work, "catalog.json"));
        (_database, _databaseUrl) = ServerFixture.CreateTestDatabase();

        var info = new ProcessStartInfo(Path.Combine(ServerFixture.RepoRoot, "Manifest", "bin", "Debug",
                                                     "net8.0", "manifest"))
        {
            WorkingDirectory = _work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in new[] { "--port", Port.ToString(), "--root", _work }) info.ArgumentList.Add(a);
        info.Environment["MANIFEST_METRICS_LISTEN"] = MetricsPort.ToString();
        info.Environment["MANIFEST_INVITE_CODE"] = ServerFixture.InviteCode;
        info.Environment.Remove("MANIFEST_REDIS_URL");
        info.Environment.Remove("ANTHROPIC_API_KEY");
        info.Environment.Remove("DATABASE_URL");
        if (_databaseUrl is null) info.Environment.Remove("MANIFEST_DATABASE_URL");
        else info.Environment["MANIFEST_DATABASE_URL"] = _databaseUrl;
        _server = Process.Start(info)!;

        _http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{Port}"),
        };
        for (var i = 0; i < 120; i++)
        {
            try { if ((await _http.GetAsync("/api/health")).IsSuccessStatusCode) break; }
            catch (HttpRequestException) { }
            await Task.Delay(250);
        }
        await ServerFixture.SignIn(_http, "metrics-test", "correct-horse-battery");
    }

    public Task DisposeAsync()
    {
        try { _server.Kill(entireProcessTree: true); _server.WaitForExit(10_000); } catch { /* gone */ }
        _http.Dispose();
        _scraper.Dispose();
        ServerFixture.DropTestDatabase(_database);
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
        return Task.CompletedTask;
    }

    async Task<string> Scrape()
    {
        var r = await _scraper.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.StartsWith("text/plain", r.Content.Headers.ContentType!.ToString());
        return await r.Content.ReadAsStringAsync();
    }

    static double Value(string text, string series)
    {
        var line = text.Split('\n').SingleOrDefault(l => l.StartsWith(series + " ", StringComparison.Ordinal));
        Assert.True(line is not null, $"no series {series} in:\n{text}");
        return double.Parse(line![(series.Length + 1)..], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task RequestsAreCountedByRouteTemplateNotPath()
    {
        await _http.GetAsync("/api/decks/123456");
        await _http.GetAsync("/api/decks/654321");
        await _http.GetAsync("/no/such/page-" + Guid.NewGuid().ToString("N"));

        var text = await Scrape();
        Assert.Equal(2, Value(text,
            "manifest_http_requests_total{method=\"GET\",route=\"/api/decks/{id:long}\",status=\"404\"}"));
        Assert.True(Value(text, "manifest_http_requests_total{method=\"GET\",route=\"unmatched\",status=\"404\"}") >= 1);
        Assert.DoesNotContain("123456", text);
        Assert.DoesNotContain("page-", text);

        // Histograms are cumulative and end in +Inf equal to the count.
        var inf = Value(text,
            "manifest_http_request_duration_seconds_bucket{method=\"GET\",route=\"/api/decks/{id:long}\",le=\"+Inf\"}");
        Assert.Equal(2, inf);
        Assert.Equal(2, Value(text,
            "manifest_http_request_duration_seconds_count{method=\"GET\",route=\"/api/decks/{id:long}\"}"));
    }

    [Fact]
    public async Task SignInOutcomesAreCounted()
    {
        using var stranger = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{Port}") };
        await stranger.PostAsync("/api/auth/login", new StringContent(
            """{"username":"metrics-test","password":"not the password"}""", Encoding.UTF8, "application/json"));
        await stranger.PostAsync("/api/auth/login", new StringContent(
            """{"username":"metrics-test","password":"correct-horse-battery"}""", Encoding.UTF8, "application/json"));

        var text = await Scrape();
        Assert.True(Value(text, "manifest_auth_attempts_total{kind=\"login\",outcome=\"refused\"}") >= 1);
        Assert.True(Value(text, "manifest_auth_attempts_total{kind=\"login\",outcome=\"ok\"}") >= 1);
    }

    [Fact]
    public async Task TheQueueAndDatabaseAreReported()
    {
        var text = await Scrape();
        Assert.Equal(1, Value(text, "manifest_database_up"));
        Assert.True(Value(text, "manifest_jobs_queued") >= 0);
        Assert.True(Value(text, "manifest_jobs_oldest_due_seconds") >= 0);
        Assert.Contains("# TYPE manifest_jobs_total counter", text);
        Assert.Contains("manifest_process_resident_memory_bytes ", text);

        // Npgsql's own command timings, renamed, when the suite runs on PostgreSQL.
        if (_databaseUrl is not null)
            Assert.True(Value(text, "manifest_db_command_duration_seconds_count") > 0);
    }

    [Fact]
    public async Task TheSitePortDoesNotServeMetrics()
    {
        var r = await _http.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("manifest_http_requests_total", await r.Content.ReadAsStringAsync());

        var other = await _scraper.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }
}

public class PrometheusExporterTests
{
    [Fact]
    public void HistogramBucketsAreCumulativeWithTheLaunchTargetEdges()
    {
        using var exporter = new PrometheusExporter();
        var mode = "test-" + Guid.NewGuid().ToString("N")[..8];
        Telemetry.Scan(mode, "read", 0.2);
        Telemetry.Scan(mode, "read", 0.4);
        Telemetry.Scan(mode, "read", 400);

        var text = exporter.Render();
        string Bucket(string le) =>
            $"manifest_scan_duration_seconds_bucket{{mode=\"{mode}\",outcome=\"read\",le=\"{le}\"}} ";
        Assert.Contains(Bucket("0.1") + "0", text);
        Assert.Contains(Bucket("0.25") + "1", text);
        Assert.Contains(Bucket("0.5") + "2", text);
        Assert.Contains(Bucket("300") + "2", text);
        Assert.Contains(Bucket("+Inf") + "3", text);
        Assert.Contains($"manifest_scan_duration_seconds_count{{mode=\"{mode}\",outcome=\"read\"}} 3", text);
    }

    [Fact]
    public void LabelValuesAreEscaped()
    {
        using var exporter = new PrometheusExporter();
        Telemetry.AuthAttempt("test", "a \"quoted\"\nvalue\\");
        Assert.Contains("outcome=\"a \\\"quoted\\\"\\nvalue\\\\\"", exporter.Render());
    }

    [Theory]
    [InlineData("9464", "127.0.0.1:9464")]
    [InlineData("0.0.0.0:9464", "0.0.0.0:9464")]
    [InlineData("[::]:9464", "[::]:9464")]
    [InlineData("nope", null)]
    [InlineData("70000", null)]
    [InlineData("", null)]
    public void TheListenSettingIsAPortOrAnAddress(string value, string? expected) =>
        Assert.Equal(expected, AppConfig.ParseListen(value)?.ToString());
}
