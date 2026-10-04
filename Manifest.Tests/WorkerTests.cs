using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Manifest.Tests;

/// <summary>
/// A stand-in for the official card site: serves a PNG on /ok and a 500 on /broken,
/// so the image pipeline can be tested without the internet.
/// </summary>
public sealed class FakeUpstream : IDisposable
{
    public static readonly byte[] Png =
        { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5 };

    readonly HttpListener _listener = new();
    public string Base { get; }
    public int Hits;

    public FakeUpstream()
    {
        var port = Random.Shared.Next(20000, 60000);
        Base = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(Base);
        _listener.Start();
        _ = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }
                Interlocked.Increment(ref Hits);
                if (ctx.Request.Url!.AbsolutePath.StartsWith("/ok"))
                {
                    ctx.Response.ContentType = "image/png";
                    await ctx.Response.OutputStream.WriteAsync(Png);
                }
                else
                {
                    ctx.Response.StatusCode = 500;
                }
                ctx.Response.Close();
            }
        });
    }

    public void Dispose() => _listener.Close();
}

/// <summary>Card art arriving through a background job, on the shared test server.</summary>
[Collection("server")]
public class ImageJobTests(ServerFixture server)
{
    async Task<HttpResponseMessage> Img(string id) => await server.Client.GetAsync($"/img/{id}");

    [Fact]
    public async Task AMissingPictureIsQueuedThenServed()
    {
        using var upstream = new FakeUpstream();
        server.ExecuteSql("UPDATE catalog SET image_url = @u WHERE card_id = 'OP01-002'",
                          ("@u", upstream.Base + "ok/OP01-002.png"));

        var first = await Img("OP01-002");
        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        Assert.Equal("pending", first.Headers.GetValues("X-Image").Single());
        Assert.Equal("2", first.Headers.GetValues("Retry-After").Single());

        HttpResponseMessage? got = null;
        for (var i = 0; i < 40 && got?.StatusCode != HttpStatusCode.OK; i++)
        {
            await Task.Delay(250);
            got = await Img("OP01-002");
        }
        Assert.Equal(HttpStatusCode.OK, got!.StatusCode);
        Assert.Equal(FakeUpstream.Png, await got.Content.ReadAsByteArrayAsync());
        Assert.Equal("stored", server.QueryScalar(
            "SELECT status FROM card_images WHERE card_id = 'OP01-002'"));

        // Fetched once: every later view comes from the store.
        var hits = upstream.Hits;
        await Img("OP01-002");
        Assert.Equal(hits, upstream.Hits);
    }

    [Fact]
    public async Task AFailingUpstreamBacksOffInsteadOfRetryingEveryView()
    {
        using var upstream = new FakeUpstream();
        server.ExecuteSql("UPDATE catalog SET image_url = @u WHERE card_id = 'OP01-003'",
                          ("@u", upstream.Base + "broken/OP01-003.png"));

        await Img("OP01-003");
        for (var i = 0; i < 40 && server.QueryScalar(
                 "SELECT status FROM card_images WHERE card_id = 'OP01-003'") as string != "failed"; i++)
            await Task.Delay(250);

        Assert.Equal("failed", server.QueryScalar("SELECT status FROM card_images WHERE card_id = 'OP01-003'"));
        Assert.Equal(1L, Convert.ToInt64(server.QueryScalar(
            "SELECT failure_count FROM card_images WHERE card_id = 'OP01-003'")));

        var hits = upstream.Hits;
        var again = await Img("OP01-003");
        Assert.Equal("missing", again.Headers.GetValues("X-Image").Single());
        await Task.Delay(500);
        Assert.Equal(hits, upstream.Hits);            // backing off: not fetched again
    }

    [Fact]
    public async Task ANumberWithNoCatalogueRowIsSimplyMissing()
    {
        var r = await Img("OP99-999");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("missing", r.Headers.GetValues("X-Image").Single());
    }
}

/// <summary>
/// MANIFEST_WORKER_MODE=external: the site only queues, and a separate
/// `manifest worker` process - sharing the database - does the work.
/// </summary>
public sealed class ExternalWorkerTests : IAsyncLifetime
{
    const int Port = 8481;
    readonly string _work = Directory.CreateTempSubdirectory("manifest-worker-").FullName;
    readonly List<Process> _procs = new();
    readonly StringBuilder _workerLog = new();
    HttpClient _http = null!;
    FakeUpstream _upstream = null!;

    static ProcessStartInfo Info(string work, params string[] args)
    {
        var info = new ProcessStartInfo(Path.Combine(ServerFixture.RepoRoot, "Manifest", "bin", "Debug",
                                                     "net8.0", "manifest"))
        {
            WorkingDirectory = work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) info.ArgumentList.Add(a);
        info.Environment["MANIFEST_WORKER_MODE"] = "external";
        info.Environment["MANIFEST_SCAN_MODE"] = "async";
        info.Environment["MANIFEST_INVITE_CODE"] = ServerFixture.InviteCode;
        info.Environment.Remove("MANIFEST_DATABASE_URL");
        info.Environment.Remove("DATABASE_URL");
        info.Environment.Remove("MANIFEST_REDIS_URL");
        info.Environment.Remove("ANTHROPIC_API_KEY");
        return info;
    }

    public async Task InitializeAsync()
    {
        File.Copy(Path.Combine(ServerFixture.RepoRoot, "catalog.json"), Path.Combine(_work, "catalog.json"));
        _upstream = new FakeUpstream();
        _procs.Add(Process.Start(Info(_work, "--port", Port.ToString(), "--root", _work))!);
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
        await ServerFixture.SignIn(_http, "worker-test", "correct-horse-battery");
    }

    void StartWorker()
    {
        var worker = Process.Start(Info(_work, "worker", "--root", _work))!;
        worker.OutputDataReceived += (_, e) => { lock (_workerLog) _workerLog.AppendLine(e.Data); };
        worker.ErrorDataReceived += (_, e) => { lock (_workerLog) _workerLog.AppendLine(e.Data); };
        worker.BeginOutputReadLine();
        worker.BeginErrorReadLine();
        _procs.Add(worker);
    }

    public Task DisposeAsync()
    {
        foreach (var p in _procs)
            try { p.Kill(entireProcessTree: true); p.WaitForExit(10_000); } catch { /* gone */ }
        _upstream.Dispose();
        _http.Dispose();
        try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
        return Task.CompletedTask;
    }

    object? Scalar(string sql)
    {
        using var conn = new SqliteConnection($"Data Source={Path.Combine(_work, "manifest.db")}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    async Task<JsonElement> ScanStatus(long id) =>
        JsonDocument.Parse(await _http.GetStringAsync($"/api/scan/{id}")).RootElement.Clone();

    [Fact]
    public async Task TheSiteQueuesAndTheWorkerDoes()
    {
        // A photo nobody could read: a valid but blank PNG corner.
        var blank = Convert.ToBase64String(FakeUpstream.Png);
        var posted = await _http.PostAsync("/api/scan", new StringContent(
            JsonSerializer.Serialize(new { variants = new[] { blank } }), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Accepted, posted.StatusCode);
        var scanId = JsonDocument.Parse(await posted.Content.ReadAsStringAsync())
                                 .RootElement.GetProperty("scan_id").GetInt64();

        using (var conn = new SqliteConnection($"Data Source={Path.Combine(_work, "manifest.db")}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE catalog SET image_url = @u WHERE card_id = 'OP01-005'";
            cmd.Parameters.AddWithValue("@u", _upstream.Base + "ok/OP01-005.png");
            cmd.ExecuteNonQuery();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/img/OP01-005")).StatusCode);

        // No worker yet, and the site does not do the work itself.
        await Task.Delay(1500);
        Assert.Equal("pending", (await ScanStatus(scanId)).GetProperty("status").GetString());
        Assert.Equal(0, _upstream.Hits);

        StartWorker();

        JsonElement status = default;
        for (var i = 0; i < 80; i++)
        {
            status = await ScanStatus(scanId);
            if (status.GetProperty("status").GetString() != "pending") break;
            await Task.Delay(250);
        }
        Assert.True(status.GetProperty("status").GetString() == "complete", _workerLog.ToString());
        var result = status.GetProperty("result");
        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.Contains(result.GetProperty("error").GetString(), new[] { "no_read", "no_engine" });

        // The photo is not kept once read.
        Assert.Equal("{}", Scalar($"SELECT payload FROM jobs WHERE id = {scanId}"));

        HttpResponseMessage? img = null;
        for (var i = 0; i < 40 && img?.StatusCode != HttpStatusCode.OK; i++)
        {
            await Task.Delay(250);
            img = await _http.GetAsync("/img/OP01-005");
        }
        Assert.Equal(HttpStatusCode.OK, img!.StatusCode);

        // Someone else's scan id is as good as no scan at all.
        using var stranger = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{Port}"),
        };
        await ServerFixture.SignIn(stranger, "someone-else", "correct-horse-battery");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/scan/{scanId}")).StatusCode);
    }
}
