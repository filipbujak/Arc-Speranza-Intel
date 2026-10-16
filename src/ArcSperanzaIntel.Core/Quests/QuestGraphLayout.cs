using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Quests;

/// <summary>
/// Własny układ drzewka questów — pozycje z MetaForge są nieaktualne (nowe questy, np. z Pendola Pass,
/// wiszą nad resztą i krzyżują całe drzewko). Warstwa = najdłuższa ścieżka od questu startowego; drzewo
/// rozpinające (główny rodzic = najbliższy) układane metodą Reingolda–Tilforda: rodzic nad środkiem dzieci,
/// poddrzewa dosunięte do siebie tak ciasno, jak pozwalają ich obrysy. Pozostałe wymagania to zwykłe linie.
/// </summary>
public sealed class QuestGraphLayout
{
    public const double NodeWidth = 220;
    public const double NodeHeight = 56;

    /// <summary>Odstęp między kartami w poziomie i między warstwami w pionie (między krawędziami kart).</summary>
    public const double GapX = 34;
    public const double GapY = 64;


    private readonly Dictionary<string, Point2> _positions;
    private readonly Dictionary<(string From, string To), IReadOnlyList<Point2>> _routes;

    private QuestGraphLayout(Dictionary<string, Point2> positions, Dictionary<(string, string), IReadOnlyList<Point2>> routes)
    {
        _positions = positions;
        _routes = routes;
    }

    public Point2 this[string id] => _positions.TryGetValue(id, out var p) ? p : default;
    public bool TryGet(string id, out Point2 p) => _positions.TryGetValue(id, out p);

    /// <summary>Punkty pośrednie krawędzi rodzic → dziecko (puste, gdy sąsiednie warstwy).</summary>
    public IReadOnlyList<Point2> Route(string from, string to) => _routes.GetValueOrDefault((from, to)) ?? [];

    public (double MinX, double MinY, double MaxX, double MaxY) Bounds()
    {
        if (_positions.Count == 0) return (0, 0, 1, 1);
        return (_positions.Values.Min(p => p.X) - NodeWidth,
                _positions.Values.Min(p => p.Y) - NodeHeight,
                _positions.Values.Max(p => p.X) + NodeWidth,
                _positions.Values.Max(p => p.Y) + NodeHeight);
    }

    // ------------------------------------------------------------------ budowa
    internal static QuestGraphLayout Build(IReadOnlyList<Quest> quests, IReadOnlyDictionary<string, List<string>> parents)
    {
        var ids = quests.Select(q => q.Id).ToList();
        var idSet = ids.ToHashSet();
        var trader = quests.ToDictionary(q => q.Id, q => q.Trader);
        var up = ids.ToDictionary(id => id, id => (parents.GetValueOrDefault(id) ?? []).Where(p => idSet.Contains(p) && p != id).Distinct().ToList());
        BreakCycles(ids, up);

        // 1) warstwa = najdłuższa ścieżka od questu startowego
        var layer = new Dictionary<string, int>();
        int LayerOf(string id)
        {
            if (layer.TryGetValue(id, out var l)) return l;
            layer[id] = 0;
            var v = up[id].Count == 0 ? 0 : up[id].Max(LayerOf) + 1;
            layer[id] = v;
            return v;
        }
        foreach (var id in ids) LayerOf(id);

        // 2) drzewo rozpinające: główny rodzic = ten z najgłębszej warstwy (dziecko jest wtedy dokładnie piętro niżej)
        var children = ids.ToDictionary(id => id, _ => new List<string>());
        var roots = new List<string>();
        foreach (var id in ids)
        {
            var main = up[id].OrderByDescending(p => layer[p]).ThenBy(p => p, StringComparer.Ordinal).FirstOrDefault();
            if (main is null) roots.Add(id);
            else children[main].Add(id);
        }
        // kolejność dzieci: ten sam handlarz obok siebie, potem większe poddrzewa bliżej środka
        var size = new Dictionary<string, int>();
        int SizeOf(string id) => size.TryGetValue(id, out var s) ? s : size[id] = 1 + children[id].Sum(SizeOf);
        foreach (var id in ids)
            children[id] = MiddleHeavy(children[id].OrderBy(c => trader[c], StringComparer.Ordinal).ThenBy(c => c, StringComparer.Ordinal).ToList(), SizeOf);

        // 3) Reingold–Tilford z konturami: poddrzewa dosuwane do siebie tak blisko, jak pozwalają ich obrysy
        var rel = new Dictionary<string, double>(); // X dziecka względem rodzica
        Contour Lay(string id)
        {
            var kids = children[id];
            if (kids.Count == 0) return Contour.Leaf();
            var placed = new List<(string Id, double X, Contour C)>();
            Contour? acc = null;
            foreach (var k in kids)
            {
                var c = Lay(k);
                var x = acc is null ? 0 : acc.MinShiftFor(c, NodeWidth + GapX);
                placed.Add((k, x, c));
                acc = acc is null ? c.Shifted(x) : acc.Merge(c.Shifted(x));
            }
            var mid = (placed[0].X + placed[^1].X) / 2;
            foreach (var (k, x, _) in placed) rel[k] = x - mid;
            return Contour.Leaf().Below(acc!.Shifted(-mid));
        }

        var pos = new Dictionary<string, Point2>();
        double Y(int l) => l * (NodeHeight + GapY);
        void Assign(string id, double x)
        {
            pos[id] = new Point2(x, Y(layer[id]));
            foreach (var k in children[id]) Assign(k, x + rel[k]);
        }

        // 4) las: drzewa obok siebie (największe pierwsze), też dosunięte konturami
        Contour? forest = null;
        foreach (var r in roots.OrderByDescending(SizeOf).ThenBy(r => r, StringComparer.Ordinal))
        {
            var c = Lay(r);
            var x = forest is null ? 0 : forest.MinShiftFor(c, NodeWidth + GapX * 3);
            forest = forest is null ? c.Shifted(x) : forest.Merge(c.Shifted(x));
            Assign(r, x);
        }
        return new QuestGraphLayout(pos, []);
    }

    /// <summary>Największe poddrzewa w środku listy, mniejsze na brzegach (mniej długich krawędzi).</summary>
    private static List<string> MiddleHeavy(List<string> kids, Func<string, int> size)
    {
        if (kids.Count < 3) return kids;
        var sorted = kids.OrderBy(size).ToList();
        var left = new List<string>();
        var right = new List<string>();
        for (var i = 0; i < sorted.Count; i++) (i % 2 == 0 ? left : right).Add(sorted[i]);
        right.Reverse();
        return [.. left, .. right];
    }

    /// <summary>Lewy i prawy brzeg poddrzewa na kolejnych poziomach (0 = korzeń), względem X korzenia.</summary>
    private sealed class Contour(List<double> left, List<double> right)
    {
        public List<double> Left { get; } = left;
        public List<double> Right { get; } = right;

        public static Contour Leaf() => new([-NodeWidth / 2], [NodeWidth / 2]);

        public Contour Shifted(double dx) => new(Left.Select(v => v + dx).ToList(), Right.Select(v => v + dx).ToList());

        /// <summary>Najmniejsze przesunięcie, przy którym <paramref name="next"/> stoi po prawej bez nachodzenia.</summary>
        public double MinShiftFor(Contour next, double minGapBetweenCenters)
        {
            var shift = double.MinValue;
            for (var d = 0; d < Math.Min(Right.Count, next.Left.Count); d++)
                shift = Math.Max(shift, Right[d] - next.Left[d] + (minGapBetweenCenters - NodeWidth));
            return shift == double.MinValue ? 0 : shift;
        }

        public Contour Merge(Contour other)
        {
            var n = Math.Max(Left.Count, other.Left.Count);
            var l = new List<double>(n);
            var r = new List<double>(n);
            for (var d = 0; d < n; d++)
            {
                l.Add(d < Left.Count ? (d < other.Left.Count ? Math.Min(Left[d], other.Left[d]) : Left[d]) : other.Left[d]);
                r.Add(d < Right.Count ? (d < other.Right.Count ? Math.Max(Right[d], other.Right[d]) : Right[d]) : other.Right[d]);
            }
            return new Contour(l, r);
        }

        /// <summary>Ten węzeł (poziom 0) z poddrzewem dzieci pod spodem.</summary>
        public Contour Below(Contour kids) => new([.. Left, .. kids.Left], [.. Right, .. kids.Right]);
    }

    /// <summary>Usuwa krawędzie tworzące cykl (dane z zewnątrz — na wszelki wypadek).</summary>
    private static void BreakCycles(List<string> ids, Dictionary<string, List<string>> up)
    {
        var state = new Dictionary<string, int>(); // 1 = w trakcie, 2 = gotowe
        void Visit(string id)
        {
            state[id] = 1;
            foreach (var p in up[id].ToList())
            {
                var st = state.GetValueOrDefault(p);
                if (st == 1) up[id].Remove(p);
                else if (st == 0) Visit(p);
            }
            state[id] = 2;
        }
        foreach (var id in ids.OrderBy(i => i, StringComparer.Ordinal))
            if (state.GetValueOrDefault(id) == 0) Visit(id);
    }
}
