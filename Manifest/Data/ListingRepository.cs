using System.Data.Common;
using System.Globalization;
using Manifest.Models;
using Manifest.Services;

namespace Manifest.Data;

/// <summary>Raised when a listing cannot be saved as asked; the message is for the person.</summary>
public sealed class ListingError(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>A listing's fields as the seller last wrote them. Saving one replaces them all.</summary>
public sealed record ListingInput(
    string? CardId, string? Title, string? Description, string? Condition, int? Qty, string? Status,
    double? PriceGbp, double? CostGbp, double? SoldPriceGbp, double? FeesGbp, double? PostageGbp,
    string? EbayUrl, string? Note);

/// <summary>
/// Cards an account is selling on eBay. The listing itself is made by hand on eBay
/// from the words <see cref="ListingWriter"/> drafts; this keeps track of each one -
/// draft, listed, sold or ended unsold - and of what the sales came to, so the
/// seller can see what sells, for how much and how quickly.
///
/// Like decks, a listing belongs to one account and every query filters on it, so
/// someone else's listing answers as one that does not exist.
/// </summary>
public sealed class ListingRepository(Database db)
{
    public static readonly string[] Statuses = { "draft", "listed", "sold", "ended" };

    public const int MaxDescription = 20_000;

    /// <summary>The listing words for a card, before anything is saved.</summary>
    public ListingDraft Draft(string rawCardId, string? condition, string? grade, int qty)
    {
        using var conn = db.Open();
        var card = Card(conn, rawCardId);
        var english = ListingWriter.IsJapanese(card) ? EnglishSetName(conn, card.SetLabel) : null;
        return ListingWriter.Write(card, english, condition, grade, qty, Market(conn, card.CardId),
                                   CardRepository.SourcePrices(conn, card.CardId));
    }

    /// <summary>
    /// A set's name from the English catalogue. Japanese and English printings of one
    /// number are not numbered alike - the English _p1 can be a different art to the
    /// Japanese _p1 - so the set is matched on its code, not on the printing.
    /// </summary>
    static string? EnglishSetName(DbConnection conn, string? setLabel)
    {
        if (string.IsNullOrEmpty(setLabel)) return null;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT set_name FROM catalog
            WHERE set_label = @set AND set_name IS NOT NULL AND card_id NOT LIKE '%{JapanesePrints.Tag}'
            ORDER BY card_id LIMIT 1
            """;
        cmd.Bind("@set", setLabel);
        return cmd.ExecuteScalar() as string;
    }

    public Listing Create(long userId, ListingInput input)
    {
        using var conn = db.Open();
        var card = Card(conn, input.CardId ?? "");
        var title = input.Title;
        if (string.IsNullOrWhiteSpace(title))
            title = Draft(card.CardId, input.Condition, null, input.Qty ?? 1).Title;
        var values = Check(input with { Title = title });

        long id;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO listings (user_id, card_id, title, description, condition, qty, status,
                                      price_gbp, cost_gbp, sold_price_gbp, fees_gbp, postage_gbp,
                                      ebay_url, note, listed_at, sold_at)
                VALUES (@user, @card, @title, @description, @condition, @qty, @status,
                        @price, @cost, @sold, @fees, @postage, @url, @note,
                        {(values.Status is "listed" or "sold" or "ended" ? db.Dialect.Now : "NULL")},
                        {(values.Status == "sold" ? db.Dialect.Now : "NULL")})
                RETURNING id
                """;
            cmd.Bind("@user", userId);
            cmd.Bind("@card", card.CardId);
            Bind(cmd, values);
            id = Convert.ToInt64(cmd.ExecuteScalar());
        }
        return Get(conn, userId, id)!;
    }

    /// <summary>
    /// Saves a listing's fields. Moving it to listed or sold stamps when that
    /// happened, the first time; moving a sold one back clears its sale date.
    /// </summary>
    public Listing Update(long userId, long id, ListingInput input)
    {
        var values = Check(input);
        using var conn = db.Open();
        using (var cmd = conn.CreateCommand())
        {
            var now = db.Dialect.Now;
            cmd.CommandText = $"""
                UPDATE listings SET
                    title = @title, description = @description, condition = @condition, qty = @qty,
                    status = @status, price_gbp = @price, cost_gbp = @cost, sold_price_gbp = @sold,
                    fees_gbp = @fees, postage_gbp = @postage, ebay_url = @url, note = @note,
                    listed_at = CASE WHEN @status = 'draft' THEN NULL
                                     ELSE COALESCE(listed_at, {now}) END,
                    sold_at = CASE WHEN @status = 'sold' THEN COALESCE(sold_at, {now}) ELSE NULL END,
                    updated_at = {now}
                WHERE id = @id AND user_id = @user
                """;
            cmd.Bind("@id", id);
            cmd.Bind("@user", userId);
            Bind(cmd, values);
            if (cmd.ExecuteNonQuery() == 0) throw new ListingError("not found", 404);
        }
        return Get(conn, userId, id)!;
    }

    public void Delete(long userId, long id)
    {
        using var conn = db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM listings WHERE id = @id AND user_id = @user";
        cmd.Bind("@id", id);
        cmd.Bind("@user", userId);
        if (cmd.ExecuteNonQuery() == 0) throw new ListingError("not found", 404);
    }

    /// <summary>Newest first.</summary>
    public List<Listing> List(long userId)
    {
        using var conn = db.Open();
        return Read(conn, "l.user_id = @user", cmd => cmd.Bind("@user", userId));
    }

    /// <summary>
    /// How selling has gone: what is up now, and what sold or ended in the last
    /// <paramref name="days"/> days - every one, when that is null.
    /// </summary>
    public (ListingStats Stats, List<TopSeller> Top) Summary(long userId, int? days)
    {
        var all = List(userId);
        var since = days is > 0 ? DateTime.UtcNow.AddDays(-days.Value) : DateTime.MinValue;
        bool Recent(string? stamp) => Stamp(stamp) is { } t && t >= since;

        var sold = all.Where(l => l.Status == "sold" && Recent(l.SoldAt)).ToList();
        var ended = all.Count(l => l.Status == "ended" && Recent(l.UpdatedAt));
        var stats = new ListingStats
        {
            Drafts = all.Count(l => l.Status == "draft"),
            Listed = all.Count(l => l.Status == "listed"),
            ListedValueGbp = Money(all.Where(l => l.Status == "listed").Sum(l => l.PriceGbp ?? 0)),
            Sold = sold.Count,
            SoldCards = sold.Sum(l => l.Qty),
            RevenueGbp = Money(sold.Sum(l => l.SoldPriceGbp ?? 0)),
            FeesGbp = Money(sold.Sum(l => (l.FeesGbp ?? 0) + (l.PostageGbp ?? 0))),
            ProfitGbp = Money(sold.Sum(l => l.ProfitGbp ?? 0)),
            MedianDaysToSell = Median(sold.Select(DaysToSell)),
            SellThrough = sold.Count + ended == 0 ? null : Math.Round((double)sold.Count / (sold.Count + ended), 3),
        };

        var top = sold
            .GroupBy(l => l.CardId)
            .Select(g => new TopSeller
            {
                CardId = g.Key,
                Name = g.First().Name,
                Sales = g.Count(),
                Cards = g.Sum(l => l.Qty),
                RevenueGbp = Money(g.Sum(l => l.SoldPriceGbp ?? 0)),
                AverageGbp = g.Any(l => l.SoldPriceGbp is not null)
                    ? Money(g.Where(l => l.SoldPriceGbp is not null).Sum(l => l.SoldPriceGbp!.Value)
                            / g.Where(l => l.SoldPriceGbp is not null).Sum(l => l.Qty))
                    : null,
                MedianDaysToSell = Median(g.Select(DaysToSell)),
            })
            .OrderByDescending(t => t.Cards)
            .ThenByDescending(t => t.RevenueGbp)
            .ThenBy(t => t.CardId, StringComparer.Ordinal)
            .Take(10)
            .ToList();
        return (stats, top);
    }

    static Listing? Get(DbConnection conn, long userId, long id) =>
        Read(conn, "l.id = @id AND l.user_id = @user", cmd =>
        {
            cmd.Bind("@id", id);
            cmd.Bind("@user", userId);
        }).SingleOrDefault();

    static List<Listing> Read(DbConnection conn, string where, Action<DbCommand> bind)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT l.*, c.name, c.set_label, c.rarity, c.variant, p.gbp AS market_gbp
            FROM listings l
            LEFT JOIN catalog c ON c.card_id = l.card_id
            LEFT JOIN prices p ON p.card_id = l.card_id
            WHERE {where}
            ORDER BY l.updated_at DESC, l.id DESC
            """;
        bind(cmd);
        var list = new List<Listing>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var status = r.Text("status");
            var soldPrice = r.Real("sold_price_gbp");
            var fees = r.Real("fees_gbp");
            var postage = r.Real("postage_gbp");
            var cost = r.Real("cost_gbp");
            list.Add(new Listing
            {
                Id = r.Long("id"),
                CardId = r.Text("card_id"),
                Name = r.Str("name"),
                SetLabel = r.Str("set_label"),
                Rarity = r.Str("rarity"),
                Variant = r.Str("variant"),
                Title = r.Text("title"),
                Description = r.Text("description"),
                Condition = r.Text("condition"),
                Qty = r.IntOr("qty", 1),
                Status = status,
                PriceGbp = r.Real("price_gbp"),
                CostGbp = cost,
                SoldPriceGbp = soldPrice,
                FeesGbp = fees,
                PostageGbp = postage,
                ProfitGbp = status == "sold" && soldPrice is { } s
                    ? Money(s - (fees ?? 0) - (postage ?? 0) - (cost ?? 0))
                    : null,
                MarketGbp = r.Real("market_gbp"),
                EbayUrl = r.Str("ebay_url"),
                Note = r.Text("note"),
                CreatedAt = r.Str("created_at"),
                ListedAt = r.Str("listed_at"),
                SoldAt = r.Str("sold_at"),
                UpdatedAt = r.Str("updated_at"),
            });
        }
        return list;
    }

    static CatalogRow Card(DbConnection conn, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new ListingError("card_id required");
        var cid = CardId.Normalise(raw) ?? raw.Trim().ToUpperInvariant();
        return CardRepository.Resolve(conn, cid)
               ?? throw new ListingError($"{cid} is not in the catalogue.", 404);
    }

    static double? Market(DbConnection conn, string cardId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT gbp FROM prices WHERE card_id = @id";
        cmd.Bind("@id", cardId);
        return cmd.ExecuteScalar() is { } v and not DBNull ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>The input, tidied, or the reason it cannot be saved.</summary>
    static ListingInput Check(ListingInput i)
    {
        var title = string.Join(' ', (i.Title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (title.Length == 0) throw new ListingError("Give the listing a title.");
        if (title.Length > ListingWriter.MaxTitle)
            throw new ListingError($"eBay titles can be at most {ListingWriter.MaxTitle} characters; this one is {title.Length}.");
        var description = (i.Description ?? "").Trim();
        if (description.Length > MaxDescription) throw new ListingError("That description is too long.");
        var status = string.IsNullOrEmpty(i.Status) ? "draft" : i.Status.Trim().ToLowerInvariant();
        if (!Statuses.Contains(status)) throw new ListingError("Status has to be draft, listed, sold or ended.");
        var qty = i.Qty ?? 1;
        if (qty is < 1 or > 999) throw new ListingError("Quantity has to be between 1 and 999.");
        var condition = (i.Condition ?? "").Trim();
        if (condition.Length == 0) condition = ListingWriter.Conditions[0];
        if (condition.Length > 40) condition = condition[..40];
        var url = (i.EbayUrl ?? "").Trim();
        if (url.Length > 0 && !(Uri.TryCreate(url, UriKind.Absolute, out var u)
                                && u.Scheme is "https" or "http"))
            throw new ListingError("The eBay link has to be a web address.");
        if (url.Length > 500) throw new ListingError("That eBay link is too long.");
        var note = (i.Note ?? "").Trim();
        if (note.Length > 1000) note = note[..1000];

        return i with
        {
            Title = title, Description = description, Status = status, Qty = qty, Condition = condition,
            EbayUrl = url.Length == 0 ? null : url, Note = note,
            PriceGbp = Amount(i.PriceGbp, "price"), CostGbp = Amount(i.CostGbp, "cost"),
            SoldPriceGbp = Amount(i.SoldPriceGbp, "sold price"), FeesGbp = Amount(i.FeesGbp, "fees"),
            PostageGbp = Amount(i.PostageGbp, "postage"),
        };
    }

    static double? Amount(double? v, string what)
    {
        if (v is not { } x) return null;
        if (double.IsNaN(x) || x < 0 || x > 100_000)
            throw new ListingError($"The {what} has to be between £0 and £100,000.");
        return Math.Round(x, 2);
    }

    static void Bind(DbCommand cmd, ListingInput v)
    {
        cmd.Bind("@title", v.Title);
        cmd.Bind("@description", v.Description);
        cmd.Bind("@condition", v.Condition);
        cmd.Bind("@qty", v.Qty);
        cmd.Bind("@status", v.Status);
        cmd.Bind("@price", v.PriceGbp);
        cmd.Bind("@cost", v.CostGbp);
        cmd.Bind("@sold", v.SoldPriceGbp);
        cmd.Bind("@fees", v.FeesGbp);
        cmd.Bind("@postage", v.PostageGbp);
        cmd.Bind("@url", v.EbayUrl);
        cmd.Bind("@note", v.Note);
    }

    static DateTime? Stamp(string? s) =>
        DateTime.TryParseExact(s, ReaderExtensions.StampFormat, CultureInfo.InvariantCulture,
                               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)
            ? t : null;

    static double? DaysToSell(Listing l) =>
        Stamp(l.ListedAt) is { } listed && Stamp(l.SoldAt) is { } sold
            ? Math.Max(0, (sold - listed).TotalDays) : null;

    static double? Median(IEnumerable<double?> values)
    {
        var v = values.Where(x => x is not null).Select(x => x!.Value).Order().ToList();
        if (v.Count == 0) return null;
        var mid = v.Count / 2;
        return Math.Round(v.Count % 2 == 1 ? v[mid] : (v[mid - 1] + v[mid]) / 2, 1);
    }

    static double Money(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
