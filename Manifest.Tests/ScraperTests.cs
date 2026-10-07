using Manifest.Tools;

namespace Manifest.Tests;

/// <summary>
/// Scraper parsing, checked against markup matching the selectors the official site
/// uses. No network needed.
/// </summary>
public class ScraperTests
{
    const string SeriesPage = """
        <html><body><div class="seriesCol"><select id="series">
        <option value="">ALL</option>
        <option value="569101">BOOSTER PACK -A FIST OF DIVINE SPEED- [OP-11]</option>
        <option value="569001">STARTER DECK -Straw Hat Crew- [ST-01]</option>
        <option value="569901">Promotion card</option>
        </select></div></body></html>
        """;

    const string CardPage = """
        <html><body>
        <dl class="modalCol" id="OP11-004">
         <dt><div class="cardName">Nico Robin</div>
          <div class="infoCol"><span>OP11-004</span><span>SR</span><span>CHARACTER</span></div></dt>
         <dd><div class="frontCol"><img src="../images/cardlist/card/OP11-004.png?250425"></div>
          <div class="backCol"><div class="color"><h3>Color</h3>Purple/Black</div>
           <div class="col2"><div class="cost"><h3>Cost</h3>5</div>
            <div class="power"><h3>Power</h3>6,000</div>
            <div class="counter"><h3>Counter</h3>1000</div></div>
           <div class="attribute">
            <h3>Attribute</h3>
            <img src="/images/cardlist/attribute/ico_type07.png?v" alt="Slash/Special"><i>Slash/Special</i>
           </div>
           <div class="block"><h3>Block<br class="spInline"> icon</h3>3</div>
           <div class="feature"><h3>Type</h3>Straw Hat Crew/Egghead</div>
           <div class="text"><h3>Effect</h3>[On Play] Draw 1 card.<br>Then, trash 1 card.</div>
           <div class="trigger"><h3>Trigger</h3>[Trigger] Play this card.</div>
          </div></dd></dl>
        <dl class="modalCol" id="OP11-004_p1">
         <dt><div class="cardName">Nico Robin</div>
          <div class="infoCol"><span>OP11-004</span><span>SR</span><span>CHARACTER</span></div></dt>
         <dd><div class="frontCol"><img src="../images/cardlist/card/OP11-004_p1.png"></div>
          <div class="backCol"><div class="color"><h3>Color</h3>Purple</div>
           <div class="col2"><div class="counter"><h3>Counter</h3>-</div></div>
           <div class="attribute"><h3>Attribute</h3> -<i></i></div>
           <div class="feature"><h3>Type</h3>Straw Hat Crew</div>
           <div class="text"><h3>Effect</h3>-</div></div></dd></dl>
        </body></html>
        """;

    [Fact]
    public void FindsEverySetOnThePage()
    {
        var packs = CatalogScraper.ParseSeries(SeriesPage);
        Assert.Equal(3, packs.Count);
        Assert.All(packs, p => Assert.NotEmpty(p.Id));   // the ALL option is skipped
    }

    [Fact]
    public void SplitsTheSetTitle()
    {
        var packs = CatalogScraper.ParseSeries(SeriesPage);
        var (prefix, title, label) = CatalogScraper.SplitTitle(packs[0].RawTitle);
        Assert.Equal("OP-11", label);
        Assert.Equal("A FIST OF DIVINE SPEED", title);
        Assert.Equal("BOOSTER PACK", prefix);
    }

    [Fact]
    public void HandlesASetWithNoLabel() =>
        Assert.Null(CatalogScraper.SplitTitle("Promotion card").Label);

    [Fact]
    public void ReadsEveryPrinting() =>
        Assert.Equal(2, CatalogScraper.ParseCards(CardPage).Count);

    [Fact]
    public void ReadsTheCardFields()
    {
        var c = CatalogScraper.ParseCards(CardPage)[0];
        Assert.Equal("Nico Robin", c.Name);
        Assert.Equal("SR", c.Rarity);
        Assert.Equal("CHARACTER", c.Category);
        Assert.Equal("Purple, Black", c.Colors);        // splits multiple colours
        Assert.Equal(6000, c.Power);                    // strips the thousands comma
        Assert.Equal(5, c.Cost);
        Assert.Equal(1000, c.Counter);
        Assert.Equal("Straw Hat Crew, Egghead", c.Types);
    }

    [Fact]
    public void TurnsALineBreakIntoASpaceAndDropsTheFieldHeading()
    {
        var c = CatalogScraper.ParseCards(CardPage)[0];
        Assert.Equal("[On Play] Draw 1 card. Then, trash 1 card.", c.Effect);
        Assert.DoesNotContain("Effect", c.Effect);
    }

    [Fact]
    public void ReadsTheTriggerBoxApartFromTheEffect()
    {
        var c = CatalogScraper.ParseCards(CardPage)[0];
        Assert.Equal("[Trigger] Play this card.", c.Trigger);
        Assert.DoesNotContain("Trigger", c.Effect);
        Assert.Equal("", CatalogScraper.ParseCards(CardPage)[1].Trigger);
    }

    [Fact]
    public void SplitsATwoAttributeIcon()
    {
        Assert.Equal("Slash, Special", CatalogScraper.ParseCards(CardPage)[0].Attributes);
        Assert.Equal("", CatalogScraper.ParseCards(CardPage)[1].Attributes);   // a dash
    }

    [Fact]
    public void ReadsTheBlockIcon()
    {
        Assert.Equal(3, CatalogScraper.ParseCards(CardPage)[0].BlockIcon);
        Assert.Null(CatalogScraper.ParseCards(CardPage)[1].BlockIcon);
    }

    [Fact]
    public void BuildsAnAbsoluteImageUrl() =>
        Assert.Equal("https://en.onepiece-cardgame.com/images/cardlist/card/OP11-004.png?250425",
                     CatalogScraper.ParseCards(CardPage)[0].ImageUrl);

    [Fact]
    public void HandlesTheParallelPrinting()
    {
        var p = CatalogScraper.ParseCards(CardPage)[1];
        Assert.Equal("Alt art", p.Variant);
        Assert.Equal("OP11-004", p.BaseId);             // grouped under its base number
        Assert.Null(p.Counter);                         // a dash counter is absent
        Assert.Equal("", p.Effect);                     // a dash effect is empty
    }
}
