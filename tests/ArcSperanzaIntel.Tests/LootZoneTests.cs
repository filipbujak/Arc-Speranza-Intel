using ArcSperanzaIntel.Core.Api;
using ArcSperanzaIntel.Core.Maps;
using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Tests;

public class LootZoneTests
{
    [Fact]
    public void ParsesCommaList() =>
        Assert.Equal("Electrical|Commercial|Residential", string.Join("|", LootZones.Parse("Electrical, Commercial, Residential")));

    [Fact]
    public void NormalizesCaseAndDuplicates() =>
        Assert.Equal("Old World|Medical", string.Join("|", LootZones.Parse("old world,Medical, medical")));

    [Fact]
    public void EmptyGivesNothing()
    {
        Assert.Empty(LootZones.Parse(null));
        Assert.Empty(LootZones.Parse("  "));
    }

    [Fact]
    public void MarkerKnowsItsZones()
    {
        var m = new MapMarker("1", "dam", 0, 0, 0, "containers", "base_container", "Locker", false, "Industrial, Mechanical");
        Assert.True(m.HasLootZone("Mechanical"));
        Assert.False(m.HasLootZone("Medical"));
    }

    [Fact]
    public void ItemLootAreaIsParsed()
    {
        const string json = """[{"id":"power-cable","name":"Power Cable","item_type":"Material","loot_area":"Electrical, Commercial, Residential"}]""";
        var items = MetaForgeParser.ParseItems(json);
        Assert.Equal("Electrical|Commercial|Residential", string.Join("|", items.Single().LootAreas));
    }
}
