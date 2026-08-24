using System.Text.Json;
using System.Text.Json.Serialization;

namespace Manifest.Tools;

/// <summary>
/// Rebuild catalog.json from the vegapull-records dataset, then reseed the database.
///
///     manifest refresh-catalog &amp;&amp; manifest --reseed
/// </summary>
public static class CatalogRefresh
{
    const string Base =
        "https://raw.githubusercontent.com/coko7/vegapull-records/main/data/english";

    sealed class TitleParts
    {
        public string? Label { get; set; }
        public string? Title { get; set; }
    }

    sealed class VegapullPack
    {
        public string Id { get; set; } = "";
        [JsonPropertyName("raw_title")] public string? RawTitle { get; set; }
        [JsonPropertyName("title_parts")] public TitleParts? TitleParts { get; set; }
    }

    sealed class VegapullCard
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public string? Rarity { get; set; }
        public string? Category { get; set; }
        public List<string>? Colors { get; set; }
        public int? Cost { get; set; }
        public int? Power { get; set; }
        public int? Counter { get; set; }
        public List<string>? Types { get; set; }
        public string? Effect { get; set; }
        [JsonPropertyName("img_full_url")] public string? ImgFullUrl { get; set; }
    }

    static readonly JsonSerializerOptions Upstream = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<int> Run(AppPaths paths)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        async Task<T?> Get<T>(string url) =>
            JsonSerializer.Deserialize<T>(await http.GetStringAsync(url), Upstream);

        var packList = await Get<List<VegapullPack>>($"{Base}/packs.json") ?? new();
        Console.WriteLine($"{packList.Count} packs");

        var rows = new Dictionary<string, CatalogScraper.ScrapedCard>();
        foreach (var p in packList)
        {
            List<VegapullCard> cards;
            try
            {
                cards = await Get<List<VegapullCard>>($"{Base}/cards_{p.Id}.json") ?? new();
            }
            catch (Exception e)
            {
                Console.WriteLine($"  skipped {p.Id}: {e.Message}");
                continue;
            }

            foreach (var c in cards)
            {
                var parts = c.Id.Split('_');
                rows[c.Id] = new CatalogScraper.ScrapedCard
                {
                    CardId = c.Id,
                    BaseId = parts[0],
                    Variant = CatalogScraper.VariantLabel(parts.Length > 1 ? parts[1] : ""),
                    Name = c.Name ?? "",
                    SetLabel = p.TitleParts?.Label ?? p.RawTitle ?? "",
                    SetName = p.TitleParts?.Title ?? p.RawTitle ?? "",
                    Rarity = c.Rarity ?? "",
                    Category = c.Category ?? "",
                    Colors = string.Join(", ", c.Colors ?? new()),
                    Cost = c.Cost,
                    Power = c.Power,
                    Counter = c.Counter,
                    Types = string.Join(", ", c.Types ?? new()),
                    Effect = c.Effect ?? "",
                    ImageUrl = c.ImgFullUrl ?? "",
                };
            }
        }

        var output = rows.Values.OrderBy(r => r.CardId, StringComparer.Ordinal).ToList();
        await using (var stream = File.Create(paths.Catalog))
            await JsonSerializer.SerializeAsync(stream, output, Json.Options);

        Console.WriteLine($"wrote catalog.json — {output.Count} printings");
        Console.WriteLine("now run: manifest --reseed");
        return 0;
    }
}
