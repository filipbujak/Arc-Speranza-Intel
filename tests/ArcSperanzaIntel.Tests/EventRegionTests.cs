using ArcSperanzaIntel.Core.Api;

namespace ArcSperanzaIntel.Tests;

public class EventRegionTests
{
    private static TimeZoneInfo Zone(string id, double hours) =>
        TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(hours), id, id);

    [Theory]
    [InlineData("Central European Standard Time", 1, EventRegions.Europe)]
    [InlineData("GMT Standard Time", 0, EventRegions.Europe)]
    [InlineData("Tokyo Standard Time", 9, EventRegions.Europe)]
    [InlineData("Eastern Standard Time", -5, EventRegions.NorthAmerica)]
    [InlineData("Pacific Standard Time", -8, EventRegions.NorthAmerica)]
    [InlineData("E. South America Standard Time", -3, EventRegions.NorthAmerica)]
    [InlineData("AUS Eastern Standard Time", 10, EventRegions.Oceania)]
    [InlineData("W. Australia Standard Time", 8, EventRegions.Oceania)]
    [InlineData("New Zealand Standard Time", 12, EventRegions.Oceania)]
    [InlineData("Australia/Sydney", 10, EventRegions.Oceania)]
    public void DetectsRegionFromTimeZone(string id, double hours, string expected) =>
        Assert.Equal(expected, EventRegions.Detect(Zone(id, hours)));

    [Fact]
    public void StoredChoiceWinsOverDetection() =>
        Assert.Equal(EventRegions.Oceania, EventRegions.Resolve(EventRegions.Oceania));

    [Fact]
    public void UnknownStoredValueFallsBackToDetection() =>
        Assert.True(EventRegions.IsKnown(EventRegions.Resolve("mars")));
}
