using ArcSperanzaIntel.Core.Quests;

namespace ArcSperanzaIntel.Tests;

public class ObjectiveMatcherTests
{
    private static readonly string[] Objectives =
    [
        "Find the fuse box in the Control Tower",
        "Collect 3 Batteries from the Water Treatment plant",
        "Return to Celeste",
    ];

    [Fact]
    public void MatchesByMarkerWords() => Assert.Equal(0, ObjectiveMatcher.BestMatch(Objectives, "Control Tower fuse box"));

    [Fact]
    public void MatchesPluralAndCase() => Assert.Equal(1, ObjectiveMatcher.BestMatch(Objectives, "battery – water treatment"));

    [Fact]
    public void ExactSubstringWins() => Assert.Equal(2, ObjectiveMatcher.BestMatch(Objectives, "Return to Celeste"));

    [Fact]
    public void NoNameNoMatch() => Assert.Null(ObjectiveMatcher.BestMatch(Objectives, null));

    [Fact]
    public void UnrelatedNameNoMatch() => Assert.Null(ObjectiveMatcher.BestMatch(Objectives, "Rusty car"));

    [Fact]
    public void TieGivesNoMatch() =>
        Assert.Null(ObjectiveMatcher.BestMatch(["Search the north tower", "Search the south tower"], "Tower"));
}
