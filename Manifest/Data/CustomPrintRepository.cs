using System.Data.Common;
using System.Text.RegularExpressions;
using Manifest.Models;
using Manifest.Services;

namespace Manifest.Data;

/// <summary>Raised when a print cannot be added or removed as asked; the message is for the person.</summary>
public sealed class CustomPrintError(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>What someone fills in to add a print: any printing of the card, and what is different.</summary>
public sealed record CustomPrintInput(string CardId, string Variant, string? SetLabel, string? Rarity,
                                      double? PriceGbp);

/// <summary>
/// Prints added by hand, because no source the app reads lists them: a promo that
/// came with a book, say. One is filed as a further printing of a card number -
/// EB02-003_c1 - and reads as that card, so binders, decks and search need nothing
/// of their own for it. Its catalog row is put back whenever the catalogue is
/// reseeded, and its price is a "manual" one the price refresh leaves alone.
///
/// A removed print stays in custom_prints, marked, so its id is never given to
/// another: card pictures are cached by id for a year.
/// </summary>
public sealed partial class CustomPrintRepository(Database db)
{
    public const string ManualSource = "manual";

    [GeneratedRegex(@"_c(\d+)$")]
    private static partial Regex Suffix();

    public static bool IsCustom(string cardId) => Suffix().IsMatch(cardId);

    /// <returns>The new print's card id.</returns>
    public string Create(long userId, CustomPrintInput input)
    {
        var variant = Clean(input.Variant, 60)
                      ?? throw new CustomPrintError("Give the print a name, like \"Book promo\".");
        var setLabel = Clean(input.SetLabel, 60);
        var rarity = Clean(input.Rarity, 20);
        if (input.PriceGbp is { } price && (double.IsNaN(price) || price < 0 || price > 100_000))
            throw new CustomPrintError("The price has to be between £0 and £100,000.");

        var raw = input.CardId ?? "";
        var from = CardId.Normalise(raw) ?? raw.Trim().ToUpperInvariant();
        using var conn = db.Open();
        var card = CardRepository.Resolve(conn, from)
                   ?? throw new CustomPrintError($"{from} is not in the catalogue.", 404);
        // The card text comes from the base printing where there is one.
        var basis = CardRepository.CatalogRowById(conn, card.BaseId) ?? card;

        // Two people adding a print of the same card at once would both be handed the
        // next number; whoever loses the insert takes the one after.
        for (var attempt = 0; ; attempt++)
        {
            using var tx = conn.BeginTransaction();
            db.Dialect.Lock(conn, "manifest:custom-print:" + card.BaseId);
            var id = NextId(conn, card.BaseId);
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = """
                        INSERT INTO custom_prints (card_id, base_id, name, variant, set_label, rarity, created_by)
                        VALUES (@id, @base, @name, @variant, @set, @rarity, @by)
                        """;
                    cmd.Bind("@id", id);
                    cmd.Bind("@base", card.BaseId);
                    cmd.Bind("@name", basis.Name);
                    cmd.Bind("@variant", variant);
                    cmd.Bind("@set", setLabel);
                    cmd.Bind("@rarity", rarity);
                    cmd.Bind("@by", userId);
                    cmd.ExecuteNonQuery();
                }
                Database.InsertCatalog(conn, new[] { Row(basis, id, card.BaseId, basis.Name, variant, setLabel, rarity) });
                if (input.PriceGbp is { } gbp) SetPrice(conn, id, Math.Round(gbp, 2));
                tx.Commit();
                return id;
            }
            catch (Exception e) when (attempt < 3 && SqlDialect.UniqueViolation(e) is not null)
            {
                tx.Rollback();
            }
        }
    }

    /// <summary>Takes a print back out, if it is the person's to remove and nothing holds it.</summary>
    public void Delete(User user, string rawCardId)
    {
        var cid = CardId.Normalise(rawCardId) ?? rawCardId.Trim().ToUpperInvariant();
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        long? createdBy;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT created_by FROM custom_prints WHERE card_id = @id AND deleted_at IS NULL";
            cmd.Bind("@id", cid);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new CustomPrintError($"{cid} is not a print added by hand.", 404);
            createdBy = r.LongOrNull("created_by");
        }
        if (!user.IsOwner && createdBy != user.Id)
            throw new CustomPrintError("Only whoever added this print, or the owner, can remove it.", 403);

        if (Count(conn, "SELECT COALESCE(SUM(qty), 0) FROM collection WHERE card_id = @id", cid) > 0)
            throw new CustomPrintError("Copies of it are logged in a binder. Take them out first.", 409);
        if (Count(conn, "SELECT COUNT(*) FROM deck_cards WHERE card_id = @id", cid)
            + Count(conn, "SELECT COUNT(*) FROM decks WHERE leader_card_id = @id", cid) > 0)
            throw new CustomPrintError("It is in a deck. Take it out of the deck first.", 409);

        foreach (var sql in new[]
                 {
                     $"UPDATE custom_prints SET deleted_at = {db.Dialect.Now} WHERE card_id = @id",
                     "DELETE FROM catalog WHERE card_id = @id",
                     "DELETE FROM card_prices WHERE card_id = @id",
                     "DELETE FROM prices WHERE card_id = @id",
                     "DELETE FROM card_images WHERE card_id = @id",
                 })
            Run(conn, sql, cid);
        tx.Commit();
    }

    /// <summary>
    /// The catalog rows for every print added by hand, given the rest of the catalogue
    /// being seeded, so each reads as its card does now.
    /// </summary>
    public static List<CatalogRow> Restore(DbConnection conn, IReadOnlyList<CatalogRow> catalogue)
    {
        var byId = new Dictionary<string, CatalogRow>(StringComparer.Ordinal);
        foreach (var row in catalogue) byId.TryAdd(row.CardId, row);
        var anyByBase = new Dictionary<string, CatalogRow>(StringComparer.Ordinal);
        foreach (var row in catalogue) anyByBase.TryAdd(row.BaseId, row);

        var rows = new List<CatalogRow>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT card_id, base_id, name, variant, set_label, rarity
            FROM custom_prints WHERE deleted_at IS NULL ORDER BY card_id
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var id = r.Text("card_id");
            if (byId.ContainsKey(id)) continue;   // a source has caught up and lists it itself
            var baseId = r.Text("base_id");
            var basis = byId.GetValueOrDefault(baseId) ?? anyByBase.GetValueOrDefault(baseId);
            rows.Add(Row(basis, id, baseId, r.Text("name"), r.Text("variant"),
                         r.Str("set_label"), r.Str("rarity")));
        }
        return rows;
    }

    static CatalogRow Row(CatalogRow? basis, string id, string baseId, string name, string variant,
                          string? setLabel, string? rarity) => new()
    {
        CardId = id,
        BaseId = baseId,
        Variant = variant,
        Name = basis?.Name ?? name,
        SetLabel = setLabel,
        SetName = setLabel,
        Rarity = rarity ?? basis?.Rarity,
        Category = basis?.Category,
        Colors = basis?.Colors,
        Cost = basis?.Cost,
        Power = basis?.Power,
        Counter = basis?.Counter,
        Types = basis?.Types,
        Attributes = basis?.Attributes,
        Effect = basis?.Effect,
        Trigger = basis?.Trigger,
        BlockIcon = basis?.BlockIcon,
        ImageUrl = null,   // the picture, if any, was put straight into the image store
    };

    static string NextId(DbConnection conn, string baseId)
    {
        var highest = 0;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT card_id FROM custom_prints WHERE base_id = @base";
        cmd.Bind("@base", baseId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (Suffix().Match(r.GetString(0)) is { Success: true } m)
                highest = Math.Max(highest, int.Parse(m.Groups[1].Value));
        return $"{baseId}_c{highest + 1}";
    }

    void SetPrice(DbConnection conn, string id, double gbp)
    {
        foreach (var sql in new[]
                 {
                     $"""
                     INSERT INTO card_prices (card_id, source, currency, amount, gbp, url, fetched_at)
                     VALUES (@id, '{ManualSource}', 'GBP', @gbp, @gbp, NULL, {db.Dialect.Now})
                     """,
                     $"""
                     INSERT INTO prices (card_id, usd, gbp, fetched_at)
                     VALUES (@id, NULL, @gbp, {db.Dialect.Now})
                     ON CONFLICT (card_id) DO UPDATE
                       SET usd = excluded.usd, gbp = excluded.gbp, fetched_at = excluded.fetched_at
                     """,
                 })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Bind("@id", id);
            cmd.Bind("@gbp", gbp);
            cmd.ExecuteNonQuery();
        }
    }

    static long Count(DbConnection conn, string sql, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Bind("@id", id);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    static void Run(DbConnection conn, string sql, string id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Bind("@id", id);
        cmd.ExecuteNonQuery();
    }

    static string? Clean(string? s, int max)
    {
        var t = string.Join(' ', (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length == 0 ? null : t.Length > max ? t[..max] : t;
    }
}
