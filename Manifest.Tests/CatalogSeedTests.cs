using Manifest.Data;
using Manifest.Models;
using Microsoft.Data.Sqlite;

namespace Manifest.Tests;

/// <summary>
/// A catalogue seeded before the Trigger, attribute and block columns existed: the
/// columns are added, and the rows refilled from catalog.json once - only when the
/// file has the Trigger text to fill them with.
/// </summary>
public sealed class CatalogSeedTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("manifest-seed-").FullName;

    AppPaths Paths => new(_root);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    void OldCatalogue(string name)
    {
        using var conn = new SqliteConnection($"Data Source={Paths.DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE catalog (
                card_id TEXT PRIMARY KEY, base_id TEXT NOT NULL,
                variant TEXT NOT NULL DEFAULT '', name TEXT NOT NULL,
                set_label TEXT, set_name TEXT, rarity TEXT, category TEXT, colors TEXT,
                cost INTEGER, power INTEGER, counter INTEGER, types TEXT, effect TEXT,
                image_url TEXT
            );
            INSERT INTO catalog (card_id, base_id, name) VALUES ('OP01-028', 'OP01-028', @name);
            """;
        cmd.Parameters.AddWithValue("@name", name);
        cmd.ExecuteNonQuery();
    }

    void CatalogJson(string? trigger) => File.WriteAllText(Paths.Catalog, $$"""
        [{"card_id": "OP01-028", "base_id": "OP01-028", "variant": "",
          "name": "Green Star Rafflesia", "category": "EVENT", "attributes": "",
          "effect": "[Counter] Give up to 1 of your opponent's Leader or Character cards −2000 power during this turn.",
          "trigger": {{(trigger is null ? "null" : $"\"{trigger}\"")}}},
         {"card_id": "OP01-004", "base_id": "OP01-004", "variant": "", "name": "Usopp",
          "category": "CHARACTER", "attributes": "Ranged", "block_icon": 1}]
        """);

    CatalogRow? Row(Database db, string id)
    {
        using var conn = db.Open();
        return CardRepository.CatalogRowById(conn, id);
    }

    [Fact]
    public void RefillsACatalogueThatPredatesTriggers()
    {
        OldCatalogue("stale");
        CatalogJson("[Trigger] Activate this card's [Counter] effect.");

        var db = new Database(Paths, "sqlite://manifest.db");
        Assert.Null(db.Initialise(false));

        var rafflesia = Row(db, "OP01-028")!;
        Assert.Equal("Green Star Rafflesia", rafflesia.Name);
        Assert.Equal("[Trigger] Activate this card's [Counter] effect.", rafflesia.Trigger);
        var usopp = Row(db, "OP01-004")!;
        Assert.Equal("Ranged", usopp.Attributes);
        Assert.Equal(1, usopp.BlockIcon);
    }

    [Fact]
    public void LeavesItAloneWhenCatalogJsonHasNoTriggersEither()
    {
        OldCatalogue("stale");
        CatalogJson(null);

        var db = new Database(Paths, "sqlite://manifest.db");
        Assert.Null(db.Initialise(false));

        Assert.Equal("stale", Row(db, "OP01-028")!.Name);
        Assert.Null(Row(db, "OP01-004"));
    }

    [Fact]
    public void AnExplicitReseedStillNeedsCatalogJson()
    {
        OldCatalogue("stale");

        var db = new Database(Paths, "sqlite://manifest.db");
        Assert.NotNull(db.Initialise(true));
        Assert.Null(db.Initialise(false));      // a plain start keeps what it has
    }
}
