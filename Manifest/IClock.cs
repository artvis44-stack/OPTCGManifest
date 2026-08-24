namespace Manifest;

/// <summary>One source of time for logic that expires tokens, sessions or throttles.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    SystemClock() { }

    public DateTime UtcNow => DateTime.UtcNow;
}
