using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Quests;

namespace ArcSperanzaIntel.Tests;

public class QuestBookTests
{
    // a → b → d ; c → d ; b ma dwa subtaski
    private static (QuestBook Book, QuestProgress Progress) Build()
    {
        var quests = new List<Quest>
        {
            new() { Id = "a", Name = "A" },
            new() { Id = "b", Name = "B", PrerequisiteIds = ["a"], Objectives = ["Napraw syrenę", "Dostarcz baterię"] },
            new() { Id = "c", Name = "C" },
            new() { Id = "d", Name = "D", PrerequisiteIds = ["b", "c"] },
        };
        var progress = new QuestProgress();
        return (new QuestBook(quests, progress), progress);
    }

    [Fact]
    public void Roots_are_available_rest_locked()
    {
        var (book, _) = Build();
        Assert.Equal(QuestStatus.Available, book.StatusOf("a"));
        Assert.Equal(QuestStatus.Available, book.StatusOf("c"));
        Assert.Equal(QuestStatus.Locked, book.StatusOf("b"));
        Assert.Equal(QuestStatus.Locked, book.StatusOf("d"));
    }

    [Fact]
    public void Completing_marks_all_ancestors()
    {
        var (book, _) = Build();
        var change = book.Complete("d");
        Assert.True(book.IsDone("a") && book.IsDone("b") && book.IsDone("c") && book.IsDone("d"));
        Assert.Equal(3, change.AlsoChanged.Count);
        Assert.All(book.ObjectiveStates("b"), s => Assert.True(s));
    }

    [Fact]
    public void Uncompleting_clears_descendants()
    {
        var (book, _) = Build();
        book.Complete("d");
        var change = book.Uncomplete("a");
        Assert.False(book.IsDone("a"));
        Assert.False(book.IsDone("b"));
        Assert.False(book.IsDone("d"));
        Assert.True(book.IsDone("c"));
        Assert.Equal(2, change.AlsoChanged.Count);
    }

    [Fact]
    public void All_objectives_done_completes_quest()
    {
        var (book, _) = Build();
        book.Complete("a");
        Assert.Null(book.SetObjective("b", 0, true));
        Assert.False(book.IsDone("b"));
        var change = book.SetObjective("b", 1, true);
        Assert.NotNull(change);
        Assert.True(book.IsDone("b"));
    }

    [Fact]
    public void Unchecking_objective_reopens_quest_and_keeps_other_objectives()
    {
        var (book, _) = Build();
        book.Complete("d");
        book.SetObjective("b", 1, false);
        Assert.False(book.IsDone("b"));
        Assert.False(book.IsDone("d"));
        Assert.True(book.IsObjectiveDone("b", 0));
        Assert.False(book.IsObjectiveDone("b", 1));
    }

    [Fact]
    public void Layout_places_every_quest()
    {
        var (book, _) = Build();
        foreach (var q in book.Quests) Assert.True(book.Layout.TryGet(q.Id, out _));
    }
}

public class CoordinateTransformTests
{
    [Fact]
    public void Pixel_round_trip()
    {
        var t = new CoordinateTransform(0.02899823907870071, -0.5500098883744897, 0.029003656496347927, -0.2453962393344682);
        var p = new LatLng(2560, 4096);
        var (x, y) = t.ToPixel(p, 3.25);
        var back = t.FromPixel(x, y, 3.25);
        Assert.Equal(p.Lat, back.Lat, 6);
        Assert.Equal(p.Lng, back.Lng, 6);
    }
}

public class QuestMapIndexTests
{
    [Fact]
    public void Map_name_matches_whole_words_only()
    {
        var index = new QuestMapIndex(ArcSperanzaIntel.Core.Maps.MapCatalog.All);
        var damage = new Quest { Id = "x", Name = "X", Objectives = ["Damage a Hydra"] };
        var dam = new Quest { Id = "y", Name = "Y", Objectives = ["On Dam Battlegrounds, mark the Pattern House"] };
        Assert.DoesNotContain(index.MapsFor(damage), m => m.Id == "dam");
        Assert.Contains(index.MapsFor(dam), m => m.Id == "dam");
    }
}

public class QuestGraphLayoutTests
{
    [Fact]
    public void Layout_keeps_children_below_parents_and_cards_apart()
    {
        // rozgałęzienie + łączenie + osobne drzewo
        var parents = new Dictionary<string, List<string>>
        {
            ["a"] = [], ["b"] = ["a"], ["c"] = ["a"], ["d"] = ["a"], ["e"] = ["b", "c"], ["f"] = ["e"],
            ["g"] = ["f", "a"], ["x"] = [], ["y"] = ["x"],
        };
        var quests = parents.Keys.Select(id => new ArcSperanzaIntel.Core.Models.Quest { Id = id, Name = id, Trader = "T" }).ToList();
        var layout = ArcSperanzaIntel.Core.Quests.QuestGraphLayout.Build(quests, parents);

        foreach (var (child, ps) in parents)
        foreach (var p in ps)
            Assert.True(layout[child].Y > layout[p].Y, $"{child} pod {p}");

        var all = parents.Keys.Select(id => layout[id]).ToList();
        for (var i = 0; i < all.Count; i++)
        for (var j = i + 1; j < all.Count; j++)
        {
            var overlap = Math.Abs(all[i].X - all[j].X) < ArcSperanzaIntel.Core.Quests.QuestGraphLayout.NodeWidth &&
                          Math.Abs(all[i].Y - all[j].Y) < ArcSperanzaIntel.Core.Quests.QuestGraphLayout.NodeHeight;
            Assert.False(overlap, "karty nachodzą na siebie");
        }
    }
}
