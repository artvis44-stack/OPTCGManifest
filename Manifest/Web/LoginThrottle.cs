using System.Collections.Concurrent;

namespace Manifest.Web;

/// <summary>
/// Slows down password guessing. In memory rather than in the database: the counters
/// are worth nothing after a restart, and a table written on every failed sign-in is
/// a denial-of-service lever of its own.
/// </summary>
public static class LoginThrottle
{
    /// <summary>Failures from one address before it has to wait.</summary>
    public const int Allowance = 8;

    /// <summary>How long the counter remembers, and how long a lockout lasts.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    sealed class Record
    {
        public int Failures;
        public DateTime FirstFailure;
        public DateTime LockedUntil;
    }

    static readonly ConcurrentDictionary<string, Record> Records = new();
    static IClock _clock = SystemClock.Instance;
    static DateTime _lastSweep = _clock.UtcNow;

    public static void UseClock(IClock clock)
    {
        _clock = clock;
        _lastSweep = clock.UtcNow;
    }

    /// <summary>Seconds the caller must wait, or null if it may try now.</summary>
    public static int? RetryAfter(string key)
    {
        Sweep();
        if (!Records.TryGetValue(key, out var record)) return null;
        var left = record.LockedUntil - _clock.UtcNow;
        return left > TimeSpan.Zero ? (int)Math.Ceiling(left.TotalSeconds) : null;
    }

    public static void Failed(string key)
    {
        var now = _clock.UtcNow;
        var record = Records.GetOrAdd(key, _ => new Record { FirstFailure = now });
        lock (record)
        {
            if (now - record.FirstFailure > Window)
            {
                record.Failures = 0;
                record.FirstFailure = now;
            }
            record.Failures++;
            if (record.Failures >= Allowance) record.LockedUntil = now + Window;
        }
    }

    /// <summary>A successful sign-in clears the slate for that address.</summary>
    public static void Succeeded(string key) => Records.TryRemove(key, out _);

    /// <summary>
    /// Drops records nothing is waiting on. Without this the dictionary grows by one
    /// entry per address that ever mistyped a password, which on a public server is
    /// a slow memory leak an attacker can drive.
    /// </summary>
    static void Sweep()
    {
        var now = _clock.UtcNow;
        if (now - _lastSweep < TimeSpan.FromMinutes(5)) return;
        _lastSweep = now;
        foreach (var (key, record) in Records)
            if (now - record.FirstFailure > Window && record.LockedUntil < now)
                Records.TryRemove(key, out _);
    }

    /// <summary>For the tests, which need a clean counter between cases.</summary>
    public static void Clear()
    {
        Records.Clear();
        _lastSweep = _clock.UtcNow;
    }
}
