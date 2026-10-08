using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Manifest.Tools.CatalogScraper;

namespace Manifest.Tools;

/// <summary>
/// What the official English site leaves out, from Limitless
/// (onepiece.limitlesstcg.com) and TCGplayer (via tcgcsv.com), into catalog-extra.json.
///
/// Some cards - P-066, P-136 and on - are on the Japanese site only, though they were
/// printed in English too; Limitless has their English text, read off each card's own
/// page, one request each with a pause between. Prints of cards the catalogue already
/// has - anniversary sets, event packs, stamped cards - come from the product lists,
/// see <see cref="ExtraPrints"/>. The official scrapes never touch this file, and an
/// official printing takes over from one here as soon as the English site lists it.
/// </summary>
public static partial class LimitlessScraper
{
    public const string Host = "https://onepiece.limitlesstcg.com";
    public const string FileName = "catalog-extra.json";
    const string UserAgent =
        "Mozilla/5.0 (compatible; Manifest/1.0; self-hosted collection tracker)";
    static readonly TimeSpan DefaultPause = TimeSpan.FromSeconds(1.5);

    [GeneratedRegex(@"(?is)<span class=""card-text-name"">(.*?)</span>")] private static partial Regex NameSpan();
    [GeneratedRegex(@"(?is)<span class=""card-text-id"">(.*?)</span>")] private static partial Regex IdSpan();
    [GeneratedRegex(@"(?is)<p class=""card-text-type"">(.*?)</p>")] private static partial Regex TypeLine();
    [GeneratedRegex(@"(?is)<p class=""card-text-section"">(.*?)</p>")] private static partial Regex StatLine();
    [GeneratedRegex(@"(?is)<div class=""card-text-section"">(.*?)</div>")] private static partial Regex TextSection();
    [GeneratedRegex(@"(?is)data-tooltip=""Category"">(.*?)</span>")] private static partial Regex CategorySpan();
    [GeneratedRegex(@"(?is)data-tooltip=""Color"">(.*?)</span>")] private static partial Regex ColorSpan();
    [GeneratedRegex(@"(?is)data-tooltip=""Attribute"">(.*?)</span>")] private static partial Regex AttributeSpan();
    [GeneratedRegex(@"(?is)data-tooltip=""Type"">(.*?)</span>")] private static partial Regex TypeSpan();
    [GeneratedRegex(@"(\d+)\s*(?:Cost|Life)")] private static partial Regex CostOrLife();
    [GeneratedRegex(@"(\d+)\s*Power")] private static partial Regex PowerText();
    [GeneratedRegex(@"\+?(\d+)\s*Counter")] private static partial Regex CounterText();
    [GeneratedRegex(@"(?is)class=""regulation-mark"">\s*Block\s*(\d+)")] private static partial Regex BlockMark();
    [GeneratedRegex(@"(?is)<div class=""card-image"">\s*<img[^>]*\bsrc=""([^""]+)""")] private static partial Regex CardImage();
    [GeneratedRegex(@"(?is)<div class=""card-prints-current"">.*?<span class=""text-lg"">(.*?)</span>")]
    private static partial Regex CurrentPrint();
    [GeneratedRegex(@"(?is)<br\s*/?>\s*<br\s*/?>")] private static partial Regex Paragraph();

    static string Text(Match m) => m.Success ? StripTags(m.Groups[1].Value) : "";
    static int? Int(Match m) => m.Success ? int.Parse(m.Groups[1].Value) : null;
    static string List(string s) =>
        string.Join(", ", s.Split('/').Select(x => x.Trim()).Where(x => x.Length > 0));

    /// <summary>A card page, /cards/P-066, as a printing in catalog.json's shape; null if it is not one.</summary>
    public static ScrapedCard? ParseCard(string doc)
    {
        var id = Text(IdSpan().Match(doc));
        var name = Text(NameSpan().Match(doc));
        if (id.Length == 0 || name.Length == 0) return null;

        var typeLine = TypeLine().Match(doc).Groups[1].Value;
        var stats = StatLine().Match(doc).Groups[1].Value;

        // The effect is the text section that is neither the title nor the types; a
        // Trigger is its last paragraph, which the official site keeps in its own box.
        var effect = new List<string>();
        var trigger = "";
        foreach (Match section in TextSection().Matches(doc))
        {
            var html = section.Groups[1].Value;
            if (html.Contains("card-text-title") || TypeSpan().IsMatch(html)) continue;
            foreach (var part in Paragraph().Split(html).Select(StripTags).Where(p => p.Length > 0))
            {
                if (part.StartsWith("[Trigger]", StringComparison.Ordinal)) trigger = part;
                else effect.Add(part);
            }
        }

        // A promo is filed with the official site's promos; anything else under the
        // product Limitless names.
        var promo = id.StartsWith("P-", StringComparison.Ordinal);
        var set = promo ? "Promotion card" : Text(CurrentPrint().Match(doc));
        var image = CardImage().Match(doc);

        return new ScrapedCard
        {
            CardId = id,
            BaseId = id,
            Name = name,
            SetLabel = set,
            SetName = set,
            Rarity = promo ? "P" : "",
            Category = Text(CategorySpan().Match(typeLine)).ToUpperInvariant(),
            Colors = List(Text(ColorSpan().Match(typeLine))),
            // A leader's Life sits where a character's cost does, as on the official site.
            Cost = Int(CostOrLife().Match(StripTags(typeLine))),
            Power = Int(PowerText().Match(StripTags(stats))),
            Counter = Int(CounterText().Match(StripTags(stats))),
            Types = List(Text(TypeSpan().Match(doc))),
            Attributes = List(Text(AttributeSpan().Match(stats))),
            Effect = string.Join(" ", effect),
            Trigger = trigger,
            BlockIcon = Int(BlockMark().Match(doc)),
            ImageUrl = image.Success ? WebUtility.HtmlDecode(image.Groups[1].Value) : "",
        };
    }

    /// <summary>
    /// The card numbers to look up: printed somewhere - the Japanese site, a Limitless
    /// product, a TCGplayer product - with no English printing and not fetched already.
    /// Base numbers only: an alt art is filed under its base card.
    /// </summary>
    public static List<string> Gaps(IEnumerable<ScrapedCard> english, IEnumerable<string> printed,
                                    IEnumerable<ScrapedCard> extra)
    {
        var have = english.Select(c => c.CardId.Split('_')[0])
                          .Concat(extra.Where(c => c.Source is null or "limitless").Select(c => c.CardId))
                          .ToHashSet(StringComparer.Ordinal);
        return printed.Select(id => id.Split('_')[0])
                      .Where(id => !have.Contains(id))
                      .Distinct().Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>What a run did, for a job's result.</summary>
    public sealed record Outcome(int Added, int Prints, int TcgplayerPrints, IReadOnlyList<string> NotListed);

    static async Task<List<ScrapedCard>> Read(string file)
    {
        if (!File.Exists(file)) return new();
        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<List<ScrapedCard>>(stream, Json.Options) ?? new();
    }

    public static async Task<int> Run(string[] args, AppPaths paths)
    {
        var outcome = await Refresh(paths);
        if (outcome is null) return 1;
        Console.WriteLine("\nnow run: manifest --reseed");
        return 0;
    }

    /// <summary>
    /// catalog-extra.json and price-links.json brought up to date: every Limitless
    /// product and TCGplayer group read, the cards no English catalogue has looked up
    /// one by one, and the prints the official site leaves out built from the lists.
    /// A card Limitless does not list yet is asked about again next run. Null when
    /// neither Limitless nor tcgcsv.com could be reached.
    /// </summary>
    public static async Task<Outcome?> Refresh(AppPaths paths, TimeSpan? pause = null)
    {
        var english = await Read(paths.Catalog);
        var japanese = await Read(paths.CatalogJapanese);
        var extra = await Read(paths.CatalogExtra);
        var before = extra.Select(c => c.CardId).ToHashSet(StringComparer.Ordinal);

        var prints = await LimitlessPrints.Crawl(pause);
        var products = await TcgCsv.Products();
        if (prints is null && products is null)
        {
            Console.Error.WriteLine($"could not reach Limitless or tcgcsv.com; {FileName} left as it was");
            return null;
        }

        var printed = japanese.Select(c => c.CardId)
                              .Concat(ExtraPrints.Numbers(prints ?? new(), products ?? new()));
        var gaps = Gaps(english, printed, extra);
        Console.WriteLine($"{gaps.Count} card numbers with no English printing yet");
        var (found, notListed) = await FetchCards(gaps, pause ?? DefaultPause);

        // What Limitless does not have yet, from its TCGplayer listing - asked for on
        // Limitless again next run, whose page takes over once it has one. Without the
        // TCGplayer lists, last run's stay.
        var fromTcgplayer = products is null
            ? extra.Where(c => c.Source == "tcgplayer-card" && notListed.Contains(c.CardId)).ToList()
            : notListed.Select(id => TcgCsv.Card(products.Where(p => p.Number == id)))
                       .OfType<ScrapedCard>().ToList();

        // Rewritten even when nothing was found, so a card the English site has since
        // picked up drops out of this file.
        var officialIds = english.Select(c => c.CardId).ToHashSet(StringComparer.Ordinal);
        extra = extra.Where(c => !officialIds.Contains(c.CardId) && c.Source != "tcgplayer-card")
                     .Concat(found).Concat(fromTcgplayer).ToList();

        // Without both lists the prints cannot be worked out afresh; last run's stay.
        var result = prints is not null && products is not null
            ? ExtraPrints.Build(english, extra, prints, products)
            : null;
        if (result is not null)
        {
            extra = result.Extra;
            await ExtraPrints.Write(Path.Combine(paths.Root, ExtraPrints.LinksFileName), result.Links);
        }
        extra = extra.OrderBy(c => c.CardId, StringComparer.Ordinal).ToList();
        await ExtraPrints.Write(paths.CatalogExtra, extra);

        int New(string source) => extra.Count(c => c.Source == source && !before.Contains(c.CardId));
        var newCards = found.Count(c => !before.Contains(c.CardId)) + New("tcgplayer-card");
        var missing = notListed.Where(id => extra.All(c => c.CardId != id)).ToList();
        var (fromLimitless, tcgplayerPrints) = (New("limitless-print"), New("tcgplayer"));
        Console.WriteLine($"\nwrote {FileName} — {extra.Count} printings; new: {newCards} cards, "
                          + $"{fromLimitless} prints from Limitless, {tcgplayerPrints} from TCGplayer"
                          + (missing.Count > 0 ? $"; found nowhere: {string.Join(", ", missing)}" : ""));
        return new Outcome(newCards, fromLimitless, tcgplayerPrints, missing);
    }

    /// <summary>Each number's own Limitless page, read as a card.</summary>
    static async Task<(List<ScrapedCard> Found, List<string> NotListed)> FetchCards(
        IReadOnlyList<string> gaps, TimeSpan pause)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        http.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");

        var found = new List<ScrapedCard>();
        var notListed = new List<string>();
        for (var i = 0; i < gaps.Count; i++)
        {
            var id = gaps[i];
            if (i > 0) await Task.Delay(pause);
            try
            {
                using var res = await http.GetAsync($"{Host}/cards/{Uri.EscapeDataString(id)}");
                if (res.StatusCode == HttpStatusCode.NotFound)
                {
                    notListed.Add(id);
                    Console.WriteLine($"  [{i + 1}/{gaps.Count}] {id,-10} not on Limitless yet");
                    continue;
                }
                res.EnsureSuccessStatusCode();
                var card = ParseCard(await res.Content.ReadAsStringAsync());
                if (card is null || card.CardId != id)
                {
                    notListed.Add(id);
                    Console.WriteLine($"  [{i + 1}/{gaps.Count}] {id,-10} page not understood");
                    continue;
                }
                card.Source = "limitless";
                found.Add(card);
                Console.WriteLine($"  [{i + 1}/{gaps.Count}] {id,-10} {card.Name}");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                notListed.Add(id);
                Console.WriteLine($"  [{i + 1}/{gaps.Count}] {id,-10} failed: {e.Message}");
            }
        }
        return (found, notListed);
    }
}
