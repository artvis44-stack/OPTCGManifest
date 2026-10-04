using Manifest.Data;

namespace Manifest.Services.Jobs;

/// <summary>
/// The jobs nobody asks for: housekeeping every hour, and the catalogue and price
/// refreshes on whatever interval MANIFEST_REFRESH_*_HOURS sets (off when unset,
/// since both fetch from third-party sites). Every worker runs this; the key on
/// each job names its time window, so between them they queue it once.
/// </summary>
public sealed class JobSchedule
{
    readonly JobQueue _queue;
    readonly ILogger<JobSchedule> _log;
    readonly IClock _clock;
    readonly List<(string Type, TimeSpan Every)> _tasks;

    public JobSchedule(JobQueue queue, ILogger<JobSchedule> log, IClock clock, AppConfig config)
    {
        _queue = queue;
        _log = log;
        _clock = clock;
        _tasks = new()
        {
            (JobTypes.PurgeExpiredSessions, TimeSpan.FromHours(1)),
            (JobTypes.PurgeExpiredAccessTokens, TimeSpan.FromHours(1)),
            (JobTypes.PurgeFinishedJobs, TimeSpan.FromHours(1)),
        };
        if (config.RefreshPricesEvery is { } prices) _tasks.Add((JobTypes.RefreshPrices, prices));
        if (config.RefreshCatalogEvery is { } catalog) _tasks.Add((JobTypes.RefreshCatalog, catalog));
    }

    public async Task Run(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            Tick();
            try { await Task.Delay(TimeSpan.FromMinutes(1), stop); }
            catch (OperationCanceledException) { return; }
        }
    }

    public void Tick()
    {
        var now = _clock.UtcNow;
        foreach (var (type, every) in _tasks)
        {
            var window = now.Ticks / every.Ticks;
            try
            {
                if (_queue.EnqueueOnce(type, new { }, $"schedule:{type}:{window}"))
                    _log.LogInformation("scheduled {Type}", type);
            }
            catch (Exception e)
            {
                _log.LogError(e, "could not schedule {Type}", type);
            }
        }
    }
}

/// <summary>
/// How urgent each kind of job is; higher runs first. Someone is watching a scan
/// and waiting on a mail; card art fills in as it comes; housekeeping can wait.
/// </summary>
public static class JobPriority
{
    public const int Scan = 30;
    public const int Mail = 20;
    public const int Image = 10;
    public const int Background = 0;
}

/// <summary>The job types, by the names stored in the jobs table.</summary>
public static class JobTypes
{
    public const string FetchCardImage = "FetchCardImage";
    public const string RunOcrScan = "RunOcrScan";
    public const string SendEmail = "SendEmail";
    public const string RefreshCatalog = "RefreshCatalog";
    public const string RefreshPrices = "RefreshPrices";
    public const string PurgeExpiredSessions = "PurgeExpiredSessions";
    public const string PurgeExpiredAccessTokens = "PurgeExpiredAccessTokens";
    public const string PurgeFinishedJobs = "PurgeFinishedJobs";
}
