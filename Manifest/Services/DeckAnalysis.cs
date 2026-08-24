using Manifest.Models;

namespace Manifest.Services;

/// <summary>
/// Rules enforced here come from the official ONE PIECE CARD GAME rulebook:
///   - exactly 1 Leader, exactly 50 other cards in the deck
///   - at most 4 copies of the same card number (parallels/alt arts count together
///     with the base printing - https://en.onepiece-cardgame.com)
///   - every non-Leader card must share at least one colour with the Leader
/// The DON!! deck (always exactly 10, identical cards) isn't customisable so there's
/// nothing to build there.
///
/// Everything else here - the cost-curve bands, character/event/stage counts,
/// counter and blocker targets - is *not* an official rule. Bandai doesn't publish
/// deckbuilding ratios; these are community-standard starting targets (per
/// onepieceonlinetcg.com's deckbuilding guide and shonentcg's deck guide) meant as a
/// sanity check while brewing, not a hard requirement - a deck that falls outside a
/// band is still perfectly legal.
/// </summary>
public static class DeckAnalysis
{
    public readonly record struct Band(int Low, int High);

    public static readonly Band Characters = new(32, 38);
    public static readonly Band Events = new(6, 10);
    public static readonly Band Stages = new(0, 4);
    public static readonly Band Counters = new(8, 14);
    public static readonly Band Blockers = new(3, 6);
    public static readonly Band CostLow = new(16, 20);   // cost 0-3
    public static readonly Band CostMid = new(16, 20);   // cost 4-6
    public static readonly Band CostHigh = new(8, 12);   // cost 7+

    public static HashSet<string> ColorsOf(string? s) =>
        (s ?? "").Split(',')
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToHashSet();

    /// <summary>
    /// Case-insensitive: see the case-insensitivity note on the search category
    /// filter - some catalogue snapshots store this shouty.
    /// </summary>
    public static bool IsLeader(CatalogRow? row) =>
        (row?.Category ?? "").Equals("LEADER", StringComparison.OrdinalIgnoreCase);

    public static string BandOf(int n, Band b) =>
        n < b.Low ? "low" : n > b.High ? "high" : "ok";

    public static Guideline Guide(int n, Band b) =>
        new() { Count = n, Min = b.Low, Max = b.High, Band = BandOf(n, b) };

    public static LeaderSummary? Summarise(CatalogRow? leader) =>
        leader is null ? null : new LeaderSummary
        {
            CardId = leader.CardId,
            Name = leader.Name,
            Colors = leader.Colors,
            // The catalogue's cost column carries Life for Leaders - see LeaderSummary.
            Life = leader.Cost,
            Power = leader.Power,
            ImageUrl = leader.ImageUrl,
            Effect = leader.Effect,
        };
}

/// <summary>A deckbuilding rule the caller broke. Surfaces to the client as a 400.</summary>
public sealed class RuleViolation : Exception
{
    public RuleViolation(string message) : base(message) { }
}
