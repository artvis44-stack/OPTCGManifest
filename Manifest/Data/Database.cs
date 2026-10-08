using System.Data.Common;
using System.Text.Json;
using Manifest.Models;
using Manifest.Services;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Manifest.Data;

/// <summary>
/// Hands out connections and owns the schema. The Python original kept one
/// connection per thread; ADO.NET pools them, so a connection per unit of work is
/// both simpler and equivalent - the pragmas below are what actually matter.
///
/// Which database is decided once, here, from MANIFEST_DATABASE_URL: a sqlite://
/// path for the single-machine install this started as, or a postgres:// URL for a
/// deployment where several app containers share one database. The repositories
/// above this see a DbConnection and a <see cref="SqlDialect"/> and nothing else.
/// </summary>
public sealed class Database
{
    readonly string _connectionString = "";
    readonly NpgsqlDataSource? _postgres;
    readonly AppPaths _paths;

    public SqlDialect Dialect { get; }

    /// <summary>Where the data is, for the startup banner. Never includes a password.</summary>
    public string Description { get; }

    public Database(AppPaths paths) : this(paths, AppConfig.DatabaseUrlFromEnvironment()) { }

    public Database(AppPaths paths, string url)
    {
        _paths = paths;

        if (ParsePostgres(url) is { } pg)
        {
            Dialect = SqlDialect.Postgres;
            _postgres = NpgsqlDataSource.Create(pg);
            Description = $"PostgreSQL at {pg.Host}:{pg.Port}/{pg.Database}";
            return;
        }

        Dialect = SqlDialect.Sqlite;
        var file = SqlitePath(paths, url);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = file,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            DefaultTimeout = 15,
        }.ToString();
        SqliteFile = file;
        Description = $"SQLite at {file}";
    }

    /// <summary>The database file, or null when the data lives in PostgreSQL.</summary>
    public string? SqliteFile { get; }

    /// <summary>
    /// Whether <paramref name="url"/> names PostgreSQL. False for SQLite and for
    /// anything unreadable, which RejectUrl reports on its own.
    /// </summary>
    public static bool IsPostgresUrl(string url)
    {
        try { return ParsePostgres(url) is not null; }
        catch (Exception e) when (e is ArgumentException or FormatException or UriFormatException)
        {
            return false;
        }
    }

    /// <summary>Whether <paramref name="url"/> is one this class knows how to open.</summary>
    public static string? RejectUrl(string url)
    {
        try
        {
            if (ParsePostgres(url) is not null) return null;
            if (url.StartsWith("sqlite://", StringComparison.OrdinalIgnoreCase)) return null;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or UriFormatException)
        {
            return $"MANIFEST_DATABASE_URL could not be read: {e.Message}";
        }
        return "MANIFEST_DATABASE_URL must start with sqlite://, postgres:// or postgresql://, "
               + "or be an Npgsql connection string (Host=...;Database=...).";
    }

    /// <summary>
    /// sqlite://manifest.db is relative to the root, as manifest.db always was;
    /// sqlite:///var/lib/manifest.db is absolute.
    /// </summary>
    static string SqlitePath(AppPaths paths, string url)
    {
        var rest = url.StartsWith("sqlite://", StringComparison.OrdinalIgnoreCase)
            ? url["sqlite://".Length..]
            : "";
        if (rest.Length == 0 || rest == "manifest.db") return paths.DbPath;
        return Path.Combine(paths.Root, rest);
    }

    /// <summary>
    /// postgres://user:pass@host:port/db?sslmode=require, the form every hosting
    /// provider hands out, or an Npgsql keyword string for anything that form cannot
    /// say. Null when <paramref name="url"/> is neither.
    /// </summary>
    static NpgsqlConnectionStringBuilder? ParsePostgres(string url)
    {
        if (url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var b = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            };
            if (uri.UserInfo.Length > 0)
            {
                var colon = uri.UserInfo.IndexOf(':');
                b.Username = Uri.UnescapeDataString(colon < 0 ? uri.UserInfo : uri.UserInfo[..colon]);
                if (colon >= 0) b.Password = Uri.UnescapeDataString(uri.UserInfo[(colon + 1)..]);
            }
            if (string.IsNullOrEmpty(b.Database))
                throw new ArgumentException("the URL names no database");

            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
                var value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..]);
                // libpq spells it sslmode; Npgsql spells it SSL Mode. Anything else
                // is passed through under the name it was given.
                b[key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) ? "SSL Mode" : key] = value;
            }
            return b;
        }

        if (url.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || url.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            return new NpgsqlConnectionStringBuilder(url);

        return null;
    }

    public DbConnection Open()
    {
        if (_postgres is not null) return _postgres.OpenConnection();

        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        // No busy_timeout: SQLite's own handler, in the build Microsoft.Data.Sqlite
        // ships, sleeps in whole seconds, so every brief collision between two
        // writers cost a full second and a page of card art stalled the app behind
        // them. Without it, a locked statement comes straight back and the driver
        // retries it in short steps, up to DefaultTimeout.
        pragma.CommandText = """
            PRAGMA journal_mode=WAL;
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
            attributes TEXT,
            effect    TEXT,
            -- The card's [Trigger] box; "trigger" itself is an SQL keyword.
            trigger_text TEXT,
            block_icon INTEGER,
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

        -- Every account has a personal binder; shared binders have several members.
        -- See Migrations/Postgres/0003_binders.sql.
        CREATE TABLE IF NOT EXISTS binders (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            name       TEXT NOT NULL,
            kind       TEXT NOT NULL CHECK (kind IN ('personal', 'shared')),
            owner_id   INTEGER,
            visible    INTEGER NOT NULL DEFAULT 0,
            created_at TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE UNIQUE INDEX IF NOT EXISTS idx_binders_personal
            ON binders(owner_id) WHERE kind = 'personal';

        CREATE TABLE IF NOT EXISTS binder_members (
            binder_id INTEGER NOT NULL REFERENCES binders(id) ON DELETE CASCADE,
            user_id   INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
            added_at  TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (binder_id, user_id)
        );
        CREATE INDEX IF NOT EXISTS idx_binder_members_user ON binder_members(user_id);

        CREATE TABLE IF NOT EXISTS collection (
            binder_id INTEGER NOT NULL,
            card_id   TEXT NOT NULL,
            qty       INTEGER NOT NULL CHECK (qty >= 0),
            note      TEXT NOT NULL DEFAULT '',
            added_at  TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at TEXT NOT NULL DEFAULT (datetime('now')),
            added_by  INTEGER,
            PRIMARY KEY (binder_id, card_id)
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

        -- Prints someone added by hand because no card source lists them. Each also
        -- has a catalog row, put back whenever the catalogue is reseeded; a deleted
        -- one is kept, marked, so its id is never handed out again.
        -- See Migrations/Postgres/0006_custom_prints.sql.
        CREATE TABLE IF NOT EXISTS custom_prints (
            card_id    TEXT PRIMARY KEY,
            base_id    TEXT NOT NULL,
            name       TEXT NOT NULL,
            variant    TEXT NOT NULL,
            set_label  TEXT,
            rarity     TEXT,
            created_by INTEGER,
            created_at TEXT NOT NULL DEFAULT (datetime('now')),
            deleted_at TEXT
        );

        -- Each source's price for a printing; prices holds the one it is shown at.
        -- See Migrations/Postgres/0005_card_prices.sql.
        CREATE TABLE IF NOT EXISTS card_prices (
            card_id    TEXT NOT NULL,
            source     TEXT NOT NULL,
            currency   TEXT NOT NULL,
            amount     REAL NOT NULL,
            gbp        REAL NOT NULL,
            url        TEXT,
            fetched_at TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (card_id, source)
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

        -- Work that happens outside a request: fetching card art, sending mail,
        -- reading a scan. See JobQueue for the life cycle of a row.
        CREATE TABLE IF NOT EXISTS jobs (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            type           TEXT NOT NULL,
            payload        TEXT NOT NULL DEFAULT '{}',
            status         TEXT NOT NULL DEFAULT 'queued',
            attempts       INTEGER NOT NULL DEFAULT 0,
            max_attempts   INTEGER NOT NULL DEFAULT 5,
            priority       INTEGER NOT NULL DEFAULT 0,
            run_after      TEXT NOT NULL DEFAULT (datetime('now')),
            locked_until   TEXT,
            dedupe_key     TEXT,
            correlation_id TEXT,
            user_id        INTEGER,
            result         TEXT,
            last_error     TEXT,
            created_at     TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at     TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE UNIQUE INDEX IF NOT EXISTS idx_jobs_dedupe ON jobs(dedupe_key)
            WHERE dedupe_key IS NOT NULL AND status IN ('queued', 'running');

        -- Where each card's picture is: fetched into the image store, waiting to
        -- be, or failing - and when to try a failing one again.
        CREATE TABLE IF NOT EXISTS card_images (
            card_id         TEXT PRIMARY KEY,
            status          TEXT NOT NULL DEFAULT 'pending',
            source_url      TEXT,
            object_key      TEXT,
            last_attempt_at TEXT,
            next_attempt_at TEXT,
            failure_count   INTEGER NOT NULL DEFAULT 0,
            last_error      TEXT,
            updated_at      TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    /// <summary>
    /// The user_id (or, in the collection, binder_id) a row carries when it
    /// predates accounts. The first account created claims it - see UserRepository.Create. Rows are left with this owner
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
        if (!HasColumn(conn, "collection", "user_id") && !HasColumn(conn, "collection", "binder_id"))
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

        // Collections move from accounts into binders. Each account's personal
        // binder takes the account's id, so its rows need only the column renamed;
        // rows still at 0 are unclaimed and stay so.
        if (HasColumn(conn, "collection", "user_id"))
        {
            using var tx = conn.BeginTransaction();
            Exec(conn, """
                INSERT INTO binders (id, name, kind, owner_id)
                    SELECT id, 'Mine', 'personal', id FROM users;
                INSERT INTO binder_members (binder_id, user_id)
                    SELECT id, id FROM users;
                ALTER TABLE collection RENAME COLUMN user_id TO binder_id;
                ALTER TABLE collection ADD COLUMN added_by INTEGER;
                UPDATE collection SET added_by = binder_id WHERE binder_id <> 0;
                """);
            tx.Commit();
            Console.WriteLine("migrated: collections now live in binders");
        }

        // The Trigger box, the attribute icon and the block icon, which the first
        // scrapers did not read. The rows stay empty until the catalogue is
        // reseeded; Initialise does that by itself for a catalogue that has none.
        if (!HasColumn(conn, "catalog", "trigger_text"))
        {
            Exec(conn, """
                ALTER TABLE catalog ADD COLUMN attributes TEXT;
                ALTER TABLE catalog ADD COLUMN trigger_text TEXT;
                ALTER TABLE catalog ADD COLUMN block_icon INTEGER;
                """);
            Console.WriteLine("migrated: the catalogue can carry Trigger text and attributes");
        }

        // Builds from before job priorities made the table without the column.
        if (!HasColumn(conn, "jobs", "priority"))
            Exec(conn, "ALTER TABLE jobs ADD COLUMN priority INTEGER NOT NULL DEFAULT 0");

        // Indexes on the migrated columns go last, once every column they name is
        // certain to exist. Putting them in Schema alongside their tables looks
        // tidier and breaks every database that predates the column, because
        // CREATE TABLE IF NOT EXISTS skips but CREATE INDEX does not.
        Exec(conn, "CREATE INDEX IF NOT EXISTS idx_decks_user ON decks(user_id)");
        Exec(conn, "CREATE INDEX IF NOT EXISTS idx_jobs_ready ON jobs(status, priority DESC, run_after)");

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

    /// <summary>
    /// Brings the schema up to date without touching the data in it: the versioned
    /// migrations on PostgreSQL, the column-sniffing upgrade on SQLite.
    /// </summary>
    public void EnsureSchema()
    {
        using var conn = Open();
        if (Dialect.IsPostgres)
        {
            foreach (var version in PostgresMigrations.Apply(conn))
                Console.WriteLine($"migrated: applied {version}");

            // Npgsql learnt the database's types on the first connection, which may
            // have been before the migrations created citext - and is, on a container
            // that started while another one was migrating. Without this it cannot
            // read a username back.
            ((NpgsqlConnection)conn).ReloadTypes();
        }
        else
        {
            conn.Exec(Schema);
            Migrate((SqliteConnection)conn);
        }
    }

    /// <summary>Creates the schema, and seeds the catalogue if it is empty.</summary>
    /// <returns>An error message to exit with, or null on success.</returns>
    public string? Initialise(bool forceReseed)
    {
        EnsureSchema();
        using var conn = Open();

        // Counted and seeded under one lock, so two containers starting on an empty
        // database do not both decide it needs seeding and both fill it.
        using var tx = conn.BeginTransaction();
        Dialect.Lock(conn, "manifest:catalog-seed");

        long have, triggers;
        using (var count = conn.CreateCommand())
        {
            count.CommandText = "SELECT count(*), count(trigger_text) FROM catalog";
            using var r = count.ExecuteReader();
            r.Read();
            have = r.GetInt64(0);
            triggers = r.GetInt64(1);
        }

        // A catalogue seeded before the Trigger column existed has it NULL on every
        // row; one seeded since has '' on a card without a Trigger. Such a catalogue
        // is reseeded once, but only when catalog.json has the triggers to fill it
        // with - an older file would otherwise be read again on every start.
        var predatesTriggers = have != 0 && triggers == 0;

        if (have != 0 && !forceReseed && !predatesTriggers)
        {
            Console.WriteLine($"catalog ready: {have} printings");
            return null;
        }

        if (!File.Exists(_paths.Catalog))
            return predatesTriggers && !forceReseed
                ? null
                : "catalog.json is missing. Run: manifest refresh-catalog";

        List<CatalogRow> rows;
        using (var stream = File.OpenRead(_paths.Catalog))
            rows = JsonSerializer.Deserialize<List<CatalogRow>>(stream, Json.Options) ?? new();

        if (have != 0 && !forceReseed && !rows.Any(r => !string.IsNullOrEmpty(r.Trigger)))
        {
            Console.WriteLine($"catalog ready: {have} printings, without Trigger text "
                              + "(catalog.json predates it)");
            return null;
        }

        // Cards the official English site leaves out, from Limitless - until it lists
        // them itself. Before the Japanese prints, so theirs read in English too.
        if (File.Exists(_paths.CatalogExtra))
        {
            using var stream = File.OpenRead(_paths.CatalogExtra);
            var extra = JsonSerializer.Deserialize<List<CatalogRow>>(stream, Json.Options) ?? new();
            var official = rows.Select(r => r.CardId).ToHashSet(StringComparer.Ordinal);
            rows.AddRange(extra.Where(r => !string.IsNullOrWhiteSpace(r.CardId) && official.Add(r.CardId)));
        }

        // Japanese prints, when the site has been scraped for them, are seeded as
        // further printings of the same card numbers.
        if (File.Exists(_paths.CatalogJapanese))
        {
            using var stream = File.OpenRead(_paths.CatalogJapanese);
            var jp = JsonSerializer.Deserialize<List<CatalogRow>>(stream, Json.Options) ?? new();
            rows.AddRange(JapanesePrints.FromScrape(rows, jp));
        }

        // Prints added by hand, last, so each takes its card text from the rows above.
        rows.AddRange(CustomPrintRepository.Restore(conn, rows));

        conn.Exec("DELETE FROM catalog");
        InsertCatalog(conn, rows);

        tx.Commit();
        Console.WriteLine($"catalog seeded: {rows.Count} printings");
        return null;
    }

    /// <summary>Catalogue rows in, one INSERT each, on the caller's connection and transaction.</summary>
    public static void InsertCatalog(DbConnection conn, IEnumerable<CatalogRow> rows)
    {
        using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO catalog
              (card_id, base_id, variant, name, set_label, set_name, rarity,
               category, colors, cost, power, counter, types, attributes, effect,
               trigger_text, block_icon, image_url)
            VALUES (@card_id,@base_id,@variant,@name,@set_label,@set_name,@rarity,
                    @category,@colors,@cost,@power,@counter,@types,@attributes,@effect,
                    @trigger_text,@block_icon,@image_url)
            """;
        // Untyped, so each provider infers from the value: SQLite would take text for
        // the numeric columns and convert it, PostgreSQL would refuse.
        var p = new[]
        {
            "@card_id", "@base_id", "@variant", "@name", "@set_label", "@set_name",
            "@rarity", "@category", "@colors", "@cost", "@power", "@counter",
            "@types", "@attributes", "@effect", "@trigger_text", "@block_icon", "@image_url",
        }.Select(n => insert.Bind(n, null)).ToArray();

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
            p[13].Value = (object?)r.Attributes ?? DBNull.Value;
            p[14].Value = (object?)r.Effect ?? DBNull.Value;
            p[15].Value = (object?)r.Trigger ?? DBNull.Value;
            p[16].Value = (object?)r.BlockIcon ?? DBNull.Value;
            p[17].Value = (object?)r.ImageUrl ?? DBNull.Value;
            insert.ExecuteNonQuery();
        }
    }
}
