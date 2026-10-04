using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Manifest.Tests;

/// <summary>
/// Signing up on a server with no shared invite code - the shape a public deployment
/// runs in, where MANIFEST_INVITE_CODE is unset and an approved link is the only way
/// to an account. The flow is walked over HTTP end to end, with both tokens read back
/// out of the mail the server logs when SMTP is not configured: they are stored
/// hashed, so the mail is the only place they exist in the clear.
/// </summary>
public class AccessTests : IAsyncLifetime
{
    const int Port = 8478;
    const string Applicant = "newcomer@example.com";

    readonly StringBuilder _log = new();
    string _work = "";
    string? _database;
    Process? _server;
    HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        _work = Directory.CreateTempSubdirectory("manifest-access-").FullName;
        foreach (var f in new[] { "ui.html", "catalog.json" })
            File.Copy(Path.Combine(ServerFixture.RepoRoot, f), Path.Combine(_work, f));

        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(ServerFixture.RepoRoot, "Manifest", "bin", "Debug",
                                    "net8.0", "manifest"),
            WorkingDirectory = _work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("--port");
        info.ArgumentList.Add(Port.ToString());
        info.ArgumentList.Add("--root");
        info.ArgumentList.Add(_work);
        info.Environment.Remove("ANTHROPIC_API_KEY");
        // The whole point of this fixture: no shared code, so nothing but a minted
        // link can make an account.
        info.Environment.Remove("MANIFEST_INVITE_CODE");
        info.Environment["MANIFEST_ADMIN_EMAIL"] = "admin@example.com";
        info.Environment.Remove("DATABASE_URL");
        (_database, var databaseUrl) = ServerFixture.CreateTestDatabase();
        if (databaseUrl is null) info.Environment.Remove("MANIFEST_DATABASE_URL");
        else info.Environment["MANIFEST_DATABASE_URL"] = databaseUrl;

        _server = Process.Start(info)
                  ?? throw new InvalidOperationException("could not start the server");
        _server.OutputDataReceived += Collect;
        _server.ErrorDataReceived += Collect;
        _server.BeginOutputReadLine();
        _server.BeginErrorReadLine();

        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{Port}"),
            Timeout = TimeSpan.FromSeconds(30),
        };

        for (var i = 0; i < 120; i++)
        {
            try
            {
                if ((await _http.GetAsync("/api/health")).IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }
            if (_server.HasExited)
                throw new InvalidOperationException("server died on startup:\n" + Text());
            await Task.Delay(500);
        }
        throw new TimeoutException("server never came up:\n" + Text());
    }

    void Collect(object _, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        lock (_log) _log.AppendLine(e.Data);
    }

    string Text()
    {
        lock (_log) return _log.ToString();
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
        ServerFixture.DropTestDatabase(_database);
        return Task.CompletedTask;
    }

    static StringContent Body(object payload) => new(
        JsonSerializer.Serialize(payload, Manifest.Json.Options),
        Encoding.UTF8, "application/json");

    static async Task<JsonElement> Read(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    /// <summary>
    /// The link the server just mailed, fished out of the log it writes instead of
    /// sending. Polled rather than read once: the send happens inside the request,
    /// but the line reaches this process through a pipe a moment later.
    /// </summary>
    async Task<string> Mailed(string pattern, string what)
    {
        var regex = new Regex(pattern);
        for (var i = 0; i < 80; i++)
        {
            var match = regex.Match(Text());
            if (match.Success) return Uri.UnescapeDataString(match.Groups[1].Value);
            await Task.Delay(250);
        }
        throw new TimeoutException($"never saw {what} in the server log:\n" + Text());
    }

    /// <summary>Files a request, approves it off the admin's link, returns the invite.</summary>
    async Task<string> ApprovedInvite()
    {
        var filed = await _http.PostAsync("/api/access/request", Body(new { email = Applicant }));
        Assert.True(filed.IsSuccessStatusCode, await filed.Content.ReadAsStringAsync());

        var action = await Mailed(@"/access/review\?t=([^\s<""']+)", "the admin's review link");
        var decided = await _http.PostAsync("/api/access/decide",
            Body(new { token = action, decision = "approve" }));
        Assert.True(decided.IsSuccessStatusCode, await decided.Content.ReadAsStringAsync());

        return await Mailed(@"/register\?invite=([^\s<""']+)", "the applicant's sign-up link");
    }

    [Fact]
    public async Task RegistrationIsClosedButRequestsAreOpen()
    {
        var session = await Read(await _http.GetAsync("/api/session"));

        // "closed" is the honest answer for anyone who merely found the page: with no
        // shared code there is nothing they can type. It must not also mean that a
        // minted link is refused.
        Assert.Equal("closed", session.GetProperty("registration").GetString());
        Assert.True(session.GetProperty("request_access").GetBoolean());
    }

    [Fact]
    public async Task AnApprovedRequestMakesAnAccountWithNoSharedCode()
    {
        var invite = await ApprovedInvite();

        // The lookup the sign-up page does before anything is typed: it names the
        // address so the person can see the link is theirs.
        var lookup = await Read(await _http.GetAsync($"/api/access/invite?t={Uri.EscapeDataString(invite)}"));
        Assert.Equal(Applicant, lookup.GetProperty("email").GetString());

        var made = await _http.PostAsync("/api/auth/register",
            Body(new { username = "newcomer", password = "correct-horse-battery", invite }));
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync());

        var body = await Read(made);
        Assert.Equal("newcomer", body.GetProperty("user").GetProperty("username").GetString());

        // One account, one link: the second attempt has nothing left to spend.
        var again = await _http.PostAsync("/api/auth/register",
            Body(new { username = "gatecrasher", password = "correct-horse-battery", invite }));
        Assert.False(again.IsSuccessStatusCode);
    }

    [Fact]
    public async Task TheSignUpPageReadsTheInviteFromTheLink()
    {
        var page = await (await _http.GetAsync("/register?invite=whatever")).Content.ReadAsStringAsync();

        // The regression this guards: the page used to ignore the query string
        // entirely, so an approved applicant landed on a sign-in form holding a token
        // it had no way to submit - and with registration reported closed, no way to
        // reach the sign-up mode either.
        Assert.Contains("URLSearchParams", page);
        Assert.Contains("/api/access/invite?t=", page);
        Assert.Contains("linkToken", page);
    }
}
