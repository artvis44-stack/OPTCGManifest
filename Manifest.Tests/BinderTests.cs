using System.Net;
using System.Text;
using System.Text.Json;
using Manifest.Data;
using Microsoft.Data.Sqlite;

namespace Manifest.Tests;

/// <summary>
/// Shared binders, the switch that shows your own binder to the people you share
/// with, and moving cards between binders. Each test makes its own accounts, so
/// what one test shares cannot leak into another.
/// </summary>
[Collection("server")]
public class BinderTests(ServerFixture server)
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
        return (response.StatusCode, text.Length > 0 ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }

    static async Task<string> Name(HttpClient client) =>
        (await Get(client, "/api/session")).Body.GetProperty("user").GetProperty("username").GetString()!;

    static async Task<JsonElement> Binders(HttpClient client) =>
        (await Get(client, "/api/binders")).Body.GetProperty("binders");

    static async Task<long> Mine(HttpClient client) =>
        (await Binders(client)).EnumerateArray().Single(b => b.GetProperty("mine").GetBoolean())
                               .GetProperty("id").GetInt64();

    static async Task<long> Shared(HttpClient owner, HttpClient member, string name = "Ours")
    {
        var (status, made) = await Post(owner, "/api/binders", new { name });
        Assert.Equal(HttpStatusCode.OK, status);
        var id = made.GetProperty("binder").GetProperty("id").GetInt64();
        var (added, _) = await Post(owner, $"/api/binders/{id}/members", new { username = await Name(member) });
        Assert.Equal(HttpStatusCode.OK, added);
        return id;
    }

    static async Task<Dictionary<string, JsonElement>> Cards(HttpClient client, string binder)
    {
        var (status, body) = await Get(client, $"/api/collection?binder={binder}");
        Assert.Equal(HttpStatusCode.OK, status);
        return body.GetProperty("cards").EnumerateArray()
                   .ToDictionary(c => c.GetProperty("card_id").GetString()!);
    }

    [Fact]
    public async Task EveryAccountStartsWithItsOwnBinder()
    {
        using var alice = await Account("alice");
        var binders = await Binders(alice);
        var mine = Assert.Single(binders.EnumerateArray());
        Assert.Equal("Mine", mine.GetProperty("name").GetString());
        Assert.Equal("personal", mine.GetProperty("kind").GetString());
        Assert.True(mine.GetProperty("writable").GetBoolean());

        // No ?binder= means your own binder, as every route did before binders.
        await Post(alice, "/api/collection", new { card_id = "OP01-016", delta = 2 });
        Assert.Equal(2, (await Cards(alice, (await Mine(alice)).ToString()))["OP01-016"]
                        .GetProperty("qty").GetInt32());
    }

    [Fact]
    public async Task BothMembersReadAndWriteASharedBinder()
    {
        using var alice = await Account("alice");
        using var bob = await Account("bob");
        var ours = await Shared(alice, bob);

        var (status, logged) = await Post(bob, $"/api/collection?binder={ours}",
                                          new { card_id = "OP01-016", delta = 3 });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(3, logged.GetProperty("qty").GetInt32());
        await Post(alice, $"/api/collection?binder={ours}", new { card_id = "OP01-016", delta = 1 });

        var seen = (await Cards(alice, ours.ToString()))["OP01-016"];
        Assert.Equal(4, seen.GetProperty("qty").GetInt32());
        Assert.Equal(await Name(bob), seen.GetProperty("added_by").GetString());

        var stats = (await Get(bob, $"/api/stats?binder={ours}")).Body;
        Assert.Equal(4, stats.GetProperty("total").GetInt32());

        // Neither person's own binder was touched.
        Assert.Empty(await Cards(alice, (await Mine(alice)).ToString()));
        Assert.Empty(await Cards(bob, (await Mine(bob)).ToString()));

        var listed = (await Binders(bob)).EnumerateArray().Single(b => b.GetProperty("id").GetInt64() == ours);
        Assert.Equal("Ours", listed.GetProperty("name").GetString());
        Assert.Equal(2, listed.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task SomeoneOutsideTheBinderCannotTellItExists()
    {
        using var alice = await Account("alice");
        using var bob = await Account("bob");
        using var carol = await Account("carol");
        var ours = await Shared(alice, bob);
        await Post(alice, $"/api/collection?binder={ours}", new { card_id = "OP01-016", delta = 1 });

        Assert.Equal(HttpStatusCode.NotFound, (await Get(carol, $"/api/collection?binder={ours}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(carol, $"/api/export.csv?binder={ours}")).Status);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Post(carol, $"/api/collection?binder={ours}", new { card_id = "OP01-016", delta = 5 })).Status);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Post(carol, $"/api/binders/{ours}/members", new { username = await Name(carol) })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(carol, $"/api/binders/{ours}/delete", new { })).Status);

        // Nor someone else's own binder.
        var bobs = await Mine(bob);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(alice, $"/api/collection?binder={bobs}")).Status);

        Assert.Equal(1, (await Cards(alice, ours.ToString()))["OP01-016"].GetProperty("qty").GetInt32());
    }

    [Fact]
    public async Task ANewBinderCanStartWithEverythingFromYourOwn()
    {
        using var alice = await Account("alice");
        await Post(alice, "/api/collection", new { card_id = "OP01-016", delta = 4 });
        await Post(alice, "/api/collection", new { card_id = "OP01-001", delta = 1 });

        var (_, made) = await Post(alice, "/api/binders", new { name = "Ours", move_mine = true });
        var ours = made.GetProperty("binder").GetProperty("id").GetInt64();

        Assert.Empty(await Cards(alice, (await Mine(alice)).ToString()));
        var moved = await Cards(alice, ours.ToString());
        Assert.Equal(4, moved["OP01-016"].GetProperty("qty").GetInt32());
        Assert.Equal(1, moved["OP01-001"].GetProperty("qty").GetInt32());
    }

    [Fact]
    public async Task AllAddsYourBindersTogetherAndCannotBeWritten()
    {
        using var alice = await Account("alice");
        using var bob = await Account("bob");
        var ours = await Shared(alice, bob);
        await Post(alice, "/api/collection", new { card_id = "OP01-016", delta = 1 });
        await Post(bob, $"/api/collection?binder={ours}", new { card_id = "OP01-016", delta = 2 });
        await Post(bob, "/api/collection", new { card_id = "OP01-016", delta = 10 });   // bob's own

        var all = await Cards(alice, "all");
        Assert.Equal(3, all["OP01-016"].GetProperty("qty").GetInt32());
        Assert.Equal(3, (await Get(alice, "/api/stats?binder=all")).Body.GetProperty("total").GetInt32());

        var (status, _) = await Post(alice, "/api/collection?binder=all", new { card_id = "OP01-016", delta = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, status);

        // A deck counts what its owner can use, from every binder they are in.
        var (_, deck) = await Post(alice, "/api/decks", new { name = "d", leader_card_id = "OP01-001" });
        var id = deck.GetProperty("deck").GetProperty("id").GetInt64();
        var (_, filled) = await Post(alice, $"/api/decks/{id}/card", new { card_id = "OP01-016", qty = 4 });
        var row = filled.GetProperty("deck").GetProperty("cards").EnumerateArray()
                        .Single(c => c.GetProperty("card_id").GetString() == "OP01-016");
        Assert.Equal(3, row.GetProperty("owned_qty").GetInt32());
    }

    [Fact]
    public async Task CardsMoveBetweenBinders()
    {
        using var alice = await Account("alice");
        using var bob = await Account("bob");
        var ours = await Shared(alice, bob);
        var mine = await Mine(alice);
        await Post(alice, "/api/collection", new { card_id = "OP01-016", delta = 3 });

        var (status, moved) = await Post(alice, "/api/binders/move",
            new { card_id = "op01-016", from = mine, to = ours, qty = 2 });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, moved.GetProperty("from_qty").GetInt32());
        Assert.Equal(2, moved.GetProperty("to_qty").GetInt32());

        // Asking for more than there is moves what there is.
        (_, moved) = await Post(alice, "/api/binders/move",
            new { card_id = "OP01-016", from = mine, to = ours, qty = 9 });
        Assert.Equal(0, moved.GetProperty("from_qty").GetInt32());
        Assert.Equal(3, moved.GetProperty("to_qty").GetInt32());
        Assert.Empty(await Cards(alice, mine.ToString()));

        (status, _) = await Post(alice, "/api/binders/move",
            new { card_id = "OP01-016", from = mine, to = ours, qty = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, status);

        // Not into a binder you only get to look at.
        var bobs = await Mine(bob);
        (status, _) = await Post(alice, "/api/binders/move",
            new { card_id = "OP01-016", from = ours, to = bobs, qty = 1 });
        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task YourOwnBinderIsShownOnlyWhenYouSaySo()
    {
        using var alice = await Account("alice");
        using var bob = await Account("bob");
        using var carol = await Account("carol");
        await Shared(alice, bob);
        await Post(bob, "/api/collection", new { card_id = "OP01-016", delta = 2 });
        var bobs = await Mine(bob);

        Assert.Equal(HttpStatusCode.NotFound, (await Get(alice, $"/api/collection?binder={bobs}")).Status);
        Assert.DoesNotContain((await Binders(alice)).EnumerateArray(), b => b.GetProperty("id").GetInt64() == bobs);

        var (status, _) = await Post(bob, "/api/binders/visibility", new { visible = true });
        Assert.Equal(HttpStatusCode.OK, status);

        var shown = (await Binders(alice)).EnumerateArray().Single(b => b.GetProperty("id").GetInt64() == bobs);
        Assert.False(shown.GetProperty("writable").GetBoolean());
        Assert.Equal($"{await Name(bob)}'s cards", shown.GetProperty("name").GetString());
        Assert.Equal(2, (await Cards(alice, bobs.ToString()))["OP01-016"].GetProperty("qty").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Post(alice, $"/api/collection?binder={bobs}", new { card_id = "OP01-016", delta = 1 })).Status);

        // Only to people bob shares a binder with, and not counted in "all".
        Assert.Equal(HttpStatusCode.NotFound, (await Get(carol, $"/api/collection?binder={bobs}")).Status);
        Assert.Empty(await Cards(alice, "all"));

        await Post(bob, "/api/binders/visibility", new { visible = false });
        Assert.Equal(HttpStatusCode.NotFound, (await Get(alice, $"/api/collection?binder={bobs}")).Status);
    }

    [Fact]
    public async Task ABinderGoesWhenItsLastMemberLeaves()
    {
        using var alice = await Account("alice");
        using var bob = await Account("bob");
        var ours = await Shared(alice, bob);
        await Post(alice, $"/api/collection?binder={ours}", new { card_id = "OP01-016", delta = 1 });

        var (status, _) = await Post(alice, $"/api/binders/{ours}/members/{await Name(alice)}/delete", new { });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(alice, $"/api/collection?binder={ours}")).Status);
        Assert.Equal(1, (await Cards(bob, ours.ToString()))["OP01-016"].GetProperty("qty").GetInt32());

        (status, _) = await Post(bob, $"/api/binders/{ours}/members/{await Name(bob)}/delete", new { });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(bob, $"/api/collection?binder={ours}")).Status);

        // Your own binder is not something you can leave, rename or delete.
        var mine = await Mine(bob);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(bob, $"/api/binders/{mine}/delete", new { })).Status);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Post(bob, $"/api/binders/{mine}", new { name = "renamed" })).Status);
    }

    [Fact]
    public async Task TwoPeopleLoggingTheSameCardAtOnceBothCount()
    {
        using var alice = await Account("alice");
        using var bob = await Account("bob");
        var ours = await Shared(alice, bob);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            Post(i % 2 == 0 ? alice : bob, $"/api/collection?binder={ours}",
                 new { card_id = "OP01-016", delta = 1 })));

        Assert.Equal(20, (await Cards(alice, ours.ToString()))["OP01-016"].GetProperty("qty").GetInt32());
    }

    /// <summary>
    /// A manifest.db from before binders: each account's cards land in its own
    /// binder, and cards from before accounts still go to whoever signs up first.
    /// </summary>
    [Fact]
    public void AnOlderDatabaseMovesIntoBinders()
    {
        var root = Directory.CreateTempSubdirectory("manifest-binders-").FullName;
        try
        {
            var file = Path.Combine(root, "manifest.db");
            using (var conn = new SqliteConnection($"Data Source={file};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE users (id INTEGER PRIMARY KEY AUTOINCREMENT,
                        username TEXT NOT NULL COLLATE NOCASE UNIQUE, password_hash TEXT NOT NULL,
                        email TEXT COLLATE NOCASE, created_at TEXT NOT NULL DEFAULT (datetime('now')),
                        last_seen TEXT);
                    CREATE TABLE collection (user_id INTEGER NOT NULL, card_id TEXT NOT NULL,
                        qty INTEGER NOT NULL CHECK (qty >= 0), note TEXT NOT NULL DEFAULT '',
                        added_at TEXT NOT NULL DEFAULT (datetime('now')),
                        updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                        PRIMARY KEY (user_id, card_id));
                    INSERT INTO users (id, username, password_hash) VALUES (2, 'bob', 'x');
                    INSERT INTO collection (user_id, card_id, qty) VALUES
                        (2, 'OP01-016', 3), (0, 'OP01-001', 1);
                    """;
                cmd.ExecuteNonQuery();
            }

            var paths = new AppPaths(root);
            var db = new Database(paths, "sqlite://manifest.db");
            db.EnsureSchema();
            var binders = new BinderRepository(db);
            var cards = new CardRepository(db);

            var bobs = cards.Collection(BinderScope.One(binders.Personal(2))).Single();
            Assert.Equal(("OP01-016", 3, "bob"), (bobs.CardId, bobs.Qty, bobs.AddedBy));

            // The schema step is safe to run again.
            db.EnsureSchema();
            Assert.Single(cards.Collection(BinderScope.Usable(2)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
