using Manifest.Models;
using Manifest.Services;
using Manifest.Tools;

namespace Manifest.Tests;

/// <summary>
/// The Japanese site's scrape turned into catalogue rows. Pure, so no server and no
/// network: the markup is the English site's, with Japanese in the fields.
/// </summary>
public class JapanesePrintsTests
{
    const string JapaneseCardPage = """
        <html><body>
        <dl class="modalCol" id="OP11-004_p1">
         <dt><div class="cardName">ニコ・ロビン</div>
          <div class="infoCol"><span>OP11-004</span><span>SPカード</span><span>CHARACTER</span></div></dt>
         <dd><div class="frontCol"><img src="../images/cardlist/card/OP11-004_p1.png"></div>
          <div class="backCol"><div class="color"><h3>色</h3>紫/黒</div>
           <div class="feature"><h3>特徴</h3>麦わらの一味</div>
           <div class="text"><h3>テキスト</h3>【登場時】カード1枚を引く。</div></div></dd></dl>
        <dl class="modalCol" id="OP99-001">
         <dt><div class="cardName">新しいカード</div>
          <div class="infoCol"><span>OP99-001</span><span>R</span><span>CHARACTER</span></div></dt>
         <dd><div class="frontCol"><img src="../images/cardlist/card/OP99-001.png"></div>
          <div class="backCol"><div class="color"><h3>色</h3>赤/緑</div>
           <div class="col2"><div class="cost"><h3>コスト</h3>3</div></div></div></dd></dl>
        </body></html>
        """;

    static readonly CatalogRow[] English =
    {
        new() { CardId = "OP11-004", BaseId = "OP11-004", Name = "Nico Robin", Category = "CHARACTER",
                Colors = "Purple, Black", Cost = 5, Power = 6000, Types = "Straw Hat Crew",
                Effect = "[On Play] Draw 1 card.", SetLabel = "OP-11" },
        new() { CardId = "OP11-004_p1", BaseId = "OP11-004", Name = "Nico Robin", Variant = "Alt art",
                Category = "CHARACTER", Colors = "Purple, Black", SetLabel = "OP-11" },
    };

    static List<CatalogRow> Scraped() =>
        CatalogScraper.ParseCards(JapaneseCardPage, CatalogScraper.Japanese.Host)
            .Select(c => new CatalogRow
            {
                CardId = c.CardId, BaseId = c.BaseId, Name = c.Name, SetLabel = "OP-11",
                SetName = "ブースターパック", Rarity = c.Rarity, Category = c.Category, Colors = c.Colors,
                Cost = c.Cost, Power = c.Power, Counter = c.Counter, Types = c.Types, Effect = c.Effect,
                ImageUrl = c.ImageUrl,
            }).ToList();

    [Fact]
    public void AJapanesePrintSitsBesideTheEnglishOnesWithTheEnglishText()
    {
        var jp = JapanesePrints.FromScrape(English, Scraped()).Single(r => r.BaseId == "OP11-004");
        Assert.Equal("OP11-004_p1_jp", jp.CardId);
        Assert.Equal("Alt art · Japanese", jp.Variant);
        Assert.Equal("Nico Robin", jp.Name);
        Assert.Equal("Purple, Black", jp.Colors);
        Assert.Equal(5, jp.Cost);
        Assert.Equal("[On Play] Draw 1 card.", jp.Effect);
        Assert.Equal("SP CARD", jp.Rarity);
        Assert.Equal("https://www.onepiece-cardgame.com/images/cardlist/card/OP11-004_p1.png",
                     jp.ImageUrl);
    }

    [Fact]
    public void ACardNotOutInEnglishKeepsItsJapaneseTextWithColoursTranslated()
    {
        var jp = JapanesePrints.FromScrape(English, Scraped()).Single(r => r.BaseId == "OP99-001");
        Assert.Equal("OP99-001_jp", jp.CardId);
        Assert.Equal("Japanese", jp.Variant);
        Assert.Equal("新しいカード", jp.Name);
        Assert.Equal("Red, Green", jp.Colors);
        Assert.Equal(3, jp.Cost);
    }

    [Fact]
    public void AJapaneseSetTitleStillGivesItsLabel()
    {
        var (_, title, label) = CatalogScraper.SplitTitle("ブースターパック 神速の拳【OP-11】");
        Assert.Equal("OP-11", label);
        Assert.Equal("ブースターパック 神速の拳", title);
    }
}
