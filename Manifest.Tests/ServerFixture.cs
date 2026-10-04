using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Manifest.Tests;

/// <summary>
/// Boots a real server on a scratch database seeded from catalog.json, the way the
/// Python suite did: black-box over HTTP, so the tests exercise routing, guards and
/// serialisation rather than calling the repositories directly.
/// </summary>
public sealed class ServerFixture : IAsyncLifetime
{
    public string WorkDir { get; private set; } = "";
    public HttpClient Client { get; private set; } = null!;
    public int Port { get; } = 8477;

    /// <summary>
    /// The account the suite runs as. Nearly every endpoint now needs one, so the
    /// fixture signs in once and HttpClient's cookie jar carries the session into
    /// every later request - the same way a browser does.
    /// </summary>
    public const string Username = "tester";
    public const string Password = "correct-horse-battery";
    public const string InviteCode = "test-invite-code";

    Process? _server;

    /// <summary>
    /// Set to a PostgreSQL URL the suite may create databases on, e.g.
    /// postgres://manifest:manifest@127.0.0.1:55432/manifest, and every test runs
    /// against a fresh PostgreSQL database instead of a scratch manifest.db.
    /// </summary>
    public static string? PostgresAdminUrl =>
        Environment.GetEnvironmentVariable("MANIFEST_TEST_POSTGRES") is { Length: > 0 } url ? url : null;

    /// <summary>What the server is started with: null for SQLite in WorkDir.</summary>
    public string? DatabaseUrl { get; private set; }

    string? _postgresDatabase;

    /// <summary>The repository root, found by walking up from the test binary.</summary>
    public static string RepoRoot { get; } = Find();

    static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ui.html"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not find the repository root");
    }

    public async Task InitializeAsync()
    {
        WorkDir = Directory.CreateTempSubdirectory("manifest-test-").FullName;
        foreach (var f in new[] { "ui.html", "catalog.json" })
            File.Copy(Path.Combine(RepoRoot, f), Path.Combine(WorkDir, f));

        (_postgresDatabase, DatabaseUrl) = CreateTestDatabase();

        Client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{Port}"),
            Timeout = TimeSpan.FromSeconds(30),
        };

        await Start();
        await SignIn(Client, Username, Password);
    }

    /// <summary>
    /// Registers the account if it is not there yet, signs in if it is. Returns the
    /// client so a test can build a second identity in one line.
    /// </summary>
    public static async Task<HttpClient> SignIn(HttpClient client, string username,
                                                string password)
    {
        var registered = await client.PostAsync("/api/auth/register",
            Body(new { username, password, invite = InviteCode }));

        if (!registered.IsSuccessStatusCode)
        {
            var signedIn = await client.PostAsync("/api/auth/login",
                Body(new { username, password }));
            if (!signedIn.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"could not sign in as {username}: {(int)signedIn.StatusCode} "
                    + await signedIn.Content.ReadAsStringAsync());
        }
        return client;
    }

    /// <summary>A second browser, with its own cookie jar and its own account.</summary>
    public async Task<HttpClient> NewClientFor(string username, string password)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{Port}"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        await SignIn(client, username, password);
        return client;
    }

    /// <summary>A client with no session at all, for checking what a stranger sees.</summary>
    public HttpClient NewAnonymousClient() => new()
    {
        BaseAddress = new Uri($"http://127.0.0.1:{Port}"),
        Timeout = TimeSpan.FromSeconds(30),
    };

    static StringContent Body(object payload) => new(
        JsonSerializer.Serialize(payload, Manifest.Json.Options),
        Encoding.UTF8, "application/json");

    public async Task Start()
    {
        _server = StartServer(WorkDir, Port, DatabaseUrl);
        for (var i = 0; i < 120; i++)
        {
            try
            {
                var probe = await Client.GetAsync("/api/health");
                if (probe.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }
            if (_server.HasExited)
                throw new InvalidOperationException(
                    "server died on startup:\n" + await _server.StandardOutput.ReadToEndAsync());
            await Task.Delay(500);
        }
        throw new TimeoutException("server never came up");
    }

    public static Process StartServer(string workDir, int port, string? databaseUrl,
                                      params string[] extra)
    {
        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(RepoRoot, "Manifest", "bin", "Debug", "net8.0", "manifest"),
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("--port");
        info.ArgumentList.Add(port.ToString());
        info.ArgumentList.Add("--root");
        info.ArgumentList.Add(workDir);
        foreach (var a in extra) info.ArgumentList.Add(a);
        // Exercise the no-key path, the same as the Python suite did.
        info.Environment.Remove("ANTHROPIC_API_KEY");
        // Registration has to be open for the suite to make itself an account.
        info.Environment["MANIFEST_INVITE_CODE"] = InviteCode;
        // Whatever the shell running the tests points at is not the suite's to use.
        info.Environment.Remove("DATABASE_URL");
        if (databaseUrl is null) info.Environment.Remove("MANIFEST_DATABASE_URL");
        else info.Environment["MANIFEST_DATABASE_URL"] = databaseUrl;

        var proc = Process.Start(info)
                   ?? throw new InvalidOperationException("could not start the server");
        return proc;
    }

    public async Task Restart()
    {
        Stop();
        await Start();
        // Sessions outlive a restart - they are rows in the database, not memory -
        // so the existing cookie is still good and this only re-signs-in if the
        // work directory was wiped underneath us.
        await SignIn(Client, Username, Password);
    }

    void Stop()
    {
        if (_server is null || _server.HasExited) return;
        try
        {
            _server.Kill(entireProcessTree: true);
            _server.WaitForExit(10_000);
        }
        catch
        {
            // already gone
        }
    }

    public Task DisposeAsync()
    {
        Stop();
        Client.Dispose();
        try { Directory.Delete(WorkDir, recursive: true); } catch { /* best effort */ }
        DropTestDatabase(_postgresDatabase);
        return Task.CompletedTask;
    }

    /// <summary>
    /// A fresh, empty PostgreSQL database and the URL a server should be given for
    /// it - or two nulls when the suite is running on SQLite.
    /// </summary>
    public static (string? Name, string? Url) CreateTestDatabase()
    {
        if (PostgresAdminUrl is not { } admin) return (null, null);
        var name = $"manifest_test_{Guid.NewGuid():N}";
        AdminExec(admin, $"CREATE DATABASE {name}");
        return (name, WithDatabase(admin, name));
    }

    public static void DropTestDatabase(string? name)
    {
        if (PostgresAdminUrl is not { } admin || name is null) return;
        try { AdminExec(admin, $"DROP DATABASE IF EXISTS {name} WITH (FORCE)"); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Runs SQL against whatever database the server is using, for the few tests
    /// that need to put the data in a state the API will not.
    /// </summary>
    public void ExecuteSql(string sql, params (string Name, object Value)[] args)
    {
        using System.Data.Common.DbConnection conn = DatabaseUrl is null
            ? new SqliteConnection($"Data Source={Path.Combine(WorkDir, "manifest.db")}")
            : new NpgsqlConnection(ConnectionString(DatabaseUrl));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
        cmd.ExecuteNonQuery();
    }

    static void AdminExec(string adminUrl, string sql)
    {
        using var conn = new NpgsqlConnection(ConnectionString(adminUrl));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    static string WithDatabase(string url, string database)
    {
        var b = new UriBuilder(url) { Path = "/" + database };
        return b.Uri.ToString();
    }

    public static string ConnectionString(string url)
    {
        var uri = new Uri(url);
        var user = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(user[0]),
            Password = user.Length > 1 ? Uri.UnescapeDataString(user[1]) : null,
            Pooling = false,
        }.ToString();
    }

    // ---- request helpers, mirroring the Python suite's get()/post()/raw()

    public async Task<JsonElement> Get(string path)
    {
        var response = await Client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    public async Task<(int Status, string Body)> Raw(string path)
    {
        var response = await Client.GetAsync(path);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public async Task<(int Status, JsonElement Body)> Post(string path, object payload)
    {
        var json = JsonSerializer.Serialize(payload, Manifest.Json.Options);
        var response = await Client.PostAsync(path,
            new StringContent(json, Encoding.UTF8, "application/json"));
        var text = await response.Content.ReadAsStringAsync();
        var element = text.Length > 0
            ? JsonDocument.Parse(text).RootElement.Clone()
            : default;
        return ((int)response.StatusCode, element);
    }

    public Task Reset() => Post("/api/reset", new { });

    public async Task Log(string cardId, int delta) =>
        await Post("/api/collection", new { card_id = cardId, delta });
}

[CollectionDefinition("server")]
public sealed class ServerCollection : ICollectionFixture<ServerFixture>;
