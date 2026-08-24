using Manifest;

namespace Manifest.Models;

/// <summary>
/// Where a request has got to. Kept as strings rather than an enum with a number,
/// because these land in the database and a person reading manifest.db with the
/// sqlite3 shell should not have to look up what 2 meant.
/// </summary>
public static class AccessStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Denied = "denied";
}

/// <summary>
/// Someone asking for an account, and what became of the asking. The tokens are
/// deliberately absent: they exist only in the moment they are minted and in the
/// mailbox they were sent to, and the repository holds nothing but their digests.
/// </summary>
public sealed class AccessRequest
{
    public long Id { get; init; }
    public string Email { get; init; } = "";
    public string Note { get; init; } = "";
    public string Status { get; init; } = AccessStatus.Pending;
    public string CreatedAt { get; init; } = "";
    public string? DecidedAt { get; init; }
    public string? DecidedBy { get; init; }
    public string? InviteExpiresAt { get; init; }
    public string? InviteSentAt { get; init; }
    public string? UsedAt { get; init; }
    public long? UserId { get; init; }

    /// <summary>The account was made. There is nothing left to do with this row.</summary>
    public bool Used => UsedAt is not null;

    /// <summary>
    /// Approved, never used, and the link has run out. Shown apart from a live
    /// approval so the admin page can offer to send a fresh one rather than leaving
    /// someone stuck on a dead link with no way to say so.
    /// </summary>
    public bool InviteExpired => InviteExpiredAt(SystemClock.Instance.UtcNow);

    public bool InviteExpiredAt(DateTime utcNow) =>
        Status == AccessStatus.Approved && !Used && InviteExpiresAt is { } until
        && string.CompareOrdinal(until, utcNow.ToString("yyyy-MM-dd HH:mm:ss")) <= 0;
}

/// <summary>What the admin page is told about a request. Same shape, minus nothing -
/// there is no secret in an AccessRequest to strip.</summary>
public sealed class AccessRequestView
{
    public long Id { get; init; }
    public string Email { get; init; } = "";
    public string Note { get; init; } = "";
    public string Status { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string? DecidedAt { get; init; }
    public string? DecidedBy { get; init; }
    public bool Used { get; init; }
    public bool InviteExpired { get; init; }
    public string? InviteExpiresAt { get; init; }

    public static AccessRequestView Of(AccessRequest r, IClock? clock = null) => new()
    {
        Id = r.Id,
        Email = r.Email,
        Note = r.Note,
        Status = r.Status,
        CreatedAt = r.CreatedAt,
        DecidedAt = r.DecidedAt,
        DecidedBy = r.DecidedBy,
        Used = r.Used,
        InviteExpired = r.InviteExpiredAt((clock ?? SystemClock.Instance).UtcNow),
        InviteExpiresAt = r.InviteExpiresAt,
    };
}
