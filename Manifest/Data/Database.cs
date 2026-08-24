using System.Text.Json;
using Manifest.Models;
using Microsoft.Data.Sqlite;

namespace Manifest.Data;

/// <summary>
/// Hands out connections and owns the schema. The Python original kept one
/// connection per thread; ADO.NET pools them, so a connection per unit of work is
/// both simpler and equivalent - the pragmas below are what actually matter.
/// </summary>
public sealed class Database
{
    readonly string _connectionString;
    readonly AppPaths _paths;

    public Database(AppPaths paths)
    {
        _paths = paths;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 15,
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=15000;
            PRAGMA foreign_keys=ON;
            """;
        pragma.ExecuteNonQuery();
        return conn;
    }

    public const string Schema = """
        CREATE TABLE IF NOT EXISTS catalog (
            card_id   TEXT PRIMARY KEY,
            base_id   TEXT NOT NULL,
            variant   TEXT NOT NULL DEFAULT '',
            name      TEXT NOT NULL,
            set_label TEXT,
            set_name  TEXT,
            rarity    TEXT,
            category  TEXT,
            colors    TEXT,
            cost      INTEGER,
            power     INTEGER,
            counter   INTEGER,
            types     TEXT,
            effect    TEXT,
            image_url TEXT
        );
        CREATE INDEX IF NOT EXISTS idx_catalog_base ON catalog(base_id);
        CREATE INDEX IF NOT EXISTS idx_catalog_name ON catalog(name);
        CREATE INDEX IF NOT EXISTS idx_catalog_set  ON catalog(set_label);

        CREATE TABLE IF NOT EXISTS users (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            username      TEXT NOT NULL COLLATE NOCASE UNIQUE,
            password_hash TEXT NOT NULL,
            email         TEXT COLLATE NOCASE,
            created_at    TEXT NOT NULL DEFAULT (datetime('now')),
            last_seen     TEXT
        );

        -- Someone asking to be let in, and the two secrets that request goes on to
        -- carry: the one the admin's mail client holds while the request is pending,
        -- and the one the applicant's mail client holds once it is approved. Both are
        -- stored as digests for the same reason session tokens are - a stolen copy of
        -- manifest.db should not be a working link to anywhere.
        CREATE TABLE IF NOT EXISTS access_requests (
            id                INTEGER PRIMARY KEY AUTOINCREMENT,
            email             TEXT NOT NULL COLLATE NOCASE,
            note              TEXT NOT NULL DEFAULT '',
            status            TEXT NOT NULL DEFAULT 'pending',
            created_at        TEXT NOT NULL DEFAULT (datetime('now')),
            decided_at        TEXT,
            decided_by        TEXT,
            requested_from    TEXT,
            action_token_hash TEXT UNIQUE,
            action_expires_at TEXT,
            invite_token_hash TEXT UNIQUE,
            invite_expires_at TEXT,
            invite_sent_at    TEXT,
            used_at           TEXT,
            user_id           INTEGER
        );
        CREATE INDEX IF NOT EXISTS idx_access_status ON access_requests(status);
        CREATE INDEX IF NOT EXISTS idx_access_email  ON access_requests(email);

        CREATE TABLE IF NOT EXISTS sessions (
            token_hash TEXT PRIMARY KEY,
            user_id    INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            created_at TEXT NOT NULL DEFAULT (datetime('now')),
            last_seen  TEXT NOT NULL DEFAULT (datetime('now')),
            expires_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_sessions_user ON sessions(user_id);

        CREATE TABLE IF NOT EXISTS collection (
            user_id   INTEGER NOT NULL,
            card_id   TEXT NOT NULL,
            qty       INTEGER NOT NULL CHECK (qty >= 0),
            note      TEXT NOT NULL DEFAULT '',
            added_at  TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (user_id, card_id)
        );

        CREATE TABLE IF NOT EXISTS scan_log (
            id        INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id   INTEGER NOT NULL DEFAULT 0,
            card_id   TEXT,
            result    TEXT,
            at        TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE TABLE IF NOT EXISTS prices (
            card_id    TEXT PRIMARY KEY,
            usd        REAL,
            gbp        REAL,
            fetched_at TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE TABLE IF NOT EXISTS decks (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id         INTEGER NOT NULL DEFAULT 0,
            name            TEXT NOT NULL,
            leader_card_id  TEXT NOT NULL,
            created_at      TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at      TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE TABLE IF NOT EXISTS deck_cards (
            deck_id   INTEGER NOT NULL REFERENCES decks(id) ON DELETE CASCADE,
            card_id   TEXT NOT NULL,
            qty       INTEGER NOT NULL CHECK (qty > 0),
            PRIMARY KEY (deck_id, card_id)
        );
        """;

    /// <summary>
    /// The user_id a row carries when it predates accounts. The first account
    /// created claims it - see UserRepository.Create. Rows are left with this owner
    /// rather than deleted, because a database made before accounts existed holds a
    /// collection someone spent real evenings typing in.
    /// </summary>
    public const long Unclaimed = 0;

    /// <summary>
    /// Adds the per-user columns to a database made before accounts existed. Keyed
    /// off the columns themselves rather than a version number, so it is idempotent
    /// and a database of unknown vintage still lands in the right shape.
    /// </summary>
    static void Migrate(SqliteConnection conn)
    {
        if (!HasColumn(conn, "collection", "user_id"))
        {
            // SQLite cannot widen a primary key in place, so the table is rebuilt.
            // No foreign key to users(id): these rows exist before any account does,
            // and an unclaimed owner of 0 would fail the constraint on the spot.
            // UserRepository.Delete clears a departing user's rows explicitly.
            using var tx = conn.BeginTransaction();
            Exec(conn, """
                CREATE TABLE collection_migrating (
                    user_id   INTEGER NOT NULL,
                    card_id   TEXT NOT NULL,
                    qty       INTEGER NOT NULL CHECK (qty >= 0),
                    note      TEXT NOT NULL DEFAULT '',
                    added_at  TEXT NOT NULL DEFAULT (datetime('now')),
                    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                    PRIMARY KEY (user_id, card_id)
                );
                INSERT INTO collection_migrating
                    (user_id, card_id, qty, note, added_at, updated_at)
                    SELECT 0, card_id, qty, note, added_at, updated_at FROM collection;
                DROP TABLE collection;
                ALTER TABLE collection_migrating RENAME TO collection;
                """);
            tx.Commit();
            Console.WriteLine("migrated: collection is now per-user");
        }

        if (!HasColumn(conn, "decks", "user_id"))
        {
            Exec(conn, "ALTER TABLE decks ADD COLUMN user_id INTEGER NOT NULL DEFAULT 0");
            Console.WriteLine("migrated: decks are now per-user");
        }

        if (!HasColumn(conn, "scan_log", "user_id"))
            Exec(conn, "ALTER TABLE scan_log ADD COLUMN user_id INTEGER NOT NULL DEFAULT 0");

        // Accounts made before the request-access flow existed have no address on
        // them. The column stays nullable rather than being backfilled, because a
        // made-up address is worse than an absent one: it would be mailed.
        if (!HasColumn(conn, "users", "email"))
        {
            Exec(conn, "ALTER TABLE users ADD COLUMN email TEXT COLLATE NOCASE");
            Console.WriteLine("migrated: accounts can carry an email address");
        }

        // Indexes on the migrated columns go last, once every column they name is
        // certain to exist. Putting them in Schema alongside their tables looks
        // tidier and breaks every database that predates the column, because
        // CREATE TABLE IF NOT EXISTS skips but CREATE INDEX does not.
        Exec(conn, "CREATE INDEX IF NOT EXISTS idx_decks_user ON decks(user_id)");

        // Partial, so the many accounts that predate the column - all NULL - do not
        // collide with each other, while two real accounts cannot share an address.
        // SQLite cannot add a UNIQUE column with ALTER TABLE, so it has to be here.
        Exec(conn, """
            CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email
                ON users(email) WHERE email IS NOT NULL
            """);
    }

    static bool HasColumn(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = @c";
        cmd.Parameters.AddWithValue("@c", column);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Creates the schema, and seeds the catalogue if it is empty.</summary>
    /// <returns>An error message to exit with, or null on success.</returns>
    public string? Initialise(bool forceReseed)
    {
        using var conn = Open();
        using (var create = conn.CreateCommand())
        {
            create.CommandText = Schema;
            create.ExecuteNonQuery();
        }

        Migrate(conn);

        long have;
        using (var count = conn.CreateCommand())
        {
            count.CommandText = "SELECT count(*) FROM catalog";
            have = (long)count.ExecuteScalar()!;
        }

        if (have != 0 && !forceReseed)
        {
            Console.WriteLine($"catalog ready: {have} printings");
            return null;
        }

        if (!File.Exists(_paths.Catalog))
            return "catalog.json is missing. Run: manifest refresh-catalog";

        List<CatalogRow> rows;
        using (var stream = File.OpenRead(_paths.Catalog))
            rows = JsonSerializer.Deserialize<List<CatalogRow>>(stream, Json.Options) ?? new();

        using var tx = conn.BeginTransaction();
        using (var wipe = conn.CreateCommand())
        {
            wipe.CommandText = "DELETE FROM catalog";
            wipe.ExecuteNonQuery();
        }

        using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO catalog
              (card_id, base_id, variant, name, set_label, set_name, rarity,
               category, colors, cost, power, counter, types, effect, image_url)
            VALUES (@card_id,@base_id,@variant,@name,@set_label,@set_name,@rarity,
                    @category,@colors,@cost,@power,@counter,@types,@effect,@image_url)
            """;
        var p = new[]
        {
            "@card_id", "@base_id", "@variant", "@name", "@set_label", "@set_name",
            "@rarity", "@category", "@colors", "@cost", "@power", "@counter",
            "@types", "@effect", "@image_url",
        }.Select(n => insert.Parameters.Add(new SqliteParameter(n, SqliteType.Text))).ToArray();

        foreach (var r in rows)
        {
            p[0].Value = r.CardId;
            p[1].Value = r.BaseId;
            p[2].Value = r.Variant;
            p[3].Value = r.Name;
            p[4].Value = (object?)r.SetLabel ?? DBNull.Value;
            p[5].Value = (object?)r.SetName ?? DBNull.Value;
            p[6].Value = (object?)r.Rarity ?? DBNull.Value;
            p[7].Value = (object?)r.Category ?? DBNull.Value;
            p[8].Value = (object?)r.Colors ?? DBNull.Value;
            p[9].Value = (object?)r.Cost ?? DBNull.Value;
            p[10].Value = (object?)r.Power ?? DBNull.Value;
            p[11].Value = (object?)r.Counter ?? DBNull.Value;
            p[12].Value = (object?)r.Types ?? DBNull.Value;
            p[13].Value = (object?)r.Effect ?? DBNull.Value;
            p[14].Value = (object?)r.ImageUrl ?? DBNull.Value;
            insert.ExecuteNonQuery();
        }

        tx.Commit();
        Console.WriteLine($"catalog seeded: {rows.Count} printings");
        return null;
    }
}
