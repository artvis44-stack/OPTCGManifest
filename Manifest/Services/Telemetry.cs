using System.Diagnostics;
using System.Diagnostics.Metrics;
using Manifest.Data;

namespace Manifest.Services;

/// <summary>
/// The numbers worth watching once strangers use this: requests, jobs, scans, card
/// art, sign-ins, and the queue. Recorded through System.Diagnostics.Metrics, which
/// costs next to nothing while nobody is listening - the exporter only listens when
/// MANIFEST_METRICS_LISTEN is set.
///
/// The instrument names are already Prometheus names, since that is the one place
/// they are read; database timings come from Npgsql's own meter and are renamed by
/// <see cref="PrometheusExporter"/>.
/// </summary>
public static class Telemetry
{
    public const string MeterName = "Manifest";
    public static readonly Meter Meter = new(MeterName, AppConfig.Version);

    /// <summary>
    /// Seconds. Finer at the bottom, where nearly every request lands, and with
    /// edges at 0.3 and 0.5 so the launch targets (reads under 300 ms, writes under
    /// 500 ms at p95) can be read straight off the buckets.
    /// </summary>
    public static readonly double[] LatencyBuckets =
        { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.3, 0.5, 1, 2.5, 5, 10 };

    /// <summary>Jobs and scans: card art from another site, OCR, mail.</summary>
    public static readonly double[] JobBuckets =
        { 0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 300 };

    static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "manifest_http_requests_total", description: "HTTP requests answered, by route and status.");

    static readonly Histogram<double> RequestSeconds = Meter.CreateHistogram<double>(
        "manifest_http_request_duration_seconds", "s", "Time to answer an HTTP request.");

    static readonly Counter<long> RateLimited = Meter.CreateCounter<long>(
        "manifest_rate_limited_total", description: "Requests refused with 429, by route.");

    static readonly Counter<long> Logins = Meter.CreateCounter<long>(
        "manifest_auth_attempts_total",
        description: "Sign-ins and registrations, by outcome: ok, refused or throttled.");

    static readonly Counter<long> Jobs = Meter.CreateCounter<long>(
        "manifest_jobs_total",
        description: "Jobs finished, by type and outcome: done, retry (failed, will run again) or failed (out of attempts).");

    static readonly Histogram<double> JobSeconds = Meter.CreateHistogram<double>(
        "manifest_job_duration_seconds", "s", "Time a job spent running, by type.");

    static readonly Histogram<double> ScanSeconds = Meter.CreateHistogram<double>(
        "manifest_scan_duration_seconds", "s",
        "Time to read a scan, by mode (sync in the request, async as a job) and outcome.");

    static readonly Counter<long> ImageRequests = Meter.CreateCounter<long>(
        "manifest_image_requests_total",
        description: "Card art asked for, by state: found, pending (fetch queued) or missing.");

    static readonly Counter<long> ImageFetches = Meter.CreateCounter<long>(
        "manifest_image_fetches_total",
        description: "Card art fetches from the card site, by outcome: stored, already, failed or missing.");

    public static void Request(string method, string route, int status, double seconds)
    {
        var code = status.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Requests.Add(1, new("method", method), new("route", route), new("status", code));
        RequestSeconds.Record(seconds, new("method", method), new("route", route));
        if (status == 429) RateLimited.Add(1, new KeyValuePair<string, object?>("route", route));
    }

    public static void AuthAttempt(string kind, string outcome) =>
        Logins.Add(1, new("kind", kind), new("outcome", outcome));

    public static void JobFinished(string type, string outcome, double seconds)
    {
        Jobs.Add(1, new("type", type), new("outcome", outcome));
        JobSeconds.Record(seconds, new KeyValuePair<string, object?>("type", type));
    }

    public static void Scan(string mode, string outcome, double seconds) =>
        ScanSeconds.Record(seconds, new("mode", mode), new("outcome", outcome));

    public static void ImageRequest(string state) =>
        ImageRequests.Add(1, new KeyValuePair<string, object?>("state", state));

    public static void ImageFetch(string outcome) =>
        ImageFetches.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    // ---- the queue, read from the database when scraped

    static JobQueue? _queue;
    static readonly object QueueLock = new();
    static long _queueReadAt;
    static JobQueue.Backlog? _backlog;
    static bool _databaseUp = true;

    /// <summary>
    /// Report the queue's depth from this process. Every process sees the same
    /// table, so each reports the same numbers; alert on max(), not sum().
    /// </summary>
    public static void ObserveQueue(JobQueue queue)
    {
        if (Interlocked.Exchange(ref _queue, queue) is not null) return;
        Meter.CreateObservableGauge("manifest_jobs_queued",
            () => Read() is { } b ? new[] { new Measurement<long>(b.Due) } : Array.Empty<Measurement<long>>(),
            description: "Jobs due to run and not yet claimed.");
        Meter.CreateObservableGauge("manifest_jobs_running",
            () => Read() is { } b ? new[] { new Measurement<long>(b.Running) } : Array.Empty<Measurement<long>>(),
            description: "Jobs claimed by a worker.");
        Meter.CreateObservableGauge("manifest_jobs_failed",
            () => Read() is { } b ? new[] { new Measurement<long>(b.Failed) } : Array.Empty<Measurement<long>>(),
            description: "Jobs out of attempts and kept for a week for someone to look at.");
        Meter.CreateObservableGauge("manifest_jobs_oldest_due_seconds",
            () => Read() is { } b ? new[] { new Measurement<double>(b.OldestDueSeconds) } : Array.Empty<Measurement<double>>(),
            description: "How long the oldest due job has waited for a worker; 0 when none are waiting.");
        Meter.CreateObservableGauge("manifest_database_up",
            () => { Read(); return _databaseUp ? 1 : 0; },
            description: "1 if this process's last queue query reached the database, else 0.");
    }

    /// <summary>
    /// One query per scrape however many gauges ask, and never more than one every
    /// few seconds however many scrapers there are.
    /// </summary>
    static JobQueue.Backlog? Read()
    {
        if (_queue is null) return null;
        lock (QueueLock)
        {
            if (_queueReadAt != 0 && Stopwatch.GetElapsedTime(_queueReadAt) < TimeSpan.FromSeconds(5))
                return _databaseUp ? _backlog : null;
            _queueReadAt = Stopwatch.GetTimestamp();
            try
            {
                _backlog = _queue.ReadBacklog();
                _databaseUp = true;
            }
            catch (Exception)
            {
                _backlog = null;
                _databaseUp = false;
            }
            return _backlog;
        }
    }

    // ---- the process

    static Telemetry()
    {
        Meter.CreateObservableGauge("manifest_process_resident_memory_bytes",
            () => Environment.WorkingSet, description: "Resident memory of this process.");
        Meter.CreateObservableGauge("manifest_dotnet_gc_heap_bytes",
            () => GC.GetTotalMemory(false), description: "Managed heap in use.");
        Meter.CreateObservableCounter("manifest_process_cpu_seconds_total",
            () => Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds,
            description: "CPU time this process has used.");
        Meter.CreateObservableGauge("manifest_dotnet_threadpool_queue_length",
            () => ThreadPool.PendingWorkItemCount,
            description: "Work items waiting for a thread-pool thread; climbing means starved.");
        Meter.CreateObservableGauge("manifest_dotnet_threadpool_threads",
            () => ThreadPool.ThreadCount, description: "Thread-pool threads.");
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Meter.CreateObservableGauge("manifest_process_start_time_seconds",
            () => started, description: "When this process started, in Unix seconds.");
    }
}
