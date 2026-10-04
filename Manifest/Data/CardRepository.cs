using Manifest.Models;
using Manifest.Services;
using System.Data.Common;

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

    public static CatalogRow? Resolve(DbConnection conn, string cardId)
    {
        var row = CatalogRowById(conn, cardId);
        if (row is not null) return row;
        return CatalogRowById(conn, cardId.Split('_')[0]);
    }

    public static CatalogRow? CatalogRowById(DbConnection conn, string cardId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM catalog WHERE card_id = @id";
        cmd.Bind("@id", cardId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadCatalog(r) : null;
    }

    static CatalogRow ReadCatalog(DbDataReader r) => new()
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
    /// The orders a catalogue search can be paged through, by the names the UI's
    /// sort menu uses. Each ends in card_id, which is unique, so every row has a
    /// position of its own. Missing values sort as '' or -1 so they still have one:
    /// a NULL compares as unknown and would fall out of the cursor's WHERE clause.
    /// </summary>
    static readonly Dictionary<string, Keyset> SearchSorts = Sorts("c.card_id", new()
    {
        ["number"] = new[] { Text("c.base_id"), Text("c.variant") },
        ["colors"] = new[] { Text("COALESCE(c.colors, '')") },
        ["set_label"] = new[] { Text("COALESCE(c.set_label, '')") },
        ["types"] = new[] { Text("COALESCE(c.types, '')") },
        ["price_gbp"] = new[] { Number("COALESCE(p.gbp, -1)", descending: true) },
        ["qty"] = new[] { Number("COALESCE(k.qty, 0)", descending: true) },
        ["not_owned"] = new[] { Number("COALESCE(k.qty, 0)") },
    });

    /// <summary>The same orders for the owned list, keyed on the collection's own card_id.</summary>
    static readonly Dictionary<string, Keyset> CollectionSorts = Sorts("k.card_id", new()
    {
        ["number"] = Array.Empty<Keyset.Key>(),
        ["colors"] = new[] { Text("COALESCE(c.colors, '')") },
        ["set_label"] = new[] { Text("COALESCE(c.set_label, '')") },
        ["types"] = new[] { Text("COALESCE(c.types, '')") },
        ["price_gbp"] = new[] { Number("COALESCE(p.gbp, -1)", descending: true) },
        ["qty"] = new[] { Number("k.qty", descending: true) },
    });

    static Keyset.Key Text(string expr) => new(expr, false, Keyset.Kind.Text);
    static Keyset.Key Number(string expr, bool descending = false) =>
        new(expr, descending, Keyset.Kind.Number);

    static Dictionary<string, Keyset> Sorts(string unique, Dictionary<string, Keyset.Key[]> keys) =>
        keys.ToDictionary(kv => kv.Key,
                          kv => new Keyset(kv.Key, kv.Value.Append(Text(unique)).ToArray()));

    static Keyset SortFor(Dictionary<string, Keyset> sorts, string? name) =>
        sorts.TryGetValue(name ?? "number", out var s) ? s
            : throw new FormatException($"unknown sort: {name}");

    public static IReadOnlyCollection<string> SearchSortNames => SearchSorts.Keys;

    /// <summary>
    /// The old unpaged search, kept for callers that predate cursors: the first
    /// page in card-number order, as long as they ask for, up to 3000.
    /// </summary>
    public List<SearchRow> Search(long userId, string? q, string? limit, bool ownedOnly,
                                  string? category, string? color, string? rarity,
                                  string? setLabel)
    {
        // Browsing a filter with no typed query (e.g. every Leader) can legitimately
        // return hundreds of rows, well past the old 200-row typeahead cap.
        if (!int.TryParse(limit ?? "40", out var n))
            throw new FormatException($"limit is not a number: {limit}");

        var filter = new CardFilter
        {
            Q = q,
            Category = category,
            Colors = string.IsNullOrEmpty(color) ? Array.Empty<string>() : new[] { color },
            Rarity = rarity,
            SetLabel = setLabel,
            Owned = ownedOnly ? true : null,
        };
        return SearchPage(userId, filter, "number", Math.Clamp(n, 1, 3000), null).Items;
    }

    /// <summary>
    /// The catalogue is shared; the qty column beside each card is not. The owner
    /// travels in the join rather than the WHERE clause, so a card nobody owns still
    /// comes back with a qty of zero instead of vanishing from the results.
    /// </summary>
    public Page<SearchRow> SearchPage(long userId, CardFilter filter, string? sort, int limit,
                                      string? cursor)
    {
        var order = SortFor(SearchSorts, sort);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.Bind("@user", userId);

        var where = new List<string>();
        var q = (filter.Q ?? "").Trim();
        if (q.Length > 0)
        {
            var exact = CardId.Normalise(q);
            if (exact is not null)
            {
                where.Add("(c.card_id = @q_exact OR c.base_id = @q_base)");
                cmd.Bind("@q_exact", exact);
                cmd.Bind("@q_base", exact.Split('_')[0]);
            }
            else
            {
                var like = _db.Dialect;
                where.Add($"({like.Like("c.name", "@q_name")} OR {like.Like("c.card_id", "@q_id")} "
                          + $"OR {like.Like("c.types", "@q_types")})");
                cmd.Bind("@q_name", $"%{q}%");
                cmd.Bind("@q_id", $"{q.ToUpperInvariant()}%");
                cmd.Bind("@q_types", $"%{q}%");
            }
        }
        if (filter.Owned is { } owned)
            where.Add(owned ? "COALESCE(k.qty, 0) > 0" : "COALESCE(k.qty, 0) = 0");
        AddCatalogFilters(where, cmd, filter);
        if (order.After(cursor, cmd, _db.Dialect) is { } after) where.Add(after);

        cmd.CommandText = $"""
            SELECT {ListColumns}, COALESCE(k.qty, 0) AS qty, p.gbp AS price_gbp,
                   {order.SelectColumns}
            FROM catalog c LEFT JOIN collection k
                             ON k.card_id = c.card_id AND k.user_id = @user
                           LEFT JOIN prices p ON p.card_id = c.card_id
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ORDER BY {order.OrderBy}
            LIMIT @limit
            """;
        cmd.Bind("@limit", limit + 1);

        return order.Read(cmd, limit, r => new SearchRow
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
    }

    /// <summary>
    /// The filters on catalogue columns, shared by search and the owned list.
    /// category = card type (Leader / Character / Event / Stage); compared
    /// case-insensitively since older catalogue snapshots stored these as shouty
    /// abbreviations (LEADER, CHARACTER...) rather than the current scraper's title
    /// case - see Facets(), which returns whichever the local catalogue actually
    /// has. A colour matches multi-colour cards too, e.g. "Red" also hits a
    /// "Red, Blue" card.
    /// </summary>
    void AddCatalogFilters(List<string> where, System.Data.Common.DbCommand cmd, CardFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Category))
        {
            where.Add("UPPER(c.category) = UPPER(@category)");
            cmd.Bind("@category", filter.Category);
        }
        if (!string.IsNullOrEmpty(filter.ExcludeCategory))
        {
            where.Add("UPPER(COALESCE(c.category, '')) <> UPPER(@exclude_category)");
            cmd.Bind("@exclude_category", filter.ExcludeCategory);
        }
        var colors = filter.Colors.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        if (colors.Count > 0)
        {
            where.Add("(" + string.Join(" OR ", colors.Select((_, i) =>
                _db.Dialect.Like("c.colors", $"@color{i}"))) + ")");
            for (var i = 0; i < colors.Count; i++) cmd.Bind($"@color{i}", $"%{colors[i].Trim()}%");
        }
        if (!string.IsNullOrEmpty(filter.Rarity))
        {
            where.Add("c.rarity = @rarity");
            cmd.Bind("@rarity", filter.Rarity);
        }
        if (!string.IsNullOrEmpty(filter.SetLabel))
        {
            where.Add("c.set_label = @set_label");
            cmd.Bind("@set_label", filter.SetLabel);
        }
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
        cmd.Bind("@user", userId);
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

    /// <summary>
    /// One page of what an account owns. Cards logged by number before they reached
    /// the catalogue are included - they have no catalogue row to filter on, so any
    /// catalogue filter leaves them out, but a plain listing or a number search does
    /// not.
    /// </summary>
    public Page<CollectionRow> CollectionPage(long userId, CardFilter filter, string? sort,
                                              int limit, string? cursor)
    {
        var order = SortFor(CollectionSorts, sort);
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.Bind("@user", userId);

        var where = new List<string> { "k.user_id = @user", "k.qty > 0" };
        var q = (filter.Q ?? "").Trim();
        if (q.Length > 0)
        {
            where.Add($"({_db.Dialect.Like("k.card_id", "@q")} OR {_db.Dialect.Like("c.name", "@q")})");
            cmd.Bind("@q", $"%{q}%");
        }
        AddCatalogFilters(where, cmd, filter);
        if (order.After(cursor, cmd, _db.Dialect) is { } after) where.Add(after);

        cmd.CommandText = $"""
            SELECT k.card_id, k.qty, k.note, k.updated_at,
                   c.name, c.set_label, c.rarity, c.variant, c.colors,
                   c.category, c.image_url, c.cost, c.power, c.counter, c.types,
                   p.gbp AS price_gbp, {order.SelectColumns}
            FROM collection k LEFT JOIN catalog c ON c.card_id = k.card_id
                              LEFT JOIN prices p ON p.card_id = k.card_id
            WHERE {string.Join(" AND ", where)}
            ORDER BY {order.OrderBy}
            LIMIT @limit
            """;
        cmd.Bind("@limit", limit + 1);

        return order.Read(cmd, limit, r => new CollectionRow
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
            cmd.Bind("@id", id);
            cmd.Bind("@user", userId);
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
            cmd.Bind("@user", userId);
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
            cmd.Bind("@user", userId);
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

        var now = _db.Dialect.Now;
        using var conn = _db.Open();
        int updated;
        using (var tx = conn.BeginTransaction())
        {
            if (qty is not null)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    INSERT INTO collection (user_id, card_id, qty, note)
                    VALUES (@user,@id,@qty,@note)
                    ON CONFLICT(user_id, card_id) DO UPDATE
                      SET qty = excluded.qty,
                          note = COALESCE(NULLIF(excluded.note,''), collection.note),
                          updated_at = {now}
                    """;
                cmd.Bind("@user", userId);
                cmd.Bind("@id", cid);
                cmd.Bind("@qty", Math.Max(0, qty.Value));
                cmd.Bind("@note", note ?? "");
                cmd.ExecuteNonQuery();
            }
            else
            {
                var d = delta ?? 0;
                using var cmd = conn.CreateCommand();
                // CASE rather than MAX/GREATEST: SQLite has only the one and
                // PostgreSQL only the other.
                cmd.CommandText = $"""
                    INSERT INTO collection (user_id, card_id, qty, note)
                    VALUES (@user,@id,@seed,@note)
                    ON CONFLICT(user_id, card_id) DO UPDATE
                      SET qty = CASE WHEN collection.qty + @delta < 0 THEN 0
                                     ELSE collection.qty + @delta END,
                          note = COALESCE(NULLIF(excluded.note,''), collection.note),
                          updated_at = {now}
                    """;
                cmd.Bind("@user", userId);
                cmd.Bind("@id", cid);
                cmd.Bind("@seed", Math.Max(0, d));
                cmd.Bind("@note", note ?? "");
                cmd.Bind("@delta", d);
                cmd.ExecuteNonQuery();
            }

            using (var clean = conn.CreateCommand())
            {
                clean.CommandText =
                    "DELETE FROM collection WHERE user_id = @user AND card_id = @id AND qty <= 0";
                clean.Bind("@user", userId);
                clean.Bind("@id", cid);
                clean.ExecuteNonQuery();
            }

            using (var read = conn.CreateCommand())
            {
                read.CommandText =
                    "SELECT qty FROM collection WHERE user_id = @user AND card_id = @id";
                read.Bind("@user", userId);
                read.Bind("@id", cid);
                var value = read.ExecuteScalar();
                updated = value is null or DBNull ? 0 : Convert.ToInt32(value);
            }

            tx.Commit();
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
        using (var tx = conn.BeginTransaction())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO collection (user_id, card_id, qty, note)
                VALUES (@user,@id,@delta,@note)
                ON CONFLICT(user_id, card_id) DO UPDATE
                  SET qty = collection.qty + excluded.qty,
                      note = COALESCE(NULLIF(excluded.note,''), collection.note),
                      updated_at = {_db.Dialect.Now}
                """;
            cmd.Bind("@user", userId);
            cmd.Bind("@note", note ?? "");
            var idParam = cmd.Bind("@id", null);
            var deltaParam = cmd.Bind("@delta", null);

            // In a fixed order. PostgreSQL locks each row as it is written, so two
            // bulk logs sharing cards but listing them differently would otherwise
            // each hold a row the other is waiting for.
            foreach (var cid in clean.Keys.Order(StringComparer.Ordinal))
            {
                idParam.Value = cid;
                deltaParam.Value = clean[cid];
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
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
            read.Bind("@user", userId);
            read.Bind("@id", cid);
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
        cmd.Bind("@user", userId);
        cmd.ExecuteNonQuery();
    }

    public long CatalogCount()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) c FROM catalog";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public void LogScan(long userId, string? cardId, string result)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO scan_log (user_id, card_id, result) VALUES (@user,@id,@result)";
        cmd.Bind("@user", userId);
        cmd.Bind("@id", (object?)cardId ?? DBNull.Value);
        cmd.Bind("@result", result);
        cmd.ExecuteNonQuery();
    }
}
