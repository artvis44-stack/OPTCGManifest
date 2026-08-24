using System.Net;
using System.Text;

namespace Manifest.Tests;

[Collection("server")]
public class ServingTests(ServerFixture server)
{
    [Fact]
    public async Task ServesTheInterface()
    {
        var (status, body) = await server.Raw("/");
        Assert.Equal(200, status);
        Assert.Contains("<title>", body);
    }

    [Fact]
    public async Task UnknownPath404s() => Assert.Equal(404, (await server.Raw("/nope")).Status);

    [Fact]
    public async Task SetupPageLoads()
    {
        var (status, page) = await server.Raw("/setup");
        Assert.Equal(200, status);
        Assert.Contains("Certificate Trust Settings", page);   // covers iPhone
        Assert.Contains("CA certificate", page);               // covers Android
        Assert.Contains("Photo", page);                        // the no-install route
    }

    [Fact]
    public async Task CaDownloadRespondsOr404sWhenThereIsNoAuthority()
    {
        var (status, _) = await server.Raw("/ca.crt");
        Assert.True(status is 200 or 404, $"unexpected status {status}");
    }

    /// <summary>
    /// The upstream CDN is unreachable from some networks; either outcome is
    /// acceptable, a hang or a 500 is not.
    /// </summary>
    [Fact]
    public async Task CardArtIsServedOrDegradesTo404()
    {
        var (status, _) = await server.Raw("/img/OP01-016");
        Assert.True(status is 200 or 404, $"unexpected status {status}");
    }

    [Fact]
    public async Task RefusesPathTraversal() =>
        Assert.Equal(404, (await server.Raw("/img/../../etc/passwd")).Status);

    [Fact]
    public async Task UnknownCardArt404s() =>
        Assert.Equal(404, (await server.Raw("/img/not-a-card")).Status);

    [Fact]
    public async Task SixRequestsOnOneConnection()
    {
        // HttpClient pools connections, so six sequential calls on one client reuse
        // the same socket - the keep-alive path the phone actually uses.
        using var client = new HttpClient { BaseAddress = server.Client.BaseAddress };
        var codes = new List<int>();
        for (var i = 0; i < 6; i++)
            codes.Add((int)(await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(Enumerable.Repeat(200, 6), codes);
    }

    [Fact]
    public async Task HealthHasLiveAndReadyEndpoints()
    {
        var live = await server.Get("/api/health/live");
        Assert.True(live.GetProperty("ok").GetBoolean());
        Assert.Equal("live", live.GetProperty("status").GetString());

        var ready = await server.Get("/api/health/ready");
        Assert.True(ready.GetProperty("ok").GetBoolean());
        Assert.Equal("ok", ready.GetProperty("checks")
            .GetProperty("database").GetProperty("status").GetString());
    }

    [Fact]
    public async Task EchoesAValidRequestId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health/live");
        request.Headers.Add("X-Request-ID", "test-request-123");

        var response = await server.Client.SendAsync(request);

        Assert.Equal("test-request-123",
            response.Headers.GetValues("X-Request-ID").Single());
    }

    [Fact]
    public async Task ScanExplainsItselfWithNoKeyAndNoVariants()
    {
        var (_, d) = await server.Post("/api/scan", new { image = "AAAA" });
        Assert.Equal("no_key", d.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UnreadableUploadFailsCleanly()
    {
        var (_, d) = await server.Post("/api/scan", new { variants = new[] { "not-a-png" } });
        Assert.False(d.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task ScanRejectsAnEmptyRequest()
    {
        var (status, _) = await server.Post("/api/scan", new { });
        Assert.Equal(400, status);
    }

    // ---- the guards. The Python suite covered these only indirectly; they are the
    // reason this server can be pointed at a phone on an untrusted network.

    async Task<HttpResponseMessage> Send(HttpMethod method, string path,
                                         Action<HttpRequestMessage> configure)
    {
        var request = new HttpRequestMessage(method, server.Client.BaseAddress + path.TrimStart('/'));
        configure(request);
        return await server.Client.SendAsync(request);
    }

    [Fact]
    public async Task RefusesAHostHeaderItDoesNotAnswerTo()
    {
        var response = await Send(HttpMethod.Get, "/api/health",
            r => r.Headers.Host = "evil.example.com");
        Assert.Equal(421, (int)response.StatusCode);
    }

    [Fact]
    public async Task RefusesACrossSiteOrigin()
    {
        var response = await Send(HttpMethod.Post, "/api/collection", r =>
        {
            r.Headers.Add("Origin", "https://evil.example.com");
            r.Content = new StringContent("{\"card_id\":\"OP01-016\",\"delta\":1}",
                                          Encoding.UTF8, "application/json");
        });
        Assert.Equal(403, (int)response.StatusCode);
    }

    [Fact]
    public async Task RefusesANullOrigin()
    {
        var response = await Send(HttpMethod.Post, "/api/collection", r =>
        {
            r.Headers.Add("Origin", "null");
            r.Content = new StringContent("{\"card_id\":\"OP01-016\",\"delta\":1}",
                                          Encoding.UTF8, "application/json");
        });
        Assert.Equal(403, (int)response.StatusCode);
    }

    [Fact]
    public async Task RefusesAFormContentType()
    {
        var response = await Send(HttpMethod.Post, "/api/collection", r =>
            r.Content = new StringContent("card_id=OP01-016", Encoding.UTF8,
                                          "application/x-www-form-urlencoded"));
        Assert.Equal(415, (int)response.StatusCode);
    }

    /// <summary>
    /// Driven over a raw socket rather than HttpClient: the server refuses on the
    /// Content-Length header and stops reading, and HttpClient blocks trying to finish
    /// writing a body nobody is draining. curl and browser fetch both handle the early
    /// response fine, so this reads the status line the way they do.
    /// </summary>
    [Fact]
    public async Task RefusesABodyOverTheLimit()
    {
        var body = $"{{\"image\":\"{new string('A', 9 * 1024 * 1024)}\"}}";
        using var tcp = new System.Net.Sockets.TcpClient();
        await tcp.ConnectAsync("127.0.0.1", server.Port);
        await using var stream = tcp.GetStream();

        var head = $"POST /api/scan HTTP/1.1\r\nHost: 127.0.0.1:{server.Port}\r\n"
                   + $"Content-Type: application/json\r\nContent-Length: {body.Length}\r\n"
                   + "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.FlushAsync();

        // Push the body without waiting for it to drain; the refusal arrives first.
        _ = stream.WriteAsync(Encoding.ASCII.GetBytes(body)).AsTask();

        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
        Assert.StartsWith("HTTP/1.1 413", statusLine);
    }

    [Fact]
    public async Task SecurityHeadersAreOnEveryResponse()
    {
        var response = await server.Client.GetAsync("/api/health");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'",
                        response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
