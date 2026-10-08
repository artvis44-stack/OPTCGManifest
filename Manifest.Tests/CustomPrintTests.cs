using System.Net;
using System.Text;
using System.Text.Json;

namespace Manifest.Tests;

/// <summary>
/// Prints added by hand, through the running server: one reads as its card, keeps
/// its photo and typed-in price, and can be taken out again only by whoever added it
/// and only while nothing holds it.
/// </summary>
[Collection("server")]
public class CustomPrintTests(ServerFixture server)
{
    const string Password = "correct-horse-battery";

    // A 1x1 PNG.
    const string Photo =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

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

    static async Task<JsonElement> Get(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    static Task<(HttpStatusCode Status, JsonElement Body)> Add(HttpClient client, string cardId,
                                                               double? price = null, string? image = Photo) =>
        Post(client, "/api/prints/custom", new
        {
            card_id = cardId, variant = "CHOPPER's book promo", set_label = "Unnumbered Promos",
            price_gbp = price, image,
        });

    static async Task<string[]> PrintIds(HttpClient client, string cardId) =>
        (await Get(client, "/api/prints/" + cardId)).GetProperty("prints").EnumerateArray()
            .Select(p => p.GetProperty("card_id").GetString()!).ToArray();

    [Fact]
    public async Task AnAddedPrintReadsAsItsCardWithItsOwnPhotoAndPrice()
    {
        var me = await Account("adder");
        var (status, body) = await Add(me, "EB02-003_p1", price: 23);
        Assert.Equal(HttpStatusCode.OK, status);

        var card = body.GetProperty("card");
        var id = card.GetProperty("card_id").GetString()!;
        Assert.Matches(@"^EB02-003_c\d+$", id);
        Assert.Equal("EB02-003", card.GetProperty("base_id").GetString());
        Assert.Equal("Tony Tony.Chopper", card.GetProperty("name").GetString());
        Assert.Equal("CHOPPER's book promo", card.GetProperty("variant").GetString());
        Assert.Equal("Unnumbered Promos", card.GetProperty("set_label").GetString());
        Assert.Contains("Straw Hat Crew", card.GetProperty("effect").GetString());   // the base card's text
        Assert.Equal(23, card.GetProperty("price_gbp").GetDouble());
        var price = card.GetProperty("prices").EnumerateArray().Single();
        Assert.Equal("manual", price.GetProperty("source").GetString());

        Assert.Contains(id, await PrintIds(me, "EB02-003"));

        var art = await me.GetAsync("/img/" + id);
        Assert.Equal(HttpStatusCode.OK, art.StatusCode);
        Assert.Equal(Convert.FromBase64String(Photo), await art.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task EachPrintOfACardGetsTheNextNumberAndARemovedOneIsNeverReused()
    {
        var me = await Account("numberer");
        var first = (await Add(me, "OP01-001")).Body.GetProperty("card").GetProperty("card_id").GetString()!;
        var second = (await Add(me, "OP01-001")).Body.GetProperty("card").GetProperty("card_id").GetString()!;
        var n = int.Parse(first.Split("_c")[1]);
        Assert.Equal($"OP01-001_c{n + 1}", second);

        Assert.Equal(HttpStatusCode.OK, (await Post(me, "/api/prints/custom/delete", new { card_id = second })).Status);
        Assert.DoesNotContain(second, await PrintIds(me, "OP01-001"));

        var third = (await Add(me, "OP01-001")).Body.GetProperty("card").GetProperty("card_id").GetString()!;
        Assert.Equal($"OP01-001_c{n + 2}", third);
    }

    [Fact]
    public async Task OnlyWhoeverAddedItCanRemoveIt()
    {
        var adder = await Account("mine");
        var other = await Account("notmine");
        var id = (await Add(adder, "OP01-002")).Body.GetProperty("card").GetProperty("card_id").GetString()!;

        Assert.Equal(HttpStatusCode.Forbidden,
                     (await Post(other, "/api/prints/custom/delete", new { card_id = id })).Status);
        Assert.Equal(HttpStatusCode.OK,
                     (await Post(adder, "/api/prints/custom/delete", new { card_id = id })).Status);
    }

    [Fact]
    public async Task APrintWithCopiesLoggedCannotBeRemoved()
    {
        var me = await Account("logger");
        var id = (await Add(me, "OP01-003")).Body.GetProperty("card").GetProperty("card_id").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await Post(me, "/api/collection", new { card_id = id, delta = 1 })).Status);

        var (status, body) = await Post(me, "/api/prints/custom/delete", new { card_id = id });
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Contains("binder", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ABadPhotoOrNoNameIsRefusedAndNothingIsAdded()
    {
        var me = await Account("careful");
        var before = await PrintIds(me, "OP01-004");

        Assert.Equal(HttpStatusCode.BadRequest, (await Add(me, "OP01-004", image: "bm90IGFuIGltYWdl")).Status);
        var (status, _) = await Post(me, "/api/prints/custom",
                                     new { card_id = "OP01-004", variant = "  ", image = Photo });
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(HttpStatusCode.NotFound, (await Add(me, "OP99-999")).Status);

        Assert.Equal(before, await PrintIds(me, "OP01-004"));
    }

    [Fact]
    public async Task APrintAddedByHandOutlivesAReseedAndKeepsItsPrice()
    {
        var me = await Account("reseeder");
        var id = (await Add(me, "OP01-005", price: 7.5)).Body.GetProperty("card").GetProperty("card_id").GetString()!;

        await server.Restart("--reseed");

        Assert.Contains(id, await PrintIds(server.Client, "OP01-005"));
        var card = (await Get(server.Client, "/api/card/" + id)).GetProperty("card");
        Assert.Equal(id, card.GetProperty("card_id").GetString());
        Assert.Equal(7.5, card.GetProperty("price_gbp").GetDouble());
    }
}
