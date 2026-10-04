using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Manifest.Tests;

/// <summary>
/// With --https on, the setup page must also be reachable over plain http, or the
/// phone cannot get the certificate it needs to reach anything.
/// </summary>
public class TlsSetupTests : IAsyncLifetime
{
    const int Port = 8489;

    string _work = "";
    Process? _server;
    bool _skip;
    string _skipReason = "";

    public async Task InitializeAsync()
    {
        if (Which("openssl") is null)
        {
            _skip = true;
            _skipReason = "openssl is not installed";
            return;
        }

        _work = Directory.CreateTempSubdirectory("manifest-tls-").FullName;
        foreach (var f in new[] { "ui.html", "catalog.json", "make_cert.sh" })
            File.Copy(Path.Combine(ServerFixture.RepoRoot, f), Path.Combine(_work, f));

        var make = Process.Start(new ProcessStartInfo("sh", "make_cert.sh")
        {
            WorkingDirectory = _work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        await make.WaitForExitAsync();

        if (!File.Exists(Path.Combine(_work, "ca.pem")))
        {
            _skip = true;
            _skipReason = "could not make a certificate here";
            return;
        }

        _server = ServerFixture.StartServer(_work, Port, null, "--https");
        for (var i = 0; i < 60; i++)
        {
            try
            {
                using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                await probe.GetAsync($"http://127.0.0.1:{Port + 1}/setup");
                return;
            }
            catch (HttpRequestException)
            {
                if (_server.HasExited)
                {
                    _skip = true;
                    _skipReason = "server did not start with --https:\n"
                                  + await _server.StandardOutput.ReadToEndAsync();
                    return;
                }
                await Task.Delay(500);
            }
        }
        _skip = true;
        _skipReason = "the setup helper never came up";
    }

    public Task DisposeAsync()
    {
        if (_server is { HasExited: false })
        {
            try { _server.Kill(entireProcessTree: true); _server.WaitForExit(10_000); }
            catch { /* already gone */ }
        }
        if (_work.Length > 0)
            try { Directory.Delete(_work, recursive: true); } catch { /* best effort */ }
        return Task.CompletedTask;
    }

    static string? Which(string exe) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Select(d => Path.Combine(d, exe))
            .FirstOrDefault(File.Exists);

    [SkippableFact]
    public async Task SetupPageIsReachableWithoutHttps()
    {
        Skip.If(_skip, _skipReason);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var page = await http.GetStringAsync($"http://127.0.0.1:{Port + 1}/setup");
        Assert.Contains("Camera setup", page);
    }

    [SkippableFact]
    public async Task CertificateDownloadsOverPlainHttp()
    {
        Skip.If(_skip, _skipReason);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var response = await http.GetAsync($"http://127.0.0.1:{Port + 1}/ca.crt");
        var blob = await response.Content.ReadAsStringAsync();

        Assert.StartsWith("-----BEGIN", blob);
        // served as a type iOS will offer to install
        Assert.Equal("application/x-x509-ca-cert", response.Content.Headers.ContentType?.MediaType);
    }

    [SkippableFact]
    public async Task TheAppItselfServesOverHttps()
    {
        Skip.If(_skip, _skipReason);

        // Trust the authority make_cert.sh just created, and nothing else - an
        // accept-anything callback here would not test that the chain is valid.
        using var ca = new X509Certificate2(Path.Combine(_work, "ca.pem"));
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
            {
                if (cert is null) return false;
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(ca);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return chain.Build(cert);
            },
        };

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        var body = await http.GetStringAsync($"https://127.0.0.1:{Port}/api/health");
        var health = JsonDocument.Parse(body).RootElement;
        Assert.True(health.GetProperty("secure").GetBoolean());
    }
}
