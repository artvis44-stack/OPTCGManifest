using System.Text.Json.Serialization;

namespace Manifest.Models;

/// <summary>A full row of the catalog table.</summary>
public sealed class CatalogRow
{
    public string CardId { get; set; } = "";
    public string BaseId { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Name { get; set; } = "";
    public string? SetLabel { get; set; }
    public string? SetName { get; set; }
    public string? Rarity { get; set; }
    public string? Category { get; set; }
    public string? Colors { get; set; }
    public int? Cost { get; set; }
    public int? Power { get; set; }
    public int? Counter { get; set; }
    public string? Types { get; set; }
    public string? Effect { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>A search result: the list columns, plus how many are owned and the price.</summary>
public sealed class SearchRow
{
    public string CardId { get; set; } = "";
    public string BaseId { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Name { get; set; } = "";
    public string? SetLabel { get; set; }
    public string? SetName { get; set; }
    public string? Rarity { get; set; }
    public string? Category { get; set; }
    public string? Colors { get; set; }
    public int? Cost { get; set; }
    public int? Power { get; set; }
    public int? Counter { get; set; }
    public string? Types { get; set; }
    public string? ImageUrl { get; set; }
    public int Qty { get; set; }
    public double? PriceGbp { get; set; }
}

/// <summary>
/// Everything about one printing, including the effect text. Fetched only when a
/// card is opened rather than on every list row - keeps search payloads small.
/// </summary>
public sealed class CardDetailRow
{
    public string CardId { get; set; } = "";
    public string BaseId { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Name { get; set; } = "";
    public string? SetLabel { get; set; }
    public string? SetName { get; set; }
    public string? Rarity { get; set; }
    public string? Category { get; set; }
    public string? Colors { get; set; }
    public int? Cost { get; set; }
    public int? Power { get; set; }
    public int? Counter { get; set; }
    public string? Types { get; set; }
    public string? Effect { get; set; }
    public string? ImageUrl { get; set; }
    public int Qty { get; set; }
    public double? PriceGbp { get; set; }
}

/// <summary>One owned card, as the collection list shows it.</summary>
public sealed class CollectionRow
{
    public string CardId { get; set; } = "";
    public int Qty { get; set; }
    public string Note { get; set; } = "";
    public string? UpdatedAt { get; set; }
    public string? Name { get; set; }
    public string? SetLabel { get; set; }
    public string? Rarity { get; set; }
    public string? Variant { get; set; }
    public string? Colors { get; set; }
    public string? Category { get; set; }
    public string? ImageUrl { get; set; }
    public int? Cost { get; set; }
    public int? Power { get; set; }
    public int? Counter { get; set; }
    public string? Types { get; set; }
    public double? PriceGbp { get; set; }
}

public sealed class SetStat
{
    [JsonPropertyName("s")] public string? SetLabel { get; set; }
    [JsonPropertyName("n")] public int Count { get; set; }
    [JsonPropertyName("uniq")] public int Unique { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}

public sealed class Stats
{
    public int Total { get; set; }
    public int Unique { get; set; }
    public double ValueGbp { get; set; }
    public List<SetStat> Sets { get; set; } = new();
}

public sealed class Facets
{
    public List<string> Categories { get; set; } = new();
    public List<string> Colors { get; set; } = new();
    public List<string> Rarities { get; set; } = new();
    public List<string> Sets { get; set; } = new();
}

/// <summary>What a +1 / -1 / set-quantity tap sends back.</summary>
public sealed class AdjustResult
{
    public string CardId { get; set; } = "";
    public int Qty { get; set; }
    public string? Name { get; set; }
    public string? SetLabel { get; set; }
    public string? Variant { get; set; }
    public string? Rarity { get; set; }
    public string? ImageUrl { get; set; }
    public bool Known { get; set; }
}

public sealed class CardQuantity
{
    public string CardId { get; set; } = "";
    public int Qty { get; set; }
}

public sealed class BulkLogResult
{
    public int Added { get; set; }
    public int Unique { get; set; }
    public List<AdjustResult> Cards { get; set; } = new();
}
