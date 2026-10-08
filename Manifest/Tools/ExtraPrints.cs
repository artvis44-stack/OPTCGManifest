using System.Text.Json;
using System.Text.RegularExpressions;
using static Manifest.Tools.CatalogScraper;

namespace Manifest.Tools;

/// <summary>
/// The prints the official English site does not list, and where each printing's
/// prices are to be found.
///
/// From Limitless: English prints with an official number - the 1st Anniversary
/// Set's Nami is OP01-016_p6 - that catalog.json does not have. From TCGplayer (via
/// tcgcsv.com): stamped release-event and pre-release cards, anniversary tournament
/// cards, judge packs and the like, which have no number of their own and are filed
/// under their card's number as _t1, _t2... Either takes its gameplay text from the
/// English card of the same number; only the art, set and rarity are its own.
///
/// The links say, for every printing Limitless or TCGplayer prices, which Limitless
/// print and which TCGplayer product it is - so a price refresh need not work it all
/// out again.
/// </summary>
public static partial class ExtraPrints
{
    public const string LinksFileName = "price-links.json";

    public sealed class Link
    {
        public string CardId { get; set; } = "";
        public string? Limitless { get; set; }
        public long? Tcgplayer { get; set; }
        public string? Cardmarket { get; set; }
    }

    public sealed record Result(List<ScrapedCard> Extra, List<Link> Links);

    [GeneratedRegex(@"^(op|st|eb|prb)-?(\d{2})")] private static partial Regex SlugCode();
    [GeneratedRegex(@"(OP|ST|EB|PRB)-?(\d{2})")] private static partial Regex LabelCode();
    [GeneratedRegex(@"\(([^()]*)\)")] private static partial Regex Parenthesised();
    [GeneratedRegex(@"Promotion Cards|Release Event|Pre-Release|Anniversary Tournament")]
    private static partial Regex StampedGroup();

    static string? CodeOfSlug(string slug) =>
        SlugCode().Match(slug) is { Success: true } m ? m.Groups[1].Value.ToUpperInvariant() + m.Groups[2].Value : null;

    static HashSet<string> CodesOfLabel(string? label) =>
        LabelCode().Matches((label ?? "").ToUpperInvariant()).Select(m => m.Groups[1].Value + m.Groups[2].Value).ToHashSet();

    /// <summary>A TCGplayer-only print's label: what its name says in brackets, or its group.</summary>
    public static (string SetLabel, string Variant) TcgplayerLabel(TcgCsv.Product p)
    {
        var note = Parenthesised().Matches(p.Name).Select(m => m.Groups[1].Value.Trim())
                                  .LastOrDefault(t => t.Length > 0 && !t.All(char.IsDigit));
        if (p.GroupName.Contains("Promotion Cards") && note is not null) return (note, note);
        var group = p.GroupName.EndsWith(" Cards") ? p.GroupName[..^" Cards".Length] : p.GroupName;
        return (p.GroupName, group);
    }

    static ScrapedCard From(ScrapedCard card, string id, string set, string variant, string rarity,
                            string image, string source, long? tcgplayer) => new()
    {
        CardId = id,
        BaseId = card.BaseId,
        Variant = variant,
        Name = card.Name,
        SetLabel = set,
        SetName = set,
        Rarity = rarity.Length > 0 ? rarity : card.Rarity,
        Category = card.Category,
        Colors = card.Colors,
        Cost = card.Cost,
        Power = card.Power,
        Counter = card.Counter,
        Types = card.Types,
        Attributes = card.Attributes,
        Effect = card.Effect,
        Trigger = card.Trigger,
        BlockIcon = card.BlockIcon,
        ImageUrl = image,
        Source = source,
        TcgplayerId = tcgplayer,
    };

    /// <param name="english">catalog.json.</param>
    /// <param name="extra">catalog-extra.json as it stands: its Limitless cards are kept, and its _tN numbers.</param>
    public static Result Build(IReadOnlyList<ScrapedCard> english, IReadOnlyList<ScrapedCard> extra,
                               IReadOnlyList<LimitlessPrints.Print> limitless,
                               IReadOnlyList<TcgCsv.Product> tcgplayer)
    {
        var cards = extra.Where(c => c.Source is null or "limitless" or "tcgplayer-card").ToList();
        var catalogue = english.Concat(cards).GroupBy(c => c.CardId).ToDictionary(g => g.Key, g => g.First());
        // The card a new print reads as: the base printing, or failing that any printing of the number.
        var card = new Dictionary<string, ScrapedCard>(StringComparer.Ordinal);
        foreach (var c in catalogue.Values)
            if (!card.ContainsKey(c.BaseId) || c.CardId == c.BaseId) card[c.BaseId] = c;

        var links = new Dictionary<string, Link>(StringComparer.Ordinal);
        var added = new List<ScrapedCard>();

        // ---- Limitless. Several rows can carry one number - a reprint keeps the art of
        // the card it reprints - so the row from the printing's own set goes first, and
        // the others look for a reprint (_r1...) of the number filed in their set.
        bool OwnSet(LimitlessPrints.Print p) =>
            catalogue.TryGetValue(p.CardId, out var c) && CodeOfSlug(p.Slug) is { } code
            && CodesOfLabel(c.SetLabel).Contains(code);

        // Limitless and the official site do not always agree which art an alt-art
        // number is. Where the official site files a printing under a set and Limitless
        // under a promo product, the official site is taken at its word: that row is
        // some other printing, and its prices are not this one's.
        bool Contradicts(LimitlessPrints.Print p) =>
            catalogue.TryGetValue(p.CardId, out var c) && c.Source is null
            && CodesOfLabel(c.SetLabel).Count > 0 && CodeOfSlug(p.Slug) is null;

        foreach (var p in limitless.Where(p => !p.Japanese).OrderBy(p => OwnSet(p) ? 0 : 1))
        {
            string? id = null;
            if (!links.ContainsKey(p.CardId) && !Contradicts(p)) id = p.CardId;
            else if (CodeOfSlug(p.Slug) is { } code)
                id = Enumerable.Range(1, 9).Select(n => $"{p.BaseId}_r{n}")
                               .FirstOrDefault(r => catalogue.TryGetValue(r, out var c) && !links.ContainsKey(r)
                                                    && CodesOfLabel(c.SetLabel).Contains(code));
            if (id is null) continue;

            if (!catalogue.ContainsKey(id))
            {
                if (!card.TryGetValue(p.BaseId, out var basis)) continue;
                var suffix = id.Contains('_') ? id.Split('_', 2)[1] : "";
                var row = From(basis, id, p.Product, VariantLabel(suffix), p.Rarity, p.ImageUrl,
                               "limitless-print", null);
                added.Add(row);
                catalogue[id] = row;
            }
            links[id] = new Link { CardId = id, Limitless = p.Href, Tcgplayer = p.TcgplayerId, Cardmarket = p.CardmarketPath };
        }

        // ---- TCGplayer: what Limitless already ties to a printing is done; a stamped
        // or promo card it does not know becomes a print of its own.
        // A card read off its TCGplayer listing is that listing's printing already.
        foreach (var c in cards.Where(c => c.Source == "tcgplayer-card" && c.TcgplayerId is not null))
            links.TryAdd(c.CardId, new Link { CardId = c.CardId, Tcgplayer = c.TcgplayerId });
        var known = limitless.Where(p => p.TcgplayerId is not null).Select(p => p.TcgplayerId!.Value)
                             .Concat(links.Values.Where(l => l.Tcgplayer is not null).Select(l => l.Tcgplayer!.Value))
                             .ToHashSet();
        var numbered = extra.Where(c => c.Source == "tcgplayer" && c.TcgplayerId is not null)
                            .GroupBy(c => c.TcgplayerId!.Value)
                            .ToDictionary(g => g.Key, g => g.First().CardId);
        var taken = catalogue.Keys.Concat(numbered.Values).ToHashSet(StringComparer.Ordinal);
        foreach (var p in tcgplayer.Where(p => !known.Contains(p.ProductId) && StampedGroup().IsMatch(p.GroupName))
                                   .OrderBy(p => p.ProductId))
        {
            if (!card.TryGetValue(p.Number, out var basis)) continue;
            if (!numbered.TryGetValue(p.ProductId, out var id))
            {
                id = Enumerable.Range(1, 999).Select(n => $"{p.Number}_t{n}").First(t => !taken.Contains(t));
                taken.Add(id);
            }
            var (set, variant) = TcgplayerLabel(p);
            var row = From(basis, id, set, variant, p.Rarity, p.ImageUrl, "tcgplayer", p.ProductId);
            added.Add(row);
            catalogue[id] = row;
            links[id] = new Link { CardId = id, Tcgplayer = p.ProductId };
        }

        var output = cards.Concat(added).OrderBy(c => c.CardId, StringComparer.Ordinal).ToList();
        return new Result(output, links.Values.OrderBy(l => l.CardId, StringComparer.Ordinal).ToList());
    }

    /// <summary>Card numbers Limitless or TCGplayer print that neither catalogue file has yet.</summary>
    public static IEnumerable<string> Numbers(IEnumerable<LimitlessPrints.Print> limitless,
                                              IEnumerable<TcgCsv.Product> tcgplayer) =>
        limitless.Where(p => !p.Japanese).Select(p => p.BaseId).Concat(tcgplayer.Select(p => p.Number));

    public static async Task<List<Link>> ReadLinks(AppPaths paths)
    {
        var file = Path.Combine(paths.Root, LinksFileName);
        if (!File.Exists(file)) return new();
        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<List<Link>>(stream, Json.Options) ?? new();
    }

    public static async Task Write<T>(string file, T value)
    {
        var tmp = $"{file}.{Guid.NewGuid():N}.part";
        await using (var stream = File.Create(tmp))
            await JsonSerializer.SerializeAsync(stream, value, Json.Options);
        File.Move(tmp, file, overwrite: true);
    }
}
