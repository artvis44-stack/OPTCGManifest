using Microsoft.Data.Sqlite;

namespace Manifest.Data;

/// <summary>
/// SQLite columns are nullable almost everywhere in this schema, and the Python
/// original leaned on dicts to carry that. These keep the repositories readable.
/// </summary>
public static class ReaderExtensions
{
    public static string? Str(this SqliteDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static string Text(this SqliteDataReader r, string column) =>
        r.Str(column) ?? "";

    public static int? Int(this SqliteDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetInt32(i);
    }

    public static int IntOr(this SqliteDataReader r, string column, int fallback = 0) =>
        r.Int(column) ?? fallback;

    public static long Long(this SqliteDataReader r, string column) =>
        r.GetInt64(r.GetOrdinal(column));

    public static long? LongOrNull(this SqliteDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetInt64(i);
    }

    public static double? Real(this SqliteDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetDouble(i);
    }
}
