using System.Data.Common;
using System.Text.Json;

namespace Manifest.Data;

/// <summary>One row of the jobs table, as a worker sees it.</summary>
public sealed record Job(
    long Id, string Type, string Payload, string Status, int Attempts, int MaxAttempts,
    string? CorrelationId, long? UserId, string? Result, string? LastError);

/// <summary>
/// Work to be done outside a request, kept in the database so it survives a restart
/// and any number of workers - in the web process or beside it - can share it.
///
/// A job is queued, then claimed by one worker for a lease, then done or failed. A
/// worker that dies mid-job simply lets its lease run out, and the job is claimed
/// again: so every handler must be safe to run twice. A failure is retried with
/// growing gaps until max_attempts, then left failed for someone to look at.
///
/// The queue is a table rather than Redis on purpose: a job enqueued in the same
/// database as the row it is about cannot be lost between the two, and there is one
/// less thing to run. SKIP LOCKED lets PostgreSQL workers claim side by side;
/// SQLite has one writer at a time anyway.
/// </summary>
public sealed class JobQueue
{
    readonly Database _db;
    readonly IClock _clock;

    public JobQueue(Database db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>How long a claim lasts before another worker may take the job over.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Raised after a job is queued from this process, so an in-process worker can
    /// start on it at once instead of on its next poll.
    /// </summary>
    public event Action? Enqueued;

    /// <summary>
    /// Queues a job, or - when <paramref name="dedupeKey"/> names one already queued
    /// or running - returns that one's id instead of queuing a second.
    /// </summary>
    public long Enqueue(string type, object payload, string? dedupeKey = null,
                        string? correlationId = null, long? userId = null,
                        int maxAttempts = 5, DateTime? runAfter = null, int priority = 0)
    {
        using var conn = _db.Open();

        // Already queued: say so without writing. Asking for the same picture again
        // - every retry of every thumbnail - would otherwise take the write lock just
        // to be told there was nothing to insert.
        if (dedupeKey is not null && Live(conn, dedupeKey) is { } live) return live;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO jobs (type, payload, dedupe_key, correlation_id, user_id, max_attempts,
                              priority, run_after, created_at, updated_at)
            VALUES (@type, @payload, @dedupe, @corr, @user, @max, @priority, @after,
                    {_db.Dialect.Now}, {_db.Dialect.Now})
            ON CONFLICT DO NOTHING
            RETURNING id
            """;
        cmd.Bind("@type", type);
        cmd.Bind("@payload", JsonSerializer.Serialize(payload, Json.Options));
        cmd.Bind("@dedupe", dedupeKey);
        cmd.Bind("@corr", correlationId);
        cmd.Bind("@user", userId);
        cmd.Bind("@max", maxAttempts);
        cmd.Bind("@priority", priority);
        cmd.Bind("@after", _db.Dialect.Time(runAfter ?? _clock.UtcNow));

        var id = cmd.ExecuteScalar();
        if (id is null or DBNull)
            // Queued by someone else in the instant since the check; or gone again
            // already, which callers treat as "done already".
            return Live(conn, dedupeKey!) ?? 0;

        Enqueued?.Invoke();
        return Convert.ToInt64(id);
    }

    static long? Live(DbConnection conn, string dedupeKey)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id FROM jobs
            WHERE dedupe_key = @dedupe AND status IN ('queued', 'running')
            """;
        cmd.Bind("@dedupe", dedupeKey);
        return cmd.ExecuteScalar() is { } found and not DBNull ? Convert.ToInt64(found) : null;
    }

    /// <summary>
    /// Queues a job unless one with this key exists in any state - for scheduled
    /// work, whose key names the time window it is for, so several workers ticking
    /// through the same window queue it once between them.
    /// </summary>
    public bool EnqueueOnce(string type, object payload, string key)
    {
        using (var conn = _db.Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT 1 FROM jobs WHERE dedupe_key = @key";
            cmd.Bind("@key", key);
            if (cmd.ExecuteScalar() is not null) return false;
        }
        Enqueue(type, payload, dedupeKey: key);
        return true;
    }

    /// <summary>
    /// Takes the next job that is due - or whose last worker's lease has run out -
    /// for <see cref="Lease"/>, highest priority first: a scan someone is waiting on
    /// goes ahead of a screenful of card art. Null when there is nothing to do.
    /// </summary>
    public Job? Claim(int minPriority = int.MinValue)
    {
        var now = _clock.UtcNow;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        // SKIP LOCKED: a row another worker is in the middle of claiming is passed
        // over rather than waited for, so workers never queue up behind each other.
        var skip = _db.Dialect.IsPostgres ? " FOR UPDATE SKIP LOCKED" : "";
        cmd.CommandText = $"""
            UPDATE jobs
               SET status = 'running', attempts = attempts + 1,
                   locked_until = @until, updated_at = {_db.Dialect.Now}
             WHERE id = (SELECT id FROM jobs
                          WHERE ((status = 'queued' AND run_after <= @now)
                                 OR (status = 'running' AND locked_until < @now))
                            AND priority >= @min
                          ORDER BY priority DESC, run_after, id
                          LIMIT 1{skip})
            RETURNING id, type, payload, status, attempts, max_attempts, correlation_id,
                      user_id, result, last_error
            """;
        cmd.Bind("@now", _db.Dialect.Time(now));
        cmd.Bind("@until", _db.Dialect.Time(now + Lease));
        cmd.Bind("@min", minPriority);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>
    /// Finished. <paramref name="forget"/> blanks the payload, for jobs that carried
    /// something that should not sit in the database afterwards - a mailed link's
    /// token, a photo of someone's card.
    /// </summary>
    public void Complete(long id, object? result = null, bool forget = false)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE jobs SET status = 'done', result = @result, last_error = NULL,
                            locked_until = NULL, updated_at = {_db.Dialect.Now}
                            {(forget ? ", payload = '{}'" : "")}
             WHERE id = @id
            """;
        cmd.Bind("@result", result is null ? null : JsonSerializer.Serialize(result, Json.Options));
        cmd.Bind("@id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Failed this time. Back in the queue after a growing pause - 10s, 20s, 40s and
    /// so on, capped at an hour - or, out of attempts, failed for good.
    /// </summary>
    public void Fail(Job job, string error, object? result = null, bool forget = false)
    {
        var final = job.Attempts >= job.MaxAttempts;
        var pause = TimeSpan.FromSeconds(Math.Min(3600, 10 * Math.Pow(2, job.Attempts - 1)));
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE jobs SET status = @status, last_error = @error, run_after = @after,
                            result = @result, locked_until = NULL, updated_at = {_db.Dialect.Now}
                            {(final && forget ? ", payload = '{}'" : "")}
             WHERE id = @id
            """;
        cmd.Bind("@status", final ? "failed" : "queued");
        cmd.Bind("@error", error.Length > 2000 ? error[..2000] : error);
        cmd.Bind("@after", _db.Dialect.Time(_clock.UtcNow + pause));
        cmd.Bind("@result", result is null ? null : JsonSerializer.Serialize(result, Json.Options));
        cmd.Bind("@id", job.Id);
        cmd.ExecuteNonQuery();
    }

    public Job? Get(long id)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, type, payload, status, attempts, max_attempts, correlation_id,
                   user_id, result, last_error
            FROM jobs WHERE id = @id
            """;
        cmd.Bind("@id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    /// <summary>How deep the queue is, for the readiness check.</summary>
    public (long Queued, long Running, long Failed) Depth()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(CASE WHEN status = 'queued' THEN 1 ELSE 0 END), 0) AS queued,
                   COALESCE(SUM(CASE WHEN status = 'running' THEN 1 ELSE 0 END), 0) AS running,
                   COALESCE(SUM(CASE WHEN status = 'failed' THEN 1 ELSE 0 END), 0) AS failed
            FROM jobs
            """;
        using var r = cmd.ExecuteReader();
        r.Read();
        return (r.Long("queued"), r.Long("running"), r.Long("failed"));
    }

    /// <summary>What the metrics report: work waiting, under way and given up on.</summary>
    public sealed record Backlog(long Due, long Running, long Failed, double OldestDueSeconds);

    /// <summary>
    /// Like <see cref="Depth"/>, but counting only queued jobs that are due - a
    /// retry waiting out its back-off is not a backlog - and how long the oldest
    /// of those has been waiting, which is what says the workers are falling behind.
    /// </summary>
    public Backlog ReadBacklog()
    {
        var now = _clock.UtcNow;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(CASE WHEN status = 'queued' AND run_after <= @now THEN 1 ELSE 0 END), 0) AS due,
                   COALESCE(SUM(CASE WHEN status = 'running' THEN 1 ELSE 0 END), 0) AS running,
                   COALESCE(SUM(CASE WHEN status = 'failed' THEN 1 ELSE 0 END), 0) AS failed,
                   MIN(CASE WHEN status = 'queued' AND run_after <= @now THEN run_after END) AS oldest
            FROM jobs
            """;
        cmd.Bind("@now", _db.Dialect.Time(now));
        using var r = cmd.ExecuteReader();
        r.Read();
        var oldest = r.Str("oldest") is { } stamp
                     && DateTime.TryParseExact(stamp, ReaderExtensions.StampFormat,
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.AdjustToUniversal
                                               | System.Globalization.DateTimeStyles.AssumeUniversal,
                                               out var at)
            ? Math.Max(0, (now - at).TotalSeconds)
            : 0;
        return new Backlog(r.Long("due"), r.Long("running"), r.Long("failed"), oldest);
    }

    /// <summary>
    /// Clears out finished rows: done ones after a day, failed ones after a week, so
    /// there is time to see why something failed before it disappears.
    /// </summary>
    public int Purge()
    {
        var now = _clock.UtcNow;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM jobs WHERE (status = 'done' AND updated_at < @day)
                                OR (status = 'failed' AND updated_at < @week)
            """;
        cmd.Bind("@day", _db.Dialect.Time(now - TimeSpan.FromDays(1)));
        cmd.Bind("@week", _db.Dialect.Time(now - TimeSpan.FromDays(7)));
        return cmd.ExecuteNonQuery();
    }

    static Job Read(DbDataReader r) => new(
        r.Long("id"), r.Text("type"), r.Text("payload"), r.Text("status"),
        r.IntOr("attempts"), r.IntOr("max_attempts"), r.Str("correlation_id"),
        r.LongOrNull("user_id"), r.Str("result"), r.Str("last_error"));
}
