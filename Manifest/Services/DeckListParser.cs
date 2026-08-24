using System.Text.RegularExpressions;
using Manifest.Models;

namespace Manifest.Services;

public static partial class DeckListParser
{
    [GeneratedRegex(@"^\s*(\d{1,3})\s*x?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex QtyOnly();

    [GeneratedRegex(@"((?:OP|ST|EB|PRB)\s?\d{2}\s?-\s?\d{3}|P\s?-\s?\d{3})[\s_-]*([PR]\d)?",
                    RegexOptions.IgnoreCase)]
    private static partial Regex CardIdInLine();

    [GeneratedRegex(@"^\s*(\d{1,3})\s*x?\b", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingQty();

    [GeneratedRegex(@"\b(?:x\s*)?(\d{1,3})\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingQty();

    public static List<CardQuantity> Parse(string? text, IEnumerable<CardQuantity>? cards = null)
    {
        var byId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (cards is not null)
        {
            foreach (var c in cards)
                Add(byId, c.CardId, c.Qty);
        }

        foreach (var line in (text ?? "").Split('\n'))
            AddLine(byId, line);

        return byId
            .Where(kv => kv.Value > 0)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new CardQuantity { CardId = kv.Key, Qty = kv.Value })
            .ToList();
    }

    static void AddLine(Dictionary<string, int> byId, string raw)
    {
        var line = raw.Split('#')[0].Trim();
        if (line.Length == 0 || QtyOnly().IsMatch(line)) return;

        var idMatch = CardIdInLine().Match(line);
        if (!idMatch.Success) return;

        var cardId = CardId.Normalise(idMatch.Value);
        if (cardId is null) return;

        var before = line[..idMatch.Index];
        var afterStart = idMatch.Index + idMatch.Length;
        var after = afterStart < line.Length ? line[afterStart..] : "";

        var qty = Quantity(before, after);
        Add(byId, cardId, qty);
    }

    static int Quantity(string before, string after)
    {
        var leading = LeadingQty().Match(before);
        if (leading.Success && int.TryParse(leading.Groups[1].Value, out var n))
            return Math.Max(0, n);

        var trailing = TrailingQty().Match(after);
        if (trailing.Success && int.TryParse(trailing.Groups[1].Value, out n))
            return Math.Max(0, n);

        return 1;
    }

    static void Add(Dictionary<string, int> byId, string? rawCardId, int qty)
    {
        var cardId = CardId.Normalise(rawCardId) ?? rawCardId?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(cardId) || qty <= 0) return;
        byId[cardId] = byId.GetValueOrDefault(cardId) + qty;
    }
}
