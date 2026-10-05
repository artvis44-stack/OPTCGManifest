namespace Manifest.Models;

/// <summary>A binder as the person looking at it sees it.</summary>
public sealed class BinderInfo
{
    public long Id { get; init; }

    /// <summary>"Mine" for your own personal binder, "Alice's cards" for someone else's.</summary>
    public string Name { get; init; } = "";

    /// <summary>personal or shared.</summary>
    public string Kind { get; init; } = "";

    /// <summary>True for your own personal binder.</summary>
    public bool Mine { get; init; }

    /// <summary>Whether you can change what is in it.</summary>
    public bool Writable { get; init; }

    /// <summary>Personal binders: whether the people you share a binder with can look at it.</summary>
    public bool Visible { get; init; }

    public List<string> Members { get; init; } = new();
}
