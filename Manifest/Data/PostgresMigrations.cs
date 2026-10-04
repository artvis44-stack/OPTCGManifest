using System.Data.Common;
using System.Reflection;

namespace Manifest.Data;

/// <summary>
/// Versioned schema changes for PostgreSQL: numbered .sql files embedded in the
/// binary, each applied once and recorded in schema_migrations. SQLite keeps its
/// column-sniffing Database.Migrate, because databases already in the wild have no
/// version table to start counting from.
///
/// Everything runs in one transaction under an advisory lock, so several app
/// containers starting at once queue up behind whichever got there first instead of
/// racing to create the same tables - and a script that fails half way leaves
/// nothing behind, PostgreSQL DDL being transactional.
/// </summary>
public static class PostgresMigrations
{
    const string Prefix = "Manifest.Data.Migrations.Postgres.";

    /// <summary>Every embedded script, in the order it applies.</summary>
    public static IReadOnlyList<(string Version, string Sql)> Scripts()
    {
        var assembly = typeof(PostgresMigrations).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)
                        && n.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(n => (Version: n[Prefix.Length..^".sql".Length], Sql: Read(assembly, n)))
            .ToList();
    }

    static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <returns>The versions applied by this call; empty when already current.</returns>
    public static List<string> Apply(DbConnection conn)
    {
        using var tx = conn.BeginTransaction();
        SqlDialect.Postgres.Lock(conn, "manifest:migrations");

        conn.Exec("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version    TEXT PRIMARY KEY,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
            )
            """);

        var done = new HashSet<string>(StringComparer.Ordinal);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT version FROM schema_migrations";
            using var r = cmd.ExecuteReader();
            while (r.Read()) done.Add(r.GetString(0));
        }

        var applied = new List<string>();
        foreach (var (version, sql) in Scripts())
        {
            if (done.Contains(version)) continue;
            conn.Exec(sql);
            using var record = conn.CreateCommand();
            record.CommandText = "INSERT INTO schema_migrations (version) VALUES (@v)";
            record.Bind("@v", version);
            record.ExecuteNonQuery();
            applied.Add(version);
        }

        tx.Commit();
        return applied;
    }
}
