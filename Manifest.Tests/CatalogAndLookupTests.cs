using System.Text.Json;

namespace Manifest.Tests;

[Collection("server")]
public class CatalogAndLookupTests(ServerFixture server)
{
    static JsonElement[] Results(JsonElement body) =>
        body.GetProperty("results").EnumerateArray().ToArray();

    [Fact]
    public async Task SeedsEveryPrinting()
    {
        var h = await server.Get("/api/health");
        Assert.Equal(2627, h.GetProperty("catalog").GetInt32());
    }

    [Fact]
    public async Task DoesNotEnableApiScanningWithoutAKey()
    {
        var h = await server.Get("/api/health");
        Assert.False(h.GetProperty("api_scanning").GetBoolean());
    }

    [Fact]
    public async Task ReportsTheLocalOcrEngine()
    {
        var installed = Manifest.Services.TesseractScanner.Binary is not null;
        var h = await server.Get("/api/health");
        Assert.Equal(installed, h.GetProperty("ocr").GetBoolean());
        Assert.Equal(installed, h.GetProperty("scanning").GetBoolean());
    }

    [Fact]
    public async Task FindsTheBasePrintingAndItsAltArts()
    {
        var r = Results(await server.Get("/api/search?q=OP01-016"));
        Assert.Equal("OP01-016", r[0].GetProperty("card_id").GetString());
        Assert.True(r.Length >= 8, $"expected at least 8 printings, got {r.Length}");
        Assert.Equal("Nami", r[0].GetProperty("name").GetString());
        Assert.Equal("OP-01", r[0].GetProperty("set_label").GetString());
        Assert.Equal("Alt art", r[1].GetProperty("variant").GetString());
        Assert.StartsWith("http", r[0].GetProperty("image_url").GetString());
    }

    [Fact]
    public async Task ListsEveryPrintOfACardNumber()
    {
        // Any printing's id finds the whole family, base printing first.
        var prints = (await server.Get("/api/prints/OP01-016_p1")).GetProperty("prints")
                                                                  .EnumerateArray().ToArray();
        Assert.True(prints.Length >= 8, $"expected at least 8 printings, got {prints.Length}");
        Assert.Equal("OP01-016", prints[0].GetProperty("card_id").GetString());
        Assert.All(prints, p => Assert.Equal("OP01-016", p.GetProperty("base_id").GetString()));
    }

    [Fact]
    public async Task GroupedSearchShowsOneRowPerCardNumber()
    {
        var items = (await server.Get("/api/search?q=OP01-016&group=prints&cursor="))
            .GetProperty("items").EnumerateArray().ToArray();
        var one = Assert.Single(items);
        Assert.Equal("OP01-016", one.GetProperty("card_id").GetString());
        Assert.True(one.GetProperty("print_count").GetInt32() >= 8);

        // Name searches fold too: no card number appears twice.
        var named = (await server.Get("/api/search?q=nami&group=prints&cursor=&limit=200"))
            .GetProperty("items").EnumerateArray().Select(x => x.GetProperty("base_id").GetString())
            .ToArray();
        Assert.NotEmpty(named);
        Assert.Equal(named.Length, named.Distinct().Count());
    }

    [Fact]
    public async Task GroupedSearchStillFindsAnAltArtByItsOwnRarity()
    {
        // The representative printing is the first that passes the filters, so a
        // rarity only an alt art has still surfaces that card, shown as the alt art.
        var all = (await server.Get("/api/prints/OP01-016")).GetProperty("prints").EnumerateArray()
                                                            .ToArray();
        var altOnly = all.Select(p => p.GetProperty("rarity").GetString())
                         .FirstOrDefault(r => r != all[0].GetProperty("rarity").GetString());
        if (altOnly is null) return;
        var items = (await server.Get($"/api/search?q=OP01-016&group=prints&cursor=&rarity={altOnly}"))
            .GetProperty("items").EnumerateArray().ToArray();
        var one = Assert.Single(items);
        Assert.Equal(altOnly, one.GetProperty("rarity").GetString());
    }

    [Fact]
    public async Task SearchesByName()
    {
        var r = Results(await server.Get("/api/search?q=nami"));
        Assert.NotEmpty(r);
        Assert.All(r, x => Assert.Contains("Nami", x.GetProperty("name").GetString()!));
    }

    [Fact]
    public async Task LowercaseInputWorks()
    {
        var r = Results(await server.Get("/api/search?q=op01-016"));
        Assert.Equal("OP01-016", r[0].GetProperty("card_id").GetString());
    }

    [Fact]
    public async Task EmptyQueryBrowsesTheCatalogue() =>
        Assert.Equal(40, Results(await server.Get("/api/search?q=")).Length);

    [Fact]
    public async Task NonsenseQueryReturnsNothing() =>
        Assert.Empty(Results(await server.Get("/api/search?q=zzzznotacard")));

    [Fact]
    public async Task ListsFacets()
    {
        var f = await server.Get("/api/facets");
        Assert.Equal(new[] { "Character", "Event", "Leader", "Stage" },
                     f.GetProperty("categories").EnumerateArray()
                      .Select(x => x.GetString()).Order().ToArray());

        // multi-colour cards are split into their single colours
        var colors = f.GetProperty("colors").EnumerateArray().Select(x => x.GetString()).ToHashSet();
        Assert.All(new[] { "Red", "Green", "Blue", "Purple", "Black", "Yellow" },
                   c => Assert.Contains(c, colors));

        Assert.Contains("SuperRare",
            f.GetProperty("rarities").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("OP-01",
            f.GetProperty("sets").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task CategoryFilterNarrowsToLeaders()
    {
        var r = Results(await server.Get("/api/search?q=&category=Leader"));
        Assert.NotEmpty(r);
        Assert.All(r, x => Assert.Equal("Leader", x.GetProperty("category").GetString()));
    }

    [Fact]
    public async Task CategoryFilterCountMatchesTheCatalogue() =>
        Assert.Equal(343, Results(await server.Get("/api/search?q=&category=Event&limit=1000")).Length);

    [Fact]
    public async Task ColourFilterHitsMultiColourCardsToo()
    {
        var r = Results(await server.Get("/api/search?q=&color=Yellow&limit=1000"));
        Assert.NotEmpty(r);
        Assert.All(r, x => Assert.Contains("Yellow", x.GetProperty("colors").GetString()!));
    }

    [Fact]
    public async Task SetFilterNarrowsToThatSet()
    {
        var r = Results(await server.Get("/api/search?q=&set=ST-01"));
        Assert.NotEmpty(r);
        Assert.All(r, x => Assert.Equal("ST-01", x.GetProperty("set_label").GetString()));
    }

    [Fact]
    public async Task RarityFilterNarrowsToThatRarity()
    {
        var r = Results(await server.Get("/api/search?q=&rarity=SecretRare&limit=1000"));
        Assert.NotEmpty(r);
        Assert.All(r, x => Assert.Equal("SecretRare", x.GetProperty("rarity").GetString()));
    }

    [Fact]
    public async Task FiltersCombineWithTextSearch()
    {
        var r = Results(await server.Get("/api/search?q=nami&category=Leader"));
        Assert.All(r, x =>
        {
            Assert.Equal("Leader", x.GetProperty("category").GetString());
            Assert.Contains("Nami", x.GetProperty("name").GetString()!);
        });
    }

    [Fact]
    public async Task CardDetailCarriesTheEffectText()
    {
        var card = (await server.Get("/api/card/OP01-016")).GetProperty("card");
        Assert.Equal("OP01-016", card.GetProperty("card_id").GetString());
        Assert.True(card.TryGetProperty("effect", out _), "detail should include the effect");
    }

    [Fact]
    public async Task UnknownCardDetail404s()
    {
        var (status, _) = await server.Raw("/api/card/NOPE-999");
        Assert.Equal(404, status);
    }
}
