using System.Security.Cryptography;
using System.Text;
using Manifest.Models;
using Microsoft.Data.Sqlite;

namespace Manifest.Data;

/// <summary>
/// The waiting room. Someone leaves an address here, the admin decides, and an
/// approval turns into a single-use link that the address - and only the address -
/// can spend on an account.
///
/// Two secrets are involved and neither is stored. The action token goes to the
/// admin's mailbox and is what makes the approve link in that mail work; the invite
/// token goes to the applicant's mailbox and is what makes the sign-up link work.
/// Only SHA-256 digests are kept, so a stolen manifest.db is not a working link into
/// anyone's inbox - the same bargain UserRepository strikes with session tokens, and
/// for the same reason a plain hash is enough: 256 bits of randomness has no small
/// space to search.
/// </summary>
public sealed class AccessRepository : IAccessRepository
{
    readonly Database _db;
    readonly IClock _clock;

    public AccessRepository(Database db) : this(db, SystemClock.Instance) { }

    public AccessRepository(Database db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// How long the approve/deny link in the admin's mail keeps working. Generous,
    /// because it sits in a mailbox that might not be read for a fortnight, and the
    /// worst it can do is admit someone the admin already meant to admit.
    /// </summary>
    public static readonly TimeSpan ActionLife = TimeSpan.FromDays(14);

    /// <summary>
    /// How long the applicant has to turn an approval into an account. Shorter than
    /// the action link: this one creates an account, and a link that makes accounts
    /// should not lie around in a mailbox indefinitely.
    /// </summary>
    public static readonly TimeSpan InviteLife = TimeSpan.FromDays(7);

    /// <summary>
    /// How long before asking again re-sends a lost invite. Without a floor, anyone
    /// who knows an approved address could use the request form to shell that
    /// person's inbox one message at a time.
    /// </summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromHours(1);

    public const int MaxNote = 500;

    /// <summary>
    /// What Submit decided to do, so the endpoint knows which mail to send while
    /// still answering the caller the same way in every case.
    /// </summary>
    public enum Outcome
    {
        /// <summary>A new request. Mail the admin.</summary>
        Filed,

        /// <summary>Already waiting on the admin. Say nothing to anyone.</summary>
        AlreadyPending,

        /// <summary>Approved and unspent, and the last mail is old enough. Re-send it.</summary>
        InviteResent,

        /// <summary>Approved recently, or denied, or already an account. Do nothing.</summary>
        Ignored,
    }

    public sealed record Submission(
        Outcome Outcome, AccessRequest? Request, string? ActionToken, string? InviteToken);

    /// <summary>
    /// Files a request, or works out that there is nothing to file. Every branch is
    /// silent to the caller - the endpoint answers identically whichever one is
    /// taken - so that the form cannot be used to ask whether an address is known
    /// here, which is the one thing this table would otherwise be happy to tell
    /// anyone who asked.
    /// </summary>
    public Submission Submit(string email, string? note, string? from)
    {
        var address = Normalise(email);
        var text = (note ?? "").Trim();
        if (text.Length > MaxNote) text = text[..MaxNote];

        using var conn = _db.Open();
        CardRepository.Exec(conn, "BEGIN IMMEDIATE");
        try
        {
            Submission result;

            if (HasAccount(conn, address))
            {
                result = new Submission(Outcome.Ignored, null, null, null);
            }
            else if (Latest(conn, address) is { } existing)
            {
                result = Reconsider(conn, existing);
            }
            else
            {
                var token = NewToken();
                long id;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = """
                        INSERT INTO access_requests
                            (email, note, requested_from, action_token_hash, action_expires_at)
                        VALUES (@e, @n, @f, @t, @x);
                        SELECT last_insert_rowid();
                        """;
                    cmd.Parameters.AddWithValue("@e", address);
                    cmd.Parameters.AddWithValue("@n", text);
                    cmd.Parameters.AddWithValue("@f", (object?)from ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@t", Digest(token));
                    cmd.Parameters.AddWithValue("@x", Stamp(_clock.UtcNow + ActionLife));
                    id = Convert.ToInt64(cmd.ExecuteScalar());
                }
                result = new Submission(Outcome.Filed, ById(conn, id), token, null);
            }

            CardRepository.Exec(conn, "COMMIT");
            return result;
        }
        catch
        {
            CardRepository.Exec(conn, "ROLLBACK");
            throw;
        }
    }

    /// <summary>
    /// What to do when this address has asked before. A pending request is left
    /// exactly as it is - re-filing it would move it to the bottom of the admin's
    /// list and re-send the mail, which is a spam lever. An unspent approval is
    /// re-sent, because the commonest reason someone asks twice is that the first
    /// mail went to spam and they have no other way to say so.
    /// </summary>
    Submission Reconsider(SqliteConnection conn, AccessRequest existing)
    {
        if (existing.Status == AccessStatus.Pending)
            return new Submission(Outcome.AlreadyPending, existing, null, null);

        if (existing.Status != AccessStatus.Approved || existing.Used)
            return new Submission(Outcome.Ignored, existing, null, null);

        if (SentRecently(conn, existing.Id))
            return new Submission(Outcome.Ignored, existing, null, null);

        var token = Reissue(conn, existing.Id);
        return new Submission(Outcome.InviteResent, ById(conn, existing.Id), null, token);
    }

    bool SentRecently(SqliteConnection conn, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT 1 FROM access_requests
            WHERE id = @id AND invite_sent_at IS NOT NULL AND invite_sent_at > @since
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@since", Stamp(_clock.UtcNow - ResendCooldown));
        return cmd.ExecuteScalar() is not null;
    }

    static bool HasAccount(SqliteConnection conn, string email)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM users WHERE email = @e";
        cmd.Parameters.AddWithValue("@e", email);
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>The most recent request from an address, whatever became of it.</summary>
    static AccessRequest? Latest(SqliteConnection conn, string email)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM access_requests WHERE email = @e ORDER BY id DESC LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@e", email);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    public AccessRequest? ById(long id)
    {
        using var conn = _db.Open();
        return ById(conn, id);
    }

    static AccessRequest? ById(SqliteConnection conn, long id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM access_requests WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>
    /// The request an approve/deny link refers to, or null if the link is unknown,
    /// out of date, or already spent. Deliberately does not act on it: the link
    /// arrives as a GET, and mail clients and link scanners fetch GETs on their own.
    /// Deciding happens on the POST the review page then makes.
    /// </summary>
    public AccessRequest? ByActionToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 200) return null;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM access_requests
            WHERE action_token_hash = @t
              AND status = 'pending'
              AND action_expires_at > @now
            """;
        cmd.Parameters.AddWithValue("@t", Digest(token));
        cmd.Parameters.AddWithValue("@now", Stamp(_clock.UtcNow));
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>
    /// Says yes. Returns the token to put in the applicant's link - the one and only
    /// moment it exists outside a mailbox - or null if the request went somewhere
    /// else in the meantime, which is what makes a second click on the same mail
    /// harmless rather than a second invite.
    /// </summary>
    public (AccessRequest Request, string InviteToken)? Approve(long id, string decidedBy)
    {
        using var conn = _db.Open();
        CardRepository.Exec(conn, "BEGIN IMMEDIATE");
        try
        {
            var found = ById(conn, id);
            (AccessRequest, string)? result = null;

            // An approved-but-unspent row can be approved again, which is how the
            // admin page re-sends a link that expired. One that was actually used
            // cannot: that would mint a second account's worth of invite.
            var open = found is not null
                       && !found.Used
                       && found.Status is AccessStatus.Pending or AccessStatus.Denied
                                          or AccessStatus.Approved;
            if (open)
            {
                var token = Reissue(conn, id);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = """
                        UPDATE access_requests
                           SET status = 'approved',
                               decided_at = @now,
                               decided_by = @by,
                               action_token_hash = NULL
                         WHERE id = @id
                        """;
                    cmd.Parameters.AddWithValue("@now", Stamp(_clock.UtcNow));
                    cmd.Parameters.AddWithValue("@by", decidedBy);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
                result = (ById(conn, id)!, token);
            }

            CardRepository.Exec(conn, "COMMIT");
            return result;
        }
        catch
        {
            CardRepository.Exec(conn, "ROLLBACK");
            throw;
        }
    }

    /// <summary>
    /// Mints a fresh invite token, replacing whatever was there. Any link already in
    /// the applicant's mailbox stops working the moment this runs, which is what
    /// makes re-sending safe: there is never more than one live link per request.
    /// </summary>
    string Reissue(SqliteConnection conn, long id)
    {
        var token = NewToken();
        var now = _clock.UtcNow;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE access_requests
               SET invite_token_hash = @t,
                   invite_expires_at = @x,
                   invite_sent_at = @now
             WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("@t", Digest(token));
        cmd.Parameters.AddWithValue("@x", Stamp(now + InviteLife));
        cmd.Parameters.AddWithValue("@now", Stamp(now));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
        return token;
    }

    /// <summary>
    /// Says no. The invite token is cleared as well as the action token, so denying
    /// something previously approved really does kill the link that went out.
    /// </summary>
    public AccessRequest? Deny(long id, string decidedBy)
    {
        using var conn = _db.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE access_requests
                   SET status = 'denied',
                       decided_at = @now,
                       decided_by = @by,
                       action_token_hash = NULL,
                       invite_token_hash = NULL,
                       invite_expires_at = NULL
                 WHERE id = @id AND used_at IS NULL
                """;
            cmd.Parameters.AddWithValue("@now", Stamp(_clock.UtcNow));
            cmd.Parameters.AddWithValue("@by", decidedBy);
            cmd.Parameters.AddWithValue("@id", id);
            if (cmd.ExecuteNonQuery() == 0) return null;
        }
        return ById(id);
    }

    /// <summary>
    /// The request a sign-up link refers to, or null if the link is unknown, denied,
    /// expired, or already spent. The address on the returned row is what the new
    /// account gets: the applicant chooses a username and a password, never an
    /// address, so an invite cannot be redirected to somewhere it was not sent.
    /// </summary>
    public AccessRequest? ByInviteToken(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 200) return null;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM access_requests
            WHERE invite_token_hash = @t
              AND status = 'approved'
              AND used_at IS NULL
              AND invite_expires_at > @now
            """;
        cmd.Parameters.AddWithValue("@t", Digest(token));
        cmd.Parameters.AddWithValue("@now", Stamp(_clock.UtcNow));
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>
    /// Spends the invite, in the same breath as checking it is still there. The
    /// WHERE clause repeats every condition ByInviteToken checked rather than
    /// trusting the earlier look: two browsers submitting the same link at once
    /// would otherwise both pass the check and both make an account.
    /// </summary>
    public bool Spend(string token)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE access_requests
               SET used_at = @now,
                   invite_token_hash = NULL
             WHERE invite_token_hash = @t
               AND status = 'approved'
               AND used_at IS NULL
               AND invite_expires_at > @now
            """;
        cmd.Parameters.AddWithValue("@now", Stamp(_clock.UtcNow));
        cmd.Parameters.AddWithValue("@t", Digest(token));
        return cmd.ExecuteNonQuery() == 1;
    }

    /// <summary>
    /// Records which account the invite turned into, once there is one. Separate
    /// from Spend because the invite has to be spent before the account is made -
    /// otherwise two browsers on one link both pass the check - and the account's id
    /// does not exist until after that.
    /// </summary>
    public void Attach(long id, long userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE access_requests SET user_id = @u WHERE id = @id";
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Puts a spent invite back, for the one case that needs it: the token was
    /// spent, and then making the account failed anyway because someone took the
    /// username in between. Burning a person's only link over a race they did not
    /// cause would leave them with no way back in and nothing to show the admin.
    /// </summary>
    public void Unspend(long id, string token)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE access_requests
               SET used_at = NULL,
                   user_id = NULL,
                   invite_token_hash = @t
             WHERE id = @id AND used_at IS NOT NULL
            """;
        cmd.Parameters.AddWithValue("@t", Digest(token));
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Newest first, because the interesting one is nearly always the last.</summary>
    public List<AccessRequest> List(string? status = null, int limit = 200)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = status is null
            ? "SELECT * FROM access_requests ORDER BY id DESC LIMIT @n"
            : "SELECT * FROM access_requests WHERE status = @s ORDER BY id DESC LIMIT @n";
        if (status is not null) cmd.Parameters.AddWithValue("@s", status);
        cmd.Parameters.AddWithValue("@n", Math.Clamp(limit, 1, 1000));

        var found = new List<AccessRequest>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) found.Add(Read(r));
        return found;
    }

    public long PendingCount()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM access_requests WHERE status = 'pending'";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    /// <summary>
    /// Drops the tokens on rows nobody can act on any more. Called at startup
    /// alongside the session purge: an expired digest is not dangerous, but leaving
    /// it means the table never stops growing secrets it has no use for.
    /// </summary>
    public int PurgeExpiredTokens()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE access_requests
               SET action_token_hash = NULL
             WHERE action_token_hash IS NOT NULL AND action_expires_at <= @now;
            UPDATE access_requests
               SET invite_token_hash = NULL
             WHERE invite_token_hash IS NOT NULL AND invite_expires_at <= @now;
            """;
        cmd.Parameters.AddWithValue("@now", Stamp(_clock.UtcNow));
        return cmd.ExecuteNonQuery();
    }

    static AccessRequest Read(SqliteDataReader r) => new()
    {
        Id = r.Long("id"),
        Email = r.Text("email"),
        Note = r.Text("note"),
        Status = r.Text("status"),
        CreatedAt = r.Text("created_at"),
        DecidedAt = r.Str("decided_at"),
        DecidedBy = r.Str("decided_by"),
        InviteExpiresAt = r.Str("invite_expires_at"),
        InviteSentAt = r.Str("invite_sent_at"),
        UsedAt = r.Str("used_at"),
        UserId = r.LongOrNull("user_id"),
    };

    /// <summary>
    /// Lower-cased and trimmed, so the address that asks and the address that is
    /// mailed and the address that ends up on the account are all the same string.
    /// The columns are COLLATE NOCASE as well; this is belt and braces, and it is
    /// what gets stored.
    /// </summary>
    public static string Normalise(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// Deliberately not a full RFC 5322 parser. Anything that gets past this is
    /// checked properly by the only test that matters - whether mail to it arrives -
    /// and a stricter rule here would reject real addresses for no gain.
    /// </summary>
    public static string? RejectEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "An email address is required.";
        var e = email.Trim();
        if (e.Length > 254) return "That address is too long.";

        var at = e.IndexOf('@');
        if (at <= 0 || at != e.LastIndexOf('@')) return "That does not look like an email address.";

        var domain = e[(at + 1)..];
        if (domain.Length < 3 || !domain.Contains('.') || domain.StartsWith('.')
            || domain.EndsWith('.') || domain.Contains(".."))
            return "That does not look like an email address.";

        foreach (var c in e)
            if (char.IsWhiteSpace(c) || c is '<' or '>' or ',' or ';' or '"' or '\\')
                return "That does not look like an email address.";

        return null;
    }

    static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    static string Digest(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    static string Stamp(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
