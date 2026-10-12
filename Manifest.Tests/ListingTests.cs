using System.Net;
using System.Text;
using System.Text.Json;

namespace Manifest.Tests;

/// <summary>
/// eBay listings, through the running server: the words drafted from a card, and a
/// listing's life from draft to sale with what it made.
/// </summary>
[Collection("server")]
public class ListingTests(ServerFixture server)
{
    const string Password = "correct-horse-battery";

    async Task<HttpClient> Account(string name) =>
        await server.NewClientFor($"{name}{Guid.NewGuid():N}"[..20], Password);

    static async Task<(HttpStatusCode Status, JsonElement Body)> Post(HttpClient client, string path,
                                                                     object payload)
    {
        var json = JsonSerializer.Serialize(payload, Manifest.Json.Options);
        var response = await client.PostAsync(path, new StringContent(json, Encoding.UTF8,
                                                                      "application/json"));
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length > 0 ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }

    static async Task<(HttpStatusCode Status, JsonElement Body)> Get(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    static async Task<JsonElement> Draft(HttpClient client, string query)
    {
        var (status, body) = await Get(client, "/api/listings/draft?" + query);
        Assert.Equal(HttpStatusCode.OK, status);
        return body.GetProperty("draft");
    }

    static string Specific(JsonElement draft, string name) =>
        draft.GetProperty("specifics").EnumerateArray()
            .Single(s => s.GetProperty("name").GetString() == name).GetProperty("value").GetString()!;

    [Fact]
    public async Task ADraftTitleCarriesWhatBuyersSearchForInEightyCharacters()
    {
        var me = await Account("seller");
        var draft = await Draft(me, "card_id=op01-120_p1&condition=Lightly%20Played");

        var title = draft.GetProperty("title").GetString()!;
        Assert.True(title.Length <= 80, title);
        Assert.StartsWith("One Piece", title);
        foreach (var term in new[] { "Shanks", "OP01-120", "SEC", "Alt Art", "English", "LP" })
            Assert.Contains(term, title);
        // The card number already says which set; the set code would only spend characters.
        Assert.DoesNotContain(" OP01 ", title);

        var description = draft.GetProperty("description").GetString()!;
        Assert.Contains("Rarity: Secret Rare (SEC)", description);
        Assert.Contains("Set: OP01 - Romance Dawn", description);
        Assert.Contains("Lightly Played", description);

        Assert.Equal("OP01-120", Specific(draft, "Card Number"));
        Assert.Equal("Alternate Art", Specific(draft, "Features"));
        Assert.Equal("Lightly played (Excellent)", Specific(draft, "Card Condition"));
        Assert.Contains("LH_Sold=1", draft.GetProperty("sold_search_url").GetString());
    }

    [Fact]
    public async Task AGradedCardIsTitledByItsGradeAndSeveralCopiesByTheirCount()
    {
        var me = await Account("grader");
        var draft = await Draft(me, "card_id=OP01-120_p1&grade=PSA%2010&qty=2");
        var title = draft.GetProperty("title").GetString()!;
        Assert.True(title.Length <= 80, title);
        Assert.StartsWith("2x ", title);
        Assert.Contains("PSA 10", title);
        Assert.DoesNotContain(" NM", title);
        Assert.Equal("PSA", Specific(draft, "Professional Grader"));
        Assert.Equal("10", Specific(draft, "Grade"));
    }

    [Fact]
    public async Task ACardNotInTheCatalogueCannotBeDrafted()
    {
        var me = await Account("nobody");
        var (status, _) = await Get(me, "/api/listings/draft?card_id=ZZ99-999");
        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task AListingGoesFromDraftToSoldAndCountsWhatItMade()
    {
        var me = await Account("trader");
        var (status, body) = await Post(me, "/api/listings", new
        {
            card_id = "OP01-120_p1", title = "", condition = "Near Mint", qty = 1, price_gbp = 120, cost_gbp = 30,
        });
        Assert.Equal(HttpStatusCode.OK, status);
        var listing = body.GetProperty("listing");
        var id = listing.GetProperty("id").GetInt64();
        Assert.Equal("draft", listing.GetProperty("status").GetString());
        Assert.Contains("Shanks", listing.GetProperty("title").GetString());   // drafted when left empty
        Assert.Equal(JsonValueKind.Null, listing.GetProperty("listed_at").ValueKind);

        object Fields(string state, double? sold = null) => new
        {
            title = listing.GetProperty("title").GetString(), condition = "Near Mint", qty = 1, status = state,
            price_gbp = 120, cost_gbp = 30, sold_price_gbp = sold, fees_gbp = sold is null ? (double?)null : 12.5,
            postage_gbp = sold is null ? (double?)null : 2.5,
        };

        (status, body) = await Post(me, $"/api/listings/{id}", Fields("listed"));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("listing").GetProperty("listed_at").ValueKind);

        (_, body) = await Get(me, "/api/listings");
        Assert.Equal(1, body.GetProperty("stats").GetProperty("listed").GetInt32());
        Assert.Equal(120, body.GetProperty("stats").GetProperty("listed_value_gbp").GetDouble());

        (status, body) = await Post(me, $"/api/listings/{id}", Fields("sold", 115));
        Assert.Equal(HttpStatusCode.OK, status);
        var sold = body.GetProperty("listing");
        Assert.NotEqual(JsonValueKind.Null, sold.GetProperty("sold_at").ValueKind);
        Assert.Equal(70, sold.GetProperty("profit_gbp").GetDouble());   // 115 - 12.50 - 2.50 - 30

        (_, body) = await Get(me, "/api/listings?days=30");
        var stats = body.GetProperty("stats");
        Assert.Equal(0, stats.GetProperty("listed").GetInt32());
        Assert.Equal(1, stats.GetProperty("sold").GetInt32());
        Assert.Equal(115, stats.GetProperty("revenue_gbp").GetDouble());
        Assert.Equal(70, stats.GetProperty("profit_gbp").GetDouble());
        Assert.Equal(1, stats.GetProperty("sell_through").GetDouble());
        var top = body.GetProperty("top").EnumerateArray().Single();
        Assert.Equal("OP01-120_p1", top.GetProperty("card_id").GetString());
        Assert.Equal(115, top.GetProperty("average_gbp").GetDouble());

        // Back to listed: it has not sold after all.
        (_, body) = await Post(me, $"/api/listings/{id}", Fields("listed"));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("listing").GetProperty("sold_at").ValueKind);
    }

    [Fact]
    public async Task ATitleLongerThanEbayAllowsIsRefused()
    {
        var me = await Account("wordy");
        var (status, body) = await Post(me, "/api/listings", new { card_id = "OP01-120", title = new string('x', 81) });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains("80", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SomeoneElsesListingsAreNeitherShownNorChangeable()
    {
        var me = await Account("owner");
        var them = await Account("other");
        var (_, body) = await Post(me, "/api/listings", new { card_id = "OP01-120", title = "Mine" });
        var id = body.GetProperty("listing").GetProperty("id").GetInt64();

        (_, body) = await Get(them, "/api/listings");
        Assert.Empty(body.GetProperty("listings").EnumerateArray());
        var (status, _) = await Post(them, $"/api/listings/{id}", new { title = "Theirs" });
        Assert.Equal(HttpStatusCode.NotFound, status);
        (status, _) = await Post(them, $"/api/listings/{id}/delete", new { });
        Assert.Equal(HttpStatusCode.NotFound, status);

        (status, _) = await Post(me, $"/api/listings/{id}/delete", new { });
        Assert.Equal(HttpStatusCode.OK, status);
        (_, body) = await Get(me, "/api/listings");
        Assert.Empty(body.GetProperty("listings").EnumerateArray());
    }
}
