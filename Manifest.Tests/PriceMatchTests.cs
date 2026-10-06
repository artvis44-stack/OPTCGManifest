using System.Text.Json;
using Manifest.Tools;
using static Manifest.Tools.PriceRefresh;

namespace Manifest.Tests;

/// <summary>
/// Which catalogue printing each optcgapi.com price lands on. Pure matching, so no
/// server and no network: the shapes below are the collisions optcgapi.com is
/// known to send.
/// </summary>
public class PriceMatchTests
{
    static readonly CatalogPrint[] Catalog =
    {
        new("OP03-003", "OP03-003", "OP-03"),
        new("OP03-003_r1", "OP03-003", "ST-15"),
        new("OP14-033", "OP14-033", "OP-14"),
        new("OP14-033_p1", "OP14-033", "OP-14"),
        new("OP14-033_p2", "OP14-033", "Promotion card"),
        new("OP01-016", "OP01-016", "OP-01"),
        new("OP01-016_p4", "OP01-016", "OP-05"),
        new("ST01-001", "ST01-001", "ST-01"),
        new("P-001", "P-001", "Promotion card"),
    };

    static ApiCard Card(string source, string id, string? set, string name, double usd) =>
        new(source, id, set, name, usd);

    [Fact]
    public void AStarterDeckReprintGetsItsOwnPriceAndLeavesTheSetCardAlone()
    {
        var p = Match(new[]
        {
            Card("set", "OP03-003", "OP-03", "Izo", 0.25),
            Card("deck", "OP03-003", "ST-15", "Izo", 0.10),
        }, Catalog);
        Assert.Equal(0.25, p["OP03-003"]);
        Assert.Equal(0.10, p["OP03-003_r1"]);
    }

    [Fact]
    public void APromoReusingASetNumberLandsOnThePromoPrinting()
    {
        var p = Match(new[]
        {
            Card("promo", "OP14-033", "P", "Perona (Extra Grand Battle for Stores 2026)", 9.00),
            Card("set", "OP14-033", "OP-14", "Perona", 0.50),
        }, Catalog);
        Assert.Equal(0.50, p["OP14-033"]);
        Assert.Equal(9.00, p["OP14-033_p2"]);
    }

    [Fact]
    public void AVariantNoteNeverOverwritesThePlainCard()
    {
        var p = Match(new[]
        {
            Card("set", "OP14-033", "OP-14", "Perona (Pandaman Art)", 40.00),
            Card("set", "OP14-033", "OP-14", "Perona", 0.50),
        }, Catalog);
        Assert.Equal(0.50, p["OP14-033"]);
    }

    [Fact]
    public void ASuffixedIdIsTrustedWhateverTheSet()
    {
        // The SP alt art sits in OP-05 in the catalogue; optcgapi.com may file it under OP-01.
        var p = Match(new[] { Card("set", "OP01-016_p4", "OP-01", "Nami", 30.00) }, Catalog);
        Assert.Equal(30.00, p["OP01-016_p4"]);
    }

    [Fact]
    public void PlainStarterAndPromoIdsMatchDirectly()
    {
        var p = Match(new[]
        {
            Card("deck", "ST01-001", "ST-01", "Luffy", 1.00),
            Card("promo", "P-001", "P", "Luffy", 2.00),
        }, Catalog);
        Assert.Equal(1.00, p["ST01-001"]);
        Assert.Equal(2.00, p["P-001"]);
    }

    [Fact]
    public void AMissingSetStillMatchesItsOwnNumber()
    {
        var p = Match(new[] { Card("set", "OP01-016", null, "Nami", 0.30) }, Catalog);
        Assert.Equal(0.30, p["OP01-016"]);
    }

    [Fact]
    public void ACardTheCatalogueLacksKeepsItsPriceUnderItsOwnId()
    {
        var p = Match(new[] { Card("set", "OP99-001", "OP-99", "Someone", 1.50) }, Catalog);
        Assert.Equal(1.50, p["OP99-001"]);
    }

    [Theory]
    [InlineData("""{"card_image_id":"op01-016_P1","market_price":3.5,"set_id":"OP-01"}""", 3.5)]
    [InlineData("""{"card_image_id":"OP01-016_p1","market_price":null,"inventory_price":4.25}""", 4.25)]
    [InlineData("""{"card_image_id":"OP01-016_p1","market_price":"2.10"}""", 2.10)]
    public void ParsesPricesLeniently(string json, double usd)
    {
        var card = Parse(JsonDocument.Parse(json).RootElement, "set");
        Assert.NotNull(card);
        Assert.Equal("OP01-016_p1", card!.ImageId);
        Assert.Equal(usd, card.Usd);
    }

    [Theory]
    [InlineData("""{"card_image_id":"OP01-016","market_price":""}""")]
    [InlineData("""{"card_image_id":"","market_price":1}""")]
    [InlineData("""{"market_price":1}""")]
    public void SkipsARowWithNoUsablePriceOrId(string json) =>
        Assert.Null(Parse(JsonDocument.Parse(json).RootElement, "set"));
}
