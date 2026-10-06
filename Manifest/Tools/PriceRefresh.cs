using System.Text.Json;
using System.Text.RegularExpressions;
using Manifest.Data;

namespace Manifest.Tools;

/// <summary>
/// Pull market prices from optcgapi.com (free, unofficial, refreshed daily there) and
/// store them in GBP, converted at the current USD/GBP rate.
///
/// Safe to re-run anytime — run it on a schedule to keep prices current.
/// </summary>
public static partial class PriceRefresh
{
    /// <summary>
    /// Where prices come from, most trusted first. A card number's plain id is shared
    /// between its own set, the starter decks that reprint it and the promos that
    /// reuse it, so the order decides who gets first claim on it - see Match().
    /// </summary>
    static readonly (string Source, string[] Urls)[] CardSources =
    {
        ("set", new[] { "https://optcgapi.com/api/allSetCards/" }),
        ("deck", new[] { "https://optcgapi.com/api/allSTCards/" }),
        // The documentation names allPromoCards; other clients report it answering
        // 404 while allPromos works, so the second is tried when the first fails.
        ("promo", new[] { "https://optcgapi.com/api/allPromoCards/",
                          "https://optcgapi.com/api/allPromos/" }),
    };
    const string FxUrl = "https://api.frankfurter.dev/v1/latest?base=USD&symbols=GBP";

    sealed class FxResponse
    {
        public Dictionary<string, double> Rates { get; set; } = new();
    }

    /// <summary>One priced row from optcgapi.com, as far as matching needs it.</summary>
    public sealed record ApiCard(string Source, string ImageId, string? SetId, string? Name, double Usd);

    /// <summary>One printing in the local catalogue.</summary>
    public sealed record CatalogPrint(string CardId, string BaseId, string? SetLabel);

    static readonly JsonSerializerOptions Upstream = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [GeneratedRegex(@"(?:OP|ST|EB|PRB)-?\d{2}")]
    private static partial Regex SetCode();

    /// <summary>"OP-03", "OP03", "OP14-EB04" -> {"OP03"}, {"OP03"}, {"OP14","EB04"}.</summary>
    static HashSet<string> SetCodes(string? set) =>
        SetCode().Matches((set ?? "").ToUpperInvariant())
                 .Select(m => m.Value.Replace("-", "")).ToHashSet();

    static bool IsPromoLabel(string? label) =>
        (label ?? "").Contains("promo", StringComparison.OrdinalIgnoreCase);

    /// <summary>"op01-016_P1 " -> "OP01-016_p1": the catalogue's own spelling.</summary>
    public static string CanonicalId(string raw)
    {
        var parts = raw.Trim().Split('_', 2);
        return parts.Length == 1 ? parts[0].ToUpperInvariant()
            : parts[0].ToUpperInvariant() + "_" + parts[1].ToLowerInvariant();
    }

    /// <summary>
    /// A row read field by field rather than deserialised whole, so one odd value
    /// (a price sent as text, or as "") costs that card and not the whole source.
    /// market_price is preferred; inventory_price stands in when it is missing.
    /// </summary>
    public static ApiCard? Parse(JsonElement e, string source)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        string? Text(string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Price(string name)
        {
            if (!e.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d > 0 ? d : null;
            if (v.ValueKind == JsonValueKind.String
                && double.TryParse(v.GetString(), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var t))
                return t > 0 ? t : null;
            return null;
        }

        var id = Text("card_image_id");
        if (string.IsNullOrWhiteSpace(id)) return null;
        var usd = Price("market_price") ?? Price("inventory_price");
        if (usd is null) return null;
        // set_name too: a set code may only be spelled out there ("... [OP-03]").
        var set = string.Join(" ", new[] { Text("set_id"), Text("set_name") }.Where(t => t is not null));
        return new ApiCard(source, CanonicalId(id), set.Length > 0 ? set : null, Text("card_name"),
                           usd.Value);
    }

    /// <summary>
    /// Which catalogue printing each optcgapi.com price belongs to.
    ///
    /// optcgapi.com's card_image_id is not unique. A starter deck's reprint of
    /// OP03-003 and a promo reusing OP14-033 both carry the plain number of the set
    /// card, and so does a variant like "Kingdew (Pandaman Art)". Read straight into
    /// a dictionary, whichever came last overwrote the set card's price, and the
    /// catalogue's own rows for those printings (OP03-003_r1 in ST-15, the promo's
    /// _pN) never got one. So:
    ///
    ///   1. An id with a suffix (_p1, _r1) is the official site's own id for one
    ///      printing - take it as is. A plain id is taken only when the set agrees.
    ///   2. Otherwise, the one unpriced printing of that number in that set (for a
    ///      promo, the one printing labelled as a promotion card), if exactly one.
    ///   3. Otherwise a plain id still lands on its own number when neither side says
    ///      which set it is in, so a missing set_id loses nothing against before.
    ///
    /// Sources are taken set, then starter deck, then promo; within one, a plain
    /// name before a name with a "(...)" variant note. Each printing is priced once.
    /// Ids not in the catalogue keep their price under their own id, for cards
    /// logged by number before the catalogue knew them.
    /// </summary>
    public static Dictionary<string, double> Match(IReadOnlyList<ApiCard> api,
                                                   IReadOnlyList<CatalogPrint> catalog)
    {
        var byId = catalog.ToDictionary(c => c.CardId, StringComparer.Ordinal);
        var byBase = catalog.GroupBy(c => c.BaseId).ToDictionary(g => g.Key, g => g.ToList());
        var rank = new Dictionary<string, int> { ["set"] = 0, ["deck"] = 1, ["promo"] = 2 };
        var ordered = api
            .Select((card, i) => (card, i))
            .OrderBy(x => rank.GetValueOrDefault(x.card.Source, 3))
            .ThenBy(x => (x.card.Name ?? "").Contains('(') ? 1 : 0)
            .ThenBy(x => x.i)
            .Select(x => x.card)
            .ToList();

        var priced = new Dictionary<string, double>(StringComparer.Ordinal);
        var used = new HashSet<ApiCard>(ReferenceEqualityComparer.Instance);

        bool SetAgrees(ApiCard a, CatalogPrint c)
        {
            if (a.Source == "promo" && IsPromoLabel(c.SetLabel)) return true;
            return SetCodes(a.SetId).Overlaps(SetCodes(c.SetLabel));
        }

        void Take(ApiCard a, CatalogPrint c)
        {
            priced[c.CardId] = a.Usd;
            used.Add(a);
        }

        foreach (var a in ordered)
        {
            if (!byId.TryGetValue(a.ImageId, out var c) || priced.ContainsKey(c.CardId)) continue;
            if (a.ImageId.Contains('_') || SetAgrees(a, c)) Take(a, c);
        }

        foreach (var a in ordered.Where(a => !used.Contains(a)))
        {
            if (!byBase.TryGetValue(a.ImageId.Split('_')[0], out var family)) continue;
            var open = family.Where(c => !priced.ContainsKey(c.CardId) && SetAgrees(a, c)).ToList();
            if (open.Count == 1) Take(a, open[0]);
        }

        foreach (var a in ordered.Where(a => !used.Contains(a)))
        {
            if (!byId.TryGetValue(a.ImageId, out var c) || priced.ContainsKey(c.CardId)) continue;
            if (SetCodes(a.SetId).Count == 0 || SetCodes(c.SetLabel).Count == 0) Take(a, c);
        }

        foreach (var a in ordered.Where(a => !used.Contains(a)))
            if (!byId.ContainsKey(a.ImageId)) priced.TryAdd(a.ImageId, a.Usd);

        return priced;
    }

    public static async Task<int> Run(AppPaths paths, Database db)
    {
        if (db.SqliteFile is { } file && !File.Exists(file))
        {
            Console.Error.WriteLine("manifest.db not found. Run the server once first to create it.");
            return 1;
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Add(
            "User-Agent", "Manifest (self-hosted collection tracker)");

        double rate;
        try
        {
            var fx = JsonSerializer.Deserialize<FxResponse>(
                await http.GetStringAsync(FxUrl), Upstream)!;
            rate = fx.Rates["GBP"];
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"could not fetch the USD/GBP rate: {e.Message}");
            return 1;
        }
        Console.WriteLine($"USD/GBP rate: {rate}");

        var api = new List<ApiCard>();
        foreach (var (source, urls) in CardSources)
        {
            foreach (var url in urls)
            {
                try
                {
                    using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                        throw new FormatException("not a list of cards");
                    var before = api.Count;
                    foreach (var e in doc.RootElement.EnumerateArray())
                        if (Parse(e, source) is { } card) api.Add(card);
                    Console.WriteLine($"  {url}: {api.Count - before} priced cards");
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"  skipped {url}: {e.Message}");
                }
            }
        }

        if (api.Count == 0)
        {
            Console.Error.WriteLine(
                "got no price data — optcgapi.com may be down or have changed shape");
            return 1;
        }

        using var conn = db.Open();
        var catalog = new List<CatalogPrint>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT card_id, base_id, set_label FROM catalog";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                catalog.Add(new CatalogPrint(r.GetString(0), r.GetString(1),
                                             r.IsDBNull(2) ? null : r.GetString(2)));
        }

        var rows = Match(api, catalog).ToDictionary(
            kv => kv.Key,
            kv => (Id: kv.Key, Usd: kv.Value,
                   Gbp: Math.Round(kv.Value * rate, 2, MidpointRounding.ToEven)));

        // An old SQLite file may predate the prices table. PostgreSQL's comes from
        // the migrations, like every other table there.
        if (!db.Dialect.IsPostgres)
            conn.Exec("""
                CREATE TABLE IF NOT EXISTS prices (
                    card_id    TEXT PRIMARY KEY,
                    usd        REAL,
                    gbp        REAL,
                    fetched_at TEXT NOT NULL DEFAULT (datetime('now'))
                )
                """);

        using (var tx = conn.BeginTransaction())
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO prices (card_id, usd, gbp, fetched_at)
                VALUES (@id,@usd,@gbp,{db.Dialect.Now})
                ON CONFLICT(card_id) DO UPDATE
                  SET usd = excluded.usd, gbp = excluded.gbp,
                      fetched_at = excluded.fetched_at
                """;
            var id = cmd.Bind("@id", null);
            var usd = cmd.Bind("@usd", null);
            var gbp = cmd.Bind("@gbp", null);
            foreach (var (cardId, u, g) in rows.Values)
            {
                id.Value = cardId; usd.Value = u; gbp.Value = g;
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        var known = catalog.Select(c => c.CardId).ToHashSet();
        var matched = rows.Keys.Count(known.Contains);
        Console.WriteLine($"priced {rows.Count} printings ({matched} of the {known.Count} "
                          + "in your catalog)");
        return 0;
    }
}
