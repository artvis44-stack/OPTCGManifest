using System.Collections.Concurrent;
using Manifest.Data;
using Manifest.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Manifest.Tests;

/// <summary>
/// The jobs table, driven directly: on a scratch SQLite file always, and on a
/// scratch PostgreSQL database too when MANIFEST_TEST_POSTGRES is set.
/// </summary>
public sealed class JobQueueTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("manifest-jobs-").FullName;
    readonly List<string> _databases = new();

    sealed class Clock : IClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    public void Dispose()
    {
        foreach (var name in _databases) ServerFixture.DropTestDatabase(name);
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    public static IEnumerable<object[]> Stores()
    {
        yield return new object[] { "sqlite" };
        yield return new object[] { "postgres" };
    }

    (JobQueue Queue, Clock Clock) Make(string store)
    {
        Skip.If(store == "postgres" && ServerFixture.PostgresAdminUrl is null,
                "MANIFEST_TEST_POSTGRES is not set");
        string url;
        if (store == "sqlite")
        {
            url = "sqlite://" + Path.Combine(_root, $"{Guid.NewGuid():N}.db");
        }
        else
        {
            var (name, pg) = ServerFixture.CreateTestDatabase();
            _databases.Add(name!);
            url = pg!;
        }
        var db = new Database(new AppPaths(_root), url);
        db.EnsureSchema();
        var clock = new Clock();
        return (new JobQueue(db, clock), clock);
    }

    [SkippableTheory, MemberData(nameof(Stores))]
    public void RunsAJobOnceAndRecordsTheResult(string store)
    {
        var (queue, _) = Make(store);
        var id = queue.Enqueue("Test", new { n = 1 }, correlationId: "req-1", userId: 7);

        var job = queue.Claim();
        Assert.NotNull(job);
        Assert.Equal(id, job!.Id);
        Assert.Equal("running", job.Status);
        Assert.Equal(1, job.Attempts);
        Assert.Equal("req-1", job.CorrelationId);
        Assert.Equal(7, job.UserId);
        Assert.Null(queue.Claim());                   // nothing else to do

        queue.Complete(job.Id, new { ok = true });
        var done = queue.Get(id)!;
        Assert.Equal("done", done.Status);
        Assert.Contains("\"ok\":true", done.Result);
    }

    /// <summary>A scan queued behind a pile of card art still goes first.</summary>
    [SkippableTheory, MemberData(nameof(Stores))]
    public void HigherPriorityJobsAreClaimedFirst(string store)
    {
        var (queue, _) = Make(store);
        for (var i = 0; i < 5; i++) queue.Enqueue("Image", new { i }, priority: JobPriority.Image);
        queue.Enqueue("Purge", new { }, priority: JobPriority.Background);
        var scan = queue.Enqueue("Scan", new { }, priority: JobPriority.Scan);

        Assert.Equal(scan, queue.Claim()!.Id);
        Assert.Equal("Image", queue.Claim()!.Type);
        var order = new List<string>();
        while (queue.Claim() is { } job) order.Add(job.Type);
        Assert.Equal("Purge", order.Last());
    }

    [SkippableTheory, MemberData(nameof(Stores))]
    public void ADedupeKeyQueuesOnceWhileLive(string store)
    {
        var (queue, _) = Make(store);
        var a = queue.Enqueue("Test", new { }, dedupeKey: "same");
        var b = queue.Enqueue("Test", new { }, dedupeKey: "same");
        Assert.Equal(a, b);

        queue.Complete(queue.Claim()!.Id);
        var c = queue.Enqueue("Test", new { }, dedupeKey: "same");   // finished: may run again
        Assert.NotEqual(a, c);
    }

    [SkippableTheory, MemberData(nameof(Stores))]
    public void RetriesWithAPauseThenGivesUp(string store)
    {
        var (queue, clock) = Make(store);
        var id = queue.Enqueue("Test", new { secret = "x" }, maxAttempts: 2);

        queue.Fail(queue.Claim()!, "first");
        Assert.Equal("queued", queue.Get(id)!.Status);
        Assert.Null(queue.Claim());                   // not due yet

        clock.UtcNow += TimeSpan.FromSeconds(11);
        var again = queue.Claim()!;
        Assert.Equal(2, again.Attempts);
        queue.Fail(again, "second", forget: true);

        var failed = queue.Get(id)!;
        Assert.Equal("failed", failed.Status);
        Assert.Equal("second", failed.LastError);
        Assert.Equal("{}", failed.Payload);           // forgotten once out of attempts
    }

    [SkippableTheory, MemberData(nameof(Stores))]
    public void AJobWhoseWorkerDiedIsTakenOverAfterTheLease(string store)
    {
        var (queue, clock) = Make(store);
        var id = queue.Enqueue("Test", new { });
        Assert.Equal(id, queue.Claim()!.Id);          // ...and then the worker vanishes

        clock.UtcNow += JobQueue.Lease - TimeSpan.FromSeconds(5);
        Assert.Null(queue.Claim());
        clock.UtcNow += TimeSpan.FromSeconds(10);
        var reclaimed = queue.Claim();
        Assert.Equal(id, reclaimed?.Id);
        Assert.Equal(2, reclaimed!.Attempts);
    }

    [SkippableTheory, MemberData(nameof(Stores))]
    public void CompletingCanForgetThePayload(string store)
    {
        var (queue, _) = Make(store);
        var id = queue.Enqueue("Test", new { token = "do-not-keep" });
        queue.Complete(queue.Claim()!.Id, forget: true);
        Assert.Equal("{}", queue.Get(id)!.Payload);
    }

    /// <summary>Eight workers racing over sixty jobs: every job once, none twice.</summary>
    [SkippableTheory, MemberData(nameof(Stores))]
    public async Task ConcurrentWorkersNeverClaimTheSameJob(string store)
    {
        var (queue, _) = Make(store);
        var ids = Enumerable.Range(0, 60).Select(i => queue.Enqueue("Test", new { i })).ToHashSet();

        var claimed = new ConcurrentBag<long>();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            while (queue.Claim() is { } job)
            {
                claimed.Add(job.Id);
                queue.Complete(job.Id);
            }
        })));

        Assert.Equal(60, claimed.Count);
        Assert.Equal(ids, claimed.ToHashSet());
    }

    [SkippableTheory, MemberData(nameof(Stores))]
    public void TheScheduleQueuesEachJobOncePerWindow(string store)
    {
        var (queue, clock) = Make(store);
        var config = new AppConfig { RefreshPricesEvery = TimeSpan.FromHours(24) };
        var a = new JobSchedule(queue, NullLogger<JobSchedule>.Instance, clock, config);
        var b = new JobSchedule(queue, NullLogger<JobSchedule>.Instance, clock, config);

        a.Tick();
        b.Tick();                                     // a second worker, same window
        var first = Drain(queue);
        Assert.Equal(new[] { "PurgeExpiredAccessTokens", "PurgeExpiredSessions", "PurgeFinishedJobs",
                             "RefreshPrices" }, first.Order());

        a.Tick();                                     // finished, but still this window
        Assert.Empty(Drain(queue));

        clock.UtcNow += TimeSpan.FromHours(1);        // a new hour: the purges again
        a.Tick();
        Assert.Equal(3, Drain(queue).Count);
    }

    static List<string> Drain(JobQueue queue)
    {
        var types = new List<string>();
        while (queue.Claim() is { } job)
        {
            types.Add(job.Type);
            queue.Complete(job.Id);
        }
        return types;
    }
}
