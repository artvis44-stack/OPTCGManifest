using System.Text;
using Manifest.Models;

namespace Manifest.Services;

/// <summary>
/// The collection as a spreadsheet. Matches Python's csv module defaults - minimal
/// quoting, doubled quotes, CRLF line endings - so a file exported before the rewrite
/// and one exported after are byte-identical.
/// </summary>
public static class CsvExport
{
    public static string Write(IEnumerable<CollectionRow> cards)
    {
        var sb = new StringBuilder();
        Row(sb, "card_id", "name", "set", "variant", "rarity", "colors",
                "category", "quantity", "note", "updated_at");
        foreach (var c in cards)
            Row(sb, c.CardId, c.Name ?? "", c.SetLabel ?? "", c.Variant ?? "",
                    c.Rarity ?? "", c.Colors ?? "", c.Category ?? "",
                    c.Qty.ToString(), c.Note ?? "", c.UpdatedAt ?? "");
        return sb.ToString();
    }

    static void Row(StringBuilder sb, params string[] fields)
    {
        sb.AppendJoin(',', fields.Select(Quote)).Append("\r\n");
    }

    static string Quote(string field)
    {
        if (field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return field;
        return '"' + field.Replace("\"", "\"\"") + '"';
    }
}
