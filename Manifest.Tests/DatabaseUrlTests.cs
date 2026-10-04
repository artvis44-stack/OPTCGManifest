using Manifest.Data;
using Npgsql;

namespace Manifest.Tests;

/// <summary>
/// Which database MANIFEST_DATABASE_URL picks, and that the PostgreSQL migrations
/// survive several app containers starting against one database at once.
/// </summary>
public class DatabaseUrlTests
{
    static readonly AppPaths Paths = new(Path.GetTempPath());

    [Fact]
    public void DefaultIsManifestDbUnderTheRoot()
    {
        var db = new Database(Paths, "sqlite://manifest.db");
        Assert.False(db.Dialect.IsPostgres);
        Assert.Equal(Paths.DbPath, db.SqliteFile);
    }

    [Fact]
    public void SqliteAcceptsAnAbsolutePath()
    {
        var db = new Database(Paths, "sqlite:///var/lib/manifest/data.db");
        Assert.Equal("/var/lib/manifest/data.db", db.SqliteFile);
    }

    [Fact]
    public void PostgresUrlIsParsedWithoutLeakingThePassword()
    {
        var db = new Database(Paths, "postgres://app:s%40cret@db.internal:6543/manifest?sslmode=require");
        Assert.True(db.Dialect.IsPostgres);
        Assert.Null(db.SqliteFile);
        Assert.Equal("PostgreSQL at db.internal:6543/manifest", db.Description);
        Assert.DoesNotContain("cret", db.Description);
    }

    [Theory]
    [InlineData("sqlite://manifest.db")]
    [InlineData("postgresql://u:p@localhost/manifest")]
    [InlineData("Host=localhost;Database=manifest;Username=u")]
    public void AcceptsTheSupportedForms(string url) => Assert.Null(Database.RejectUrl(url));

    [Theory]
    [InlineData("mysql://u:p@localhost/manifest")]
    [InlineData("manifest.db")]
    [InlineData("postgres://u:p@localhost/")]
    public void RejectsEverythingElse(string url) => Assert.NotNull(Database.RejectUrl(url));

    [SkippableFact]
    public async Task MigrationsApplyOnceWhenContainersStartTogether()
    {
        Skip.If(ServerFixture.PostgresAdminUrl is null, "MANIFEST_TEST_POSTGRES is not set");
        var admin = ServerFixture.PostgresAdminUrl!;
        var name = $"manifest_migrate_{Guid.NewGuid():N}";

        await using (var conn = new NpgsqlConnection(ServerFixture.ConnectionString(admin)))
        {
            await conn.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", conn);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var db = new Database(Paths, new UriBuilder(admin) { Path = "/" + name }.Uri.ToString());
            var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                using var conn = db.Open();
                return PostgresMigrations.Apply(conn);
            })));

            // Exactly one starter did the work; the rest waited and found nothing to do.
            var all = PostgresMigrations.Scripts().Select(s => s.Version).ToList();
            Assert.Single(runs, r => r.Count > 0);
            Assert.Equal(all, runs.Single(r => r.Count > 0));

            using var again = db.Open();
            Assert.Empty(PostgresMigrations.Apply(again));
        }
        finally
        {
            await using var conn = new NpgsqlConnection(ServerFixture.ConnectionString(admin));
            await conn.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", conn);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
