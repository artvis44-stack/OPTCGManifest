using System.Text.Json;

namespace Manifest.Tests;

/// <summary>
/// The paged shape of /api/search, /api/collection and /api/decks: asked for by
/// sending a cursor (empty for the first page), answered with items and a
/// next_cursor that is null on the last page.
/// </summary>
[Collection("server")]
public class PaginationTests(ServerFixture server)
{
    async Task<List<JsonElement>> Walk(string path, int limit, Action<JsonElement>? afterFirst = null)
    {
        var all = new List<JsonElement>();
        string? cursor = "";
        var pages = 0;
        while (cursor is not null)
        {
            var sep = path.Contains('?') ? '&' : '?';
            var page = await server.Get($"{path}{sep}limit={limit}&cursor={Uri.EscapeDataString(cursor)}");
            var items = page.GetProperty("items").EnumerateArray().ToList();
            Assert.True(items.Count <= limit);
            all.AddRange(items);
            var next = page.GetProperty("next_cursor");
            cursor = next.ValueKind == JsonValueKind.Null ? null : next.GetString();
            if (cursor is not null) Assert.Equal(limit, items.Count);   // only the last page is short
            if (pages++ == 0) afterFirst?.Invoke(page);
            Assert.True(pages < 500, "pagination did not terminate");
        }
        return all;
    }

    static List<string> Ids(IEnumerable<JsonElement> rows) =>
        rows.Select(r => r.GetProperty("card_id").GetString()!).ToList();

    [Fact]
    public async Task PagesCoverExactlyWhatTheUnpagedSearchReturns()
    {
        var unpaged = Ids((await server.Get("/api/search?q=&category=Leader&limit=3000"))
                          .GetProperty("results").EnumerateArray());
        var paged = Ids(await Walk("/api/search?q=&category=Leader", 37));

        Assert.Equal(unpaged, paged);                   // same rows, same order
        Assert.Equal(paged.Count, paged.Distinct().Count());
    }

    [Theory]
    [InlineData("number")]
    [InlineData("colors")]
    [InlineData("set_label")]
    [InlineData("types")]
    [InlineData("price_gbp")]
    [InlineData("qty")]
    [InlineData("not_owned")]
    public async Task EverySortWalksEveryRowOnce(string sort)
    {
        await server.Reset();
        await server.Log("OP01-016", 3);
        await server.Log("OP01-001", 1);

        var expected = (await server.Get("/api/search?q=OP01&limit=3000"))
                       .GetProperty("results").GetArrayLength();
        var rows = await Walk($"/api/search?q=OP01&sort={sort}", 23);
        var ids = Ids(rows);

        Assert.Equal(expected, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());

        if (sort == "qty")
        {
            Assert.Equal("OP01-016", ids[0]);
            Assert.Equal("OP01-001", ids[1]);
        }
        if (sort == "not_owned")
            Assert.DoesNotContain(rows.Take(rows.Count - 2), r => r.GetProperty("qty").GetInt32() > 0);
        if (sort == "price_gbp")
        {
            var prices = rows.Select(r => r.GetProperty("price_gbp") is { ValueKind: JsonValueKind.Number } p
                ? p.GetDouble() : -1).ToList();
            Assert.Equal(prices.OrderByDescending(p => p).ToList(), prices);
        }
    }

    [Fact]
    public async Task ARowDeletedMidScrollDoesNotSkipAnother()
    {
        await server.Reset();
        var logged = new[] { "OP01-001", "OP01-002", "OP01-003", "OP01-004", "OP01-005",
                             "OP01-006", "OP01-007", "OP01-008", "OP01-009", "OP01-010" };
        foreach (var id in logged) await server.Log(id, 1);

        var seen = Ids(await Walk("/api/collection", 3, afterFirst: page =>
        {
            // Remove a card already shown, the way an offset would be thrown by.
            var first = page.GetProperty("items")[0].GetProperty("card_id").GetString();
            server.Post("/api/collection", new { card_id = first, qty = 0 }).GetAwaiter().GetResult();
        }));

        Assert.Equal(logged, seen);
    }

    [Fact]
    public async Task CollectionFiltersAndSortsOnTheServer()
    {
        await server.Reset();
        await server.Log("OP01-016", 2);      // Nami, Red Character
        await server.Log("OP01-001", 5);      // Leader
        await server.Log("OP99-999", 1);      // not in the catalogue

        Assert.Equal(new[] { "OP01-001", "OP01-016", "OP99-999" }, Ids(await Walk("/api/collection", 2)));
        Assert.Equal(new[] { "OP01-001", "OP01-016", "OP99-999" },
                     Ids(await Walk("/api/collection?sort=qty", 2)));
        Assert.Equal(new[] { "OP01-016" }, Ids(await Walk("/api/collection?q=nami", 5)));
        Assert.Equal(new[] { "OP01-001" }, Ids(await Walk("/api/collection?category=Leader", 5)));
        Assert.Equal(new[] { "OP99-999" }, Ids(await Walk("/api/collection?q=op99", 5)));

        var stats = await server.Get("/api/collection/stats");
        Assert.Equal(8, stats.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task NotOwnedAnyColourAndNoLeadersAreServerSideFilters()
    {
        await server.Reset();
        await server.Log("OP01-016", 1);

        var rows = await Walk("/api/search?q=OP01&owned=0&colors=Red,Green&exclude_category=Leader", 50);
        Assert.NotEmpty(rows);
        Assert.DoesNotContain(rows, r => r.GetProperty("card_id").GetString() == "OP01-016");
        Assert.All(rows, r =>
        {
            Assert.Equal(0, r.GetProperty("qty").GetInt32());
            Assert.NotEqual("LEADER", r.GetProperty("category").GetString()!.ToUpperInvariant());
            var colors = r.GetProperty("colors").GetString()!;
            Assert.True(colors.Contains("Red") || colors.Contains("Green"), colors);
        });

        var owned = Ids(await Walk("/api/search?q=OP01&owned=1", 50));
        Assert.Equal(new[] { "OP01-016" }, owned);
    }

    [Fact]
    public async Task DecksPageNewestFirst()
    {
        var made = new List<long>();
        for (var i = 0; i < 5; i++)
        {
            var (_, d) = await server.Post("/api/decks", new { name = $"Paging {i}", leader_card_id = "EB01-001" });
            made.Add(d.GetProperty("deck").GetProperty("id").GetInt64());
        }
        try
        {
            var ids = (await Walk("/api/decks", 2)).Select(d => d.GetProperty("id").GetInt64()).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            var ours = ids.Where(made.Contains).ToList();
            Assert.Equal(Enumerable.Reverse(made).ToList(), ours);
        }
        finally
        {
            foreach (var id in made) await server.Post($"/api/decks/{id}/delete", new { });
        }
    }

    [Theory]
    [InlineData("/api/search?q=&cursor=not-a-cursor")]
    [InlineData("/api/search?q=&sort=bogus&cursor=")]
    public async Task RefusesWhatItCannotRead(string path) =>
        Assert.Equal(400, (await server.Raw(path)).Status);

    [Fact]
    public async Task RefusesACursorFromAnotherSort()
    {
        var page = await server.Get("/api/search?q=OP01&sort=colors&limit=2&cursor=");
        var cursor = page.GetProperty("next_cursor").GetString()!;
        var (status, _) = await server.Raw($"/api/search?q=OP01&sort=number&cursor={Uri.EscapeDataString(cursor)}");
        Assert.Equal(400, status);
    }
}
