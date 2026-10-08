using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using static Manifest.Tools.CatalogScraper;

namespace Manifest.Tools;

/// <summary>
/// Every print Limitless lists, product by product, with its TCGplayer and Cardmarket
/// prices. A product page in list view (/cards/english-version-1st-anniversary-set
/// ?display=list&amp;show=all) has a row per print; each row's picture is named after
/// the official card number of that print - OP01-016_p6_EN.webp - which is what ties
/// it to a printing in the catalogue. About 150 pages for the lot.
///
/// A reprint keeps the picture of the card it reprints, so one number can turn up in
/// several products; <see cref="Href"/> (/cards/OP01-016?v=6) is what tells them apart.
/// </summary>
public static partial class LimitlessPrints
{
    public sealed record Print(
        string CardId, string Href, string Slug, string Product, string Name, string Category,
        string Rarity, double? Usd, long? TcgplayerId, double? Eur, string? CardmarketPath,
        string ImageUrl, bool Japanese)
    {
        public string BaseId => CardId.Split('_')[0];
    }

    static readonly TimeSpan DefaultPause = TimeSpan.FromSeconds(1.5);

    [GeneratedRegex(@"href=""/cards/([a-z][a-z0-9\-]*)""")] private static partial Regex ProductLink();
    [GeneratedRegex(@"(?is)<div class=""infobox-heading[^""]*"">(.*?)</div>")] private static partial Regex Heading();
    [GeneratedRegex(@"(?is)<tr data-hover=""([^""]+)"">(.*?)</tr>")] private static partial Regex Row();
    [GeneratedRegex(@"(?is)<td[^>]*>(.*?)</td>")] private static partial Regex Cell();
    [GeneratedRegex(@"/one-piece/[^/]+/([A-Za-z0-9\-]+(?:_[a-z]\d+)?)_(EN|JP)\.webp")] private static partial Regex PictureId();
    [GeneratedRegex(@"href=""/cards/([^""]+)""")] private static partial Regex CardHref();
    [GeneratedRegex(@"(?is)class=""card-price usd""[^>]*>([^<]*)<")] private static partial Regex UsdPrice();
    [GeneratedRegex(@"(?is)class=""card-price eur""[^>]*>([^<]*)<")] private static partial Regex EurPrice();
    [GeneratedRegex(@"tcgplayer\.com%2Fproduct%2F(\d+)")] private static partial Regex TcgplayerProduct();
    [GeneratedRegex(@"cardmarket\.com/en/OnePiece/Products/Singles/([^""?]+)")] private static partial Regex CardmarketProduct();

    /// <summary>The product pages an index page (/cards, /cards/promos) links to.</summary>
    public static List<string> ParseIndex(string doc) =>
        ProductLink().Matches(doc).Select(m => m.Groups[1].Value)
                     .Where(s => s is not "advanced" and not "promos")
                     .Distinct().ToList();

    /// <summary>"$1,265.62", "804.42€" -> the amount; "" or "-" -> null.</summary>
    public static double? Amount(string text)
    {
        var digits = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v : null;
    }

    /// <summary>One product page, in list view, as its prints.</summary>
    public static List<Print> ParseProduct(string doc, string slug)
    {
        var heading = Heading().Match(doc);
        var product = heading.Success ? StripTags(heading.Groups[1].Value) : slug;

        var prints = new List<Print>();
        foreach (Match row in Row().Matches(doc))
        {
            var picture = WebUtility.HtmlDecode(row.Groups[1].Value);
            var body = row.Groups[2].Value;
            var id = PictureId().Match(picture);
            var href = CardHref().Match(body);
            if (!id.Success || !href.Success) continue;

            var cells = Cell().Matches(body).Select(c => StripTags(c.Groups[1].Value)).ToList();
            var tcgplayer = TcgplayerProduct().Match(body);
            var cardmarket = CardmarketProduct().Match(body);
            var usd = UsdPrice().Match(body);
            var eur = EurPrice().Match(body);
            prints.Add(new Print(
                CardId: id.Groups[1].Value,
                Href: WebUtility.HtmlDecode(href.Groups[1].Value),
                Slug: slug,
                Product: product,
                Name: cells.ElementAtOrDefault(1) ?? "",
                Category: (cells.ElementAtOrDefault(2) ?? "").ToUpperInvariant(),
                Rarity: cells.ElementAtOrDefault(3) ?? "",
                Usd: usd.Success ? Amount(usd.Groups[1].Value) : null,
                TcgplayerId: tcgplayer.Success ? long.Parse(tcgplayer.Groups[1].Value) : null,
                Eur: eur.Success ? Amount(WebUtility.HtmlDecode(eur.Groups[1].Value)) : null,
                CardmarketPath: cardmarket.Success ? cardmarket.Groups[1].Value : null,
                ImageUrl: picture,
                Japanese: id.Groups[2].Value == "JP"));
        }
        return prints;
    }

    /// <summary>
    /// Every product's prints, one page at a time with a pause between. Null when the
    /// index pages could not be read; a product page that fails is left out and said so.
    /// </summary>
    public static async Task<List<Print>?> Crawl(TimeSpan? pause = null)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (compatible; Manifest/1.0; self-hosted collection tracker)");
        http.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
        var wait = pause ?? DefaultPause;

        var slugs = new List<string>();
        try
        {
            foreach (var index in new[] { "/cards", "/cards/promos" })
            {
                slugs.AddRange(ParseIndex(await http.GetStringAsync(LimitlessScraper.Host + index)));
                await Task.Delay(wait);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine($"could not read Limitless's product list: {e.Message}");
            return null;
        }
        slugs = slugs.Distinct().ToList();
        if (slugs.Count == 0)
        {
            Console.Error.WriteLine("no products on Limitless's card pages; its markup has probably changed");
            return null;
        }

        Console.WriteLine($"reading {slugs.Count} Limitless products");
        var prints = new List<Print>();
        for (var i = 0; i < slugs.Count; i++)
        {
            try
            {
                var page = await http.GetStringAsync(
                    $"{LimitlessScraper.Host}/cards/{slugs[i]}?display=list&show=all&unique=prints");
                prints.AddRange(ParseProduct(page, slugs[i]));
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"  {slugs[i]} failed: {e.Message}");
            }
            await Task.Delay(wait);
        }
        Console.WriteLine($"  {prints.Count} prints");
        return prints;
    }
}
