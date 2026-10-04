using Manifest.Data;

namespace Manifest.Services.Jobs;

/// <summary>Does one type of job. Must be safe to run twice for the same job.</summary>
public interface IJobHandler
{
    string Type { get; }

    /// <summary>
    /// Blank the payload once the job is finished with, for payloads that should not
    /// outlive their use - mailed tokens, photos.
    /// </summary>
    bool ForgetPayload => false;

    /// <returns>Anything worth keeping as the job's result, or null.</returns>
    Task<object?> Run(Job job, CancellationToken cancel);
}

/// <summary>
/// Pulls jobs off the queue and runs them. In the web process when
/// MANIFEST_WORKER_MODE is inline - the single-machine default - and in its own
/// process (`manifest worker`) when it is external, so slow work can be scaled and
/// restarted separately from the site.
/// </summary>
public sealed class JobWorker : BackgroundService
{
    readonly JobQueue _queue;
    readonly Dictionary<string, IJobHandler> _handlers;
    readonly ILogger<JobWorker> _log;
    readonly JobSchedule _schedule;
    readonly SemaphoreSlim _wake = new(0);
    readonly int _slots;

    public JobWorker(JobQueue queue, IEnumerable<IJobHandler> handlers, ILogger<JobWorker> log,
                     JobSchedule schedule, AppConfig config)
    {
        _queue = queue;
        _handlers = handlers.ToDictionary(h => h.Type);
        _log = log;
        _schedule = schedule;
        _slots = config.WorkerConcurrency;
        _queue.Enqueued += () => _wake.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        _log.LogInformation("job worker started: {Slots} at a time, handling {Types}",
                            _slots, string.Join(", ", _handlers.Keys.Order()));
        // One slot only ever takes urgent work, so a scan someone is watching never
        // waits for a slot behind slow fetches from the card site.
        var loops = Enumerable.Range(0, _slots)
            .Select(i => Loop(stop, i == 0 && _slots > 1 ? JobPriority.Mail : int.MinValue))
            .ToList();
        loops.Add(_schedule.Run(stop));
        await Task.WhenAll(loops);
    }

    async Task Loop(CancellationToken stop, int minPriority)
    {
        while (!stop.IsCancellationRequested)
        {
            Job? job;
            try
            {
                job = _queue.Claim(minPriority);
            }
            catch (Exception e)
            {
                // The database is away. Not this job's fault; try again shortly.
                _log.LogError(e, "could not claim a job");
                job = null;
            }

            if (job is null)
            {
                try { await _wake.WaitAsync(TimeSpan.FromSeconds(1), stop); }
                catch (OperationCanceledException) { return; }
                continue;
            }

            await RunOne(job, stop);
        }
    }

    async Task RunOne(Job job, CancellationToken stop)
    {
        using var scope = _log.BeginScope(new Dictionary<string, object?>
        {
            ["job_id"] = job.Id,
            ["job_type"] = job.Type,
            ["correlation_id"] = job.CorrelationId,
        });

        if (!_handlers.TryGetValue(job.Type, out var handler))
        {
            _log.LogError("job {JobId}: no handler for {Type}", job.Id, job.Type);
            _queue.Fail(job with { Attempts = job.MaxAttempts }, $"no handler for {job.Type}");
            return;
        }

        var started = DateTime.UtcNow;
        try
        {
            var result = await handler.Run(job, stop);
            _queue.Complete(job.Id, result, handler.ForgetPayload);
            _log.LogInformation("job {JobId} {Type} done in {Ms:0} ms correlation_id={CorrelationId}",
                                job.Id, job.Type, (DateTime.UtcNow - started).TotalMilliseconds,
                                job.CorrelationId);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Shutting down mid-job: leave it running, and the lease hands it to
            // whichever worker is up next.
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "job {JobId} {Type} failed (attempt {Attempt} of {Max}) correlation_id={CorrelationId}",
                            job.Id, job.Type, job.Attempts, job.MaxAttempts, job.CorrelationId);
            _queue.Fail(job, $"{e.GetType().Name}: {e.Message}", forget: handler.ForgetPayload);
        }
    }
}
