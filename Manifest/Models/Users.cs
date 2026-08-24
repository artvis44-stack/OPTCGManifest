namespace Manifest.Models;

/// <summary>An account. The hash never leaves the repository layer.</summary>
public sealed class User
{
    public long Id { get; init; }
    public string Username { get; init; } = "";

    /// <summary>
    /// The address the account was invited at, when it came in through the
    /// request-access flow. Null for accounts made with `manifest user add` or with
    /// a shared invite code, which never involve an address at all.
    /// </summary>
    public string? Email { get; init; }

    public string CreatedAt { get; init; } = "";
    public string? LastSeen { get; init; }

    /// <summary>
    /// The first account created. It owns whatever was in the database before
    /// accounts existed, and it is the one that cannot be deleted by accident.
    /// </summary>
    public bool IsOwner { get; init; }
}

/// <summary>What the UI is told about who it is talking as.</summary>
public sealed class SessionInfo
{
    public string Username { get; init; } = "";
    public string? Email { get; init; }
    public bool Owner { get; init; }
    public string? CreatedAt { get; init; }
}
