using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Manifest.Models;

namespace Manifest.Services;

/// <summary>
/// The words an eBay listing for one card needs, made from the catalogue: an 80
/// character title with the terms buyers search for, a description, and the item
/// specifics eBay's trading card category asks for. Nothing is sent to eBay; the
/// seller copies these into its listing form.
/// </summary>
public static partial class ListingWriter
{
    /// <summary>eBay refuses a longer title.</summary>
    public const int MaxTitle = 80;

    /// <summary>Raw (ungraded) conditions, best first, as a seller picks from them.</summary>
    public static readonly string[] Conditions =
        { "Near Mint", "Lightly Played", "Moderately Played", "Heavily Played", "Damaged" };

    static readonly Dictionary<string, (string Short, string Blurb, string Ebay)> ConditionText = new()
    {
        ["Near Mint"] = ("NM", "Near Mint - straight from the pack into a sleeve, no visible wear.",
                         "Near mint or better"),
        ["Lightly Played"] = ("LP", "Lightly Played - light edge or corner wear; please see the photos.",
                              "Lightly played (Excellent)"),
        ["Moderately Played"] = ("MP", "Moderately Played - noticeable wear; please see the photos.",
                                 "Moderately played (Very good)"),
        ["Heavily Played"] = ("HP", "Heavily Played - heavy wear, scuffs or creases; please see the photos.",
                              "Heavily played (Poor)"),
        ["Damaged"] = ("Damaged", "Damaged - creased, bent or marked; please see the photos.",
                       "Heavily played (Poor)"),
    };

    // The catalogue's rarities come in several spellings depending on the source.
    static readonly Dictionary<string, (string Code, string Name)> Rarities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = ("C", "Common"), ["Common"] = ("C", "Common"),
            ["UC"] = ("UC", "Uncommon"), ["Uncommon"] = ("UC", "Uncommon"),
            ["R"] = ("R", "Rare"), ["Rare"] = ("R", "Rare"),
            ["SR"] = ("SR", "Super Rare"), ["SuperRare"] = ("SR", "Super Rare"),
            ["SEC"] = ("SEC", "Secret Rare"), ["SecretRare"] = ("SEC", "Secret Rare"),
            ["L"] = ("L", "Leader"), ["Leader"] = ("L", "Leader"),
            ["P"] = ("P", "Promo"), ["Promo"] = ("P", "Promo"),
            ["SP CARD"] = ("SP", "Special Card"), ["SP"] = ("SP", "Special Card"),
            ["Special"] = ("SP", "Special Card"), ["Special Card"] = ("SP", "Special Card"),
            ["TR"] = ("TR", "Treasure Rare"), ["TreasureRare"] = ("TR", "Treasure Rare"),
        };

    [GeneratedRegex(@"^([A-Z]{2,4})-?(\d{2})$")]
    private static partial Regex SetCode();

    [GeneratedRegex(@"\s*(·\s*Japanese|\(Japanese\))\s*", RegexOptions.IgnoreCase)]
    private static partial Regex JapaneseTag();

    [GeneratedRegex(@"^Alt art( \d+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex AltArt();

    [GeneratedRegex(@"^Reprint( \d+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Reprint();

    public static bool IsCondition(string? value) => value is not null && ConditionText.ContainsKey(value);

    public static bool IsJapanese(CatalogRow card) =>
        card.CardId.EndsWith(JapanesePrints.Tag, StringComparison.Ordinal)
        || card.Variant.Contains("Japanese", StringComparison.OrdinalIgnoreCase);

    /// <param name="card">The printing being sold.</param>
    /// <param name="englishSetName">
    /// What the English catalogue calls the card's set, for a Japanese card: its own
    /// row names the set in Japanese, which an English-language listing cannot use.
    /// </param>
    /// <param name="grade">A grading company and grade, "PSA 10", in place of a raw condition.</param>
    public static ListingDraft Write(CatalogRow card, string? englishSetName, string? condition, string? grade,
                                     int qty, double? marketGbp, List<SourcePriceRow> prices)
    {
        condition = IsCondition(condition) ? condition! : Conditions[0];
        grade = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim();
        qty = Math.Max(1, qty);

        var japanese = IsJapanese(card);
        var variant = JapaneseTag().Replace(card.Variant, " ").Trim();
        var alt = AltArt().IsMatch(variant);
        var rarity = card.Rarity is { Length: > 0 } r && Rarities.TryGetValue(r.Trim(), out var known)
            ? known
            : (Code: card.Rarity?.Trim() ?? "", Name: card.Rarity?.Trim() ?? "");
        var (setCode, setName) = SetOf(card, englishSetName);

        return new ListingDraft
        {
            CardId = card.CardId,
            Title = Title(card, qty, rarity.Code, variant, alt, setCode, setName, japanese, condition, grade),
            Description = Description(card, qty, rarity, variant, setCode, setName, japanese, condition, grade),
            Specifics = Specifics(card, rarity.Name, alt, setName ?? setCode, japanese, condition, grade),
            MarketGbp = marketGbp,
            Prices = prices,
            SoldSearchUrl = "https://www.ebay.co.uk/sch/i.html?LH_Sold=1&LH_Complete=1&_nkw="
                            + Uri.EscapeDataString(string.Join(' ', new[]
                            {
                                "One Piece", card.BaseId, alt ? "alt art" : null, japanese ? "japanese" : null,
                            }.Where(s => s is not null))),
        };
    }

    /// <summary>
    /// A set's code, OP01, when the catalogue has one, and its name in title case
    /// when it is written in English.
    /// </summary>
    static (string? Code, string? Name) SetOf(CatalogRow card, string? englishSetName)
    {
        var label = (card.SetLabel ?? "").Trim();
        var m = SetCode().Match(label);
        var code = m.Success ? m.Groups[1].Value + m.Groups[2].Value : null;

        var name = English(card.SetName) ?? English(englishSetName)
                   ?? (m.Success ? null : English(JapaneseTag().Replace(label, "").Trim()));
        if (name is not null && name == name.ToUpperInvariant())
            name = TitleCase(name);
        if (name is not null && code is not null && name.Replace("-", "") == code) name = null;
        return (code, name);
    }

    static readonly HashSet<string> SmallWords = new(StringComparer.OrdinalIgnoreCase)
        { "a", "an", "and", "at", "by", "for", "in", "of", "on", "or", "the", "to" };

    /// <summary>ROMANCE DAWN as Romance Dawn, and AWAKENING OF THE NEW ERA without a capital Of.</summary>
    static string TitleCase(string s)
    {
        var words = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant()).Split(' ');
        for (var i = 1; i < words.Length; i++)
            if (SmallWords.Contains(words[i])) words[i] = words[i].ToLowerInvariant();
        return string.Join(' ', words);
    }

    static string? English(string? s) =>
        string.IsNullOrWhiteSpace(s) || s.Any(c => c > ' ' && c != '’') ? null : s.Trim();

    /// <summary>
    /// One part of a title: the ways to write it from longest to shortest, and how
    /// much it matters. Parts that matter least are shortened, then dropped, until
    /// the title fits; a required part is only ever shortened.
    /// </summary>
    sealed class Part(int keep, params string?[] forms)
    {
        public const int Required = 1000;
        public readonly int Keep = keep;
        public readonly List<string> Forms = forms.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f!).ToList();
        public int At;
        public string? Text => At < Forms.Count ? Forms[At] : null;
        public bool CanGive => Keep >= Required ? At < Forms.Count - 1 : Text is not null;
    }

    static string Title(CatalogRow card, int qty, string rarity, string variant, bool alt, string? setCode,
                        string? setName, bool japanese, string condition, string? grade)
    {
        var redundantSet = setCode is not null
                           && card.BaseId.StartsWith(setCode + "-", StringComparison.OrdinalIgnoreCase);
        var reprint = Reprint().IsMatch(variant);
        var parts = new List<Part>
        {
            new(99, qty > 1 ? $"{qty}x" : null),
            new(Part.Required + 55, "One Piece Card Game", "One Piece TCG", "One Piece"),
            new(Part.Required, card.Name),
            new(Part.Required, card.BaseId),
            new(70, rarity),
            alt ? new Part(85, "Alt Art Parallel", "Alt Art")
                : variant.Length > 0 && !reprint ? new Part(45, variant, "Promo") : new Part(0),
            new(50, redundantSet ? null : setCode),
            new(20, setName),
            new(60, japanese ? "Japanese" : "English", japanese ? "JP" : "EN"),
            grade is not null ? new Part(90, grade) : new Part(65, ConditionText[condition].Short),
        };

        string Render() => string.Join(' ', parts.Select(p => p.Text).Where(t => t is not null));

        var title = Render();
        while (title.Length > MaxTitle)
        {
            var give = parts.Where(p => p.CanGive)
                            .OrderBy(p => p.Keep % Part.Required).FirstOrDefault();
            if (give is null) break;
            give.At++;
            title = Render();
        }
        return title.Length > MaxTitle ? title[..MaxTitle].TrimEnd() : title;
    }

    static string Description(CatalogRow card, int qty, (string Code, string Name) rarity, string variant,
                              string? setCode, string? setName, bool japanese, string condition, string? grade)
    {
        var sb = new StringBuilder();
        sb.Append(qty > 1 ? $"{qty}x " : "").Append("One Piece Card Game - ").Append(card.Name)
          .Append(" (").Append(card.BaseId).AppendLine(")");
        sb.AppendLine();

        void Line(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) sb.Append(label).Append(": ").AppendLine(value);
        }

        Line("Card", card.Name);
        Line("Number", card.BaseId);
        Line("Set", string.Join(" - ", new[] { setCode, setName }.Where(s => s is not null)));
        Line("Rarity", rarity.Name.Length > 0 && rarity.Name != rarity.Code
            ? $"{rarity.Name} ({rarity.Code})" : rarity.Code);
        Line("Version", variant.Length > 0 ? variant : "Standard");
        Line("Language", japanese ? "Japanese" : "English");
        Line("Type", string.Join(" · ", new[]
        {
            Pretty(card.Category), card.Colors,
            card.Cost is { } cost ? $"{cost} {(Pretty(card.Category) == "Leader" ? "life" : "cost")}" : null,
            card.Power is { } power ? $"{power} power" : null,
        }.Where(s => !string.IsNullOrWhiteSpace(s))));
        Line("Quantity", qty.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();
        sb.Append("Condition: ").AppendLine(grade is not null
            ? $"Graded {grade}, sealed in its slab."
            : ConditionText[condition].Blurb);
        sb.AppendLine("The card in the photos is the one you will receive.");
        return sb.ToString().TrimEnd();
    }

    static List<ItemSpecific> Specifics(CatalogRow card, string rarity, bool alt, string? set, bool japanese,
                                        string condition, string? grade)
    {
        var list = new List<ItemSpecific>
        {
            new("Game", "One Piece Card Game"),
            new("Manufacturer", "Bandai"),
            new("Card Name", card.Name),
            new("Character", card.Name),
            new("Card Number", card.BaseId),
        };
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) list.Add(new(name, value));
        }
        Add("Set", set);
        Add("Rarity", rarity);
        Add("Card Type", Pretty(card.Category));
        Add("Colour", card.Colors);
        Add("Features", alt ? "Alternate Art" : null);
        Add("Language", japanese ? "Japanese" : "English");
        if (grade is not null)
        {
            var split = grade.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            Add("Graded", "Yes");
            Add("Professional Grader", split[0]);
            Add("Grade", split.Length > 1 ? split[1] : null);
        }
        else
        {
            Add("Graded", "No");
            Add("Card Condition", ConditionText[condition].Ebay);
        }
        return list;
    }

    static string? Pretty(string? s) => string.IsNullOrWhiteSpace(s)
        ? null
        : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Trim().ToLowerInvariant());
}
