using System.Collections.Concurrent;

namespace Manifest.Web;

/// <summary>
/// Slows down password guessing: <see cref="Allowance"/> failures from one key
/// within <see cref="Window"/>, and that key waits out a lockout of the same length.
///
/// Two homes for the counters. In memory, for one process on one machine: they are
/// worth nothing after a restart, and a database table written on every failed
/// sign-in would be a denial-of-service lever of its own. In Redis, once there is
/// more than one app container: a counter each container keeps to itself lets an
/// attacker multiply the allowance by the number of containers behind the proxy.
/// </summary>
public interface ILoginThrottle
{
    /// <summary>Failures from one key before it has to wait.</summary>
    const int Allowance = 8;

    /// <summary>How long the counter remembers, and how long a lockout lasts.</summary>
    static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>Where the counters live, for the startup banner.</summary>
    string Description { get; }

    /// <summary>Seconds the caller must wait, or null if it may try now.</summary>
    Task<int?> RetryAfter(string key);

    Task Failed(string key);

    /// <summary>A successful sign-in clears the slate for that key.</summary>
    Task Succeeded(string key);
}

public sealed class MemoryLoginThrottle : ILoginThrottle
{
    sealed class Record
    {
        public int Failures;
        public DateTime FirstFailure;
        public DateTime LockedUntil;
    }

    readonly ConcurrentDictionary<string, Record> _records = new();
    readonly IClock _clock;
    DateTime _lastSweep;

    public MemoryLoginThrottle(IClock clock)
    {
        _clock = clock;
        _lastSweep = clock.UtcNow;
    }

    public string Description => "in memory (this process only)";

    public Task<int?> RetryAfter(string key)
    {
        Sweep();
        if (!_records.TryGetValue(key, out var record)) return Task.FromResult<int?>(null);
        var left = record.LockedUntil - _clock.UtcNow;
        return Task.FromResult<int?>(left > TimeSpan.Zero ? (int)Math.Ceiling(left.TotalSeconds) : null);
    }

    public Task Failed(string key)
    {
        var now = _clock.UtcNow;
        var record = _records.GetOrAdd(key, _ => new Record { FirstFailure = now });
        lock (record)
        {
            if (now - record.FirstFailure > ILoginThrottle.Window)
            {
                record.Failures = 0;
                record.FirstFailure = now;
            }
            record.Failures++;
            if (record.Failures >= ILoginThrottle.Allowance)
                record.LockedUntil = now + ILoginThrottle.Window;
        }
        return Task.CompletedTask;
    }

    public Task Succeeded(string key)
    {
        _records.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Drops records nothing is waiting on. Without this the dictionary grows by one
    /// entry per address that ever mistyped a password, which on a public server is
    /// a slow memory leak an attacker can drive.
    /// </summary>
    void Sweep()
    {
        var now = _clock.UtcNow;
        if (now - _lastSweep < TimeSpan.FromMinutes(5)) return;
        _lastSweep = now;
        foreach (var (key, record) in _records)
            if (now - record.FirstFailure > ILoginThrottle.Window && record.LockedUntil < now)
                _records.TryRemove(key, out _);
    }
}
