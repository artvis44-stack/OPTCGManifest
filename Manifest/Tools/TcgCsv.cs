using System.Text.Json;
using System.Text.RegularExpressions;

namespace Manifest.Tools;

/// <summary>
/// TCGplayer's One Piece catalogue and market prices, from tcgcsv.com - a free daily
/// copy of TCGplayer's own data, made to be downloaded. A group per product (sets,
/// release-event cards, "One Piece Promotion Cards"...), products per group, and a
/// price per product: about 90 small requests for the lot.
/// </summary>
public static partial class TcgCsv
{
    const string Base = "https://tcgcsv.com/tcgplayer/68";

    public sealed record Group(long GroupId, string Name);

    public sealed record Product(long ProductId, long GroupId, string GroupName, string Name,
                                 string Number, string Rarity, string ImageUrl)
    {
        /// <summary>The rest of TCGplayer's card fields - Description, Color, Cost... - by name.</summary>
        public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();
    }

    public sealed record Price(long ProductId, string SubType, double Market);

    [GeneratedRegex(@"^(?:OP|ST|EB|PRB)\d{2}-\d{3}$|^P-\d{3}$")] private static partial Regex CardNumber();

    public static bool IsCardNumber(string number) => CardNumber().IsMatch(number);

    static async Task<JsonElement> Results(HttpClient http, string url)
    {
        using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
        return doc.RootElement.GetProperty("results").Clone();
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static List<Group> ParseGroups(JsonElement results) =>
        results.EnumerateArray()
               .Select(g => new Group(g.GetProperty("groupId").GetInt64(), Str(g, "name") ?? ""))
               .ToList();

    /// <summary>Single cards only: a product with a card number. Sealed product and DON!! cards have none.</summary>
    public static List<Product> ParseProducts(JsonElement results, Group group)
    {
        var products = new List<Product>();
        foreach (var p in results.EnumerateArray())
        {
            string Extended(string name) =>
                p.TryGetProperty("extendedData", out var data) && data.ValueKind == JsonValueKind.Array
                    ? data.EnumerateArray().Where(d => Str(d, "name") == name)
                          .Select(d => Str(d, "value")).FirstOrDefault() ?? ""
                    : "";
            var number = Extended("Number").Trim().ToUpperInvariant();
            if (!IsCardNumber(number)) continue;
            // The listing's thumbnail; the same picture at full size is a size suffix away.
            var image = (Str(p, "imageUrl") ?? "").Replace("_200w.", "_in_1000x1000.");
            var fields = p.TryGetProperty("extendedData", out var all) && all.ValueKind == JsonValueKind.Array
                ? all.EnumerateArray()
                     .Select(d => (Name: Str(d, "name"), Value: Str(d, "value")))
                     .Where(d => d.Name is not null && d.Value is not null)
                     .GroupBy(d => d.Name!).ToDictionary(g => g.Key, g => g.First().Value!)
                : new Dictionary<string, string>();
            products.Add(new Product(p.GetProperty("productId").GetInt64(), group.GroupId, group.Name,
                                     Str(p, "name") ?? "", number, Extended("Rarity"), image) { Fields = fields });
        }
        return products;
    }

    [GeneratedRegex(@"(?i)<br\s*/?>")] private static partial Regex LineBreak();
    [GeneratedRegex(@"^([A-Z]+)(\d{2})-\d{3}$")] private static partial Regex SetOf();

    /// <summary>
    /// A card the catalogue has no English printing of, read off its TCGplayer listing -
    /// for a set too new for the official site and Limitless. The plainest listing of
    /// the number is used: one from its own set, with no "(...)" variant note.
    /// </summary>
    public static CatalogScraper.ScrapedCard? Card(IEnumerable<Product> listings)
    {
        var p = listings.OrderBy(l => l.Name.Contains('(') ? 1 : 0)
                        .ThenBy(l => l.GroupName.Contains("Promotion") || l.GroupName.Contains("Event")
                                     || l.GroupName.Contains("Pre-Release") ? 1 : 0)
                        .ThenBy(l => l.ProductId)
                        .FirstOrDefault();
        if (p is null) return null;

        string F(string name) => p.Fields.GetValueOrDefault(name, "").Trim();
        int? N(string name) => int.TryParse(new string(F(name).Where(char.IsDigit).ToArray()), out var n) ? n : null;
        string List(string name) =>
            string.Join(", ", F(name).Split(';', '/').Select(x => x.Trim()).Where(x => x.Length > 0));

        var text = LineBreak().Split(F("Description"))
                              .Select(CatalogScraper.StripTags).Where(t => t.Length > 0).ToList();
        var trigger = text.FirstOrDefault(t => t.StartsWith("[Trigger]", StringComparison.Ordinal)) ?? "";
        var promo = p.Number.StartsWith("P-", StringComparison.Ordinal);
        // OP18-017 -> OP-18, as the official site labels its sets.
        var label = promo ? "Promotion card" : SetOf().Replace(p.Number, "$1-$2");
        var name = p.Name.Split(" (")[0].Split(" - ")[0].Trim();

        return new CatalogScraper.ScrapedCard
        {
            CardId = p.Number,
            BaseId = p.Number,
            Name = name,
            SetLabel = label,
            SetName = promo ? label : p.GroupName,
            Rarity = p.Rarity,
            Category = F("CardType").ToUpperInvariant(),
            Colors = List("Color"),
            // A leader's Life sits where a character's cost does, as on the official site.
            Cost = N("Cost") ?? N("Life"),
            Power = N("Power"),
            Counter = N("Counterplus"),
            Types = List("Subtypes"),
            Attributes = List("Attribute"),
            Effect = string.Join(" ", text.Where(t => t != trigger)),
            Trigger = trigger,
            ImageUrl = p.ImageUrl,
            Source = "tcgplayer-card",
            TcgplayerId = p.ProductId,
        };
    }

    public static List<Price> ParsePrices(JsonElement results)
    {
        var prices = new List<Price>();
        foreach (var p in results.EnumerateArray())
        {
            if (!p.TryGetProperty("marketPrice", out var m) || m.ValueKind != JsonValueKind.Number) continue;
            var market = m.GetDouble();
            if (market <= 0) continue;
            prices.Add(new Price(p.GetProperty("productId").GetInt64(), Str(p, "subTypeName") ?? "", market));
        }
        return prices;
    }

    /// <summary>
    /// A product's one price: Normal when it is sold as Normal and Foil - the plain
    /// card, as Limitless and the card sites quote it - otherwise whatever there is.
    /// </summary>
    public static Dictionary<long, double> ByProduct(IEnumerable<Price> prices) =>
        prices.GroupBy(p => p.ProductId)
              .ToDictionary(g => g.Key,
                            g => (g.FirstOrDefault(p => p.SubType == "Normal") ?? g.First()).Market);

    static HttpClient Client()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Add("User-Agent", "Manifest (self-hosted collection tracker)");
        return http;
    }

    /// <summary>Every group, and fetch(group) for each in turn. Null when the group list cannot be read.</summary>
    static async Task<List<T>?> EachGroup<T>(string what, Func<HttpClient, Group, Task<IEnumerable<T>>> fetch)
    {
        using var http = Client();
        List<Group> groups;
        try
        {
            groups = ParseGroups(await Results(http, $"{Base}/groups"));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException
                                     or KeyNotFoundException or InvalidOperationException)
        {
            Console.Error.WriteLine($"could not read tcgcsv.com's group list: {e.Message}");
            return null;
        }

        var all = new List<T>();
        foreach (var g in groups)
        {
            try
            {
                all.AddRange(await fetch(http, g));
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException
                                         or KeyNotFoundException or InvalidOperationException)
            {
                Console.WriteLine($"  tcgcsv {what} for {g.Name} failed: {e.Message}");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
        Console.WriteLine($"  tcgcsv: {all.Count} {what} in {groups.Count} groups");
        return all;
    }

    public static Task<List<Product>?> Products() =>
        EachGroup("cards", async (http, g) =>
            (IEnumerable<Product>)ParseProducts(await Results(http, $"{Base}/{g.GroupId}/products"), g));

    public static Task<List<Price>?> Prices() =>
        EachGroup("prices", async (http, g) =>
            (IEnumerable<Price>)ParsePrices(await Results(http, $"{Base}/{g.GroupId}/prices")));
}
