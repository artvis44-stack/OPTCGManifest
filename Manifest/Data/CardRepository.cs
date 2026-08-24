using Manifest.Models;
using Manifest.Services;
using Microsoft.Data.Sqlite;

namespace Manifest.Data;

public sealed class CardRepository : ICardRepository, IScanRepository
{
    readonly Database _db;
    public CardRepository(Database db) => _db = db;

    const string ListColumns = """
        c.card_id, c.base_id, c.variant, c.name, c.set_label, c.set_name,
        c.rarity, c.category, c.colors, c.cost, c.power, c.counter, c.types, c.image_url
        """;

    /// <summary>Exact printing, else the base printing, else null.</summary>
    public CatalogRow? Resolve(string cardId)
    {
        using var conn = _db.Open();
        return Resolve(conn, cardId);
    }

    public static CatalogRow? Resolve(SqliteConnection conn, string cardId)
    {
        var row = CatalogRowById(conn, cardId);
        if (row is not null) return row;
        return CatalogRowById(conn, cardId.Split('_')[0]);
    }

    public static CatalogRow? CatalogRowById(SqliteConnection conn, string cardId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM catalog WHERE card_id = @id";
        cmd.Parameters.AddWithValue("@id", cardId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadCatalog(r) : null;
    }

    static CatalogRow ReadCatalog(SqliteDataReader r) => new()
    {
        CardId = r.Text("card_id"),
        BaseId = r.Text("base_id"),
        Variant = r.Text("variant"),
        Name = r.Text("name"),
        SetLabel = r.Str("set_label"),
        SetName = r.Str("set_name"),
        Rarity = r.Str("rarity"),
        Category = r.Str("category"),
        Colors = r.Str("colors"),
        Cost = r.Int("cost"),
        Power = r.Int("power"),
        Counter = r.Int("counter"),
        Types = r.Str("types"),
        Effect = r.Str("effect"),
        ImageUrl = r.Str("image_url"),
    };

    /// <summary>
    /// The catalogue is shared; the qty column beside each card is not. The owner
    /// travels in the join rather than the WHERE clause, so a card nobody owns still
    /// comes back with a qty of zero instead of vanishing from the results.
    /// </summary>
    public List<SearchRow> Search(long userId, string? q, string? limit, bool ownedOnly,
                                  string? category, string? color, string? rarity,
                                  string? setLabel)
    {
        q = (q ?? "").Trim();

        var sql = new System.Text.StringBuilder($"""
            SELECT {ListColumns}, COALESCE(k.qty, 0) AS qty, p.gbp AS price_gbp
            FROM catalog c LEFT JOIN collection k
                             ON k.card_id = c.card_id AND k.user_id = @user
                           LEFT JOIN prices p ON p.card_id = c.card_id
            """);

        var where = new List<string>();
        var args = new List<(string Name, object Value)> { ("@user", userId) };

        if (q.Length > 0)
        {
            var exact = CardId.Normalise(q);
            if (exact is not null)
            {
                where.Add("(c.card_id = @q_exact OR c.base_id = @q_base)");
                args.Add(("@q_exact", exact));
                args.Add(("@q_base", exact.Split('_')[0]));
            }
            else
            {
                where.Add("(c.name LIKE @q_name OR c.card_id LIKE @q_id OR c.types LIKE @q_types)");
                args.Add(("@q_name", $"%{q}%"));
                args.Add(("@q_id", $"{q.ToUpperInvariant()}%"));
                args.Add(("@q_types", $"%{q}%"));
            }
        }
        if (ownedOnly)
            where.Add("COALESCE(k.qty,0) > 0");

        // category = card type (Leader / Character / Event / Stage); compared
        // case-insensitively since older catalogue snapshots stored these as
        // shouty abbreviations (LEADER, CHARACTER...) rather than the current
        // scraper's title case - see Facets(), which returns whichever the local
        // catalogue actually has. color matches multi-colour cards too, e.g. a
        // "Red" filter also hits a "Red, Blue" card.
        if (!string.IsNullOrEmpty(category))
        {
            where.Add("UPPER(c.category) = UPPER(@category)");
            args.Add(("@category", category));
        }
        if (!string.IsNullOrEmpty(color))
        {
            where.Add("c.colors LIKE @color");
            args.Add(("@color", $"%{color}%"));
        }
        if (!string.IsNullOrEmpty(rarity))
        {
            where.Add("c.rarity = @rarity");
            args.Add(("@rarity", rarity));
        }
        if (!string.IsNullOrEmpty(setLabel))
        {
            where.Add("c.set_label = @set_label");
            args.Add(("@set_label", setLabel));
        }

        if (where.Count > 0)
            sql.Append(" WHERE ").Append(string.Join(" AND ", where));
        sql.Append(" ORDER BY c.base_id, c.variant LIMIT @limit");

        // Browsing a filter with no typed query (e.g. every Leader) can legitimately
        // return hundreds of rows, well past the old 200-row typeahead cap.
        if (!int.TryParse(limit ?? "40", out var n))
            throw new FormatException($"limit is not a number: {limit}");
        args.Add(("@limit", Math.Min(n, 3000)));

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql.ToString();
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);

        var results = new List<SearchRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            results.Add(new SearchRow
            {
                CardId = r.Text("card_id"),
                BaseId = r.Text("base_id"),
                Variant = r.Text("variant"),
                Name = r.Text("name"),
                SetLabel = r.Str("set_label"),
                SetName = r.Str("set_name"),
                Rarity = r.Str("rarity"),
                Category = r.Str("category"),
                Colors = r.Str("colors"),
                Cost = r.Int("cost"),
                Power = r.Int("power"),
                Counter = r.Int("counter"),
                Types = r.Str("types"),
                ImageUrl = r.Str("image_url"),
                Qty = r.IntOr("qty"),
                PriceGbp = r.Real("price_gbp"),
            });
        return results;
    }

    /// <summary>
    /// Distinct filter values straight from the catalogue, for populating the filter
    /// dropdowns - independent of what's owned or currently searched.
    /// </summary>
    public Facets Facets()
    {
        using var conn = _db.Open();

        List<string> Column(string name)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT DISTINCT {name} FROM catalog WHERE {name} IS NOT NULL "
                              + $"AND {name} != '' ORDER BY {name}";
            var values = new List<string>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) values.Add(r.GetString(0));
            return values;
        }

        var colors = Column("colors")
            .SelectMany(row => row.Split(','))
            .Select(part => part.Trim())
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return new Facets
        {
            Categories = Column("category"),
            Colors = colors,
            Rarities = Column("rarity"),
            Sets = Column("set_label"),
        };
    }

    public List<CollectionRow> Collection(long userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT k.card_id, k.qty, k.note, k.updated_at,
                   c.name, c.set_label, c.rarity, c.variant, c.colors,
                   c.category, c.image_url, c.cost, c.power, c.counter, c.types,
                   p.gbp AS price_gbp
            FROM collection k LEFT JOIN catalog c ON c.card_id = k.card_id
                              LEFT JOIN prices p ON p.card_id = k.card_id
            WHERE k.user_id = @user AND k.qty > 0
            ORDER BY k.card_id
            """;
        cmd.Parameters.AddWithValue("@user", userId);
        var rows = new List<CollectionRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add(new CollectionRow
            {
                CardId = r.Text("card_id"),
                Qty = r.IntOr("qty"),
                Note = r.Text("note"),
                UpdatedAt = r.Str("updated_at"),
                Name = r.Str("name"),
                SetLabel = r.Str("set_label"),
                Rarity = r.Str("rarity"),
                Variant = r.Str("variant"),
                Colors = r.Str("colors"),
                Category = r.Str("category"),
                ImageUrl = r.Str("image_url"),
                Cost = r.Int("cost"),
                Power = r.Int("power"),
                Counter = r.Int("counter"),
                Types = r.Str("types"),
                PriceGbp = r.Real("price_gbp"),
            });
        return rows;
    }

    public CardDetailRow? CardDetail(long userId, string rawCardId)
    {
        var cid = CardId.Normalise(rawCardId) ?? rawCardId.ToUpperInvariant().Trim();
        using var conn = _db.Open();

        CardDetailRow? Lookup(string id)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT c.*, COALESCE(k.qty, 0) AS qty, p.gbp AS price_gbp
                FROM catalog c LEFT JOIN collection k
                                 ON k.card_id = c.card_id AND k.user_id = @user
                               LEFT JOIN prices p ON p.card_id = c.card_id
                WHERE c.card_id = @id
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@user", userId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new CardDetailRow
            {
                CardId = r.Text("card_id"),
                BaseId = r.Text("base_id"),
                Variant = r.Text("variant"),
                Name = r.Text("name"),
                SetLabel = r.Str("set_label"),
                SetName = r.Str("set_name"),
                Rarity = r.Str("rarity"),
                Category = r.Str("category"),
                Colors = r.Str("colors"),
                Cost = r.Int("cost"),
                Power = r.Int("power"),
                Counter = r.Int("counter"),
                Types = r.Str("types"),
                Effect = r.Str("effect"),
                ImageUrl = r.Str("image_url"),
                Qty = r.IntOr("qty"),
                PriceGbp = r.Real("price_gbp"),
            };
        }

        return Lookup(cid) ?? Lookup(cid.Split('_')[0]);
    }

    public Stats Stats(long userId)
    {
        using var conn = _db.Open();
        var stats = new Stats();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(SUM(k.qty),0) t, COUNT(*) u,
                       COALESCE(SUM(k.qty * p.gbp),0) v
                FROM collection k LEFT JOIN prices p ON p.card_id = k.card_id
                WHERE k.user_id = @user AND k.qty > 0
                """;
            cmd.Parameters.AddWithValue("@user", userId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                stats.Total = r.IntOr("t");
                stats.Unique = r.IntOr("u");
                stats.ValueGbp = Math.Round(r.Real("v") ?? 0, 2, MidpointRounding.ToEven);
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT c.set_label AS s, SUM(k.qty) AS n,
                       COUNT(DISTINCT k.card_id) AS uniq,
                       (SELECT COUNT(*) FROM catalog x WHERE x.set_label = c.set_label) AS total
                FROM collection k JOIN catalog c ON c.card_id = k.card_id
                WHERE k.user_id = @user AND k.qty > 0
                GROUP BY c.set_label ORDER BY c.set_label
                """;
            cmd.Parameters.AddWithValue("@user", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                stats.Sets.Add(new SetStat
                {
                    SetLabel = r.Str("s"),
                    Count = r.IntOr("n"),
                    Unique = r.IntOr("uniq"),
                    Total = r.IntOr("total"),
                });
        }

        return stats;
    }

    /// <summary>Atomic. Two phones logging the same card at once both count.</summary>
    public AdjustResult Adjust(long userId, string rawCardId, int? delta, int? qty, string? note)
    {
        var cid = CardId.Normalise(rawCardId) ?? rawCardId.ToUpperInvariant().Trim();
        if (cid.Length == 0) throw new ArgumentException("empty card_id");

        using var conn = _db.Open();
        Exec(conn, "BEGIN IMMEDIATE");
        int updated;
        try
        {
            if (qty is not null)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO collection (user_id, card_id, qty, note)
                    VALUES (@user,@id,@qty,@note)
                    ON CONFLICT(user_id, card_id) DO UPDATE
                      SET qty = excluded.qty,
                          note = COALESCE(NULLIF(excluded.note,''), collection.note),
                          updated_at = datetime('now')
                    """;
                cmd.Parameters.AddWithValue("@user", userId);
                cmd.Parameters.AddWithValue("@id", cid);
                cmd.Parameters.AddWithValue("@qty", Math.Max(0, qty.Value));
                cmd.Parameters.AddWithValue("@note", note ?? "");
                cmd.ExecuteNonQuery();
            }
            else
            {
                var d = delta ?? 0;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO collection (user_id, card_id, qty, note)
                    VALUES (@user,@id,@seed,@note)
                    ON CONFLICT(user_id, card_id) DO UPDATE
                      SET qty = MAX(0, collection.qty + @delta),
                          note = COALESCE(NULLIF(excluded.note,''), collection.note),
                          updated_at = datetime('now')
                    """;
                cmd.Parameters.AddWithValue("@user", userId);
                cmd.Parameters.AddWithValue("@id", cid);
                cmd.Parameters.AddWithValue("@seed", Math.Max(0, d));
                cmd.Parameters.AddWithValue("@note", note ?? "");
                cmd.Parameters.AddWithValue("@delta", d);
                cmd.ExecuteNonQuery();
            }

            using (var clean = conn.CreateCommand())
            {
                clean.CommandText =
                    "DELETE FROM collection WHERE user_id = @user AND card_id = @id AND qty <= 0";
                clean.Parameters.AddWithValue("@user", userId);
                clean.Parameters.AddWithValue("@id", cid);
                clean.ExecuteNonQuery();
            }

            using (var read = conn.CreateCommand())
            {
                read.CommandText =
                    "SELECT qty FROM collection WHERE user_id = @user AND card_id = @id";
                read.Parameters.AddWithValue("@user", userId);
                read.Parameters.AddWithValue("@id", cid);
                var value = read.ExecuteScalar();
                updated = value is null or DBNull ? 0 : Convert.ToInt32(value);
            }

            Exec(conn, "COMMIT");
        }
        catch
        {
            Exec(conn, "ROLLBACK");
            throw;
        }

        var card = Resolve(conn, cid);
        return new AdjustResult
        {
            CardId = cid,
            Qty = updated,
            Name = card?.Name,
            SetLabel = card?.SetLabel,
            Variant = card?.Variant,
            Rarity = card?.Rarity,
            ImageUrl = card?.ImageUrl,
            Known = card is not null,
        };
    }

    public BulkLogResult AdjustMany(long userId, IReadOnlyList<CardQuantity> cards, string? note)
    {
        var clean = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cards)
        {
            var cid = CardId.Normalise(c.CardId) ?? c.CardId.ToUpperInvariant().Trim();
            if (cid.Length == 0 || c.Qty <= 0) continue;
            clean[cid] = clean.GetValueOrDefault(cid) + c.Qty;
        }
        if (clean.Count == 0)
            return new BulkLogResult();

        using var conn = _db.Open();
        Exec(conn, "BEGIN IMMEDIATE");
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO collection (user_id, card_id, qty, note)
                VALUES (@user,@id,@delta,@note)
                ON CONFLICT(user_id, card_id) DO UPDATE
                  SET qty = collection.qty + excluded.qty,
                      note = COALESCE(NULLIF(excluded.note,''), collection.note),
                      updated_at = datetime('now')
                """;
            var userParam = cmd.Parameters.Add("@user", SqliteType.Integer);
            var idParam = cmd.Parameters.Add("@id", SqliteType.Text);
            var deltaParam = cmd.Parameters.Add("@delta", SqliteType.Integer);
            var noteParam = cmd.Parameters.Add("@note", SqliteType.Text);

            userParam.Value = userId;
            noteParam.Value = note ?? "";
            foreach (var (cid, delta) in clean)
            {
                idParam.Value = cid;
                deltaParam.Value = delta;
                cmd.ExecuteNonQuery();
            }

            Exec(conn, "COMMIT");
        }
        catch
        {
            Exec(conn, "ROLLBACK");
            throw;
        }

        var result = new BulkLogResult
        {
            Added = clean.Values.Sum(),
            Unique = clean.Count,
        };
        foreach (var cid in clean.Keys.Order(StringComparer.Ordinal))
        {
            var card = Resolve(conn, cid);
            using var read = conn.CreateCommand();
            read.CommandText = "SELECT qty FROM collection WHERE user_id = @user AND card_id = @id";
            read.Parameters.AddWithValue("@user", userId);
            read.Parameters.AddWithValue("@id", cid);
            result.Cards.Add(new AdjustResult
            {
                CardId = cid,
                Qty = Convert.ToInt32(read.ExecuteScalar()),
                Name = card?.Name,
                SetLabel = card?.SetLabel,
                Variant = card?.Variant,
                Rarity = card?.Rarity,
                ImageUrl = card?.ImageUrl,
                Known = card is not null,
            });
        }

        return result;
    }

    /// <summary>Empties one account's collection. Everyone else's is untouched.</summary>
    public void ResetCollection(long userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collection WHERE user_id = @user";
        cmd.Parameters.AddWithValue("@user", userId);
        cmd.ExecuteNonQuery();
    }

    public long CatalogCount()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) c FROM catalog";
        return (long)cmd.ExecuteScalar()!;
    }

    public void LogScan(long userId, string? cardId, string result)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO scan_log (user_id, card_id, result) VALUES (@user,@id,@result)";
        cmd.Parameters.AddWithValue("@user", userId);
        cmd.Parameters.AddWithValue("@id", (object?)cardId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@result", result);
        cmd.ExecuteNonQuery();
    }

    internal static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
