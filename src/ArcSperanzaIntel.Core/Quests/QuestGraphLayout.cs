using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Quests;

/// <summary>Pozycje węzłów grafu questów: z MetaForge, a brakujące liczone pod rodzicami.</summary>
public sealed class QuestGraphLayout
{
    public const double NodeWidth = 220;
    public const double NodeHeight = 56;

    private readonly Dictionary<string, Point2> _positions;

    private QuestGraphLayout(Dictionary<string, Point2> positions) => _positions = positions;

    public Point2 this[string id] => _positions.TryGetValue(id, out var p) ? p : default;
    public bool TryGet(string id, out Point2 p) => _positions.TryGetValue(id, out p);

    public (double MinX, double MinY, double MaxX, double MaxY) Bounds()
    {
        if (_positions.Count == 0) return (0, 0, 1, 1);
        return (_positions.Values.Min(p => p.X) - NodeWidth,
                _positions.Values.Min(p => p.Y) - NodeHeight,
                _positions.Values.Max(p => p.X) + NodeWidth,
                _positions.Values.Max(p => p.Y) + NodeHeight);
    }

    internal static QuestGraphLayout Build(IReadOnlyList<Quest> quests, IReadOnlyDictionary<string, List<string>> parents)
    {
        var pos = new Dictionary<string, Point2>();
        foreach (var q in quests)
            if (q.GraphPosition is { } p) pos[q.Id] = p;

        // Brakujące: pod rodzicami (średnie X, najniższe Y + odstęp); kilka przebiegów dla łańcuchów.
        for (var pass = 0; pass < 8; pass++)
        {
            foreach (var q in quests)
            {
                if (pos.ContainsKey(q.Id)) continue;
                var ps = parents.GetValueOrDefault(q.Id)?.Where(pos.ContainsKey).Select(id => pos[id]).ToList() ?? [];
                var allParentsPlaced = parents.GetValueOrDefault(q.Id)?.All(pos.ContainsKey) ?? true;
                if (!allParentsPlaced && pass < 7) continue;
                var x = ps.Count > 0 ? ps.Average(p => p.X) : 0;
                var y = ps.Count > 0 ? ps.Max(p => p.Y) + 240 : 0;
                pos[q.Id] = FreeSpot(pos, x, y);
            }
        }
        return new QuestGraphLayout(pos);
    }

    private static Point2 FreeSpot(Dictionary<string, Point2> pos, double x, double y)
    {
        bool Taken(double cx) => pos.Values.Any(p => Math.Abs(p.X - cx) < NodeWidth + 10 && Math.Abs(p.Y - y) < NodeHeight + 10);
        var dx = 0.0;
        for (var i = 0; i < 60 && Taken(x + dx); i++)
            dx = (i % 2 == 0 ? 1 : -1) * Math.Ceiling((i + 1) / 2.0) * (NodeWidth + 24);
        return new Point2(x + dx, y);
    }
}
