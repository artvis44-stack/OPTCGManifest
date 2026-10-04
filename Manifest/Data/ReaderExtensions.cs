using System.Data.Common;
using System.Globalization;

namespace Manifest.Data;

/// <summary>
/// SQLite columns are nullable almost everywhere in this schema, and the Python
/// original leaned on dicts to carry that. These keep the repositories readable.
///
/// They convert rather than cast, because the two databases hand the same column
/// back as different types: COUNT and SUM are bigint on PostgreSQL, and its
/// timestamps arrive as DateTime where SQLite's are already text.
/// </summary>
public static class ReaderExtensions
{
    /// <summary>The text format timestamps leave this layer in, whichever database.</summary>
    public const string StampFormat = "yyyy-MM-dd HH:mm:ss";

    public static string? Str(this DbDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        if (r.IsDBNull(i)) return null;
        return r.GetValue(i) switch
        {
            string s => s,
            DateTime t => t.ToString(StampFormat, CultureInfo.InvariantCulture),
            var v => Convert.ToString(v, CultureInfo.InvariantCulture),
        };
    }

    public static string Text(this DbDataReader r, string column) =>
        r.Str(column) ?? "";

    public static int? Int(this DbDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);
    }

    public static int IntOr(this DbDataReader r, string column, int fallback = 0) =>
        r.Int(column) ?? fallback;

    public static long Long(this DbDataReader r, string column) =>
        Convert.ToInt64(r.GetValue(r.GetOrdinal(column)), CultureInfo.InvariantCulture);

    public static long? LongOrNull(this DbDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture);
    }

    public static double? Real(this DbDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : Convert.ToDouble(r.GetValue(i), CultureInfo.InvariantCulture);
    }
}
