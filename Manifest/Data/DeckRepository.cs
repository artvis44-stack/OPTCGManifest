using Manifest.Models;
using Manifest.Services;
using Microsoft.Data.Sqlite;

namespace Manifest.Data;

public sealed class DeckRepository : IDeckRepository
{
    readonly Database _db;
    public DeckRepository(Database db) => _db = db;

    /// <summary>
    /// Every method here takes the account as well as the deck id, and every query
    /// filters on both. A deck belonging to someone else is therefore not "forbidden"
    /// but simply absent - the caller gets the same 404 as for an id that never
    /// existed, which is also what stops deck ids being probed for who owns what.
    /// </summary>
    public List<DeckListItem> List(long userId)
    {
        using var conn = _db.Open();
        var decks = new List<(long Id, string Name, string? Created, string? Updated, string LeaderId)>();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT * FROM decks WHERE user_id = @user ORDER BY updated_at DESC";
            cmd.Parameters.AddWithValue("@user", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                decks.Add((r.Long("id"), r.Text("name"), r.Str("created_at"),
                           r.Str("updated_at"), r.Text("leader_card_id")));
        }

        var out_ = new List<DeckListItem>();
        foreach (var d in decks)
        {
            var leader = CardRepository.CatalogRowById(conn, d.LeaderId);
            using var count = conn.CreateCommand();
            count.CommandText = "SELECT COALESCE(SUM(qty),0) n FROM deck_cards WHERE deck_id = @id";
            count.Parameters.AddWithValue("@id", d.Id);
            var total = Convert.ToInt32(count.ExecuteScalar());

            out_.Add(new DeckListItem
            {
                Id = d.Id,
                Name = d.Name,
                CreatedAt = d.Created,
                UpdatedAt = d.Updated,
                Leader = DeckAnalysis.Summarise(leader),
                CardCount = total,
                SizeOk = total == AppConfig.DeckSize,
            });
        }
        return out_;
    }

    public DeckDetail? Detail(long userId, long deckId)
    {
        using var conn = _db.Open();
        return Detail(conn, userId, deckId);
    }

    static DeckDetail? Detail(SqliteConnection conn, long userId, long deckId)
    {
        string name, leaderId;
        string? created, updated;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM decks WHERE id = @id AND user_id = @user";
            cmd.Parameters.AddWithValue("@id", deckId);
            cmd.Parameters.AddWithValue("@user", userId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            name = r.Text("name");
            leaderId = r.Text("leader_card_id");
            created = r.Str("created_at");
            updated = r.Str("updated_at");
        }

        var leader = CardRepository.CatalogRowById(conn, leaderId);
        var leaderColors = leader is null
            ? new HashSet<string>()
            : DeckAnalysis.ColorsOf(leader.Colors);

        var cards = new List<DeckCardRow>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT dc.qty, c.card_id, c.base_id, c.variant, c.name, c.category,
                       c.colors, c.cost, c.power, c.counter, c.types, c.effect,
                       c.set_label, c.rarity, c.image_url,
                       COALESCE(k.qty, 0) AS owned_qty, p.gbp AS price_gbp
                FROM deck_cards dc
                JOIN catalog c ON c.card_id = dc.card_id
                LEFT JOIN collection k ON k.card_id = dc.card_id AND k.user_id = @user
                LEFT JOIN prices p ON p.card_id = dc.card_id
                WHERE dc.deck_id = @id
                ORDER BY c.base_id, c.variant
                """;
            cmd.Parameters.AddWithValue("@id", deckId);
            cmd.Parameters.AddWithValue("@user", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                cards.Add(new DeckCardRow
                {
                    Qty = r.IntOr("qty"),
                    CardId = r.Text("card_id"),
                    BaseId = r.Text("base_id"),
                    Variant = r.Text("variant"),
                    Name = r.Text("name"),
                    Category = r.Str("category"),
                    Colors = r.Str("colors"),
                    Cost = r.Int("cost"),
                    Power = r.Int("power"),
                    Counter = r.Int("counter"),
                    Types = r.Str("types"),
                    Effect = r.Str("effect"),
                    SetLabel = r.Str("set_label"),
                    Rarity = r.Str("rarity"),
                    ImageUrl = r.Str("image_url"),
                    OwnedQty = r.IntOr("owned_qty"),
                    PriceGbp = r.Real("price_gbp"),
                });
        }

        var total = cards.Sum(c => c.Qty);

        var byBase = new Dictionary<string, int>();
        foreach (var c in cards)
            byBase[c.BaseId] = byBase.GetValueOrDefault(c.BaseId) + c.Qty;
        var overLimit = byBase.Where(kv => kv.Value > AppConfig.MaxCopies)
                              .Select(kv => kv.Key)
                              .OrderBy(x => x, StringComparer.Ordinal)
                              .ToList();

        var offColor = cards
            .Where(c => leaderColors.Count > 0 && !DeckAnalysis.ColorsOf(c.Colors).Overlaps(leaderColors))
            .Select(c => c.CardId)
            .ToList();

        int CategoryQty(string category) => cards
            .Where(c => (c.Category ?? "").Equals(category, StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.Qty);

        var characters = CategoryQty("Character");
        var events = CategoryQty("Event");
        var stages = CategoryQty("Stage");
        var counters = cards.Where(c => c.Counter is not null).Sum(c => c.Qty);
        var blockers = cards
            .Where(c => !string.IsNullOrEmpty(c.Effect)
                        && c.Effect!.Contains("blocker", StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.Qty);

        var curve = new Dictionary<string, int>();
        foreach (var c in cards)
        {
            var key = c.Cost?.ToString() ?? "?";
            curve[key] = curve.GetValueOrDefault(key) + c.Qty;
        }

        int CostBand(int lo, int hi) => curve
            .Where(kv => kv.Key != "?" && int.Parse(kv.Key) >= lo && int.Parse(kv.Key) <= hi)
            .Sum(kv => kv.Value);

        var buyList = new List<BuyListItem>();
        var buyTotal = 0.0;
        foreach (var c in cards)
        {
            var short_ = c.Qty - c.OwnedQty;
            if (short_ <= 0) continue;
            var price = c.PriceGbp;
            var subtotal = price is not null
                ? Math.Round(price.Value * short_, 2, MidpointRounding.ToEven)
                : (double?)null;
            if (subtotal is not null) buyTotal += subtotal.Value;
            buyList.Add(new BuyListItem
            {
                CardId = c.CardId,
                Name = c.Name,
                SetLabel = c.SetLabel,
                Rarity = c.Rarity,
                Need = short_,
                Owned = c.OwnedQty,
                DeckQty = c.Qty,
                PriceGbp = price,
                SubtotalGbp = subtotal,
                ImageUrl = c.ImageUrl,
            });
        }

        return new DeckDetail
        {
            Id = deckId,
            Name = name,
            CreatedAt = created,
            UpdatedAt = updated,
            Leader = DeckAnalysis.Summarise(leader),
            Cards = cards,
            Total = total,
            DeckSizeTarget = AppConfig.DeckSize,
            Legal = new Legality
            {
                SizeOk = total == AppConfig.DeckSize,
                ShortBy = Math.Max(0, AppConfig.DeckSize - total),
                OverBy = Math.Max(0, total - AppConfig.DeckSize),
                OverLimit = overLimit,
                OffColor = offColor,
                Clean = total == AppConfig.DeckSize && overLimit.Count == 0 && offColor.Count == 0,
            },
            Guidelines = new Guidelines
            {
                Characters = DeckAnalysis.Guide(characters, DeckAnalysis.Characters),
                Events = DeckAnalysis.Guide(events, DeckAnalysis.Events),
                Stages = DeckAnalysis.Guide(stages, DeckAnalysis.Stages),
                Counters = DeckAnalysis.Guide(counters, DeckAnalysis.Counters),
                Blockers = DeckAnalysis.Guide(blockers, DeckAnalysis.Blockers),
                CostLow = DeckAnalysis.Guide(CostBand(0, 3), DeckAnalysis.CostLow),
                CostMid = DeckAnalysis.Guide(CostBand(4, 6), DeckAnalysis.CostMid),
                CostHigh = DeckAnalysis.Guide(CostBand(7, 99), DeckAnalysis.CostHigh),
            },
            Curve = curve,
            BuyList = buyList,
            BuyTotalGbp = Math.Round(buyTotal, 2, MidpointRounding.ToEven),
        };
    }

    public DeckDetail Create(long userId, string? name, string leaderCardId)
    {
        using var conn = _db.Open();
        var leader = CardRepository.CatalogRowById(conn, leaderCardId)
                     ?? throw new RuleViolation("unknown leader card");
        if (!DeckAnalysis.IsLeader(leader))
            throw new RuleViolation("that card isn't a Leader");

        var trimmed = (name ?? "New deck").Trim();
        if (trimmed.Length == 0) trimmed = "New deck";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO decks (user_id, name, leader_card_id) VALUES (@user, @name, @leader);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@user", userId);
        cmd.Parameters.AddWithValue("@name", trimmed);
        cmd.Parameters.AddWithValue("@leader", leader.CardId);
        var id = (long)cmd.ExecuteScalar()!;
        return Detail(conn, userId, id)!;
    }

    public DeckDetail? Update(long userId, long deckId, string? name, string? leaderCardId)
    {
        using var conn = _db.Open();
        using (var exists = conn.CreateCommand())
        {
            exists.CommandText = "SELECT 1 FROM decks WHERE id = @id AND user_id = @user";
            exists.Parameters.AddWithValue("@id", deckId);
            exists.Parameters.AddWithValue("@user", userId);
            if (exists.ExecuteScalar() is null) return null;
        }

        string? newLeaderId = null;
        if (!string.IsNullOrEmpty(leaderCardId))
        {
            var leader = CardRepository.CatalogRowById(conn, leaderCardId)
                         ?? throw new RuleViolation("unknown leader card");
            if (!DeckAnalysis.IsLeader(leader))
                throw new RuleViolation("that card isn't a Leader");
            newLeaderId = leader.CardId;
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE decks SET name = COALESCE(NULLIF(@name, ''), name),
                                 leader_card_id = COALESCE(@leader, leader_card_id),
                                 updated_at = datetime('now')
                WHERE id = @id AND user_id = @user
                """;
            cmd.Parameters.AddWithValue("@name", (name ?? "").Trim());
            cmd.Parameters.AddWithValue("@leader", (object?)newLeaderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", deckId);
            cmd.Parameters.AddWithValue("@user", userId);
            cmd.ExecuteNonQuery();
        }

        return Detail(conn, userId, deckId);
    }

    public void Delete(long userId, long deckId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        // deck_cards carries no owner of its own, so the delete is qualified by the
        // parent deck's owner rather than trusting the id it was handed.
        cmd.CommandText = """
            DELETE FROM deck_cards WHERE deck_id IN
                (SELECT id FROM decks WHERE id = @id AND user_id = @user);
            DELETE FROM decks WHERE id = @id AND user_id = @user;
            """;
        cmd.Parameters.AddWithValue("@id", deckId);
        cmd.Parameters.AddWithValue("@user", userId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// qty &lt;= 0 removes the card. Atomic so two devices editing the same deck (or a
    /// fast double-tap on +/-) can't drop each other's write.
    /// </summary>
    public DeckDetail? SetCard(long userId, long deckId, string cardId, int? qty)
    {
        using var conn = _db.Open();
        var card = CardRepository.CatalogRowById(conn, cardId)
                   ?? throw new RuleViolation("unknown card");
        if (DeckAnalysis.IsLeader(card))
            throw new RuleViolation("leaders go in the leader slot, not the card list");

        CardRepository.Exec(conn, "BEGIN IMMEDIATE");
        try
        {
            using (var exists = conn.CreateCommand())
            {
                exists.CommandText = "SELECT 1 FROM decks WHERE id = @id AND user_id = @user";
                exists.Parameters.AddWithValue("@id", deckId);
                exists.Parameters.AddWithValue("@user", userId);
                if (exists.ExecuteScalar() is null)
                {
                    CardRepository.Exec(conn, "ROLLBACK");
                    return null;
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                if (qty is null || qty.Value <= 0)
                {
                    cmd.CommandText =
                        "DELETE FROM deck_cards WHERE deck_id = @deck AND card_id = @card";
                }
                else
                {
                    cmd.CommandText = """
                        INSERT INTO deck_cards (deck_id, card_id, qty) VALUES (@deck,@card,@qty)
                        ON CONFLICT(deck_id, card_id) DO UPDATE SET qty = excluded.qty
                        """;
                    cmd.Parameters.AddWithValue("@qty", qty.Value);
                }
                cmd.Parameters.AddWithValue("@deck", deckId);
                cmd.Parameters.AddWithValue("@card", card.CardId);
                cmd.ExecuteNonQuery();
            }

            using (var touch = conn.CreateCommand())
            {
                touch.CommandText =
                    "UPDATE decks SET updated_at = datetime('now') WHERE id = @id";
                touch.Parameters.AddWithValue("@id", deckId);
                touch.ExecuteNonQuery();
            }

            CardRepository.Exec(conn, "COMMIT");
        }
        catch
        {
            CardRepository.Exec(conn, "ROLLBACK");
            throw;
        }

        return Detail(conn, userId, deckId);
    }

    public DeckDetail? SetCards(long userId, long deckId, IReadOnlyList<CardQuantity> cards)
    {
        using var conn = _db.Open();
        CardRepository.Exec(conn, "BEGIN IMMEDIATE");
        try
        {
            string leaderId;
            using (var exists = conn.CreateCommand())
            {
                exists.CommandText = "SELECT leader_card_id FROM decks WHERE id = @id AND user_id = @user";
                exists.Parameters.AddWithValue("@id", deckId);
                exists.Parameters.AddWithValue("@user", userId);
                var got = exists.ExecuteScalar();
                if (got is null)
                {
                    CardRepository.Exec(conn, "ROLLBACK");
                    return null;
                }
                leaderId = Convert.ToString(got) ?? "";
            }

            string? importedLeader = null;
            var clean = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in cards)
            {
                var cid = CardId.Normalise(entry.CardId) ?? entry.CardId.ToUpperInvariant().Trim();
                if (cid.Length == 0 || entry.Qty <= 0) continue;

                var card = CardRepository.CatalogRowById(conn, cid)
                           ?? throw new RuleViolation($"unknown card: {cid}");

                if (DeckAnalysis.IsLeader(card))
                {
                    if (importedLeader is not null && importedLeader != card.CardId)
                        throw new RuleViolation("a deck can only have one Leader");
                    importedLeader = card.CardId;
                    continue;
                }

                clean[card.CardId] = clean.GetValueOrDefault(card.CardId) + entry.Qty;
            }

            using (var wipe = conn.CreateCommand())
            {
                wipe.CommandText = "DELETE FROM deck_cards WHERE deck_id = @deck";
                wipe.Parameters.AddWithValue("@deck", deckId);
                wipe.ExecuteNonQuery();
            }

            using (var insert = conn.CreateCommand())
            {
                insert.CommandText =
                    "INSERT INTO deck_cards (deck_id, card_id, qty) VALUES (@deck,@card,@qty)";
                var deckParam = insert.Parameters.Add("@deck", SqliteType.Integer);
                var cardParam = insert.Parameters.Add("@card", SqliteType.Text);
                var qtyParam = insert.Parameters.Add("@qty", SqliteType.Integer);
                deckParam.Value = deckId;

                foreach (var (cid, qty) in clean)
                {
                    cardParam.Value = cid;
                    qtyParam.Value = qty;
                    insert.ExecuteNonQuery();
                }
            }

            using (var touch = conn.CreateCommand())
            {
                touch.CommandText = """
                    UPDATE decks
                    SET leader_card_id = COALESCE(@leader, leader_card_id),
                        updated_at = datetime('now')
                    WHERE id = @id
                    """;
                touch.Parameters.AddWithValue("@leader",
                    importedLeader is not null && importedLeader != leaderId
                        ? importedLeader
                        : DBNull.Value);
                touch.Parameters.AddWithValue("@id", deckId);
                touch.ExecuteNonQuery();
            }

            CardRepository.Exec(conn, "COMMIT");
        }
        catch
        {
            CardRepository.Exec(conn, "ROLLBACK");
            throw;
        }

        return Detail(conn, userId, deckId);
    }
}
