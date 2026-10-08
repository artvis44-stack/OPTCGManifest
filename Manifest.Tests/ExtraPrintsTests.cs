using System.Text.Json;
using Manifest.Services;
using Manifest.Tools;
using static Manifest.Tools.CatalogScraper;

namespace Manifest.Tests;

/// <summary>
/// Limitless's product lists, TCGplayer's catalogue and prices, and the prints and
/// price links built from them - against markup and JSON cut down from the live
/// sites. No network needed.
/// </summary>
public class ExtraPrintsTests
{
    const string AnniversaryPage = """
        <div class="infobox"><div class="infobox-heading sm">English Version 1st Anniversary Set</div></div>
        <table class="data-table striped highlight card-list">
          <tr><th>Card</th><th>Name</th><th>Category</th><th>Rarity</th><th>USD</th><th>EUR</th></tr>
          <tr data-hover="https://limitlesstcg.nyc3.cdn.digitaloceanspaces.com/one-piece/OP01/OP01-016_p6_EN.webp">
            <td><a href="/cards/OP01-016?v=6">OP01-016</a></td>
            <td><a href="/cards/OP01-016?v=6">Nami</a></td>
            <td class="md-only"><a href="/cards/OP01-016?v=6"> Character </a></td>
            <td class="md-only"><a href="/cards/OP01-016?v=6">R</a></td>
            <td> <a class="card-price usd" href="https://partner.tcgplayer.com/ONEPIECE?u=https%3A%2F%2Fwww.tcgplayer.com%2Fproduct%2F557286%2Fnami" target="_blank">$1,500.21</a> </td>
            <td> <a class="card-price eur" href="https://www.cardmarket.com/en/OnePiece/Products/Singles/Premium-Bandai-Products/Nami-OP01-016-V2?utm_source=limitlesstcg" target="_blank">442.92€</a> </td>
          </tr>
          <tr data-hover="https://limitlesstcg.nyc3.cdn.digitaloceanspaces.com/one-piece/ST01/ST01-006_EN.webp">
            <td><a href="/cards/ST01-006?v=3">ST01-006</a></td>
            <td><a href="/cards/ST01-006?v=3">Tony Tony.Chopper</a></td>
            <td class="md-only"><a href="/cards/ST01-006?v=3"> Character </a></td>
            <td class="md-only"><a href="/cards/ST01-006?v=3"></a></td>
            <td> </td>
            <td> </td>
          </tr>
        </table>
        """;

    [Fact]
    public void ReadsAProductList()
    {
        var prints = LimitlessPrints.ParseProduct(AnniversaryPage, "english-version-1st-anniversary-set");
        Assert.Equal(2, prints.Count);

        var nami = prints[0];
        Assert.Equal("OP01-016_p6", nami.CardId);
        Assert.Equal("OP01-016", nami.BaseId);
        Assert.Equal("OP01-016?v=6", nami.Href);
        Assert.Equal("English Version 1st Anniversary Set", nami.Product);
        Assert.Equal("Nami", nami.Name);
        Assert.Equal("CHARACTER", nami.Category);
        Assert.Equal("R", nami.Rarity);
        Assert.Equal(1500.21, nami.Usd);
        Assert.Equal(557286, nami.TcgplayerId);
        Assert.Equal(442.92, nami.Eur);
        Assert.Equal("Premium-Bandai-Products/Nami-OP01-016-V2", nami.CardmarketPath);
        Assert.False(nami.Japanese);

        // A reprint keeps its card's picture, and some rows are not priced yet.
        var chopper = prints[1];
        Assert.Equal("ST01-006", chopper.CardId);
        Assert.Equal("ST01-006?v=3", chopper.Href);
        Assert.Null(chopper.Usd);
        Assert.Null(chopper.Eur);
        Assert.Null(chopper.TcgplayerId);
    }

    [Fact]
    public void ListsTheProductsAnIndexLinksTo() =>
        Assert.Equal(new[] { "op01-romance-dawn", "english-version-1st-anniversary-set" },
                     LimitlessPrints.ParseIndex("""
                         <a href="/cards/advanced">Advanced</a>
                         <a href="/cards/op01-romance-dawn">OP01</a>
                         <a href="/cards/OP01-016">a card, not a product</a>
                         <a href="/cards/english-version-1st-anniversary-set">Anniversary</a>
                         <a href="/cards/op01-romance-dawn">again</a>
                         <a href="/cards/promos">Promos</a>
                         """));

    // ---- tcgcsv.com

    static readonly TcgCsv.Group PromoGroup = new(17675, "One Piece Promotion Cards");

    static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    const string Products = """
        [{"productId": 714349, "name": "Kaido (4th Anniversary Treasure Campaign Pack)",
          "imageUrl": "https://tcgplayer-cdn.tcgplayer.com/product/714349_200w.jpg",
          "extendedData": [
            {"name": "Rarity", "value": "R"}, {"name": "Number", "value": "EB04-030"},
            {"name": "Description", "value": "If this Character would be K.O.'d...\r\n<br>[On Play] <strong>DON!! -2:</strong> Rest up to 1.\r\n<br>[Trigger] Play this card."},
            {"name": "Color", "value": "Purple"}, {"name": "CardType", "value": "Character"},
            {"name": "Cost", "value": "7"}, {"name": "Power", "value": "9000"},
            {"name": "Subtypes", "value": "Animal Kingdom Pirates;The Four Emperors"},
            {"name": "Attribute", "value": "Strike"}]},
         {"productId": 1, "name": "4th Anniversary Treasure Campaign Pack", "extendedData": []},
         {"productId": 2, "name": "DON!! Card (Yamato)", "extendedData": [{"name": "Number", "value": ""}]}]
        """;

    [Fact]
    public void ReadsTcgplayerCardsAndSkipsSealedProduct()
    {
        var products = TcgCsv.ParseProducts(Json(Products), PromoGroup);
        var kaido = Assert.Single(products);
        Assert.Equal(714349, kaido.ProductId);
        Assert.Equal("EB04-030", kaido.Number);
        Assert.Equal("R", kaido.Rarity);
        Assert.Equal("One Piece Promotion Cards", kaido.GroupName);
        Assert.Equal("https://tcgplayer-cdn.tcgplayer.com/product/714349_in_1000x1000.jpg", kaido.ImageUrl);
    }

    [Fact]
    public void ACardTooNewForTheOfficialSiteIsReadOffItsListing()
    {
        var listing = TcgCsv.ParseProducts(Json(Products), new TcgCsv.Group(1, "Extra Booster: Example")).Single();
        var card = TcgCsv.Card(new[] { listing with { Name = "Kaido" } })!;
        Assert.Equal("EB04-030", card.CardId);
        Assert.Equal("Kaido", card.Name);
        Assert.Equal("EB-04", card.SetLabel);
        Assert.Equal("CHARACTER", card.Category);
        Assert.Equal("Purple", card.Colors);
        Assert.Equal(7, card.Cost);
        Assert.Equal(9000, card.Power);
        Assert.Equal("Animal Kingdom Pirates, The Four Emperors", card.Types);
        Assert.Equal("Strike", card.Attributes);
        Assert.Equal("If this Character would be K.O.'d... [On Play] DON!! -2: Rest up to 1.", card.Effect);
        Assert.Equal("[Trigger] Play this card.", card.Trigger);
        Assert.Equal("tcgplayer-card", card.Source);
    }

    [Fact]
    public void APriceIsTheNormalCardsWhenThereIsAFoilToo()
    {
        var prices = TcgCsv.ParsePrices(Json("""
            [{"productId": 552138, "marketPrice": 178.58, "subTypeName": "Foil"},
             {"productId": 552138, "marketPrice": 8.46, "subTypeName": "Normal"},
             {"productId": 714349, "marketPrice": 1.15, "subTypeName": "Foil"},
             {"productId": 3, "marketPrice": null, "subTypeName": "Normal"}]
            """));
        var byProduct = TcgCsv.ByProduct(prices);
        Assert.Equal(8.46, byProduct[552138]);
        Assert.Equal(1.15, byProduct[714349]);
        Assert.False(byProduct.ContainsKey(3));
    }

    // ---- building the prints

    static ScrapedCard Official(string id, string set, string name = "Nami") => new()
    {
        CardId = id, BaseId = id.Split('_')[0], Name = name, SetLabel = set, Category = "CHARACTER",
        Effect = "[On Play] Look at 5 cards.", Colors = "Red",
    };

    static LimitlessPrints.Print Lim(string id, string href, string slug, string product,
                                     long? tcgplayer = null, double? eur = null) =>
        new(id, href, slug, product, "Nami", "CHARACTER", "R", 1.0, tcgplayer, eur, "Singles/x", $"https://cdn/{id}_EN.webp", false);

    static TcgCsv.Product Tcg(long id, string number, string name, string group = "One Piece Promotion Cards") =>
        new(id, 17675, group, name, number, "R", $"https://tcgplayer-cdn.tcgplayer.com/product/{id}_in_1000x1000.jpg");

    [Fact]
    public void AddsTheEnglishPrintsTheOfficialSiteLeavesOut()
    {
        var english = new[] { Official("OP01-016", "OP-01"), Official("OP01-016_p1", "OP-01") };
        var limitless = new[]
        {
            Lim("OP01-016", "OP01-016", "op01-romance-dawn", "Romance Dawn", 454534, 1.66),
            Lim("OP01-016_p6", "OP01-016?v=6", "english-version-1st-anniversary-set",
                "English Version 1st Anniversary Set", 557286, 442.92),
        };

        var result = ExtraPrints.Build(english, Array.Empty<ScrapedCard>(), limitless, Array.Empty<TcgCsv.Product>());

        var p6 = Assert.Single(result.Extra);
        Assert.Equal("OP01-016_p6", p6.CardId);
        Assert.Equal("OP01-016", p6.BaseId);
        Assert.Equal("Alt art 6", p6.Variant);
        Assert.Equal("English Version 1st Anniversary Set", p6.SetLabel);
        Assert.Equal("Nami", p6.Name);
        Assert.Equal("[On Play] Look at 5 cards.", p6.Effect);   // the English card's text
        Assert.Equal("https://cdn/OP01-016_p6_EN.webp", p6.ImageUrl);
        Assert.Equal("limitless-print", p6.Source);

        Assert.Equal(new[] { "OP01-016", "OP01-016_p6" }, result.Links.Select(l => l.CardId));
        Assert.Equal(557286, result.Links[1].Tcgplayer);
        Assert.Equal("OP01-016?v=6", result.Links[1].Limitless);
    }

    [Fact]
    public void AReprintSharingItsCardsPictureFindsItsOwnPrinting()
    {
        var english = new[] { Official("EB01-009", "EB-01"), Official("EB01-009_r1", "PRB-01") };
        var limitless = new[]
        {
            // The reprint's row is listed first; the card's own set still gets the plain number.
            Lim("EB01-009", "EB01-009?v=2", "prb01-premium-booster-one-piece-the-best", "The Best", 2),
            Lim("EB01-009", "EB01-009", "eb01-memorial-collection", "Memorial Collection", 1),
        };

        var links = ExtraPrints.Build(english, Array.Empty<ScrapedCard>(), limitless, Array.Empty<TcgCsv.Product>())
                               .Links.ToDictionary(l => l.CardId);
        Assert.Equal(1, links["EB01-009"].Tcgplayer);
        Assert.Equal(2, links["EB01-009_r1"].Tcgplayer);
    }

    [Fact]
    public void TheOfficialSitesSetWinsOverALimitlessPromoWithTheSameNumber()
    {
        // Officially OP03-112_p4 is the OP-08 SP card; Limitless files a tournament
        // pack's art under that number. Its prices are not the SP card's.
        var english = new[] { Official("OP03-112", "OP-03"), Official("OP03-112_p4", "OP-08") };
        var limitless = new[] { Lim("OP03-112_p4", "OP03-112?v=4", "tournament-pack-09", "Tournament Pack", 99, 0.25) };

        var result = ExtraPrints.Build(english, Array.Empty<ScrapedCard>(), limitless, Array.Empty<TcgCsv.Product>());
        Assert.Empty(result.Links);
        Assert.Empty(result.Extra);
    }

    [Fact]
    public void ATcgplayerOnlyPrintGetsANumberThatStaysPut()
    {
        var english = new[] { Official("EB04-030", "EB-04", "Kaido"), Official("OP12-049", "OP-12", "Buggy") };
        var tcgplayer = new[]
        {
            Tcg(714349, "EB04-030", "Kaido (4th Anniversary Treasure Campaign Pack)"),
            Tcg(714350, "OP12-049", "Buggy (4th Anniversary Treasure Campaign Pack)"),
            Tcg(800000, "EB04-030", "Kaido", "Royal Blood Release Event Cards"),
            Tcg(5, "EB04-030", "Kaido", "Extra Booster: Memorial Collection"),   // a set's own listing: not a print
            Tcg(6, "OP99-001", "Nobody (Judge Pack)"),                          // no such card
            Tcg(557286, "EB04-030", "Kaido (Already on Limitless)"),
        };
        var limitless = new[] { Lim("EB04-030", "EB04-030", "eb04-x", "x", 557286) };

        var first = ExtraPrints.Build(english, Array.Empty<ScrapedCard>(), limitless, tcgplayer);
        var byProduct = first.Extra.ToDictionary(c => c.TcgplayerId!.Value);
        Assert.Equal(3, byProduct.Count);
        Assert.Equal("EB04-030_t1", byProduct[714349].CardId);
        Assert.Equal("OP12-049_t1", byProduct[714350].CardId);
        Assert.Equal("EB04-030_t2", byProduct[800000].CardId);

        var anniversary = byProduct[714349];
        Assert.Equal("4th Anniversary Treasure Campaign Pack", anniversary.SetLabel);
        Assert.Equal("4th Anniversary Treasure Campaign Pack", anniversary.Variant);
        Assert.Equal("Kaido", anniversary.Name);
        Assert.Equal("tcgplayer", anniversary.Source);
        Assert.Equal("Royal Blood Release Event Cards", byProduct[800000].SetLabel);
        Assert.Equal("Royal Blood Release Event", byProduct[800000].Variant);

        // Next run, an earlier product turns up: the numbers already given stay as they were.
        var again = ExtraPrints.Build(english, first.Extra,
                                      limitless, tcgplayer.Append(Tcg(700000, "EB04-030", "Kaido (Judge Pack)")).ToList());
        var ids = again.Extra.ToDictionary(c => c.TcgplayerId!.Value, c => c.CardId);
        Assert.Equal("EB04-030_t1", ids[714349]);
        Assert.Equal("EB04-030_t2", ids[800000]);
        Assert.Equal("EB04-030_t3", ids[700000]);
    }

    [Fact]
    public void ACardReadOffATcgplayerListingIsNotAlsoAPrintOfItself()
    {
        var english = new[] { Official("OP01-001", "OP-01") };
        var extra = new[]
        {
            new ScrapedCard { CardId = "P-136", BaseId = "P-136", Name = "Usopp", Source = "tcgplayer-card", TcgplayerId = 690672 },
        };
        var tcgplayer = new[]
        {
            Tcg(690672, "P-136", "Usopp - P-136 (Premium Card Collection -Live Action Edition Vol.2-)"),
            Tcg(690700, "P-136", "Usopp (Judge Pack)"),
        };

        var result = ExtraPrints.Build(english, extra, Array.Empty<LimitlessPrints.Print>(), tcgplayer);
        Assert.Equal(new[] { "P-136", "P-136_t1" }, result.Extra.Select(c => c.CardId));
        Assert.Equal(690700, result.Extra[1].TcgplayerId);
        Assert.Equal(690672, result.Links.Single(l => l.CardId == "P-136").Tcgplayer);
    }

    // ---- prices

    [Fact]
    public void LinkedPricesComeFromLimitlessAndTcgcsv()
    {
        var links = new[]
        {
            new ExtraPrints.Link { CardId = "OP01-016_p6", Limitless = "OP01-016?v=6", Tcgplayer = 557286,
                                   Cardmarket = "Premium-Bandai-Products/Nami-OP01-016-V2" },
            new ExtraPrints.Link { CardId = "EB04-030_t1", Tcgplayer = 714349 },
        };
        var limitless = LimitlessPrints.ParseProduct(AnniversaryPage, "x");
        var tcgcsv = new Dictionary<long, double> { [714349] = 1.15 };

        var prices = PriceRefresh.FromLinks(links, limitless, tcgcsv);
        Assert.Equal(3, prices.Count);
        var cm = prices.Single(p => p.Source == "cardmarket");
        Assert.Equal(("OP01-016_p6", "EUR", 442.92), (cm.CardId, cm.Currency, cm.Amount));
        Assert.Equal("https://www.cardmarket.com/en/OnePiece/Products/Singles/Premium-Bandai-Products/Nami-OP01-016-V2", cm.Url);
        // tcgcsv.com has no price for 557286, so Limitless's copy of TCGplayer's stands in.
        Assert.Equal(1500.21, prices.Single(p => p.CardId == "OP01-016_p6" && p.Source == "tcgplayer").Amount);
        var kaido = prices.Single(p => p.CardId == "EB04-030_t1");
        Assert.Equal(("tcgplayer", "USD", 1.15), (kaido.Source, kaido.Currency, kaido.Amount));
        Assert.Equal("https://www.tcgplayer.com/product/714349", kaido.Url);
    }

    [Fact]
    public void APriceFarFromOptcgapisIsLeftOut()
    {
        var linked = new[]
        {
            new PriceRefresh.SourcePrice("OP03-112_p4", "tcgplayer", "USD", 0.29, null),
            new PriceRefresh.SourcePrice("OP03-112_p4", "cardmarket", "EUR", 0.20, null),
            new PriceRefresh.SourcePrice("OP01-016", "tcgplayer", "USD", 5.03, null),
            new PriceRefresh.SourcePrice("OP01-016", "cardmarket", "EUR", 1.66, null),
            new PriceRefresh.SourcePrice("P-001", "tcgplayer", "USD", 0.10, null),   // cheap either way
        };
        var optcgapi = new Dictionary<string, double> { ["OP03-112_p4"] = 92.08, ["OP01-016"] = 4.91, ["P-001"] = 0.40 };

        var kept = PriceRefresh.Plausible(linked, optcgapi).ToList();
        Assert.DoesNotContain(kept, p => p.CardId == "OP03-112_p4");
        Assert.Equal(2, kept.Count(p => p.CardId == "OP01-016"));
        Assert.Single(kept, p => p.CardId == "P-001");
    }

    [Theory]
    [InlineData("EB04-030_t1", "EB04-030_t1")]
    [InlineData("eb04-030_T12", "EB04-030_t12")]
    [InlineData("OP01-016_p10", "OP01-016_p10")]
    [InlineData("OP01-016_p6_jp", "OP01-016_p6_jp")]
    public void TheNewNumbersSurviveNormalising(string raw, string expected) =>
        Assert.Equal(expected, CardId.Normalise(raw));

    [Fact]
    public void TellsJpegToo() =>
        Assert.Equal("image/jpeg", ImageCache.ContentTypeOf(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
}
