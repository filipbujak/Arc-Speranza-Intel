using ArcSperanzaIntel.Core.Api;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Workshop;
using Xunit;

namespace ArcSperanzaIntel.Tests;

public class WorkshopTests
{
    [Theory]
    [InlineData("Gunsmith III", "weapon-bench", 3)]
    [InlineData("Weapon Bench 2", "weapon-bench", 2)]
    [InlineData("Explosive Bench 1 or Basic Bench", "explosives-bench", 1)]
    [InlineData("Med Stations 1", "med-station", 1)]
    [InlineData("Utility Bench I ", "utility-bench", 1)]
    [InlineData("Gear Bench III", "equipment-bench", 3)]
    [InlineData("Refiner", "refiner", null)]
    public void Workbench_text_maps_to_station_and_level(string text, string station, int? level)
    {
        var r = WorkshopBook.ParseWorkbench(text);
        Assert.NotNull(r);
        Assert.Equal(station, r!.Value.StationId);
        Assert.Equal(level, r.Value.Level);
    }

    [Fact]
    public void Basic_bench_has_no_level_and_unknown_is_null()
    {
        Assert.Equal(Station.BasicId, WorkshopBook.ParseWorkbench("Basic Bench or Med Station 1")!.Value.StationId);
        Assert.Null(WorkshopBook.ParseWorkbench("Something Else"));
        Assert.Null(WorkshopBook.ParseWorkbench(null));
    }

    private const string Hideout = """
        [{"id":"weapon-bench","name":"Gunsmith","maxLevel":3,"levels":{
          "1":{"unlockRequirements":[{"id":"rounds-played","quantity":3}],"requiredItems":[{"id":"metal-parts","quantity":20}],"crafts":[]},
          "2":{"requiredItems":[{"id":"wasp-driver","quantity":8}],"crafts":["anvil-ii"]},
          "3":{"requiredItems":[{"id":"wasp-driver","quantity":2}],"crafts":[]}}}]
        """;

    private static (WorkshopBook Book, UserState State) Make()
    {
        var stations = MetaForgeParser.ParseHideout(Hideout);
        var items = new List<Item>
        {
            new() { Id = "metal-parts", Name = "Metal Parts" },
            new() { Id = "wasp-driver", Name = "Wasp Driver" },
            new() { Id = "anvil-ii", Name = "Anvil II", Type = "Weapon" },
            new() { Id = "anvil-ii-blueprint", Name = "Anvil II Blueprint", Type = "Blueprint" },
            new() { Id = "ferro", Name = "Ferro", Type = "Weapon", Workbench = "Gunsmith 1" },
        };
        var state = new UserState();
        return (new WorkshopBook(stations, items, state), state);
    }

    [Fact]
    public void Levels_next_requirements_and_needs()
    {
        var (book, _) = Make();
        var gun = book.Get("weapon-bench")!;
        Assert.Equal(0, book.LevelOf(gun));
        Assert.Equal(1, book.NextLevel(gun)!.Level);
        Assert.Equal(2, book.NeededFor("wasp-driver").Count);
        book.SetLevel(gun, 1);
        Assert.Equal(2, book.NextLevel(gun)!.Level);

        book.SetLevel(gun, 3);
        Assert.True(book.IsMaxed(gun));
        Assert.Null(book.NextLevel(gun));
        Assert.Empty(book.NeededFor("wasp-driver"));
        Assert.Equal("rounds-played", gun.Level(1)!.Conditions[0].Id);
    }

    [Fact]
    public void Crafts_and_blueprints_are_linked()
    {
        var (book, state) = Make();
        var crafts = book.CraftsAt("weapon-bench");
        Assert.Contains(crafts, c => c.Item.Id == "anvil-ii" && c.Level == 2);
        Assert.Contains(crafts, c => c.Item.Id == "ferro" && c.Level == 1);
        Assert.Equal("anvil-ii-blueprint", book.BlueprintFor("anvil-ii")!.Id);
        Assert.Equal("anvil-ii", book.CraftedBy("anvil-ii-blueprint")!.Id);

        book.SetFound("anvil-ii-blueprint", true);
        Assert.Contains("anvil-ii-blueprint", state.FoundBlueprints);
        Assert.Equal(1, book.FoundCount);
    }
}

public class ItemComponentsTests
{
    [Fact]
    public void Components_and_recycling_are_parsed()
    {
        const string json = """
            [{"id":"defibrillator","name":"Defibrillator","item_type":"Quick Use",
              "components":[{"quantity":3,"component":{"id":"plastic-parts","name":"Plastic Parts","rarity":"Common"}}],
              "recycle_components":[{"quantity":1,"component":{"id":"moss","name":"Moss"}}]}]
            """;
        var item = MetaForgeParser.ParseItems(json)[0];
        Assert.Equal("plastic-parts", item.Components[0].ItemId);
        Assert.Equal(3, item.Components[0].Quantity);
        Assert.Equal("Moss", item.RecycleComponents[0].Name);
    }
}

public class RelationshipsTests
{
    // Skrót prawdziwego kształtu MetaForge (październik 2026): relationships.recipes / blueprintUnlocks / operations
    private const string Items = """
        [
          {"id":"bettina-blueprint","name":"Bettina Blueprint","item_type":"Blueprint","rarity":"Legendary","workbench":"Gunsmith III",
           "relationships":{"recipes":[{"station":"Research Station Level 3","outputQuantity":1,
              "ingredients":[{"item":{"id":"research-points","name":"Research Points"},"quantity":4500}],"blueprints":[]}],
             "blueprintUnlocks":[{"id":"bettina-i","name":"Bettina I"}]}},
          {"id":"bettina-i","name":"Bettina I","item_type":"Weapon","rarity":"Epic","workbench":"Gunsmith III",
           "relationships":{
             "recipes":[{"station":"Gunsmith Level 3","outputQuantity":1,
                "ingredients":[{"item":{"id":"canister","name":"Canister"},"quantity":3}],
                "blueprints":[{"id":"bettina-blueprint","name":"Bettina Blueprint"}]}],
             "operations":[
               {"id":"1","kind":"salvage","inputs":[{"item":{"id":"bettina-i"},"quantity":1}],"outputs":[{"item":{"id":"advanced-mechanical-components","name":"Advanced Mechanical Components"},"quantity":1}],"conditions":[]},
               {"id":"2","kind":"upgrade","inputs":[{"item":{"id":"heavy-gun-parts"},"quantity":2}],"outputs":[{"item":{"id":"bettina-ii"},"quantity":1}],"conditions":[]},
               {"id":"3","kind":"round-production","inputs":[],"outputs":[{"item":{"id":"x"},"quantity":1}],"conditions":[]}]}},
          {"id":"bettina-ii","name":"Bettina II","item_type":"Weapon","rarity":"Epic","workbench":"Weapon Bench",
           "relationships":{"operations":[{"id":"4","kind":"upgrade","inputs":[{"item":{"id":"heavy-gun-parts"},"quantity":2}],"outputs":[{"item":{"id":"bettina-iii"},"quantity":1}]}]}},
          {"id":"bettina-iii","name":"Bettina III","item_type":"Weapon","rarity":"Epic","workbench":"Weapon Bench"},
          {"id":"rubber-parts-recipe","name":"Rubber Parts","item_type":"Basic Material",
           "recycle_from":[{"item":{"id":"tire","name":"Tire"},"quantity":2}]},
          {"id":"looting-mk-3-survivor","name":"Looting Mk. 3 (Survivor)","item_type":"Augment","workbench":null,
           "relationships":{"recipes":[{"station":"Gear Bench Level 3","outputQuantity":1,"ingredients":[],"blueprints":[]}]}},
          {"id":"anvil","name":"Anvil","item_type":"Weapon"},
          {"id":"anvil-recipe","name":"Anvil Blueprint","item_type":"Blueprint"},
          {"id":"looting-mk-3-survivor-blueprint","name":"Looting Mk. 3 (Survivor) Blueprint","item_type":"Blueprint",
           "relationships":{"blueprintUnlocks":[{"id":"looting-mk-3-survivor"}]}}
        ]
        """;

    private const string Hideout = """
        [{"id":"scrappy","name":"Scrappy","maxLevel":6,"levels":{
            "1":{"requiredItems":[]},"2":{"requiredItems":[{"id":"dog-collar","quantity":1}]},
            "3":{"requiredItems":[{"id":"lemon","quantity":3}]},"4":{"requiredItems":[{"id":"olives","quantity":6}]},
            "5":{"requiredItems":[{"id":"mushroom","quantity":12}]},"6":{"requiredItems":[]}}},
         {"id":"weapon-bench","name":"Gunsmith","maxLevel":4,"levels":{
            "1":{"requiredItems":[{"id":"rubber-parts-recipe","quantity":30}]},"2":{"requiredItems":[{"id":"a","quantity":1}]},
            "3":{"requiredItems":[{"id":"b","quantity":1}]},"4":{"requiredItems":[{"id":"c","quantity":1}]}}},
         {"id":"equipment-bench","name":"Gear Bench","maxLevel":3,"levels":{"1":{"requiredItems":[{"id":"a","quantity":1}]},"2":{"requiredItems":[{"id":"a","quantity":1}]},"3":{"requiredItems":[{"id":"a","quantity":1}]}}},
         {"id":"research-station","name":"Research Station","maxLevel":4,"levels":{"1":{"requiredItems":[{"id":"a","quantity":1}]},"2":{"requiredItems":[{"id":"a","quantity":1}]},"3":{"requiredItems":[{"id":"a","quantity":1}]},"4":{"requiredItems":[{"id":"a","quantity":1}]}}}]
        """;

    private static (WorkshopBook Book, IReadOnlyList<Item> Items) Make()
    {
        var items = MetaForgeParser.ParseItems(Items);
        return (new WorkshopBook(MetaForgeParser.ParseHideout(Hideout), items, new UserState()), items);
    }

    [Fact]
    public void Scrappy_drops_empty_last_level_and_starts_at_one()
    {
        var (book, _) = Make();
        var scrappy = book.Get("scrappy")!;
        Assert.Equal(5, scrappy.MaxLevel);
        Assert.Equal(1, scrappy.MinLevel);
        Assert.Equal(1, book.LevelOf(scrappy));
        book.SetLevel(scrappy, 0);
        Assert.Equal(1, book.LevelOf(scrappy));
        var gun = book.Get("weapon-bench")!;
        Assert.Equal(0, gun.MinLevel);
        Assert.Equal(4, gun.MaxLevel);
    }

    [Fact]
    public void Recipes_give_station_level_ingredients_and_blueprint()
    {
        var (book, items) = Make();
        var bettina = book.CraftsAt("weapon-bench").Single(c => c.Item.Id == "bettina-i");
        Assert.Equal(3, bettina.Level);
        Assert.Equal("canister", bettina.Ingredients[0].ItemId);
        Assert.Equal("bettina-blueprint", book.BlueprintFor("bettina-i")!.Id);
        Assert.True(book.IsLockedByBlueprint(bettina));
        book.SetFound("bettina-blueprint", true);
        Assert.False(book.IsLockedByBlueprint(bettina));

        // tiery II+ nie są craftami, tylko ulepszeniami
        Assert.DoesNotContain(book.CraftsAt("weapon-bench"), c => c.Item.Id == "bettina-ii");
        var chain = book.UpgradeChain("bettina-i");
        Assert.Equal(["bettina-ii", "bettina-iii"], chain.Select(s => s.To.Id));
        Assert.Equal("bettina-i", book.UpgradedFrom("bettina-ii")!.From.Id);

        // schemat badany na Research Station
        Assert.Contains(book.CraftsAt("research-station"), c => c.Item.Id == "bettina-blueprint" && c.Level == 3);

        // operacje: salvage zostaje, round-production nie
        var b1 = items.Single(i => i.Id == "bettina-i");
        Assert.Equal("advanced-mechanical-components", b1.SalvageComponents.Single().ItemId);
        Assert.DoesNotContain(b1.Operations, o => o.Kind == "round-production");
    }

    [Fact]
    public void Survivor_kit_is_crafted_and_linked_and_rubber_parts_is_not_a_blueprint()
    {
        var (book, items) = Make();
        Assert.Contains(book.CraftsAt("equipment-bench"), c => c.Item.Id == "looting-mk-3-survivor" && c.Level == 3);
        Assert.Equal("looting-mk-3-survivor-blueprint", book.BlueprintFor("looting-mk-3-survivor")!.Id);
        Assert.Null(book.CraftedBy("rubber-parts-recipe"));
        Assert.Equal("anvil-recipe", book.BlueprintFor("anvil")!.Id); // schemat o id "-recipe"
        Assert.DoesNotContain(book.Blueprints, b => b.Id == "rubber-parts-recipe");
        Assert.Equal("tire", items.Single(i => i.Id == "rubber-parts-recipe").RecycleFrom.Single().ItemId);
    }

    [Theory]
    [InlineData("Gunsmith Level 3", "weapon-bench", 3)]
    [InlineData("Medical Lab Level 1", "med-station", 1)]
    [InlineData("Research Station Level 4", "research-station", 4)]
    [InlineData("Utility Station Level 1", "utility-bench", 1)]
    public void Recipe_station_text_is_parsed(string text, string station, int level)
    {
        var r = WorkshopBook.ParseStation(text);
        Assert.Equal(station, r!.Value.StationId);
        Assert.Equal(level, r.Value.Level);
    }
}
