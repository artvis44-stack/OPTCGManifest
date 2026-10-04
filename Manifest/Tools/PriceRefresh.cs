using System.Text.Json;
using System.Text.Json.Serialization;
using Manifest.Data;

namespace Manifest.Tools;

/// <summary>
/// Pull market prices from optcgapi.com (free, unofficial, refreshed daily there) and
/// store them in GBP, converted at the current USD/GBP rate.
///
/// Safe to re-run anytime — run it on a schedule to keep prices current.
/// </summary>
public static class PriceRefresh
{
    static readonly string[] CardSources =
    {
        "https://optcgapi.com/api/allSetCards/",
        "https://optcgapi.com/api/allSTCards/",
    };
    const string FxUrl = "https://api.frankfurter.dev/v1/latest?base=USD&symbols=GBP";

    sealed class FxResponse
    {
        public Dictionary<string, double> Rates { get; set; } = new();
    }

    sealed class PricedCard
    {
        [JsonPropertyName("card_image_id")] public string? CardImageId { get; set; }
        [JsonPropertyName("market_price")] public double? MarketPrice { get; set; }
    }

    static readonly JsonSerializerOptions Upstream = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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

        var rows = new Dictionary<string, (string Id, double Usd, double Gbp)>();
        foreach (var url in CardSources)
        {
            List<PricedCard> cards;
            try
            {
                cards = JsonSerializer.Deserialize<List<PricedCard>>(
                    await http.GetStringAsync(url), Upstream) ?? new();
            }
            catch (Exception e)
            {
                Console.WriteLine($"  skipped {url}: {e.Message}");
                continue;
            }
            foreach (var c in cards)
            {
                if (string.IsNullOrEmpty(c.CardImageId) || c.MarketPrice is null) continue;
                rows[c.CardImageId] = (c.CardImageId, c.MarketPrice.Value,
                                       Math.Round(c.MarketPrice.Value * rate, 2,
                                                  MidpointRounding.ToEven));
            }
        }

        if (rows.Count == 0)
        {
            Console.Error.WriteLine(
                "got no price data — optcgapi.com may be down or have changed shape");
            return 1;
        }

        using var conn = db.Open();
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

        var known = new HashSet<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT card_id FROM catalog";
            using var r = cmd.ExecuteReader();
            while (r.Read()) known.Add(r.GetString(0));
        }
        var matched = rows.Keys.Count(known.Contains);
        Console.WriteLine($"priced {rows.Count} printings ({matched} match cards in your catalog)");
        return 0;
    }
}
