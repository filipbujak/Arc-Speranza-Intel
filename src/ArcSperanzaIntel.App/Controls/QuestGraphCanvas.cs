using System.Globalization;
using ArcSperanzaIntel.Core.Quests;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>Węzeł grafu do narysowania (stan liczy ViewModel).</summary>
public sealed class QuestNodeVisual
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Subtitle { get; init; }
    public required Color TraderColor { get; init; }
    public required QuestStatus Status { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public int ObjectivesDone { get; init; }
    public int ObjectivesTotal { get; init; }
    public bool Dimmed { get; init; }
    public bool Selected { get; init; }
    public bool Matched { get; init; }
}

public sealed record QuestEdge(string From, string To);

public sealed class QuestNodeEventArgs(string questId) : EventArgs
{
    public string QuestId { get; } = questId;
}

/// <summary>Duży graf questów: zoom kółkiem, przesuwanie, klik w węzeł, kółko = ukończ/odznacz, płynny przelot.</summary>
public sealed class QuestGraphCanvas : Control
{
    public static readonly StyledProperty<IReadOnlyList<QuestNodeVisual>?> NodesProperty =
        AvaloniaProperty.Register<QuestGraphCanvas, IReadOnlyList<QuestNodeVisual>?>(nameof(Nodes));

    public static readonly StyledProperty<IReadOnlyList<QuestEdge>?> EdgesProperty =
        AvaloniaProperty.Register<QuestGraphCanvas, IReadOnlyList<QuestEdge>?>(nameof(Edges));

    static QuestGraphCanvas()
    {
        AffectsRender<QuestGraphCanvas>(NodesProperty, EdgesProperty);
    }

    public IReadOnlyList<QuestNodeVisual>? Nodes { get => GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public IReadOnlyList<QuestEdge>? Edges { get => GetValue(EdgesProperty); set => SetValue(EdgesProperty, value); }

    public event EventHandler<QuestNodeEventArgs>? NodeClicked;
    public event EventHandler<QuestNodeEventArgs>? NodeToggleClicked;
    public event EventHandler? BackgroundClicked;
    public event EventHandler? ViewChanged;

    private const double W = QuestGraphLayout.NodeWidth;
    private const double H = QuestGraphLayout.NodeHeight;

    public double Scale { get; private set; } = 0.6;
    public Point Offset { get; private set; }

    private static readonly IBrush Back = new SolidColorBrush(Color.Parse("#090A0C"));
    private static readonly IBrush GridDot = new SolidColorBrush(Color.Parse("#1A1E25"));
    private static readonly IBrush NodeFill = new SolidColorBrush(Color.Parse("#15181D"));
    private static readonly IBrush NodeFillAvailable = new SolidColorBrush(Color.Parse("#1E1A0D"));
    private static readonly IBrush NodeFillDone = new SolidColorBrush(Color.Parse("#0F1C14"));
    private static readonly IPen NodeLine = new Pen(new SolidColorBrush(Color.Parse("#2E343E")), 1.2);
    private static readonly IPen NodeLineAvailable = new Pen(new SolidColorBrush(Color.Parse("#FFC72C")), 1.6);
    private static readonly IPen NodeLineDone = new Pen(new SolidColorBrush(Color.Parse("#2E7A4A")), 1.4);
    private static readonly IPen NodeLineSelected = new Pen(Brushes.White, 2.6);
    private static readonly IPen NodeLineMatched = new Pen(new SolidColorBrush(Color.Parse("#36B5E6")), 2.6);
    private static readonly IPen EdgeLocked = new Pen(new SolidColorBrush(Color.Parse("#2A303A")), 1.6);
    private static readonly IPen EdgeDone = new Pen(new SolidColorBrush(Color.Parse("#2E7A4A")), 1.8);
    private static readonly IPen EdgeNext = new Pen(new SolidColorBrush(Color.Parse("#8A6D1A")), 1.8, new DashStyle([5, 4], 0));
    private static readonly IPen EdgeSelected = new Pen(new SolidColorBrush(Color.Parse("#FFC72C")), 2.8);
    private static readonly IBrush Text = new SolidColorBrush(Color.Parse("#ECE7DC"));
    private static readonly IBrush TextMuted = new SolidColorBrush(Color.Parse("#9298A3"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#3FD07A"));
    private static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#FFC72C"));
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#0B0C0E"));
    private static readonly IPen CheckRing = new Pen(new SolidColorBrush(Color.Parse("#4A5263")), 2);
    private static readonly IPen CheckRingAvailable = new Pen(new SolidColorBrush(Color.Parse("#FFC72C")), 2);
    private static readonly IPen TickPen = new Pen(new SolidColorBrush(Color.Parse("#0B0C0E")), 2.4, lineCap: PenLineCap.Round);
    private static readonly Typeface NameFace = new(new FontFamily("avares://ArcSperanzaIntel/Assets/Fonts#Barlow"), FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface SubFace = new(new FontFamily("avares://ArcSperanzaIntel/Assets/Fonts#Barlow"), FontStyle.Normal, FontWeight.Normal);
    private static readonly Dictionary<Color, IBrush> Stripes = [];

    private readonly Dictionary<string, QuestNodeVisual> _byId = [];
    private Point _press;
    private Point _last;
    private bool _dragging;
    private bool _moved;
    private DispatcherTimer? _anim;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NodesProperty)
        {
            _byId.Clear();
            foreach (var n in Nodes ?? []) _byId[n.Id] = n;
        }
    }

    // ---------------------------------------------------------------- widok
    private Point ToScreen(double x, double y) => new(x * Scale + Offset.X, y * Scale + Offset.Y);

    public void SetView(double scale, Point offset)
    {
        Scale = scale;
        Offset = offset;
        InvalidateVisual();
    }

    public void FitAll()
    {
        var nodes = Nodes;
        if (nodes is null || nodes.Count == 0 || Bounds.Width < 10) return;
        var minX = nodes.Min(n => n.X) - W;
        var maxX = nodes.Max(n => n.X) + W;
        var minY = nodes.Min(n => n.Y) - H;
        var maxY = nodes.Max(n => n.Y) + H;
        var k = Math.Clamp(Math.Min(Bounds.Width / (maxX - minX), Bounds.Height / (maxY - minY)) * 0.95, 0.06, 1.5);
        AnimateTo(k, new Point(Bounds.Width / 2 - (minX + maxX) / 2 * k, Bounds.Height / 2 - (minY + maxY) / 2 * k));
    }

    /// <summary>Płynny przelot i przybliżenie do questu.</summary>
    public void FlyTo(string questId, double scale = 0.95)
    {
        if (!_byId.TryGetValue(questId, out var n) || Bounds.Width < 10) return;
        var k = Math.Max(scale, Scale);
        AnimateTo(k, new Point(Bounds.Width / 2 - n.X * k, Bounds.Height / 2 - n.Y * k));
    }

    private void AnimateTo(double k, Point offset, int ms = 600)
    {
        _anim?.Stop();
        var k0 = Scale;
        var o0 = Offset;
        var t0 = DateTime.UtcNow;
        _anim = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) =>
        {
            var t = Math.Min(1, (DateTime.UtcNow - t0).TotalMilliseconds / ms);
            var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
            // interpolujemy środek ekranu w świecie, żeby przelot nie "pływał" przy zmianie skali
            var cw0 = new Point((Bounds.Width / 2 - o0.X) / k0, (Bounds.Height / 2 - o0.Y) / k0);
            var cw1 = new Point((Bounds.Width / 2 - offset.X) / k, (Bounds.Height / 2 - offset.Y) / k);
            var kk = k0 + (k - k0) * e;
            var cw = new Point(cw0.X + (cw1.X - cw0.X) * e, cw0.Y + (cw1.Y - cw0.Y) * e);
            Scale = kk;
            Offset = new Point(Bounds.Width / 2 - cw.X * kk, Bounds.Height / 2 - cw.Y * kk);
            InvalidateVisual();
            if (t >= 1)
            {
                _anim?.Stop();
                _anim = null;
                ViewChanged?.Invoke(this, EventArgs.Empty);
            }
        });
        _anim.Start();
    }

    // ---------------------------------------------------------------- rysowanie
    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        ctx.FillRectangle(Back, new Rect(size));
        using var clip = ctx.PushClip(new Rect(size));
        DrawGrid(ctx, size);

        var nodes = Nodes;
        if (nodes is null) return;
        var selected = nodes.FirstOrDefault(n => n.Selected)?.Id;
        var path = selected is null ? null : AncestorPath(selected);

        foreach (var e in Edges ?? [])
        {
            if (!_byId.TryGetValue(e.From, out var a) || !_byId.TryGetValue(e.To, out var b)) continue;
            var p1 = ToScreen(a.X, a.Y + H / 2);
            var p2 = ToScreen(b.X, b.Y - H / 2);
            if (Math.Max(p1.Y, p2.Y) < -50 || Math.Min(p1.Y, p2.Y) > size.Height + 50 ||
                Math.Max(p1.X, p2.X) < -50 || Math.Min(p1.X, p2.X) > size.Width + 50) continue;
            var my = (p1.Y + p2.Y) / 2;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(p1, false);
                c.CubicBezierTo(new Point(p1.X, my), new Point(p2.X, my), p2);
                c.EndFigure(false);
            }
            var pen = path is not null && path.Contains(e.From) && path.Contains(e.To) ? EdgeSelected
                : a.Status == QuestStatus.Done && b.Status == QuestStatus.Done ? EdgeDone
                : a.Status == QuestStatus.Done && b.Status == QuestStatus.Available ? EdgeNext
                : EdgeLocked;
            using (ctx.PushOpacity(a.Dimmed || b.Dimmed ? 0.15 : 1))
                ctx.DrawGeometry(null, pen, g);
        }

        foreach (var n in nodes) DrawNode(ctx, n, size);
    }

    private HashSet<string> AncestorPath(string id)
    {
        var set = new HashSet<string> { id };
        var stack = new Stack<string>([id]);
        var parents = (Edges ?? []).GroupBy(e => e.To).ToDictionary(g => g.Key, g => g.Select(e => e.From).ToList());
        while (stack.Count > 0)
            foreach (var p in parents.GetValueOrDefault(stack.Pop()) ?? [])
                if (set.Add(p)) stack.Push(p);
        return set;
    }

    private void DrawGrid(DrawingContext ctx, Size size)
    {
        var step = 40 * Scale;
        if (step < 10) return;
        var ox = Offset.X % step;
        var oy = Offset.Y % step;
        for (var x = ox; x < size.Width; x += step)
        for (var y = oy; y < size.Height; y += step)
            ctx.FillRectangle(GridDot, new Rect(x, y, 1.5, 1.5));
    }

    private void DrawNode(DrawingContext ctx, QuestNodeVisual n, Size size)
    {
        var tl = ToScreen(n.X - W / 2, n.Y - H / 2);
        var rect = new Rect(tl, new Size(W * Scale, H * Scale));
        if (rect.Right < 0 || rect.Bottom < 0 || rect.X > size.Width || rect.Y > size.Height) return;

        using var op = ctx.PushOpacity(n.Dimmed ? 0.12 : n.Status == QuestStatus.Locked ? 0.7 : 1);
        var fill = n.Status switch
        {
            QuestStatus.Done => NodeFillDone,
            QuestStatus.Available => NodeFillAvailable,
            _ => NodeFill,
        };
        var pen = n.Selected ? NodeLineSelected
            : n.Matched ? NodeLineMatched
            : n.Status switch
            {
                QuestStatus.Done => NodeLineDone,
                QuestStatus.Available => NodeLineAvailable,
                _ => NodeLine,
            };
        ctx.DrawRectangle(fill, pen, rect, 4 * Scale, 4 * Scale);

        if (!Stripes.TryGetValue(n.TraderColor, out var stripe)) Stripes[n.TraderColor] = stripe = new SolidColorBrush(n.TraderColor);
        ctx.FillRectangle(stripe, new Rect(rect.X, rect.Y, 5 * Scale, rect.Height));

        // kółko ukończ / odznacz
        var cc = new Point(rect.Right - 18 * Scale, rect.Y + rect.Height / 2);
        var cr = 10 * Scale;
        if (n.Status == QuestStatus.Done)
        {
            ctx.DrawEllipse(Green, null, cc, cr, cr);
            ctx.DrawLine(TickPen, new Point(cc.X - cr * 0.45, cc.Y), new Point(cc.X - cr * 0.1, cc.Y + cr * 0.4));
            ctx.DrawLine(TickPen, new Point(cc.X - cr * 0.1, cc.Y + cr * 0.4), new Point(cc.X + cr * 0.5, cc.Y - cr * 0.4));
        }
        else
        {
            ctx.DrawEllipse(Ink, n.Status == QuestStatus.Available ? CheckRingAvailable : CheckRing, cc, cr, cr);
        }

        if (Scale < 0.28) return; // przy dużym oddaleniu bez tekstu — czytelniej i szybciej

        var textW = rect.Width - 46 * Scale;
        var name = new FormattedText(n.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, NameFace, 14 * Scale, Text)
        {
            MaxTextWidth = Math.Max(10, textW),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        ctx.DrawText(name, new Point(rect.X + 14 * Scale, rect.Y + 9 * Scale));

        var subText = n.ObjectivesTotal > 0 && n.Status != QuestStatus.Done
            ? $"{n.Subtitle}  ·  {n.ObjectivesDone}/{n.ObjectivesTotal}"
            : n.Subtitle;
        var sub = new FormattedText(subText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, SubFace, 11.5 * Scale,
            n.Status == QuestStatus.Available ? Yellow : TextMuted)
        {
            MaxTextWidth = Math.Max(10, textW),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        ctx.DrawText(sub, new Point(rect.X + 14 * Scale, rect.Y + 31 * Scale));
    }

    // ---------------------------------------------------------------- trafienia i wejście
    private (QuestNodeVisual? Node, bool OnToggle) Hit(Point p)
    {
        var nodes = Nodes;
        if (nodes is null) return (null, false);
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var n = nodes[i];
            var tl = ToScreen(n.X - W / 2, n.Y - H / 2);
            var rect = new Rect(tl, new Size(W * Scale, H * Scale));
            if (!rect.Contains(p)) continue;
            var cc = new Point(rect.Right - 18 * Scale, rect.Y + rect.Height / 2);
            var onToggle = Math.Abs(p.X - cc.X) <= 14 * Scale && Math.Abs(p.Y - cc.Y) <= 14 * Scale;
            return (n, onToggle);
        }
        return (null, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _anim?.Stop();
        _press = _last = e.GetPosition(this);
        _dragging = true;
        _moved = false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (!_dragging)
        {
            var (node, _) = Hit(p);
            Cursor = node is not null ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            return;
        }
        if (!_moved && Math.Abs(p.X - _press.X) + Math.Abs(p.Y - _press.Y) > 4) _moved = true;
        if (!_moved) return;
        Offset += p - _last;
        _last = p;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        if (_moved)
        {
            ViewChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        var (node, onToggle) = Hit(e.GetPosition(this));
        if (node is null) BackgroundClicked?.Invoke(this, EventArgs.Empty);
        else if (onToggle) NodeToggleClicked?.Invoke(this, new QuestNodeEventArgs(node.Id));
        else NodeClicked?.Invoke(this, new QuestNodeEventArgs(node.Id));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _anim?.Stop();
        var p = e.GetPosition(this);
        var k = Math.Clamp(Scale * Math.Exp(e.Delta.Y * 0.18), 0.06, 2.5);
        Offset = new Point(p.X - (p.X - Offset.X) * (k / Scale), p.Y - (p.Y - Offset.Y) * (k / Scale));
        Scale = k;
        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
