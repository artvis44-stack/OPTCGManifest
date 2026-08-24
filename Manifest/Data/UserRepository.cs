using System.Security.Cryptography;
using System.Text;
using Manifest.Models;
using Manifest.Services;
using Microsoft.Data.Sqlite;

namespace Manifest.Data;

/// <summary>Raised when an account cannot be created or changed as asked.</summary>
public sealed class AccountError : Exception
{
    public AccountError(string message) : base(message) { }
}

/// <summary>
/// Accounts and the sessions that stand in for them. Session tokens are stored as
/// SHA-256 digests: a stolen copy of manifest.db then yields no usable cookie, the
/// same reason passwords are not stored either. The digest is a plain hash rather
/// than PBKDF2 because the token is 256 bits of randomness already - there is no
/// small space to brute force, and the lookup runs on every request.
/// </summary>
public sealed class UserRepository : IUserRepository, ISessionRepository
{
    readonly Database _db;
    readonly IClock _clock;

    public UserRepository(Database db) : this(db, SystemClock.Instance) { }

    public UserRepository(Database db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>How long a session stays valid without being used.</summary>
    public static readonly TimeSpan SessionLife = TimeSpan.FromDays(30);

    public TimeSpan SessionLifetime => SessionLife;

    /// <summary>
    /// A hash of nothing in particular, used to spend the same time verifying a
    /// password for a username that does not exist as for one that does. Without it
    /// the response time alone tells an attacker which usernames are real.
    /// </summary>
    static readonly string DecoyHash = Passwords.Hash("decoy-for-constant-time-verification");

    public long Count()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public User? ById(long id)
    {
        using var conn = _db.Open();
        return ById(conn, id);
    }

    static User? ById(SqliteConnection conn, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM users WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    public User? ByName(string username)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM users WHERE username = @n";
        cmd.Parameters.AddWithValue("@n", username.Trim());
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    public List<User> List()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM users ORDER BY id";
        var users = new List<User>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) users.Add(Read(r));
        return users;
    }

    static User Read(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        Username = r.Text("username"),
        Email = r.Str("email"),
        CreatedAt = r.Str("created_at") ?? "",
        LastSeen = r.Str("last_seen"),
        // Account 1 is whoever set the server up. It owns the pre-accounts data and
        // is the one the admin tools refuse to delete out from under you.
        IsOwner = r.Long("id") == 1,
    };

    /// <summary>
    /// Creates an account. The first one to exist also adopts everything that was in
    /// the database before accounts did - the collection and decks from the days when
    /// this ran on one machine on a home network.
    /// </summary>
    /// <summary>
    /// <paramref name="email"/> is set only when the account came in through the
    /// request-access flow, where it is the address the invite was sent to rather
    /// than anything the new account typed. Accounts made any other way have none,
    /// and nothing here invents one.
    /// </summary>
    public User Create(string username, string password, string? email = null)
    {
        if (Passwords.RejectUsername(username) is { } badName) throw new AccountError(badName);
        if (Passwords.RejectPassword(password) is { } badPass) throw new AccountError(badPass);

        var name = username.Trim();
        var address = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        var hash = Passwords.Hash(password);

        using var conn = _db.Open();
        CardRepository.Exec(conn, "BEGIN IMMEDIATE");
        try
        {
            using (var clash = conn.CreateCommand())
            {
                clash.CommandText = "SELECT 1 FROM users WHERE username = @n";
                clash.Parameters.AddWithValue("@n", name);
                if (clash.ExecuteScalar() is not null)
                    throw new AccountError("That username is taken.");
            }

            long id;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO users (username, password_hash, email) VALUES (@n, @h, @e);
                    SELECT last_insert_rowid();
                    """;
                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@h", hash);
                cmd.Parameters.AddWithValue("@e", (object?)address ?? DBNull.Value);
                id = Convert.ToInt64(cmd.ExecuteScalar());
            }

            // First account in: take ownership of the pre-accounts data.
            if (id == 1) Claim(conn, id);

            CardRepository.Exec(conn, "COMMIT");
            return new User { Id = id, Username = name, Email = address, IsOwner = id == 1 };
        }
        catch (SqliteException e) when (e.SqliteErrorCode == 19)
        {
            // 19 is SQLITE_CONSTRAINT. The only constraint reachable here that the
            // explicit check above does not already cover is the unique index on
            // email, so this is a second account for one address.
            CardRepository.Exec(conn, "ROLLBACK");
            throw new AccountError("There is already an account for that email address.");
        }
        catch
        {
            CardRepository.Exec(conn, "ROLLBACK");
            throw;
        }
    }

    static void Claim(SqliteConnection conn, long userId)
    {
        var moved = 0;
        foreach (var table in new[] { "collection", "decks", "scan_log" })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE {table} SET user_id = @u WHERE user_id = @unclaimed";
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@unclaimed", Database.Unclaimed);
            moved += cmd.ExecuteNonQuery();
        }
        if (moved > 0)
            Console.WriteLine($"  claimed {moved} pre-account rows for the first account");
    }

    /// <summary>The username and password of an existing account, or null.</summary>
    public User? Authenticate(string? username, string? password)
    {
        var name = (username ?? "").Trim();
        var secret = password ?? "";

        using var conn = _db.Open();
        string? hash = null;
        User? user = null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM users WHERE username = @n";
            cmd.Parameters.AddWithValue("@n", name);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                user = Read(r);
                hash = r.Text("password_hash");
            }
        }

        // Verify either way, so a missing username and a wrong password cost the same.
        var ok = Passwords.Verify(secret, hash ?? DecoyHash);
        if (!ok || user is null) return null;

        using (var seen = conn.CreateCommand())
        {
            seen.CommandText = "UPDATE users SET last_seen = @now WHERE id = @id";
            seen.Parameters.AddWithValue("@now", Stamp(_clock.UtcNow));
            seen.Parameters.AddWithValue("@id", user.Id);
            seen.ExecuteNonQuery();
        }
        return user;
    }

    public void SetPassword(long userId, string password)
    {
        if (Passwords.RejectPassword(password) is { } bad) throw new AccountError(bad);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE users SET password_hash = @h WHERE id = @id;
            DELETE FROM sessions WHERE user_id = @id;
            """;
        cmd.Parameters.AddWithValue("@h", Passwords.Hash(password));
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Removes an account and everything it owns. There is no undo.</summary>
    public bool Delete(string username)
    {
        var user = ByName(username);
        if (user is null) return false;

        using var conn = _db.Open();
        CardRepository.Exec(conn, "BEGIN IMMEDIATE");
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                // deck_cards has no user_id of its own; it belongs to whoever owns
                // the deck, so it goes by subquery before the decks themselves.
                cmd.CommandText = """
                    DELETE FROM deck_cards WHERE deck_id IN
                        (SELECT id FROM decks WHERE user_id = @id);
                    DELETE FROM decks WHERE user_id = @id;
                    DELETE FROM collection WHERE user_id = @id;
                    DELETE FROM scan_log WHERE user_id = @id;
                    DELETE FROM sessions WHERE user_id = @id;
                    DELETE FROM users WHERE id = @id;
                    """;
                cmd.Parameters.AddWithValue("@id", user.Id);
                cmd.ExecuteNonQuery();
            }
            CardRepository.Exec(conn, "COMMIT");
        }
        catch
        {
            CardRepository.Exec(conn, "ROLLBACK");
            throw;
        }
        return true;
    }

    // ---- sessions

    /// <summary>
    /// Returns the token to put in the cookie. Only its digest is kept here, so this
    /// is the one and only moment the token itself exists on the server.
    /// </summary>
    public string StartSession(long userId)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = _clock.UtcNow;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (token_hash, user_id, expires_at)
            VALUES (@t, @u, @e)
            """;
        cmd.Parameters.AddWithValue("@t", Digest(token));
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@e", Stamp(now + SessionLife));
        cmd.ExecuteNonQuery();
        return token;
    }

    /// <summary>
    /// The account a cookie belongs to, or null if the token is unknown or stale.
    /// Expiry slides: using the app keeps you signed in, a month away signs you out.
    /// </summary>
    public User? ForToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 200) return null;

        var now = _clock.UtcNow;
        using var conn = _db.Open();
        long userId;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT user_id FROM sessions
                WHERE token_hash = @t AND expires_at > @now
                """;
            cmd.Parameters.AddWithValue("@t", Digest(token));
            cmd.Parameters.AddWithValue("@now", Stamp(now));
            var found = cmd.ExecuteScalar();
            if (found is null or DBNull) return null;
            userId = Convert.ToInt64(found);
        }

        using (var touch = conn.CreateCommand())
        {
            touch.CommandText = """
                UPDATE sessions SET last_seen = @now, expires_at = @e
                WHERE token_hash = @t
                """;
            touch.Parameters.AddWithValue("@now", Stamp(now));
            touch.Parameters.AddWithValue("@e", Stamp(now + SessionLife));
            touch.Parameters.AddWithValue("@t", Digest(token));
            touch.ExecuteNonQuery();
        }

        return ById(conn, userId);
    }

    public void EndSession(string? token)
    {
        if (string.IsNullOrEmpty(token)) return;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE token_hash = @t";
        cmd.Parameters.AddWithValue("@t", Digest(token));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Drops sessions nobody can present any more. Called at startup.</summary>
    public int PurgeExpiredSessions()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE expires_at <= @now";
        cmd.Parameters.AddWithValue("@now", Stamp(_clock.UtcNow));
        return cmd.ExecuteNonQuery();
    }

    static string Digest(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>SQLite's datetime('now') format, so string comparison sorts by time.</summary>
    static string Stamp(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
