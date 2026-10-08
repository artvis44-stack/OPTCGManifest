using System.Text.Json;
using System.Text.RegularExpressions;
using Manifest.Data;

namespace Manifest.Tools;

/// <summary>
/// Pull market prices from three places and store each, in its own currency and in
/// GBP: Cardmarket (EUR, as Limitless shows it), TCGplayer (USD, from tcgcsv.com) and
/// optcgapi.com (USD, free and unofficial). A card is shown at the first of those
/// that has a price for it - see <see cref="Preference"/>.
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
    const string FxUrl = "https://api.frankfurter.dev/v1/latest?base=USD&symbols=GBP,EUR";

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

    /// <summary>One source's price for one printing, as stored in card_prices.</summary>
    public sealed record SourcePrice(string CardId, string Source, string Currency, double Amount, string? Url);

    /// <summary>The order a card's shown price is picked in: the first source that has one.</summary>
    public static readonly string[] Preference = { "cardmarket", "tcgplayer", "optcgapi" };

    const string CardmarketBase = "https://www.cardmarket.com/en/OnePiece/Products/Singles/";

    /// <summary>
    /// Cardmarket (EUR) and TCGplayer (USD) prices for the printings in price-links.json:
    /// Cardmarket's from Limitless's product lists, TCGplayer's from tcgcsv.com - fresher
    /// than the copy Limitless shows, which stands in when tcgcsv.com has none.
    /// </summary>
    public static List<SourcePrice> FromLinks(IReadOnlyList<ExtraPrints.Link> links,
                                              IReadOnlyList<LimitlessPrints.Print>? limitless,
                                              IReadOnlyDictionary<long, double>? tcgplayer)
    {
        var byHref = (limitless ?? Array.Empty<LimitlessPrints.Print>()).GroupBy(p => p.Href).ToDictionary(g => g.Key, g => g.First());
        var prices = new List<SourcePrice>();
        foreach (var link in links)
        {
            var print = link.Limitless is { } href ? byHref.GetValueOrDefault(href) : null;
            if (print?.Eur is { } eur)
                prices.Add(new SourcePrice(link.CardId, "cardmarket", "EUR", eur,
                                           link.Cardmarket is { } cm ? CardmarketBase + cm : null));

            double? usd = link.Tcgplayer is { } id && tcgplayer?.TryGetValue(id, out var market) == true
                ? market : print?.Usd;
            if (usd is { } u)
                prices.Add(new SourcePrice(link.CardId, "tcgplayer", "USD", u,
                                           link.Tcgplayer is { } pid ? $"https://www.tcgplayer.com/product/{pid}" : null));
        }
        return prices;
    }

    /// <summary>
    /// The linked prices, less those for a printing whose TCGplayer price is more than
    /// three times off optcgapi.com's. optcgapi.com goes by the official site's numbers;
    /// a gap that size means Limitless filed some other art under this one's number,
    /// and its Cardmarket price is that other card's too.
    /// </summary>
    public static IEnumerable<SourcePrice> Plausible(IEnumerable<SourcePrice> linked,
                                                     IReadOnlyDictionary<string, double> optcgapi)
    {
        var list = linked.ToList();
        var doubtful = list
            .Where(p => p.Source == "tcgplayer" && optcgapi.TryGetValue(p.CardId, out var o)
                        && Math.Max(o, p.Amount) > 2 && Math.Max(o / p.Amount, p.Amount / o) > 3)
            .Select(p => p.CardId).ToHashSet(StringComparer.Ordinal);
        if (doubtful.Count > 0)
            Console.WriteLine($"  {doubtful.Count} printings' Cardmarket and TCGplayer prices left out: "
                              + "too far from optcgapi.com's to be the same card");
        return list.Where(p => !doubtful.Contains(p.CardId));
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

        double usdGbp, eurGbp;
        try
        {
            var fx = JsonSerializer.Deserialize<FxResponse>(
                await http.GetStringAsync(FxUrl), Upstream)!;
            usdGbp = fx.Rates["GBP"];
            eurGbp = fx.Rates["GBP"] / fx.Rates["EUR"];
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"could not fetch exchange rates: {e.Message}");
            return 1;
        }
        Console.WriteLine($"rates: USD/GBP {usdGbp}, EUR/GBP {eurGbp:0.#####}");

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
            Console.WriteLine("  got no prices from optcgapi.com — it may be down or have changed shape");

        // Cardmarket and TCGplayer, for what the last catalogue refresh tied to them.
        var links = await ExtraPrints.ReadLinks(paths);
        var linked = new List<SourcePrice>();
        if (links.Count == 0)
            Console.WriteLine($"  no {ExtraPrints.LinksFileName} yet, so no Cardmarket or TCGplayer prices: "
                              + "run `manifest scrape --limitless` first");
        else
        {
            var limitless = await LimitlessPrints.Crawl();
            var tcgplayer = await TcgCsv.Prices() is { } list ? TcgCsv.ByProduct(list) : null;
            linked = FromLinks(links, limitless, tcgplayer);
            Console.WriteLine($"  {linked.Count(p => p.Source == "cardmarket")} Cardmarket and "
                              + $"{linked.Count(p => p.Source == "tcgplayer")} TCGplayer prices");
        }

        if (api.Count == 0 && linked.Count == 0)
        {
            Console.Error.WriteLine("got no prices from anywhere; the ones stored are left as they were");
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

        var optcgapi = Match(api, catalog);
        var all = optcgapi
            .Select(kv => new SourcePrice(kv.Key, "optcgapi", "USD", kv.Value, null))
            .Concat(Plausible(linked, optcgapi))
            .ToList();
        double Gbp(SourcePrice p) =>
            Math.Round(p.Amount * (p.Currency == "EUR" ? eurGbp : usdGbp), 2, MidpointRounding.ToEven);

        // An old SQLite file may predate the price tables. PostgreSQL's come from
        // the migrations, like every other table there.
        if (!db.Dialect.IsPostgres)
            db.EnsureSchema();

        using (var tx = conn.BeginTransaction())
        {
            // A source that answered this time replaces what it said last time; one that
            // did not keeps its last prices rather than leaving cards with none.
            foreach (var source in all.Select(p => p.Source).Distinct())
            {
                using var clear = conn.CreateCommand();
                clear.CommandText = "DELETE FROM card_prices WHERE source = @source";
                clear.Bind("@source", source);
                clear.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"""
                    INSERT INTO card_prices (card_id, source, currency, amount, gbp, url, fetched_at)
                    VALUES (@id, @source, @currency, @amount, @gbp, @url, {db.Dialect.Now})
                    ON CONFLICT (card_id, source) DO NOTHING
                    """;
                var p = new[] { "@id", "@source", "@currency", "@amount", "@gbp", "@url" }
                    .Select(n => cmd.Bind(n, null)).ToArray();
                foreach (var price in all)
                {
                    p[0].Value = price.CardId; p[1].Value = price.Source; p[2].Value = price.Currency;
                    p[3].Value = price.Amount; p[4].Value = Gbp(price);
                    p[5].Value = (object?)price.Url ?? DBNull.Value;
                    cmd.ExecuteNonQuery();
                }
            }

            // The price a card is shown at: the first source in Preference that has one,
            // and the US dollar figure from TCGplayer where there is one.
            using (var cmd = conn.CreateCommand())
            {
                string Pick(string column) => "COALESCE(" + string.Join(", ", Preference.Select(s =>
                    $"MAX(CASE WHEN source = '{s}' THEN {column} END)")) + ")";
                cmd.CommandText = $"""
                    INSERT INTO prices (card_id, usd, gbp, fetched_at)
                    SELECT card_id,
                           COALESCE(MAX(CASE WHEN source = 'tcgplayer' THEN amount END),
                                    MAX(CASE WHEN source = 'optcgapi' THEN amount END)),
                           {Pick("gbp")}, {db.Dialect.Now}
                    FROM card_prices GROUP BY card_id
                    ON CONFLICT(card_id) DO UPDATE
                      SET usd = excluded.usd, gbp = excluded.gbp, fetched_at = excluded.fetched_at
                    """;
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        var known = catalog.Select(c => c.CardId).ToHashSet();
        foreach (var source in Preference)
        {
            var ids = all.Where(p => p.Source == source).Select(p => p.CardId).ToHashSet();
            Console.WriteLine($"  {source,-10} {ids.Count,6} printings ({ids.Count(known.Contains)} in your catalog)");
        }
        var priced = all.Select(p => p.CardId).Where(known.Contains).Distinct().Count();
        Console.WriteLine($"priced {priced} of the {known.Count} printings in your catalog");
        return 0;
    }
}
