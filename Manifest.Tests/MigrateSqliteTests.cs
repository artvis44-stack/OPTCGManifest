using Manifest.Data;
using Manifest.Tools;

namespace Manifest.Tests;

/// <summary>
/// `manifest migrate-sqlite`, run in-process: a manifest.db is built through the
/// repositories, moved, and then read back through the same repositories pointed at
/// PostgreSQL. Needs MANIFEST_TEST_POSTGRES, and skips without it.
/// </summary>
public sealed class MigrateSqliteTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("manifest-migrate-").FullName;
    readonly List<string> _databases = new();

    AppPaths Paths => new(_root);

    public MigrateSqliteTests() =>
        File.Copy(Path.Combine(ServerFixture.RepoRoot, "catalog.json"), Path.Combine(_root, "catalog.json"));

    public void Dispose()
    {
        foreach (var name in _databases) ServerFixture.DropTestDatabase(name);
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    string FreshTarget()
    {
        var (name, url) = ServerFixture.CreateTestDatabase();
        _databases.Add(name!);
        return url!;
    }

    Database Source()
    {
        var db = new Database(Paths, "sqlite://manifest.db");
        Assert.Null(db.Initialise(false));
        return db;
    }

    int Migrate(string target, params string[] extra) =>
        SqliteToPostgres.Run(new[] { "--postgres", target }.Concat(extra).ToArray(), Paths);

    [SkippableFact]
    public void EverythingArrivesAndStillWorks()
    {
        Skip.If(ServerFixture.PostgresAdminUrl is null, "MANIFEST_TEST_POSTGRES is not set");

        var source = Source();
        var users = new UserRepository(source);
        var alice = users.Create("Alice", "correct-horse-battery");
        var bob = users.Create("bob", "correct-horse-battery", "bob@example.com");
        var session = users.StartSession(bob.Id);

        var binders = new BinderRepository(source);
        var cards = new CardRepository(source);
        cards.Adjust(binders.Personal(alice.Id), alice.Id, "OP01-016", 4, null, "binder");
        cards.Adjust(binders.Personal(bob.Id), bob.Id, "OP01-016_p1", 1, null, null);
        var ours = binders.Create(alice.Id, "Ours", moveMine: false);
        binders.AddMember(alice.Id, ours.Id, "bob");
        cards.Adjust(ours.Id, bob.Id, "OP01-001", 3, null, null);
        cards.LogScan(bob.Id, "OP01-016", "ok");

        var decks = new DeckRepository(source);
        var deck = decks.Create(bob.Id, "Bob's deck", "EB01-001");
        decks.SetCard(bob.Id, deck.Id, "OP01-016", 4);

        var access = new AccessRepository(source);
        var filed = access.Submit("new@example.com", "hi", null);
        var (_, invite) = access.Approve(filed.Request!.Id, "Alice")!.Value;

        // Two copies from before accounts, one of a card Alice already has.
        using (var conn = source.Open())
            conn.Exec("INSERT INTO collection (binder_id, card_id, qty) VALUES (0, 'OP01-016', 2), "
                      + "(0, 'OP01-001', 1)");

        var target = FreshTarget();
        Assert.Equal(0, Migrate(target, "--owner", "alice"));

        var pg = new Database(Paths, target);
        var pgUsers = new UserRepository(pg);
        Assert.Equal(alice.Id, pgUsers.Authenticate("alice", "correct-horse-battery")?.Id);
        Assert.Equal(bob.Id, pgUsers.ForToken(session)?.Id);

        var pgCards = new CardRepository(pg);
        var pgBinders = new BinderRepository(pg);
        var aliceOwn = BinderScope.One(pgBinders.Personal(alice.Id));
        var aliceHas = pgCards.Collection(aliceOwn).ToDictionary(r => r.CardId, r => r.Qty);
        Assert.Equal(6, aliceHas["OP01-016"]);     // 4 of her own + 2 unclaimed
        Assert.Equal(1, aliceHas["OP01-001"]);
        Assert.Equal("binder", pgCards.Collection(aliceOwn).Single(r => r.CardId == "OP01-016").Note);

        // The shared binder came across with both of them in it, and who added what.
        var shared = pgCards.Collection(BinderScope.One(ours.Id)).Single();
        Assert.Equal(3, shared.Qty);
        Assert.Equal("bob", shared.AddedBy);
        Assert.Equal(new[] { "Alice", "bob" }, pgBinders.Get(alice.Id, ours.Id)!.Members);
        var aliceCanUse = pgCards.Collection(BinderScope.Usable(alice.Id)).ToDictionary(r => r.CardId, r => r.Qty);
        Assert.Equal(4, aliceCanUse["OP01-001"]);  // 1 of her own + 3 shared
        Assert.Equal(cards.CatalogCount(), pgCards.CatalogCount());

        var pgDeck = new DeckRepository(pg).Detail(bob.Id, deck.Id);
        Assert.NotNull(pgDeck);
        Assert.Equal(4, pgDeck!.Total);

        // The link already in the applicant's mailbox still works.
        Assert.NotNull(new AccessRepository(pg).ByInviteToken(invite));

        // The next account is not handed an id that was copied in, nor its binder.
        var carol = pgUsers.Create("carol", "correct-horse-battery");
        Assert.True(carol.Id > bob.Id);
        Assert.True(pgBinders.Personal(carol.Id) > ours.Id);
        Assert.False(carol.IsOwner);
    }

    [SkippableFact]
    public void AsksWhoOwnsRowsThatBelongToNobody()
    {
        Skip.If(ServerFixture.PostgresAdminUrl is null, "MANIFEST_TEST_POSTGRES is not set");

        var source = Source();
        new UserRepository(source).Create("alice", "correct-horse-battery");
        using (var conn = source.Open())
            conn.Exec("INSERT INTO collection (binder_id, card_id, qty) VALUES (0, 'OP01-016', 2)");

        var target = FreshTarget();
        Assert.Equal(1, Migrate(target));
        Assert.Equal(1, Migrate(target, "--owner", "nobody-by-this-name"));

        // Neither refusal left anything behind.
        Assert.Equal(0, new UserRepository(new Database(Paths, target)).Count());
    }

    [SkippableFact]
    public void RefusesATargetThatIsAlreadyInUse()
    {
        Skip.If(ServerFixture.PostgresAdminUrl is null, "MANIFEST_TEST_POSTGRES is not set");

        new UserRepository(Source()).Create("alice", "correct-horse-battery");
        var target = FreshTarget();
        Assert.Equal(0, Migrate(target));
        Assert.Equal(1, Migrate(target));
        Assert.Equal(1, new UserRepository(new Database(Paths, target)).Count());
    }

    /// <summary>The source is read from a copy: the original comes out byte for byte as it went in.</summary>
    [SkippableFact]
    public void LeavesTheSourceFileUntouched()
    {
        Skip.If(ServerFixture.PostgresAdminUrl is null, "MANIFEST_TEST_POSTGRES is not set");

        new UserRepository(Source()).Create("alice", "correct-horse-battery");
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var file = Path.Combine(_root, "manifest.db");
        // As old a file as can be: no WAL, so the move would have to change it to use one.
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={file}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=DELETE";
            cmd.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var before = File.ReadAllBytes(file);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        try
        {
            Assert.Equal(0, Migrate(FreshTarget()));
            Assert.Equal(before, File.ReadAllBytes(file));
            Assert.False(File.Exists(file + "-wal"));
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    [Fact]
    public void RefusesAFileThatIsNotThere()
    {
        Assert.Equal(1, SqliteToPostgres.Run(
            new[] { "--sqlite", Path.Combine(_root, "missing.db"), "--postgres", "postgres://u:p@localhost/x" },
            Paths));
        Assert.False(File.Exists(Path.Combine(_root, "missing.db")));
    }
}
