namespace Manifest.Models;

/// <summary>A card put up for sale, as its seller sees it.</summary>
public sealed class Listing
{
    public long Id { get; init; }
    public string CardId { get; init; } = "";
    public string? Name { get; init; }
    public string? SetLabel { get; init; }
    public string? Rarity { get; init; }
    public string? Variant { get; init; }

    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Condition { get; init; } = "";
    public int Qty { get; init; }

    /// <summary>draft, listed, sold or ended.</summary>
    public string Status { get; init; } = "";

    /// <summary>The asking price for the whole listing.</summary>
    public double? PriceGbp { get; init; }

    /// <summary>What the seller paid for the card, so a sale can show what it made.</summary>
    public double? CostGbp { get; init; }

    /// <summary>What the buyer paid, postage they paid included.</summary>
    public double? SoldPriceGbp { get; init; }

    public double? FeesGbp { get; init; }
    public double? PostageGbp { get; init; }

    /// <summary>Sold price less fees, postage and cost; null until sold.</summary>
    public double? ProfitGbp { get; init; }

    /// <summary>The card's market price now, for comparing an asking price against.</summary>
    public double? MarketGbp { get; init; }

    public string? EbayUrl { get; init; }
    public string Note { get; init; } = "";
    public string? CreatedAt { get; init; }
    public string? ListedAt { get; init; }
    public string? SoldAt { get; init; }
    public string? UpdatedAt { get; init; }
}

/// <summary>An eBay item specific: a name and the value to type in beside it.</summary>
public sealed record ItemSpecific(string Name, string Value);

/// <summary>Everything typed into eBay's listing form for one card, made from the catalogue.</summary>
public sealed class ListingDraft
{
    public string CardId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public List<ItemSpecific> Specifics { get; init; } = new();
    public double? MarketGbp { get; init; }
    public List<SourcePriceRow> Prices { get; init; } = new();

    /// <summary>eBay's own search of sold and completed listings for this card.</summary>
    public string SoldSearchUrl { get; init; } = "";
}

/// <summary>How selling is going, over the listings a summary covers.</summary>
public sealed class ListingStats
{
    public int Drafts { get; init; }
    public int Listed { get; init; }
    public double ListedValueGbp { get; init; }
    public int Sold { get; init; }
    public int SoldCards { get; init; }
    public double RevenueGbp { get; init; }
    public double FeesGbp { get; init; }
    public double ProfitGbp { get; init; }

    /// <summary>The middle of how long sold listings were up for; null with none to go on.</summary>
    public double? MedianDaysToSell { get; init; }

    /// <summary>Of the listings that finished, the share that sold rather than ended unsold.</summary>
    public double? SellThrough { get; init; }
}

/// <summary>One card's sales, for seeing what sells.</summary>
public sealed class TopSeller
{
    public string CardId { get; init; } = "";
    public string? Name { get; init; }
    public int Sales { get; init; }
    public int Cards { get; init; }
    public double RevenueGbp { get; init; }
    public double? AverageGbp { get; init; }
    public double? MedianDaysToSell { get; init; }
}
