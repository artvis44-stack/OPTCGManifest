namespace Manifest.Models;

/// <summary>
/// The Leader as the deck screens show it. Note <see cref="Life"/>: the scraper's
/// "cost" column holds the printed DON cost for Character/Event/Stage cards, but for
/// Leader cards the official site's markup puts the Life value in that same slot
/// (Leaders aren't played from hand, so they have no cost). Naming it here means the
/// quirk is stated once instead of being re-derived at every call site.
/// </summary>
public sealed class LeaderSummary
{
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Colors { get; set; }
    public int? Life { get; set; }
    public int? Power { get; set; }
    public string? ImageUrl { get; set; }
    public string? Effect { get; set; }
}

public sealed class DeckListItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public LeaderSummary? Leader { get; set; }
    public int CardCount { get; set; }
    public bool SizeOk { get; set; }
}

public sealed class DeckCardRow
{
    public int Qty { get; set; }
    public string CardId { get; set; } = "";
    public string BaseId { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? Colors { get; set; }
    public int? Cost { get; set; }
    public int? Power { get; set; }
    public int? Counter { get; set; }
    public string? Types { get; set; }
    public string? Effect { get; set; }
    public string? SetLabel { get; set; }
    public string? Rarity { get; set; }
    public string? ImageUrl { get; set; }
    public int OwnedQty { get; set; }
    public double? PriceGbp { get; set; }
}

public sealed class BuyListItem
{
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? SetLabel { get; set; }
    public string? Rarity { get; set; }
    public int Need { get; set; }
    public int Owned { get; set; }
    public int DeckQty { get; set; }
    public double? PriceGbp { get; set; }
    public double? SubtotalGbp { get; set; }
    public string? ImageUrl { get; set; }
}

public sealed class Legality
{
    public bool SizeOk { get; set; }
    public int ShortBy { get; set; }
    public int OverBy { get; set; }
    public List<string> OverLimit { get; set; } = new();
    public List<string> OffColor { get; set; } = new();
    public bool Clean { get; set; }
}

/// <summary>
/// A community-standard target, not an official rule - see <see cref="Services.DeckAnalysis"/>.
/// </summary>
public sealed class Guideline
{
    public int Count { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
    public string Band { get; set; } = "ok";
}

public sealed class Guidelines
{
    public Guideline Characters { get; set; } = new();
    public Guideline Events { get; set; } = new();
    public Guideline Stages { get; set; } = new();
    public Guideline Counters { get; set; } = new();
    public Guideline Blockers { get; set; } = new();
    public Guideline CostLow { get; set; } = new();
    public Guideline CostMid { get; set; } = new();
    public Guideline CostHigh { get; set; } = new();
}

public sealed class DeckDetail
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public LeaderSummary? Leader { get; set; }
    public List<DeckCardRow> Cards { get; set; } = new();
    public int Total { get; set; }
    public int DeckSizeTarget { get; set; }
    public Legality Legal { get; set; } = new();
    public Guidelines Guidelines { get; set; } = new();
    public Dictionary<string, int> Curve { get; set; } = new();
    public List<BuyListItem> BuyList { get; set; } = new();
    public double BuyTotalGbp { get; set; }
}
