using System.Data.Common;
using System.Globalization;
using Manifest.Data;
using Microsoft.Data.Sqlite;

namespace Manifest.Tools;

/// <summary>
/// The one-way move from a manifest.db to PostgreSQL. Every row is copied with its
/// id, so sessions stay signed in, links already sitting in someone's mailbox keep
/// working, and account 1 is still the owner. It all goes in one transaction that
/// checks its own counts before committing: a move that stops half way leaves the
/// target exactly as empty as it found it.
/// </summary>
public static class SqliteToPostgres
{
    const string Usage = """
        manifest migrate-sqlite [--sqlite PATH] [--postgres URL] [--owner NAME]

          --sqlite PATH    the database to copy (default: manifest.db under --root)
          --postgres URL   where to (default: MANIFEST_DATABASE_URL)
          --owner NAME     the account to give rows that belong to nobody - the
                           collection and decks from before accounts existed

        The target must have no accounts, collections, decks or requests in it yet;
        its catalogue and prices are replaced. Stop the server first. The file is
        read, never written: the move works on a temporary copy of it, so it can be
        mounted read-only into a container.
        """;

    /// <summary>
    /// What to copy, in an order that satisfies the foreign keys. Where is applied
    /// to the source, and drops rows whose parent is gone - SQLite only enforces
    /// foreign keys when asked to, and an old file may hold rows from a time when
    /// nothing asked.
    /// </summary>
    static readonly (string Table, string Columns, string? Where)[] Tables =
    {
        ("catalog", "card_id, base_id, variant, name, set_label, set_name, rarity, category, "
                    + "colors, cost, power, counter, types, attributes, effect, trigger_text, "
                    + "block_icon, image_url", null),
        ("prices", "card_id, usd, gbp, fetched_at", null),
        ("card_prices", "card_id, source, currency, amount, gbp, url, fetched_at", null),
        ("users", "id, username, password_hash, email, created_at, last_seen", null),
        ("binders", "id, name, kind, owner_id, visible, created_at", null),
        ("binder_members", "binder_id, user_id, added_at",
         "binder_id IN (SELECT id FROM binders) AND user_id IN (SELECT id FROM users)"),
        ("access_requests", "id, email, note, status, created_at, decided_at, decided_by, "
                            + "requested_from, action_token_hash, action_expires_at, "
                            + "invite_token_hash, invite_expires_at, invite_sent_at, used_at, user_id",
         null),
        ("sessions", "token_hash, user_id, created_at, last_seen, expires_at",
         "user_id IN (SELECT id FROM users)"),
        ("decks", "id, user_id, name, leader_card_id, created_at, updated_at", null),
        ("deck_cards", "deck_id, card_id, qty", "deck_id IN (SELECT id FROM decks)"),
        ("collection", "binder_id, card_id, qty, note, added_at, updated_at, added_by", null),
        ("scan_log", "id, user_id, card_id, result, at", null),
    };

    /// <summary>
    /// The tables whose rows can belong to nobody, and the column that says so: 0
    /// there means the row predates accounts. The collection's rows belong to a
    /// binder; the others' to an account.
    /// </summary>
    static readonly Dictionary<string, string> Owned = new()
    {
        ["collection"] = "binder_id",
        ["decks"] = "user_id",
        ["scan_log"] = "user_id",
    };

    static readonly HashSet<string> Timestamps = new(StringComparer.Ordinal)
    {
        "created_at", "last_seen", "decided_at", "action_expires_at", "invite_expires_at",
        "invite_sent_at", "used_at", "expires_at", "added_at", "updated_at", "at", "fetched_at",
    };

    public static int Run(string[] args, AppPaths paths)
    {
        string? sqlite = null, postgres = null, owner = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    Console.WriteLine(Usage);
                    return 0;
                case "--sqlite" when i + 1 < args.Length:
                    sqlite = args[++i];
                    break;
                case "--postgres" when i + 1 < args.Length:
                    postgres = args[++i];
                    break;
                case "--owner" when i + 1 < args.Length:
                    owner = args[++i];
                    break;
                case "--root" when i + 1 < args.Length:
                    i++;   // already taken by AppPaths
                    break;
                default:
                    Console.Error.WriteLine($"unknown option: {args[i]}\n\n{Usage}");
                    return 2;
            }
        }

        var file = Path.GetFullPath(sqlite ?? paths.DbPath);
        // Checked here because opening a path that is not there creates an empty
        // database, which would then migrate very successfully into nothing.
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"{file} does not exist.");
            return 1;
        }

        postgres ??= AppConfig.DatabaseUrlFromEnvironment();
        if (Database.RejectUrl(postgres) is { } bad)
        {
            Console.Error.WriteLine(bad);
            return 1;
        }

        // A working copy: bringing an old file's schema up to date - and SQLite's
        // WAL mode - both write, and the original should come out of this as it
        // went in. Any -wal beside it holds writes not yet folded into the file.
        var work = Directory.CreateTempSubdirectory("manifest-migrate-").FullName;
        var copy = Path.Combine(work, "manifest.db");
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(file + suffix))
            {
                File.Copy(file + suffix, copy + suffix);
                // The copy inherits the original's permissions, read-only included.
                File.SetAttributes(copy + suffix, FileAttributes.Normal);
            }
        var source = new Database(paths, "sqlite://" + copy);
        var target = new Database(paths, postgres);
        if (!target.Dialect.IsPostgres)
        {
            Console.Error.WriteLine("--postgres (or MANIFEST_DATABASE_URL) must be a PostgreSQL URL.");
            return 1;
        }

        Console.WriteLine($"from SQLite at {file}");
        Console.WriteLine($"  to {target.Description}");

        try
        {
            return Copy(source, target, owner);
        }
        catch (MigrationError e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(work, recursive: true); } catch { /* temp; best effort */ }
        }
    }

    sealed class MigrationError(string message) : Exception(message);

    static int Copy(Database source, Database target, string? ownerName)
    {
        source.EnsureSchema();
        target.EnsureSchema();

        using var src = (SqliteConnection)source.Open();
        // A read transaction, so the copy is of one moment in the file's life even
        // if something writes to it meanwhile.
        using var snapshot = src.BeginTransaction(deferred: true);

        var owner = ResolveOwner(src, ownerName);
        // Unclaimed cards go into the owner's own binder, which the file has because
        // bringing its schema up to date gave every account one.
        long? ownerBinder = owner is { } o ? PersonalBinder(src, o) : null;

        using var dst = target.Open();
        using var tx = dst.BeginTransaction();
        RefuseIfInUse(dst);

        // Replaced rather than merged: the server may already have seeded the
        // target's catalogue from catalog.json, and the file's is the one its
        // collection and decks were made against.
        dst.Exec("DELETE FROM card_prices; DELETE FROM prices; DELETE FROM catalog;");

        var copied = new Dictionary<string, long>();
        foreach (var (table, columns, where) in Tables)
        {
            copied[table] = CopyTable(src, dst, table, columns, where, owner, ownerBinder);
            Console.WriteLine($"  {table,-16} {copied[table],8:N0} rows");
        }

        Verify(src, dst, copied);
        ResetIdentities(dst);
        tx.Commit();

        Console.WriteLine("done. Point MANIFEST_DATABASE_URL at it and start the server.");
        return 0;
    }

    /// <summary>
    /// Who the rows from before accounts get. The server's own rule is that the first
    /// account claims them; by the time a file is old enough to move, that may have
    /// been missed (account 1 deleted, say), so it is asked for rather than guessed.
    /// </summary>
    static long? ResolveOwner(SqliteConnection src, string? name)
    {
        long? owner = null;
        if (name is not null)
        {
            using var cmd = src.CreateCommand();
            cmd.CommandText = "SELECT id FROM users WHERE username = @n";
            cmd.Bind("@n", name.Trim());
            owner = cmd.ExecuteScalar() is { } id
                ? Convert.ToInt64(id)
                : throw new MigrationError($"--owner: there is no account called {name}.");
        }

        var unclaimed = Owned.Sum(t => Count(src, $"SELECT COUNT(*) FROM {t.Key} WHERE {t.Value} = 0"));
        if (unclaimed == 0 || owner is not null) return owner;

        // Nobody to give them to yet: left as they are, and the first account made
        // on the new server claims them, exactly as it would have on the old one.
        if (Count(src, "SELECT COUNT(*) FROM users") == 0) return null;

        throw new MigrationError(
            $"{unclaimed} rows belong to no account. Say whose they are with --owner <name>.");
    }

    static long PersonalBinder(SqliteConnection src, long userId)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT id FROM binders WHERE kind = 'personal' AND owner_id = @u";
        cmd.Bind("@u", userId);
        return cmd.ExecuteScalar() is { } id
            ? Convert.ToInt64(id)
            : throw new MigrationError($"account {userId} has no binder of its own in the file.");
    }

    static void RefuseIfInUse(DbConnection dst)
    {
        var busy = new[] { "users", "access_requests", "sessions", "binders", "decks", "collection",
                           "scan_log" }
            .Where(t => Count(dst, $"SELECT COUNT(*) FROM (SELECT 1 FROM {t} LIMIT 1) x") > 0)
            .ToList();
        if (busy.Count > 0)
            throw new MigrationError(
                $"the target already has data in {string.Join(", ", busy)}. "
                + "Migrate into a fresh database, so nothing there is overwritten or mixed in.");
    }

    static long CopyTable(SqliteConnection src, DbConnection dst, string table, string columns,
                          string? where, long? owner, long? ownerBinder)
    {
        var names = columns.Split(',', StringSplitOptions.TrimEntries);

        using var read = src.CreateCommand();
        read.CommandText = $"SELECT {columns} FROM {table}" + (where is null ? "" : $" WHERE {where}");

        using var write = dst.CreateCommand();
        write.CommandText = $"INSERT INTO {table} ({columns}) VALUES ("
                            + string.Join(", ", names.Select(n => "@" + n)) + ")";
        // The owner's own copy of a card and the unclaimed copy of it become one row.
        if (table == "collection" && owner is not null)
            write.CommandText += """
                 ON CONFLICT (binder_id, card_id) DO UPDATE
                   SET qty = collection.qty + excluded.qty,
                       note = COALESCE(NULLIF(collection.note, ''), excluded.note),
                       added_at = LEAST(collection.added_at, excluded.added_at),
                       updated_at = GREATEST(collection.updated_at, excluded.updated_at)
                """;
        var parameters = names.Select(n => write.Bind("@" + n, null)).ToArray();

        long rows = 0;
        using var r = read.ExecuteReader();
        while (r.Read())
        {
            for (var i = 0; i < names.Length; i++)
            {
                var value = r.IsDBNull(i) ? null : r.GetValue(i);
                if (value is not null && Timestamps.Contains(names[i]))
                    value = Instant(value, table, names[i]);
                if (owner is not null && Owned.TryGetValue(table, out var column)
                    && names[i] == column && value is long id && id == Database.Unclaimed)
                    value = table == "collection" ? ownerBinder!.Value : owner.Value;
                // SQLite keeps booleans as 0 and 1; PostgreSQL will not take a number.
                if (names[i] == "visible" && value is not null)
                    value = Convert.ToInt64(value) != 0;
                parameters[i].Value = value ?? DBNull.Value;
            }
            write.ExecuteNonQuery();
            rows++;
        }
        return rows;
    }

    /// <summary>SQLite's "yyyy-MM-dd HH:mm:ss" text, which is UTC, as an instant.</summary>
    static DateTime Instant(object value, string table, string column)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (DateTime.TryParseExact(text, ReaderExtensions.StampFormat, CultureInfo.InvariantCulture,
                                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                   out var exact))
            return exact;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                              DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                              out var loose))
            return loose;
        throw new MigrationError($"{table}.{column} holds \"{text}\", which is not a time.");
    }

    /// <summary>
    /// Counts in the target against what was read, before anything is committed.
    /// The collection is checked by total copies rather than rows, because folding
    /// unclaimed rows into the owner's merges some of them.
    /// </summary>
    static void Verify(SqliteConnection src, DbConnection dst, Dictionary<string, long> copied)
    {
        foreach (var (table, _, _) in Tables)
        {
            if (table == "collection") continue;
            var have = Count(dst, $"SELECT COUNT(*) FROM {table}");
            if (have != copied[table])
                throw new MigrationError($"{table}: read {copied[table]} rows but the target has {have}. "
                                         + "Nothing was committed.");
        }

        var want = Count(src, "SELECT COALESCE(SUM(qty), 0) FROM collection");
        var got = Count(dst, "SELECT COALESCE(SUM(qty), 0) FROM collection");
        if (want != got)
            throw new MigrationError($"collection: {want} copies in the file but {got} in the target. "
                                     + "Nothing was committed.");
    }

    /// <summary>
    /// Moves each id sequence past the ids just copied in. Without this the next
    /// account would be given id 1 - which is taken, and which is the owner.
    /// </summary>
    static void ResetIdentities(DbConnection dst)
    {
        foreach (var table in new[] { "users", "binders", "access_requests", "decks", "scan_log" })
            dst.Exec($"SELECT setval(pg_get_serial_sequence('{table}', 'id'), "
                     + $"COALESCE(MAX(id), 0) + 1, false) FROM {table}");
    }

    static long Count(DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }
}
