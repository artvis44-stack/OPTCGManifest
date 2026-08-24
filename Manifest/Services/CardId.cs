using System.Text;
using System.Text.RegularExpressions;

namespace Manifest.Services;

/// <summary>Turning what a human or a camera produces into a card number.</summary>
public static partial class CardId
{
    [GeneratedRegex(@"((?:OP|ST|EB|PRB)\s?\d{2}\s?-\s?\d{3}|P\s?-\s?\d{3})[\s_-]*([PR]\d)?",
                    RegexOptions.IgnoreCase)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^([A-Z0-9]{1,4}?)(\d{2}-\d{3}.*)$")]
    private static partial Regex RepairSplitAtDigits();

    [GeneratedRegex(@"^([A-Z0-9]{2,4})[\-]?([A-Z0-9]{2})[\-]([A-Z0-9]{3})(.*)$")]
    private static partial Regex RepairThreeParts();

    [GeneratedRegex(@"^([A-Z0-9]+)-([A-Z0-9]+)$")]
    private static partial Regex RepairTwoParts();

    [GeneratedRegex(@"^([A-Z0-9]{2,3})(\d{2})$")]
    private static partial Regex RepairHeadSplit();

    [GeneratedRegex(@"\s")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^A-Z0-9\-_]")]
    private static partial Regex NotIdCharacter();

    /// <summary>
    /// Anything a human or a camera produces -> "OP01-016_p1". Null if unparseable.
    /// Handles lowercase, stray spaces, en-dashes, and an ID sitting inside a sentence.
    /// </summary>
    public static string? Normalise(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var s = raw.ToUpperInvariant().Replace('–', '-').Replace('—', '-');
        var m = IdPattern().Match(s);
        if (!m.Success) return null;
        var core = Whitespace().Replace(m.Groups[1].Value, "");
        var suffix = m.Groups[2];
        return suffix.Success ? core + "_" + suffix.Value.ToLowerInvariant() : core;
    }

    // OCR mixes up characters that look alike. The card number's shape is known
    // - letters, then digits - so each confusion can only go one way.
    static readonly Dictionary<char, char> ToLetters = new()
    {
        ['0'] = 'O', ['1'] = 'I', ['5'] = 'S', ['8'] = 'B',
    };

    static readonly Dictionary<char, char> ToDigits = new()
    {
        ['O'] = '0', ['Q'] = '0', ['D'] = '0', ['I'] = '1', ['L'] = '1',
        ['S'] = '5', ['B'] = '8', ['Z'] = '2', ['G'] = '6', ['T'] = '7',
    };

    static string Translate(string s, Dictionary<char, char> map)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s) sb.Append(map.TryGetValue(c, out var to) ? to : c);
        return sb.ToString();
    }

    /// <summary>
    /// Candidate readings of one OCR result, best guess first. Each is still just a
    /// string - it is <see cref="Normalise"/> and a catalogue lookup that decide
    /// whether any of them is a real card.
    /// </summary>
    public static List<string> Repairs(string? text)
    {
        var s = NotIdCharacter().Replace((text ?? "").ToUpperInvariant(), "");
        if (s.Length == 0) return new List<string>();

        var out_ = new List<string> { s };

        // leading run is always letters, everything after is always digits
        var m = RepairSplitAtDigits().Match(s);
        if (m.Success)
            out_.Add(Translate(m.Groups[1].Value, ToLetters) + m.Groups[2].Value);

        m = RepairThreeParts().Match(s);
        if (m.Success)
            out_.Add(Translate(m.Groups[1].Value, ToLetters)
                     + Translate(m.Groups[2].Value, ToDigits) + "-"
                     + Translate(m.Groups[3].Value, ToDigits) + m.Groups[4].Value);

        m = RepairTwoParts().Match(s);
        if (m.Success)
        {
            var head = m.Groups[1].Value;
            var tail = Translate(m.Groups[2].Value, ToDigits);
            var split = RepairHeadSplit().Match(Translate(head, ToLetters));
            if (split.Success)
                out_.Add(split.Groups[1].Value + split.Groups[2].Value + "-" + tail);
            out_.Add(Translate(head, ToLetters) + "-" + tail);
        }

        return out_.Distinct().ToList();
    }

    /// <summary>Strips anything that could walk out of the image cache directory.</summary>
    public static string SafeFileStem(string cardId) =>
        Regex.Replace(cardId, "[^A-Za-z0-9_\\-]", "");
}
