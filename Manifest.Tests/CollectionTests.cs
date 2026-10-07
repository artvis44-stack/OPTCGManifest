using System.Text.Json;

namespace Manifest.Tests;

[Collection("server")]
public class CollectionTests(ServerFixture server)
{
    [Fact]
    public async Task LogsCopiesAndResolvesTheName()
    {
        await server.Reset();
        var (_, d) = await server.Post("/api/collection", new { card_id = "OP01-016", delta = 4 });
        Assert.Equal(4, d.GetProperty("qty").GetInt32());
        Assert.Equal("Nami", d.GetProperty("name").GetString());
    }

    [Fact]
    public async Task AltArtCountsSeparately()
    {
        await server.Reset();
        await server.Log("OP01-016", 4);
        var (_, d) = await server.Post("/api/collection",
                                       new { card_id = "OP01-016_p1", delta = 1 });
        Assert.Equal(1, d.GetProperty("qty").GetInt32());
    }

    [Fact]
    public async Task ChangesCopiesToAnotherPrint()
    {
        await server.Reset();
        await server.Log("OP01-016", 3);
        await server.Log("OP01-016_p1", 1);
        var (status, d) = await server.Post("/api/collection/print",
                                            new { from = "OP01-016", to = "OP01-016_p1", qty = 2 });
        Assert.Equal(200, status);
        Assert.Equal(1, d.GetProperty("from_qty").GetInt32());
        Assert.Equal(3, d.GetProperty("to_qty").GetInt32());

        // Asking for more than there are moves what there is, and clears the old print.
        (_, d) = await server.Post("/api/collection/print",
                                   new { from = "OP01-016", to = "OP01-016_p1", qty = 9 });
        Assert.Equal(0, d.GetProperty("from_qty").GetInt32());
        Assert.Equal(4, d.GetProperty("to_qty").GetInt32());
    }

    [Fact]
    public async Task WillNotChangeACardIntoADifferentCard()
    {
        await server.Reset();
        await server.Log("OP01-016", 1);
        var (status, _) = await server.Post("/api/collection/print",
                                            new { from = "OP01-016", to = "OP01-004", qty = 1 });
        Assert.Equal(400, status);
        (status, _) = await server.Post("/api/collection/print",
                                        new { from = "OP01-004", to = "OP01-004_p1", qty = 1 });
        Assert.Equal(400, status);   // none of it owned
    }

    [Fact]
    public async Task NormalisesLowercaseOnWrite()
    {
        await server.Reset();
        var (_, d) = await server.Post("/api/collection", new { card_id = "op01-001", delta = 2 });
        Assert.Equal("OP01-001", d.GetProperty("card_id").GetString());
    }

    [Fact]
    public async Task LogsACardNewerThanTheCatalogue()
    {
        await server.Reset();
        var (_, d) = await server.Post("/api/collection", new { card_id = "OP13-005", delta = 3 });
        Assert.Equal(3, d.GetProperty("qty").GetInt32());
        Assert.False(d.GetProperty("known").GetBoolean());
    }

    [Fact]
    public async Task AbsoluteQuantityOverwrites()
    {
        await server.Reset();
        await server.Log("OP01-016", 4);
        var (_, d) = await server.Post("/api/collection", new { card_id = "OP01-016", qty = 7 });
        Assert.Equal(7, d.GetProperty("qty").GetInt32());
    }

    [Fact]
    public async Task BulkLogsAPastedDeckList()
    {
        await server.Reset();
        var text = """
            Leader
            1x EB01-001 Monkey.D.Luffy

            Main Deck
            OP01-017
            4 OP01-016 Nami
            OP01-016_p1 x2
            2x OP99-999 Newer card
            """;

        var (status, d) = await server.Post("/api/collection/bulk",
                                            new { text, note = "starter deck" });
        Assert.Equal(200, status);
        Assert.Equal(10, d.GetProperty("added").GetInt32());
        Assert.Equal(5, d.GetProperty("unique").GetInt32());

        var byId = d.GetProperty("cards").EnumerateArray()
                    .ToDictionary(c => c.GetProperty("card_id").GetString()!);
        Assert.Equal(1, byId["EB01-001"].GetProperty("qty").GetInt32());
        Assert.Equal(1, byId["OP01-017"].GetProperty("qty").GetInt32());
        Assert.Equal(4, byId["OP01-016"].GetProperty("qty").GetInt32());
        Assert.Equal(2, byId["OP01-016_p1"].GetProperty("qty").GetInt32());
        Assert.False(byId["OP99-999"].GetProperty("known").GetBoolean());

        var s = await server.Get("/api/stats");
        Assert.Equal(10, s.GetProperty("total").GetInt32());
        Assert.Equal(5, s.GetProperty("unique").GetInt32());
    }

    [Fact]
    public async Task ClampsAtZeroInsteadOfGoingNegative()
    {
        await server.Reset();
        await server.Log("OP01-016_p1", 1);
        var (_, d) = await server.Post("/api/collection",
                                       new { card_id = "OP01-016_p1", delta = -99 });
        Assert.Equal(0, d.GetProperty("qty").GetInt32());

        var results = (await server.Get("/api/search?q=OP01-016"))
                      .GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(0, results[1].GetProperty("qty").GetInt32());  // drops out of counts
    }

    [Fact]
    public async Task RejectsARequestWithNoCardId()
    {
        var (status, _) = await server.Post("/api/collection", new { delta = 1 });
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task TotalsAndSetBreakdown()
    {
        await server.Reset();
        foreach (var (cid, n) in new[] { ("OP01-016", 4), ("OP01-001", 2),
                                         ("OP03-114_p1", 1), ("EB01-006", 3), ("OP13-005", 2) })
            await server.Log(cid, n);

        var s = await server.Get("/api/stats");
        Assert.Equal(12, s.GetProperty("total").GetInt32());
        Assert.Equal(5, s.GetProperty("unique").GetInt32());

        var sets = s.GetProperty("sets").EnumerateArray().ToArray();
        Assert.True(sets.Length >= 3, $"expected at least 3 sets, got {sets.Length}");
        var op01 = sets.Single(x => x.GetProperty("s").GetString() == "OP-01");
        Assert.Equal(154, op01.GetProperty("total").GetInt32());   // set size for completion
    }

    [Fact]
    public async Task LedgerListsWhatIsOwnedInNumberOrder()
    {
        await server.Reset();
        foreach (var (cid, n) in new[] { ("OP01-016", 4), ("OP01-001", 2),
                                         ("OP03-114_p1", 1), ("EB01-006", 3), ("OP13-005", 2) })
            await server.Log(cid, n);

        var cards = (await server.Get("/api/collection")).GetProperty("cards")
                    .EnumerateArray().ToArray();
        Assert.Equal(5, cards.Length);

        var ids = cards.Select(c => c.GetProperty("card_id").GetString()!).ToArray();
        Assert.Equal(ids.Order(StringComparer.Ordinal).ToArray(), ids);

        var unknown = cards.Single(c => c.GetProperty("card_id").GetString() == "OP13-005");
        Assert.Equal(2, unknown.GetProperty("qty").GetInt32());   // still carries its count
    }

    [Fact]
    public async Task SimultaneousIncrementsAllLand()
    {
        await server.Reset();
        await Task.WhenAll(Enumerable.Range(0, 40).Select(_ =>
            server.Post("/api/collection", new { card_id = "ST01-001", delta = 1 })));

        var q = (await server.Get("/api/search?q=ST01-001")).GetProperty("results")[0]
                .GetProperty("qty").GetInt32();
        Assert.Equal(40, q);
    }

    [Fact]
    public async Task InterleavedWritesAcrossCardsAllLand()
    {
        await server.Reset();
        var ids = new[] { "OP01-016", "OP02-013", "ST01-002" };
        await Task.WhenAll(Enumerable.Range(0, 30).Select(i =>
            server.Post("/api/collection", new { card_id = ids[i % 3], delta = 1 })));

        var s = await server.Get("/api/stats");
        Assert.Equal(30, s.GetProperty("total").GetInt32());
        Assert.Equal(3, s.GetProperty("unique").GetInt32());
    }

    [Fact]
    public async Task CountsSurviveARestart()
    {
        await server.Reset();
        foreach (var (cid, n) in new[] { ("OP01-016", 4), ("OP01-001", 2),
                                         ("OP03-114_p1", 1), ("EB01-006", 3), ("OP13-005", 2) })
            await server.Log(cid, n);

        await server.Restart();

        var s = await server.Get("/api/stats");
        Assert.Equal(12, s.GetProperty("total").GetInt32());
        // the catalogue is not rebuilt on a restart
        Assert.Equal(2627, (await server.Get("/api/health")).GetProperty("catalog").GetInt32());
    }

    [Fact]
    public async Task ExportsCsv()
    {
        await server.Reset();
        foreach (var (cid, n) in new[] { ("OP01-016", 4), ("OP01-001", 2),
                                         ("OP03-114_p1", 1), ("EB01-006", 3), ("OP13-005", 2) })
            await server.Log(cid, n);

        var (status, body) = await server.Raw("/api/export.csv");
        Assert.Equal(200, status);

        var lines = body.Trim().Split("\r\n");
        Assert.Equal(6, lines.Length);                              // header plus every row
        Assert.Equal(new[] { "card_id", "name", "set", "variant" }, lines[0].Split(',')[..4]);
        // lines[1] is EB01-006, first alphabetically, logged with qty 3 above.
        Assert.Equal("3", lines[1].Split(',')[7]);
    }
}
