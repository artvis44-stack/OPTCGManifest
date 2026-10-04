using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Manifest.Data;

/// <summary>One page of a list, and the cursor for the next page (null on the last).</summary>
public sealed record Page<T>(List<T> Items, string? NextCursor);

/// <summary>
/// Keyset pagination: a page starts strictly after the sort-key values of the last
/// row of the previous one, rather than at an offset. An offset skips a row for
/// every row deleted from an earlier page while someone is scrolling, and repeats
/// one for every row inserted there; a position in the sort order does neither.
///
/// Every order ends in a unique column, so no two rows share a position and a page
/// boundary can never fall between two rows the cursor cannot tell apart.
/// </summary>
public sealed class Keyset
{
    public enum Kind { Text, Number, Time }

    public sealed record Key(string Expr, bool Descending, Kind Kind);

    public string Name { get; }
    readonly Key[] _keys;

    public Keyset(string name, params Key[] keys)
    {
        Name = name;
        _keys = keys;
    }

    public const int DefaultLimit = 60;
    public const int MaxLimit = 200;

    public static int Limit(string? raw) =>
        int.TryParse(raw, out var n) ? Math.Clamp(n, 1, MaxLimit) : DefaultLimit;

    /// <summary>The sort keys as extra columns, so the last row can be turned into a cursor.</summary>
    public string SelectColumns =>
        string.Join(", ", _keys.Select((k, i) => $"{k.Expr} AS _k{i}"));

    public string OrderBy =>
        string.Join(", ", _keys.Select(k => k.Expr + (k.Descending ? " DESC" : "")));

    /// <summary>
    /// "After the cursor" in this order, as SQL, with its parameters bound on
    /// <paramref name="cmd"/>. Spelled out as (a &gt; x) OR (a = x AND b &gt; y) ...
    /// rather than a row comparison, because the directions can differ per key.
    /// </summary>
    public string? After(string? cursor, DbCommand cmd, SqlDialect dialect)
    {
        if (string.IsNullOrEmpty(cursor)) return null;
        var values = Decode(cursor);

        var ors = new List<string>();
        for (var i = 0; i < _keys.Length; i++)
        {
            var ands = new List<string>();
            for (var j = 0; j < i; j++) ands.Add($"{_keys[j].Expr} = @_c{j}");
            ands.Add($"{_keys[i].Expr} {(_keys[i].Descending ? "<" : ">")} @_c{i}");
            ors.Add("(" + string.Join(" AND ", ands) + ")");
        }

        for (var i = 0; i < _keys.Length; i++)
            cmd.Bind($"@_c{i}", Value(_keys[i].Kind, values[i], dialect));

        return "(" + string.Join(" OR ", ors) + ")";
    }

    /// <summary>The cursor that continues after the row <paramref name="r"/> is on.</summary>
    public string CursorAt(DbDataReader r)
    {
        var values = new object?[_keys.Length];
        for (var i = 0; i < _keys.Length; i++)
        {
            var v = r.GetValue(r.GetOrdinal($"_k{i}"));
            values[i] = _keys[i].Kind switch
            {
                Kind.Number => Convert.ToDouble(v, CultureInfo.InvariantCulture),
                // Full precision: PostgreSQL keeps microseconds, and a cursor cut to
                // the second would skip rows written later in that same second.
                Kind.Time when v is DateTime t => t.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
            };
        }
        var json = JsonSerializer.SerializeToUtf8Bytes(new { s = Name, k = values });
        return Convert.ToBase64String(json).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Reads rows until <paramref name="limit"/>, asking for one more than that so
    /// the last page knows it is the last without a second query.
    /// </summary>
    public Page<T> Read<T>(DbCommand cmd, int limit, Func<DbDataReader, T> row)
    {
        var items = new List<T>();
        string? next = null;
        using var r = cmd.ExecuteReader();
        string? lastCursor = null;
        while (r.Read())
        {
            if (items.Count == limit)
            {
                next = lastCursor;
                break;
            }
            items.Add(row(r));
            lastCursor = CursorAt(r);
        }
        return new Page<T>(items, next);
    }

    object?[] Decode(string cursor)
    {
        try
        {
            var b64 = cursor.Replace('-', '+').Replace('_', '/');
            b64 += new string('=', (4 - b64.Length % 4) % 4);
            using var doc = JsonDocument.Parse(Convert.FromBase64String(b64));
            var root = doc.RootElement;
            if (root.GetProperty("s").GetString() != Name)
                throw new FormatException("that cursor belongs to a different sort order");
            var k = root.GetProperty("k");
            if (k.GetArrayLength() != _keys.Length)
                throw new FormatException("that cursor does not fit this list");
            return k.EnumerateArray().Select((e, i) => _keys[i].Kind == Kind.Number
                ? (object?)e.GetDouble()
                : e.GetString()).ToArray();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException
                                     or InvalidOperationException)
        {
            throw new FormatException("that cursor could not be read");
        }
    }

    static object? Value(Kind kind, object? value, SqlDialect dialect) => kind switch
    {
        Kind.Time when dialect.IsPostgres && value is string s =>
            DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(),
        _ => value,
    };
}
