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
