using Manifest.Data;
using Manifest.Models;
using Manifest.Services;
using Manifest.Tools;
using Microsoft.Data.Sqlite;

namespace Manifest.Tests;

/// <summary>
/// Limitless card pages, checked against markup cut down from the live site, and
/// the cards they fill in seeded beside the official ones. No network needed.
/// </summary>
public sealed class LimitlessScraperTests : IDisposable
{
    static string Page(string id, string name, string typeLine, string stats, string text,
                       string types, int block) => $$"""
        <section class="card-page-main"><div class="card-profile">
        <div class="card-image">
            <img class="card shadow resp-w" src="https://limitlesstcg.nyc3.cdn.digitaloceanspaces.com/one-piece/P/{{id}}_EN.webp" width=600 height=838>
        </div>
        <div class="card-details"><div class="card-details-main"><div class="card-text">
        <div class="card-text-section">
            <p class="card-text-title">
                <span class="card-text-name"><a href="/cards/{{id}}">{{name}}</a></span>
                <span class="card-text-id">{{id}}</span>
            </p>
            <p class="card-text-type">
                {{typeLine}}
            </p>
        </div>
        {{stats}}
        <div class="card-text-section">
            {{text}}
        </div>
        <div class="card-text-section">
            <span data-tooltip="Type">{{types}}</span>
        </div>
        <div class="card-text-section card-text-artist">
            Illustrated by <a href="/cards?q=!artist:someone">Someone</a>
        </div>
        </div>
        <div class="card-legality"><div class="regulation-mark">
             Block {{block}}</div></div>
        </div></div></div>
        <div class="card-prints"><div class="card-prints-current"><a href=/cards/misc-promos>
            <div class="prints-current-details"><span class="text-lg">
                Misc. Promos
            </span></div></a></div></div>
        </section>
        """;

    static readonly string Hancock = Page("P-066", "Boa Hancock",
        """<span data-tooltip="Category">Character</span> • <span data-tooltip="Color">Blue</span> • 4 Cost""",
        """
        <p class="card-text-section">
            5000 Power • <span data-tooltip="Attribute">Special</span> • +1000 Counter
        </p>
        """,
        """[Your Turn] If you have 5 or less cards in your hand, all of your <a href="/cards/?q=type%3A%22Kuja+Pirates%22">{Kuja Pirates}</a> type Characters gain +1000 power.""",
        "The Seven Warlords of the Sea/Kuja Pirates", 2);

    static readonly string Adventure = Page("P-002", "I Smell Adventure!!!",
        """<span data-tooltip="Category">Event</span> • <span data-tooltip="Color">Red</span> • 1 Cost""",
        "",
        """
        [Main] Return all cards in your hand to your deck and shuffle your deck.
        <br><br>
        [Trigger] Activate this card's [Main] effect.
        """,
        "Straw Hat Crew", 1);

    static readonly string Sakazuki = Page("P-076", "Sakazuki",
        """<span data-tooltip="Category">Leader</span> • <span data-tooltip="Color">Blue/Black</span> • 4 Life""",
        """
        <p class="card-text-section">
            5000 Power • <span data-tooltip="Attribute">Special</span>
        </p>
        """,
        "[Activate: Main] [Once Per Turn] Give up to 1 of your opponent's Characters −1 cost during this turn.",
        "Navy", 2);

    [Fact]
    public void ReadsACharacter()
    {
        var c = LimitlessScraper.ParseCard(Hancock)!;
        Assert.Equal("P-066", c.CardId);
        Assert.Equal("P-066", c.BaseId);
        Assert.Equal("", c.Variant);
        Assert.Equal("Boa Hancock", c.Name);
        Assert.Equal("CHARACTER", c.Category);
        Assert.Equal("Blue", c.Colors);
        Assert.Equal(4, c.Cost);
        Assert.Equal(5000, c.Power);
        Assert.Equal(1000, c.Counter);
        Assert.Equal("Special", c.Attributes);
        Assert.Equal("The Seven Warlords of the Sea, Kuja Pirates", c.Types);
        Assert.Equal("[Your Turn] If you have 5 or less cards in your hand, all of your "
                     + "{Kuja Pirates} type Characters gain +1000 power.", c.Effect);
        Assert.Equal("", c.Trigger);
        Assert.Equal(2, c.BlockIcon);
        // Filed with the official site's promos, so they list as one set.
        Assert.Equal("Promotion card", c.SetLabel);
        Assert.Equal("Promotion card", c.SetName);
        Assert.Equal("P", c.Rarity);
        Assert.Equal("https://limitlesstcg.nyc3.cdn.digitaloceanspaces.com/one-piece/P/P-066_EN.webp", c.ImageUrl);
    }

    [Fact]
    public void SplitsTheTriggerFromTheEffect()
    {
        var c = LimitlessScraper.ParseCard(Adventure)!;
        Assert.Equal("EVENT", c.Category);
        Assert.Equal(1, c.Cost);
        Assert.Null(c.Power);
        Assert.Null(c.Counter);
        Assert.Equal("", c.Attributes);
        Assert.Equal("[Main] Return all cards in your hand to your deck and shuffle your deck.", c.Effect);
        Assert.Equal("[Trigger] Activate this card's [Main] effect.", c.Trigger);
    }

    [Fact]
    public void ALeadersLifeGoesWhereTheOfficialSiteKeepsIt()
    {
        var c = LimitlessScraper.ParseCard(Sakazuki)!;
        Assert.Equal("LEADER", c.Category);
        Assert.Equal("Blue, Black", c.Colors);
        Assert.Equal(4, c.Cost);
        Assert.Equal(5000, c.Power);
        Assert.Null(c.Counter);
    }

    [Fact]
    public void APageThatIsNotACardIsNothing() =>
        Assert.Null(LimitlessScraper.ParseCard("<html><body>Page not found</body></html>"));

    static CatalogScraper.ScrapedCard Card(string id) => new() { CardId = id };

    [Fact]
    public void LooksUpOnlyNumbersNoEnglishPrintingHas()
    {
        var english = new[] { Card("P-001"), Card("OP01-016"), Card("OP01-016_p1") };
        var printed = new[]
        {
            "P-001", "OP01-016_p6",                     // English has the card: an alt art
            "P-066", "P-066_p1",                        // no English printing at all
            "P-150", "P-136",
        };
        var extra = new[]
        {
            new CatalogScraper.ScrapedCard { CardId = "P-136", Source = "limitless" },      // fetched last time
            new CatalogScraper.ScrapedCard { CardId = "P-150_t1", Source = "tcgplayer" },   // a print, not the card
        };

        Assert.Equal(new[] { "P-066", "P-150" }, LimitlessScraper.Gaps(english, printed, extra));
    }

    // ---- seeding

    readonly string _root = Directory.CreateTempSubdirectory("manifest-limitless-").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    CatalogRow? Seeded(string id)
    {
        var db = new Database(new AppPaths(_root), "sqlite://manifest.db");
        Assert.Null(db.Initialise(forceReseed: true));
        using var conn = db.Open();
        return CardRepository.CatalogRowById(conn, id);
    }

    [Fact]
    public void FillsTheGapAndTheJapanesePrintReadsInEnglish()
    {
        var paths = new AppPaths(_root);
        File.WriteAllText(paths.Catalog, """
            [{"card_id": "P-001", "base_id": "P-001", "variant": "", "name": "Monkey.D.Luffy",
              "set_label": "Promotion card", "category": "CHARACTER", "trigger": ""}]
            """);
        File.WriteAllText(paths.CatalogExtra, """
            [{"card_id": "P-066", "base_id": "P-066", "variant": "", "name": "Boa Hancock",
              "set_label": "Promotion card", "category": "CHARACTER", "colors": "Blue",
              "effect": "[Your Turn] ...", "trigger": ""},
             {"card_id": "P-001", "base_id": "P-001", "variant": "", "name": "Stale Luffy",
              "category": "CHARACTER", "trigger": ""}]
            """);
        File.WriteAllText(paths.CatalogJapanese, """
            [{"card_id": "P-066", "base_id": "P-066", "variant": "", "name": "ボア・ハンコック",
              "set_label": "プロモーションカード", "category": "CHARACTER", "colors": "青",
              "effect": "【自分のターン中】...", "trigger": ""}]
            """);

        Assert.Equal("Boa Hancock", Seeded("P-066")!.Name);
        var jp = Seeded("P-066_jp")!;
        Assert.Equal("Boa Hancock", jp.Name);
        Assert.Equal("[Your Turn] ...", jp.Effect);
        // The official site's printing wins over a stale fill-in of the same number.
        Assert.Equal("Monkey.D.Luffy", Seeded("P-001")!.Name);
    }

    [Fact]
    public void TellsPngFromWebp()
    {
        Assert.Equal("image/png", ImageCache.ContentTypeOf(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0 }));
        Assert.Equal("image/webp", ImageCache.ContentTypeOf("RIFF\0\0\0\0WEBPVP8 "u8.ToArray()));
        Assert.Null(ImageCache.ContentTypeOf("<html>"u8.ToArray()));
    }
}
