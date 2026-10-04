using Manifest.Data;
using Manifest.Services.Jobs;

namespace Manifest.Services;

/// <summary>
/// The official card site sends a Cross-Origin-Resource-Policy header, so a browser
/// refuses to render its images inside our page. We fetch them server-side instead,
/// keep them in the image store, and serve them from this origin.
///
/// The fetch happens in a background job, never in the request that noticed the
/// picture was missing: an upstream that is slow or down would otherwise hold a web
/// worker for every card on the screen. That request gets a 404 with Retry-After,
/// and the page asks again a moment later.
/// </summary>
public sealed class ImageCache : IJobHandler
{
    readonly IImageStore _store;
    readonly Database _db;
    readonly JobQueue _queue;
    readonly IClock _clock;
    readonly HttpClient _http;

    static readonly byte[] PngMagic = { 0x89, (byte)'P', (byte)'N', (byte)'G' };

    public ImageCache(IImageStore store, Database db, JobQueue queue, IClock clock)
    {
        _store = store;
        _db = db;
        _queue = queue;
        _clock = clock;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Add(
            "User-Agent", "Mozilla/5.0 (Manifest, self-hosted collection tracker)");
        _http.DefaultRequestHeaders.Add("Referer", "https://en.onepiece-cardgame.com/cardlist/");
        _http.DefaultRequestHeaders.Add("Accept", "image/png,image/*;q=0.8,*/*;q=0.5");
    }

    public enum State
    {
        /// <summary>Here it is.</summary>
        Found,

        /// <summary>Not stored yet; a fetch is queued. Ask again shortly.</summary>
        Pending,

        /// <summary>No such card, no source to fetch from, or failing and backing off.</summary>
        Missing,
    }

    public sealed record Result(State State, byte[]? Bytes = null);

    public static string KeyFor(string cardId) => $"cards/{CardId.SafeFileStem(cardId)}.png";

    public async Task<Result> Get(string rawCardId, string? correlationId = null)
    {
        var cid = CardId.Normalise(rawCardId);
        if (cid is null || CardId.SafeFileStem(cid).Length == 0) return new Result(State.Missing);

        if (await _store.Get(KeyFor(cid)) is { } bytes) return new Result(State.Found, bytes);

        string? url;
        using (var conn = _db.Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT c.image_url, i.status, i.next_attempt_at
                FROM catalog c LEFT JOIN card_images i ON i.card_id = c.card_id
                WHERE c.card_id = @id
                """;
            cmd.Bind("@id", cid);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return new Result(State.Missing);
            url = r.Str("image_url");
            if (string.IsNullOrEmpty(url)) return new Result(State.Missing);

            // Failing upstream: leave it alone until its back-off is over, or every
            // page view of that card would queue another doomed fetch.
            if (r.Str("status") == "failed" && r.Str("next_attempt_at") is { } next
                && string.CompareOrdinal(next, Stamp(_clock.UtcNow)) > 0)
                return new Result(State.Missing);
        }

        _queue.Enqueue(JobTypes.FetchCardImage, new FetchPayload(cid), dedupeKey: "image:" + cid,
                       correlationId: correlationId, maxAttempts: 1, priority: JobPriority.Image);
        return new Result(State.Pending);
    }

    sealed record FetchPayload(string CardId);

    // ---- the job

    public string Type => JobTypes.FetchCardImage;

    /// <summary>
    /// Fetches one picture into the store. Idempotent: an image already there is
    /// left as it is. An upstream failure is recorded against the card with its own
    /// back-off - a minute, doubling to a day - rather than retried by the queue,
    /// because the next person to look at the card is a better trigger than a timer.
    /// </summary>
    public async Task<object?> Run(Job job, CancellationToken cancel)
    {
        var cid = System.Text.Json.JsonSerializer.Deserialize<FetchPayload>(job.Payload, Json.Options)!.CardId;
        var key = KeyFor(cid);
        if (await _store.Get(key, cancel) is not null)
        {
            Record(cid, "stored", null, key, null);
            return new { stored = key, already = true };
        }

        string? url;
        using (var conn = _db.Open())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT image_url FROM catalog WHERE card_id = @id";
            cmd.Bind("@id", cid);
            url = cmd.ExecuteScalar() as string;
        }
        if (string.IsNullOrEmpty(url)) return new { missing = cid };

        try
        {
            var blob = await _http.GetByteArrayAsync(url, cancel);
            if (blob.Length < 4 || !blob.Take(4).SequenceEqual(PngMagic))
                throw new InvalidDataException("upstream did not send a PNG");
            await _store.Put(key, blob, cancel);
            Record(cid, "stored", url, key, null);
            return new { stored = key, bytes = blob.Length };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                     or InvalidDataException && !cancel.IsCancellationRequested)
        {
            Record(cid, "failed", url, null, $"{e.GetType().Name}: {e.Message}");
            return new { failed = cid, error = e.Message };
        }
    }

    void Record(string cid, string status, string? url, string? key, string? error)
    {
        var now = _clock.UtcNow;
        using var conn = _db.Open();
        // Worked out here rather than in the SQL: a NULL parameter inside a CASE has
        // no type PostgreSQL can infer, and the doubling needs the stored count anyway.
        var failures = status == "failed" ? FailureCount(conn, cid) + 1 : 0;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO card_images (card_id, status, source_url, object_key, last_attempt_at,
                                     next_attempt_at, failure_count, last_error, updated_at)
            VALUES (@id, @status, @url, @key, @now, @next, @fails, @error, {_db.Dialect.Now})
            ON CONFLICT (card_id) DO UPDATE SET
                status = excluded.status,
                source_url = COALESCE(excluded.source_url, card_images.source_url),
                object_key = COALESCE(excluded.object_key, card_images.object_key),
                last_attempt_at = excluded.last_attempt_at,
                failure_count = excluded.failure_count,
                next_attempt_at = excluded.next_attempt_at,
                last_error = excluded.last_error,
                updated_at = excluded.updated_at
            """;
        cmd.Bind("@id", cid);
        cmd.Bind("@status", status);
        cmd.Bind("@url", url);
        cmd.Bind("@key", key);
        cmd.Bind("@now", _db.Dialect.Time(now));
        cmd.Bind("@next", status == "failed" ? _db.Dialect.Time(now + Backoff(failures)) : null);
        cmd.Bind("@fails", failures);
        cmd.Bind("@error", error);
        cmd.ExecuteNonQuery();
    }

    static int FailureCount(System.Data.Common.DbConnection conn, string cid)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT failure_count FROM card_images WHERE card_id = @id";
        cmd.Bind("@id", cid);
        return cmd.ExecuteScalar() is { } n and not DBNull ? Convert.ToInt32(n) : 0;
    }

    /// <summary>One minute after the first failure, doubling each time, at most a day.</summary>
    public static TimeSpan Backoff(int failures) =>
        TimeSpan.FromMinutes(Math.Min(24 * 60, Math.Pow(2, Math.Max(0, failures - 1))));

    static string Stamp(DateTime utc) => utc.ToString(ReaderExtensions.StampFormat);
}
