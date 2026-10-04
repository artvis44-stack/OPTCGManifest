using System.Text.Json;

namespace Manifest.Tests;

[Collection("server")]
public class DeckTests(ServerFixture server)
{
    // Leader is Red/Green, life 4 (the catalogue stores a Leader's Life in the same
    // "cost" column Character/Event/Stage use for DON cost - see LeaderSummary).
    const string Leader = "EB01-001";

    async Task<(long Id, JsonElement Deck)> NewDeck(string name = "Test Deck")
    {
        var (status, d) = await server.Post("/api/decks",
                                            new { name, leader_card_id = Leader });
        Assert.Equal(200, status);
        var deck = d.GetProperty("deck");
        return (deck.GetProperty("id").GetInt64(), deck);
    }

    async Task<JsonElement> SetCard(long deckId, string cardId, int qty)
    {
        var (status, d) = await server.Post($"/api/decks/{deckId}/card",
                                            new { card_id = cardId, qty });
        Assert.Equal(200, status);
        return d.GetProperty("deck");
    }

    static int Guideline(JsonElement deck, string name) =>
        deck.GetProperty("guidelines").GetProperty(name).GetProperty("count").GetInt32();

    static string[] Strings(JsonElement array) =>
        array.EnumerateArray().Select(x => x.GetString()!).ToArray();

    [Fact]
    public async Task CreatingADeckSetsTheLeader()
    {
        var (id, deck) = await NewDeck();
        try
        {
            var leader = deck.GetProperty("leader");
            Assert.Equal(Leader, leader.GetProperty("card_id").GetString());
            Assert.Equal(4, leader.GetProperty("life").GetInt32());     // from the cost column
            Assert.Equal("Red, Green", leader.GetProperty("colors").GetString());

            Assert.False(deck.GetProperty("legal").GetProperty("size_ok").GetBoolean());
            Assert.Equal(50, deck.GetProperty("legal").GetProperty("short_by").GetInt32());

            var listing = (await server.Get("/api/decks")).GetProperty("decks");
            Assert.Contains(listing.EnumerateArray(), x => x.GetProperty("id").GetInt64() == id);
        }
        finally
        {
            await server.Post($"/api/decks/{id}/delete", new { });
        }
    }

    [Fact]
    public async Task CopyLimitCountsParallelsWithTheBasePrinting()
    {
        var (id, _) = await NewDeck();
        try
        {
            // OP01-004 is a Red 2-cost 2000-counter character; adding it and its alt
            // art (same base_id, different card_id) past 4 total should trip the
            // "max 4 copies of the same card number" rule.
            var deck = await SetCard(id, "OP01-004", 4);
            Assert.Equal(4, Guideline(deck, "characters"));
            Assert.Equal(4, deck.GetProperty("curve").GetProperty("2").GetInt32());
            Assert.Empty(deck.GetProperty("legal").GetProperty("over_limit").EnumerateArray());

            deck = await SetCard(id, "OP01-004_p1", 1);
            Assert.Equal(new[] { "OP01-004" },
                         Strings(deck.GetProperty("legal").GetProperty("over_limit")));
        }
        finally
        {
            await server.Post($"/api/decks/{id}/delete", new { });
        }
    }

    [Fact]
    public async Task OffColourCardIsFlaggedNotRejected()
    {
        var (id, _) = await NewDeck();
        try
        {
            await SetCard(id, "OP01-004", 4);
            await SetCard(id, "OP01-004_p1", 1);
            // EB01-022 is Blue-only - off colour for a Red/Green leader.
            var deck = await SetCard(id, "EB01-022", 2);
            Assert.Equal(new[] { "EB01-022" },
                         Strings(deck.GetProperty("legal").GetProperty("off_color")));
            Assert.Equal(7, deck.GetProperty("total").GetInt32());   // still counts

            deck = await SetCard(id, "EB01-022", 0);                 // qty 0 removes it
            Assert.DoesNotContain(deck.GetProperty("cards").EnumerateArray(),
                                  c => c.GetProperty("card_id").GetString() == "EB01-022");
            Assert.Empty(deck.GetProperty("legal").GetProperty("off_color").EnumerateArray());
            Assert.Equal(5, deck.GetProperty("total").GetInt32());
        }
        finally
        {
            await server.Post($"/api/decks/{id}/delete", new { });
        }
    }

    [Fact]
    public async Task CompositionCountsAndBuyList()
    {
        await server.Reset();
        await server.Log("EB01-006", 3);        // owned, so the buy list has a shortfall

        var (id, _) = await NewDeck();
        try
        {
            await SetCard(id, "OP01-004", 4);
            await SetCard(id, "OP01-004_p1", 1);
            await SetCard(id, "EB01-022", 2);
            await SetCard(id, "EB01-009", 4);
            var deck = await SetCard(id, "EB01-006", 4);

            Assert.Equal(4, Guideline(deck, "events"));
            Assert.Equal(4, Guideline(deck, "blockers"));   // only EB01-006 has [Blocker]
            Assert.Equal(11, Guideline(deck, "counters"));  // sums every card with a counter
            Assert.Equal(15, deck.GetProperty("total").GetInt32());
            Assert.False(deck.GetProperty("legal").GetProperty("size_ok").GetBoolean());

            var byId = deck.GetProperty("cards").EnumerateArray()
                           .ToDictionary(c => c.GetProperty("card_id").GetString()!);
            Assert.Equal(3, byId["EB01-006"].GetProperty("owned_qty").GetInt32());

            var buy = deck.GetProperty("buy_list").EnumerateArray()
                          .ToDictionary(b => b.GetProperty("card_id").GetString()!);
            Assert.Equal(4, buy["OP01-004"].GetProperty("need").GetInt32());  // fully unowned
            Assert.Equal(1, buy["EB01-006"].GetProperty("need").GetInt32());  // only shortfall
            Assert.Equal(4, buy["EB01-009"].GetProperty("need").GetInt32());
        }
        finally
        {
            await server.Post($"/api/decks/{id}/delete", new { });
        }
    }

    [Fact]
    public async Task BulkImportReplacesADeckFromPastedText()
    {
        var (id, _) = await NewDeck();
        try
        {
            await SetCard(id, "EB01-009", 4);
            var text = """
                Leader
                1 EB01-001

                Main Deck
                4x OP01-004
                OP01-004_p1 x1
                2 EB01-006
                """;

            var (status, d) = await server.Post($"/api/decks/{id}/cards", new { text });
            Assert.Equal(200, status);
            var deck = d.GetProperty("deck");
            Assert.Equal(7, deck.GetProperty("total").GetInt32());
            Assert.Equal(new[] { "OP01-004" },
                         Strings(deck.GetProperty("legal").GetProperty("over_limit")));

            var byId = deck.GetProperty("cards").EnumerateArray()
                           .ToDictionary(c => c.GetProperty("card_id").GetString()!);
            Assert.Equal(4, byId["OP01-004"].GetProperty("qty").GetInt32());
            Assert.Equal(1, byId["OP01-004_p1"].GetProperty("qty").GetInt32());
            Assert.Equal(2, byId["EB01-006"].GetProperty("qty").GetInt32());
            Assert.DoesNotContain("EB01-009", byId.Keys);
            Assert.DoesNotContain(Leader, byId.Keys);
        }
        finally
        {
            await server.Post($"/api/decks/{id}/delete", new { });
        }
    }

    [Fact]
    public async Task RenamingAndSwappingTheLeader()
    {
        var (id, _) = await NewDeck();
        try
        {
            await SetCard(id, "OP01-004", 4);

            var (_, d) = await server.Post($"/api/decks/{id}", new { name = "Renamed Deck" });
            Assert.Equal("Renamed Deck", d.GetProperty("deck").GetProperty("name").GetString());
            Assert.Equal(Leader,
                d.GetProperty("deck").GetProperty("leader").GetProperty("card_id").GetString());

            (_, d) = await server.Post($"/api/decks/{id}", new { leader_card_id = "OP01-060" });
            var deck = d.GetProperty("deck");
            Assert.Equal("Blue", deck.GetProperty("leader").GetProperty("colors").GetString());
            // every Red card is now off-colour under the new leader
            Assert.Equal(deck.GetProperty("cards").EnumerateArray()
                             .Select(c => c.GetProperty("card_id").GetString()!).Order().ToArray(),
                         Strings(deck.GetProperty("legal").GetProperty("off_color")).Order().ToArray());
        }
        finally
        {
            await server.Post($"/api/decks/{id}/delete", new { });
        }
    }

    [Fact]
    public async Task ANonLeaderCardCannotBeUsedAsALeader()
    {
        var (status, _) = await server.Post("/api/decks",
                                            new { name = "Bad", leader_card_id = "OP01-004" });
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task TheLeaderItselfCannotBeAddedAsADeckCard()
    {
        var (id, _) = await NewDeck();
        try
        {
            var (status, _) = await server.Post($"/api/decks/{id}/card",
                                                new { card_id = Leader, qty = 1 });
            Assert.Equal(400, status);
        }
        finally
        {
            await server.Post($"/api/decks/{id}/delete", new { });
        }
    }

    [Fact]
    public async Task DeckDeletes()
    {
        var (id, _) = await NewDeck();

        var (status, _) = await server.Post($"/api/decks/{id}/delete", new { });
        Assert.Equal(200, status);

        Assert.Equal(404, (await server.Raw($"/api/decks/{id}")).Status);

        var listing = (await server.Get("/api/decks")).GetProperty("decks");
        Assert.DoesNotContain(listing.EnumerateArray(), x => x.GetProperty("id").GetInt64() == id);
    }

    /// <summary>
    /// Some real manifest.db files predate the current catalogue and store category as
    /// "LEADER"/"CHARACTER" instead of "Leader"/"Character". Exercised here against a
    /// row edited in place rather than only against the clean catalog.json every other
    /// test in this file uses.
    /// </summary>
    [Fact]
    public async Task WorksAgainstAnUpperCasedCatalogueRow()
    {
        const string shouty = "EB01-021";
        SetCategory(shouty, "LEADER");
        try
        {
            var r = (await server.Get("/api/search?q=&category=Leader&limit=500"))
                    .GetProperty("results").EnumerateArray();
            Assert.Contains(r, x => x.GetProperty("card_id").GetString() == shouty);

            var (status, d) = await server.Post("/api/decks",
                new { name = "Case Test", leader_card_id = shouty });
            Assert.Equal(200, status);
            var id = d.GetProperty("deck").GetProperty("id").GetInt64();

            try
            {
                var (bad, _) = await server.Post($"/api/decks/{id}/card",
                    new { card_id = shouty, qty = 1 });
                Assert.Equal(400, bad);   // still can't be added as a deck card

                var deck = await SetCard(id, "OP01-004", 4);
                Assert.Equal(4, Guideline(deck, "characters"));
            }
            finally
            {
                await server.Post($"/api/decks/{id}/delete", new { });
            }
        }
        finally
        {
            SetCategory(shouty, "Leader");
        }
    }

    void SetCategory(string cardId, string category) =>
        server.ExecuteSql("UPDATE catalog SET category = @category WHERE card_id = @id",
                          ("@category", category), ("@id", cardId));
}
