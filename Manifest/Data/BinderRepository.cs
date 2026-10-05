using System.Data.Common;
using Manifest.Models;

namespace Manifest.Data;

/// <summary>Raised when a binder cannot be changed as asked; the message is for the person.</summary>
public sealed class BinderError(string message) : Exception(message);

/// <summary>What an account may do with a binder.</summary>
public enum BinderRights
{
    /// <summary>Not theirs to see - reported the same as no binder at all.</summary>
    None,

    /// <summary>Someone else's personal binder, shown because they let it be.</summary>
    Read,

    /// <summary>A member: change the cards in it, and for a shared binder, who is in it.</summary>
    Write,
}

/// <summary>
/// The collection rows a read covers: one binder, or every binder an account
/// belongs to - their own plus each shared one - added together.
/// </summary>
public readonly record struct BinderScope
{
    public long? BinderId { get; private init; }
    public long? MemberId { get; private init; }

    public static BinderScope One(long binderId) => new() { BinderId = binderId };

    /// <summary>Everything this account can play with: what "All" shows and decks count.</summary>
    public static BinderScope Usable(long userId) => new() { MemberId = userId };

    /// <summary>The condition picking this scope's rows out of the collection table.</summary>
    public string Filter(DbCommand cmd, string column = "binder_id")
    {
        if (BinderId is { } binder)
        {
            cmd.Bind("@scope_binder", binder);
            return $"{column} = @scope_binder";
        }
        cmd.Bind("@scope_user", MemberId!.Value);
        return $"{column} IN (SELECT binder_id FROM binder_members WHERE user_id = @scope_user)";
    }

    /// <summary>
    /// The scope as a table of one row per card, quantities summed, to stand where
    /// the collection table used to. Who added a card only means something for one
    /// binder, so across several it is left empty.
    /// </summary>
    public string Rows(DbCommand cmd)
    {
        var addedBy = BinderId is null ? "CAST(NULL AS BIGINT)" : "MIN(added_by)";
        return $"""
            (SELECT card_id, SUM(qty) AS qty, MAX(note) AS note, MAX(updated_at) AS updated_at,
                    {addedBy} AS added_by
             FROM collection WHERE {Filter(cmd)} AND qty > 0 GROUP BY card_id)
            """;
    }
}

/// <summary>
/// Binders: where a collection lives. Every account has a personal one, made with
/// the account; shared binders have members, each of whom can change the cards in
/// it and who else is in it. A personal binder can be made visible, read-only, to
/// the people its owner shares a binder with.
/// </summary>
public sealed class BinderRepository
{
    public const int MaxName = 60;

    readonly Database _db;
    public BinderRepository(Database db) => _db = db;

    /// <summary>The account's personal binder, made now if it has none.</summary>
    public long Personal(long userId)
    {
        using var conn = _db.Open();
        return Personal(conn, userId);
    }

    public static long Personal(DbConnection conn, long userId)
    {
        using (var find = conn.CreateCommand())
        {
            find.CommandText = "SELECT id FROM binders WHERE kind = 'personal' AND owner_id = @u";
            find.Bind("@u", userId);
            if (find.ExecuteScalar() is { } id and not DBNull) return Convert.ToInt64(id);
        }
        return CreatePersonal(conn, userId);
    }

    /// <summary>Called inside the transaction that makes the account.</summary>
    public static long CreatePersonal(DbConnection conn, long userId)
    {
        long id;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO binders (name, kind, owner_id) VALUES ('Mine', 'personal', @u)
                RETURNING id
                """;
            cmd.Bind("@u", userId);
            id = Convert.ToInt64(cmd.ExecuteScalar());
        }
        AddMembership(conn, id, userId);
        return id;
    }

    static void AddMembership(DbConnection conn, long binderId, long userId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO binder_members (binder_id, user_id) VALUES (@b, @u)
            ON CONFLICT (binder_id, user_id) DO NOTHING
            """;
        cmd.Bind("@b", binderId);
        cmd.Bind("@u", userId);
        cmd.ExecuteNonQuery();
    }

    public BinderRights Rights(long userId, long binderId)
    {
        using var conn = _db.Open();
        return Rights(conn, userId, binderId);
    }

    static BinderRights Rights(DbConnection conn, long userId, long binderId)
    {
        string kind;
        long? owner;
        bool visible, member;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT b.kind, b.owner_id, b.visible,
                       (SELECT COUNT(*) FROM binder_members m
                        WHERE m.binder_id = b.id AND m.user_id = @u) AS member
                FROM binders b WHERE b.id = @b
                """;
            cmd.Bind("@u", userId);
            cmd.Bind("@b", binderId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return BinderRights.None;
            kind = r.Text("kind");
            owner = r.LongOrNull("owner_id");
            visible = Flag(r, "visible");
            member = r.Long("member") > 0;
        }
        if (member) return BinderRights.Write;
        if (kind == "personal" && visible && owner is { } o && SharesWith(conn, userId, o))
            return BinderRights.Read;
        return BinderRights.None;
    }

    /// <summary>Whether two accounts are both in at least one shared binder.</summary>
    static bool SharesWith(DbConnection conn, long userId, long otherId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM binder_members a
            JOIN binder_members o ON o.binder_id = a.binder_id
            JOIN binders s ON s.id = a.binder_id
            WHERE s.kind = 'shared' AND a.user_id = @u AND o.user_id = @o
            """;
        cmd.Bind("@u", userId);
        cmd.Bind("@o", otherId);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    static bool Flag(DbDataReader r, string column) =>
        Convert.ToBoolean(r.GetValue(r.GetOrdinal(column)));

    /// <summary>
    /// The binders this account can open: their own first, then shared ones by name,
    /// then other people's that have been made visible to them.
    /// </summary>
    public List<BinderInfo> List(long userId)
    {
        using var conn = _db.Open();
        Personal(conn, userId);

        var rows = new List<(long Id, string Name, string Kind, long? Owner, string? OwnerName,
                             bool Visible, bool Member)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT b.id, b.name, b.kind, b.owner_id, b.visible, u.username AS owner_name,
                       (SELECT COUNT(*) FROM binder_members m
                        WHERE m.binder_id = b.id AND m.user_id = @u) AS member
                FROM binders b LEFT JOIN users u ON u.id = b.owner_id
                WHERE b.id IN (SELECT binder_id FROM binder_members WHERE user_id = @u)
                   OR (b.kind = 'personal' AND b.visible = @yes AND b.owner_id IN (
                         SELECT o.user_id FROM binder_members a
                         JOIN binder_members o ON o.binder_id = a.binder_id
                         JOIN binders s ON s.id = a.binder_id
                         WHERE s.kind = 'shared' AND a.user_id = @u AND o.user_id <> @u))
                """;
            cmd.Bind("@u", userId);
            cmd.Bind("@yes", true);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                rows.Add((r.Long("id"), r.Text("name"), r.Text("kind"), r.LongOrNull("owner_id"),
                          r.Str("owner_name"), Flag(r, "visible"), r.Long("member") > 0));
        }

        var members = new Dictionary<long, List<string>>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT m.binder_id, u.username FROM binder_members m
                JOIN users u ON u.id = m.user_id
                WHERE m.binder_id IN (SELECT binder_id FROM binder_members WHERE user_id = @u)
                ORDER BY u.username
                """;
            cmd.Bind("@u", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var id = r.Long("binder_id");
                if (!members.TryGetValue(id, out var list)) members[id] = list = new();
                list.Add(r.Text("username"));
            }
        }

        int Rank(bool own, string kind) => own ? 0 : kind == "shared" ? 1 : 2;
        return rows
            .Select(b =>
            {
                var own = b.Kind == "personal" && b.Owner == userId;
                return new BinderInfo
                {
                    Id = b.Id,
                    Name = own ? "Mine"
                         : b.Kind == "personal" ? $"{b.OwnerName}'s cards"
                         : b.Name,
                    Kind = b.Kind,
                    Mine = own,
                    Writable = b.Member,
                    Visible = b.Visible,
                    Members = members.GetValueOrDefault(b.Id) ?? new(),
                };
            })
            .OrderBy(b => Rank(b.Mine, b.Kind))
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.Id)
            .ToList();
    }

    public BinderInfo? Get(long userId, long binderId) =>
        List(userId).FirstOrDefault(b => b.Id == binderId);

    static string CleanName(string? name)
    {
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) throw new BinderError("Give the binder a name.");
        if (clean.Length > MaxName) throw new BinderError($"Keep the name to {MaxName} characters.");
        return clean;
    }

    /// <summary>
    /// A new shared binder with its maker as the only member. With
    /// <paramref name="moveMine"/>, everything in the maker's own binder moves into it.
    /// </summary>
    public BinderInfo Create(long userId, string? name, bool moveMine)
    {
        var clean = CleanName(name);
        long id;
        using (var conn = _db.Open())
        using (var tx = conn.BeginTransaction())
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO binders (name, kind, owner_id) VALUES (@n, 'shared', @u)
                    RETURNING id
                    """;
                cmd.Bind("@n", clean);
                cmd.Bind("@u", userId);
                id = Convert.ToInt64(cmd.ExecuteScalar());
            }
            AddMembership(conn, id, userId);
            if (moveMine) MoveAll(conn, Personal(conn, userId), id);
            tx.Commit();
        }
        return Get(userId, id)!;
    }

    /// <summary>Every card in one binder into another, adding to any copies already there.</summary>
    static void MoveAll(DbConnection conn, long from, long to)
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO collection (binder_id, card_id, qty, note, added_at, updated_at, added_by)
                SELECT @to, card_id, qty, note, added_at, updated_at, added_by
                FROM collection WHERE binder_id = @from AND qty > 0
                ON CONFLICT (binder_id, card_id) DO UPDATE
                  SET qty = collection.qty + excluded.qty,
                      note = COALESCE(NULLIF(collection.note, ''), excluded.note),
                      updated_at = excluded.updated_at
                """;
            cmd.Bind("@from", from);
            cmd.Bind("@to", to);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM collection WHERE binder_id = @from";
            cmd.Bind("@from", from);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>The binder, if it is shared and this account is in it; otherwise why not.</summary>
    static void RequireSharedMember(DbConnection conn, long userId, long binderId)
    {
        if (Rights(conn, userId, binderId) != BinderRights.Write)
            throw new KeyNotFoundException();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT kind FROM binders WHERE id = @b";
        cmd.Bind("@b", binderId);
        if (cmd.ExecuteScalar() as string != "shared")
            throw new BinderError("Your own binder can't be renamed, shared or deleted.");
    }

    public BinderInfo Rename(long userId, long binderId, string? name)
    {
        var clean = CleanName(name);
        using (var conn = _db.Open())
        {
            RequireSharedMember(conn, userId, binderId);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE binders SET name = @n WHERE id = @b";
            cmd.Bind("@n", clean);
            cmd.Bind("@b", binderId);
            cmd.ExecuteNonQuery();
        }
        return Get(userId, binderId)!;
    }

    public BinderInfo AddMember(long userId, long binderId, string? username)
    {
        var name = (username ?? "").Trim();
        if (name.Length == 0) throw new BinderError("Say who to add.");
        using (var conn = _db.Open())
        {
            RequireSharedMember(conn, userId, binderId);
            long other;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT id FROM users WHERE username = {_db.Dialect.Nocase("@n")}";
                cmd.Bind("@n", name);
                other = cmd.ExecuteScalar() is { } id and not DBNull
                    ? Convert.ToInt64(id)
                    : throw new BinderError($"There is no account called {name}.");
            }
            AddMembership(conn, binderId, other);
        }
        return Get(userId, binderId)!;
    }

    /// <summary>
    /// Takes someone out of a shared binder - yourself included, which is leaving it.
    /// A binder nobody is left in goes, with its cards.
    /// </summary>
    public void RemoveMember(long userId, long binderId, string? username)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        RequireSharedMember(conn, userId, binderId);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                DELETE FROM binder_members WHERE binder_id = @b
                  AND user_id = (SELECT id FROM users WHERE username = {_db.Dialect.Nocase("@n")})
                """;
            cmd.Bind("@b", binderId);
            cmd.Bind("@n", (username ?? "").Trim());
            if (cmd.ExecuteNonQuery() == 0)
                throw new BinderError($"{username} is not in this binder.");
        }
        DropEmptySharedBinders(conn);
        tx.Commit();
    }

    /// <summary>Deletes a shared binder and every card in it, for all its members.</summary>
    public void Delete(long userId, long binderId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        RequireSharedMember(conn, userId, binderId);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                DELETE FROM collection WHERE binder_id = @b;
                DELETE FROM binder_members WHERE binder_id = @b;
                DELETE FROM binders WHERE id = @b;
                """;
            cmd.Bind("@b", binderId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>Shared binders whose last member has gone, and their cards.</summary>
    public static void DropEmptySharedBinders(DbConnection conn)
    {
        conn.Exec("""
            DELETE FROM collection WHERE binder_id IN (
                SELECT b.id FROM binders b WHERE b.kind = 'shared'
                AND NOT EXISTS (SELECT 1 FROM binder_members m WHERE m.binder_id = b.id));
            DELETE FROM binders WHERE kind = 'shared'
                AND NOT EXISTS (SELECT 1 FROM binder_members m WHERE m.binder_id = binders.id);
            """);
    }

    /// <summary>Whether the people you share a binder with can look at your own binder.</summary>
    public BinderInfo SetVisible(long userId, bool visible)
    {
        long id;
        using (var conn = _db.Open())
        {
            id = Personal(conn, userId);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE binders SET visible = @v WHERE id = @b";
            cmd.Bind("@v", visible);
            cmd.Bind("@b", id);
            cmd.ExecuteNonQuery();
        }
        return Get(userId, id)!;
    }

    /// <summary>
    /// Moves copies of one card between two binders this account can change. Moves
    /// as many as asked for, or as many as there are if that is fewer.
    /// </summary>
    /// <returns>What is left in each binder afterwards.</returns>
    public (int From, int To) Move(long userId, string cardId, long from, long to, int qty)
    {
        if (from == to) throw new BinderError("Pick a different binder to move it to.");
        if (qty < 1) throw new BinderError("Move at least one copy.");

        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        if (Rights(conn, userId, from) != BinderRights.Write
            || Rights(conn, userId, to) != BinderRights.Write)
            throw new KeyNotFoundException();

        int have;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT qty FROM collection WHERE binder_id = @b AND card_id = @id"
                              + _db.Dialect.ForUpdate;
            cmd.Bind("@b", from);
            cmd.Bind("@id", cardId);
            have = cmd.ExecuteScalar() is { } q and not DBNull ? Convert.ToInt32(q) : 0;
        }
        var moving = Math.Min(qty, have);
        if (moving == 0) throw new BinderError($"There is no {cardId} in that binder to move.");

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                UPDATE collection SET qty = qty - @n, updated_at = {_db.Dialect.Now}
                WHERE binder_id = @from AND card_id = @id;
                DELETE FROM collection WHERE binder_id = @from AND card_id = @id AND qty <= 0;
                INSERT INTO collection (binder_id, card_id, qty, added_by)
                VALUES (@to, @id, @n, @u)
                ON CONFLICT (binder_id, card_id) DO UPDATE
                  SET qty = collection.qty + excluded.qty, updated_at = {_db.Dialect.Now};
                """;
            cmd.Bind("@n", moving);
            cmd.Bind("@from", from);
            cmd.Bind("@to", to);
            cmd.Bind("@id", cardId);
            cmd.Bind("@u", userId);
            cmd.ExecuteNonQuery();
        }

        int Qty(long binder)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT qty FROM collection WHERE binder_id = @b AND card_id = @id";
            cmd.Bind("@b", binder);
            cmd.Bind("@id", cardId);
            return cmd.ExecuteScalar() is { } q and not DBNull ? Convert.ToInt32(q) : 0;
        }
        var result = (Qty(from), Qty(to));
        tx.Commit();
        return result;
    }
}
