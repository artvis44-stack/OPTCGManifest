using Manifest.Models;
using Manifest.Tools;

namespace Manifest.Services;

/// <summary>
/// Japanese printings, as further printings of the English catalogue's card numbers.
///
/// The Japanese card site prints the same numbers (OP01-016, OP01-016_p1) as the
/// English one, so a Japanese printing is filed under the same base_id with "_jp"
/// on the end of its own id: it shows in the print picker beside the English
/// printings, counts toward the same 4-copy limit and is its own line in a binder.
///
/// What the Japanese site writes in its fields is Japanese - colours as 赤/緑,
/// names and effects in Japanese. Searching, the deck builder's colour lock and
/// grouping all read those fields, so a printing whose number the English catalogue
/// knows takes the gameplay text from the English card and keeps only its own art,
/// set and rarity. A card not out in English yet keeps its Japanese text, with the
/// colours translated so it still lands in the right decks.
/// </summary>
public static class JapanesePrints
{
    public const string Tag = "_jp";

    static readonly Dictionary<string, string> Colours = new()
    {
        ["赤"] = "Red", ["緑"] = "Green", ["青"] = "Blue",
        ["紫"] = "Purple", ["黒"] = "Black", ["黄"] = "Yellow",
    };

    static readonly Dictionary<string, string> Rarities = new()
    {
        ["SPカード"] = "SP CARD",
    };

    public static bool IsJapanese(string cardId) => cardId.EndsWith(Tag, StringComparison.Ordinal);

    /// <summary>"Japanese", or "Alt art · Japanese" for a Japanese alt art.</summary>
    public static string VariantLabel(string siteId)
    {
        var parts = siteId.Split('_', 2);
        var label = CatalogScraper.VariantLabel(parts.Length > 1 ? parts[1] : "");
        return label.Length == 0 ? "Japanese" : label + " · Japanese";
    }

    static string TranslateColours(string? colours) =>
        string.Join(", ", (colours ?? "").Split(new[] { ',', '/' },
                                                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                         .Select(c => Colours.GetValueOrDefault(c, c)));

    /// <summary>
    /// The catalogue rows for a scrape of the Japanese site (catalog-jp.json, ids as
    /// the site gives them), given the English rows they sit beside.
    /// </summary>
    public static List<CatalogRow> FromScrape(IReadOnlyList<CatalogRow> english,
                                              IReadOnlyList<CatalogRow> scraped)
    {
        // The card a Japanese printing reads as: the English base printing, or failing
        // that any English printing of the number.
        var card = new Dictionary<string, CatalogRow>(StringComparer.Ordinal);
        foreach (var e in english.Where(e => !IsJapanese(e.CardId)))
            if (!card.ContainsKey(e.BaseId) || e.CardId == e.BaseId) card[e.BaseId] = e;

        var rows = new List<CatalogRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var jp in scraped)
        {
            if (string.IsNullOrWhiteSpace(jp.CardId)) continue;
            var id = jp.CardId.Trim() + Tag;
            if (!seen.Add(id)) continue;   // the site lists a reprint under each set it is in

            var baseId = jp.CardId.Split('_')[0];
            var row = new CatalogRow
            {
                CardId = id,
                BaseId = baseId,
                Variant = VariantLabel(jp.CardId),
                SetLabel = jp.SetLabel,
                SetName = jp.SetName,
                Rarity = jp.Rarity is { } r ? Rarities.GetValueOrDefault(r, r) : null,
                ImageUrl = jp.ImageUrl,
            };
            if (card.TryGetValue(baseId, out var en))
            {
                row.Name = en.Name;
                row.Category = en.Category;
                row.Colors = en.Colors;
                row.Cost = en.Cost;
                row.Power = en.Power;
                row.Counter = en.Counter;
                row.Types = en.Types;
                row.Effect = en.Effect;
            }
            else
            {
                row.Name = jp.Name;
                row.Category = jp.Category;
                row.Colors = TranslateColours(jp.Colors);
                row.Cost = jp.Cost;
                row.Power = jp.Power;
                row.Counter = jp.Counter;
                row.Types = jp.Types;
                row.Effect = jp.Effect;
            }
            rows.Add(row);
        }
        return rows;
    }
}
