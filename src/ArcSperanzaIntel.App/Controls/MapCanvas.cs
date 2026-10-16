using ArcSperanzaIntel.Core.Localization;
using System.Diagnostics;
using System.Globalization;
using ArcSperanzaIntel.App.Icons;
using ArcSperanzaIntel.App.Services;
using ArcSperanzaIntel.Core.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Controls;

public enum MarkerShape { Circle, Diamond }

/// <summary>Marker gotowy do narysowania (filtrowanie i kolory liczy ViewModel).</summary>
public sealed class MarkerVisual(MapMarker marker, Color color, Geometry icon, MarkerShape shape, string label)
{
    public MapMarker Marker { get; } = marker;
    public Color Color { get; } = color;
    public Geometry Icon { get; } = icon;
    public MarkerShape Shape { get; } = shape;
    public string Label { get; } = label;
    public bool Favorite { get; init; }
    public bool Dimmed { get; init; }
    public bool Highlighted { get; init; }
}

public sealed class MapPointerEventArgs(LatLng position, Point screen) : EventArgs
{
    public LatLng Position { get; } = position;
    public Point Screen { get; } = screen;
}

public sealed class MarkerClickEventArgs(MarkerVisual marker, Point screen) : EventArgs
{
    public MarkerVisual Marker { get; } = marker;
    public Point Screen { get; } = screen;
}

public sealed class PinEventArgs(MapPin pin, Point screen) : EventArgs
{
    public MapPin Pin { get; } = pin;
    public Point Screen { get; } = screen;
}

/// <summary>
/// Mapa gry: kafelki MetaForge (układ Leaflet CRS.Simple), zoom kółkiem, przesuwanie, markery z ikonami,
/// własne pinezki (przeciągane), dymek nazwy przy najechaniu i płynny przelot do punktu.
/// </summary>
public sealed class MapCanvas : Control
{
    // ---------------------------------------------------------------- właściwości
    public static readonly StyledProperty<MapDefinition?> MapProperty =
        AvaloniaProperty.Register<MapCanvas, MapDefinition?>(nameof(Map));

    public static readonly StyledProperty<MapFloor?> FloorProperty =
        AvaloniaProperty.Register<MapCanvas, MapFloor?>(nameof(Floor));

    public static readonly StyledProperty<IReadOnlyList<MarkerVisual>?> MarkersProperty =
        AvaloniaProperty.Register<MapCanvas, IReadOnlyList<MarkerVisual>?>(nameof(Markers));

    public static readonly StyledProperty<IReadOnlyList<MapPin>?> PinsProperty =
        AvaloniaProperty.Register<MapCanvas, IReadOnlyList<MapPin>?>(nameof(Pins));

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(MarkersProperty, PinsProperty, FloorProperty);
        FocusableProperty.OverrideDefaultValue<MapCanvas>(true);
    }

    public MapCanvas()
    {
        // Dwuliniowe skalowanie kafelków — wyraźnie szybsze niż domyślne wysokiej jakości.
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.LowQuality);
    }

    public MapDefinition? Map { get => GetValue(MapProperty); set => SetValue(MapProperty, value); }
    public MapFloor? Floor { get => GetValue(FloorProperty); set => SetValue(FloorProperty, value); }
    public IReadOnlyList<MarkerVisual>? Markers { get => GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }
    public IReadOnlyList<MapPin>? Pins { get => GetValue(PinsProperty); set => SetValue(PinsProperty, value); }

    public const double MinZoom = 0.5;
    public const double MaxZoom = 8;

    public LatLng Center { get; private set; }
    public double Zoom { get; private set; } = 3;

    // ---------------------------------------------------------------- zdarzenia
    public event EventHandler<MarkerClickEventArgs>? MarkerClicked;
    public event EventHandler<MapPointerEventArgs>? MapRightClicked;
    public event EventHandler? BackgroundClicked;
    public event EventHandler<PinEventArgs>? PinClicked;
    public event EventHandler<PinEventArgs>? PinMoved;

    /// <summary>Każda zmiana widoku (także w trakcie przesuwania) — do śledzenia dymków.</summary>
    public event EventHandler? ViewChanging;

    /// <summary>Koniec zmiany widoku — do zapisu pozycji.</summary>
    public event EventHandler? ViewChanged;

    // ---------------------------------------------------------------- pędzle
    private static readonly IBrush Background = new SolidColorBrush(Color.Parse("#08090B"));
    private static readonly IBrush BaseDim = new SolidColorBrush(Color.Parse("#B8000000"));
    private static readonly IBrush IconInk = new SolidColorBrush(Color.Parse("#111317"));
    private static readonly IPen MarkerOutline = new Pen(new SolidColorBrush(Color.Parse("#E6080A0C")), 1.5);
    private static readonly IPen HighlightPen = new Pen(new SolidColorBrush(Color.Parse("#FFFFFF")), 2.5);
    private static readonly IBrush FavoriteBrush = new SolidColorBrush(Color.Parse("#FFC72C"));
    private static readonly IBrush LabelBack = new SolidColorBrush(Color.Parse("#F2121418"));
    private static readonly IPen LabelBorder = new Pen(new SolidColorBrush(Color.Parse("#363C47")), 1);
    private static readonly IBrush LabelText = new SolidColorBrush(Color.Parse("#ECE7DC"));
    private static readonly IBrush PinFill = new SolidColorBrush(Color.Parse("#FFC72C"));
    private static readonly IPen PinOutline = new Pen(new SolidColorBrush(Color.Parse("#0B0C0E")), 1.5);
    private static readonly Typeface LabelFace = new(new FontFamily("avares://ArcSperanzaIntel/Assets/Fonts#Barlow"), FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Dictionary<Color, IBrush> BrushCache = [];

    private static IBrush BrushOf(Color c)
    {
        if (!BrushCache.TryGetValue(c, out var b)) BrushCache[c] = b = new SolidColorBrush(c);
        return b;
    }

    // ---------------------------------------------------------------- stan interakcji
    private readonly List<(MarkerVisual Marker, Point P)> _drawn = [];
    private readonly HashSet<string> _requestedTiles = [];
    private MarkerVisual? _hover;
    private MapPin? _hoverPin;
    private Point _pressPoint;
    private Point _lastPoint;
    private bool _panning;
    private bool _moved;
    private MapPin? _draggingPin;
    private bool _badges;

    // animacja (przelot, płynny zoom) — klatki z RequestAnimationFrame, nie z timera
    private Func<double, bool>? _animation;   // argument: ms od startu; zwraca true = koniec
    private readonly Stopwatch _animClock = new();
    private bool _frameRequested;
    private double _wheelTarget;
    private Point _wheelScreen;
    private LatLng _wheelAnchor;
    private bool _wheelActive;

    // gotowe bitmapy markerów — rysowanie geometrii ikon w każdej klatce było główną przyczyną klatkowania
    private readonly Dictionary<BadgeKey, (RenderTargetBitmap Bmp, double Size)> _badgeCache = [];
    private double _badgeScale = 1;

    private readonly record struct BadgeKey(Geometry? Icon, Color Color, MarkerShape Shape, double Radius, bool Highlight, bool Dimmed, bool Dot);

    // ---------------------------------------------------------------- widok
    public void SetView(LatLng center, double zoom)
    {
        StopFlight();
        Center = ClampCenter(center);
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        RaiseViewChanged(final: true);
    }

    /// <summary>Płynny przelot do punktu.</summary>
    public void FlyTo(LatLng target, double zoom, int durationMs = 650)
    {
        StopFlight();
        var start = Center;
        var startZoom = Zoom;
        var endZoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        Animate(ms =>
        {
            var t = Math.Min(1, ms / durationMs);
            var e = t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2; // easeInOutCubic
            Center = new LatLng(start.Lat + (target.Lat - start.Lat) * e, start.Lng + (target.Lng - start.Lng) * e);
            Zoom = startZoom + (endZoom - startZoom) * e;
            return t >= 1;
        });
    }

    private void Animate(Func<double, bool> step)
    {
        _animation = step;
        _animClock.Restart();
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (_frameRequested) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        _frameRequested = true;
        top.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        _frameRequested = false;
        var step = _animation;
        if (step is null) return;
        var done = step(_animClock.Elapsed.TotalMilliseconds);
        if (done) StopFlight();
        RaiseViewChanged(final: done);
        if (!done) RequestFrame();
    }

    public void FlyToBounds(LatLngBounds bounds, double maxZoom = 5)
    {
        if (Map is null || Bounds.Width < 10) return;
        // zoom, przy którym prostokąt mieści się w widoku z marginesem
        var (x1, y1) = Map.Transform.ToPixel(bounds.Min, 0);
        var (x2, y2) = Map.Transform.ToPixel(bounds.Max, 0);
        var w = Math.Max(1e-6, Math.Abs(x2 - x1));
        var h = Math.Max(1e-6, Math.Abs(y2 - y1));
        var z = Math.Log2(Math.Min(Bounds.Width * 0.6 / w, Bounds.Height * 0.6 / h));
        FlyTo(bounds.Center, Math.Min(maxZoom, z));
    }

    public void ZoomBy(double delta) =>
        FlyTo(Center, Math.Clamp(Zoom + delta, MinZoom, MaxZoom), 250);

    private void StopFlight()
    {
        _animation = null;
        _wheelActive = false;
    }

    private void RaiseViewChanged(bool final)
    {
        InvalidateVisual();
        ViewChanging?.Invoke(this, EventArgs.Empty);
        if (final) ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private LatLng ClampCenter(LatLng c)
    {
        if (Map is null) return c;
        var b = Map.Bounds;
        return new LatLng(Math.Clamp(c.Lat, b.Min.Lat, b.Max.Lat), Math.Clamp(c.Lng, b.Min.Lng, b.Max.Lng));
    }

    /// <summary>Punkt na ekranie (w układzie tej kontrolki) dla współrzędnych mapy.</summary>
    public Point ToScreen(LatLng p)
    {
        if (Map is null) return default;
        var (cx, cy) = Map.Transform.ToPixel(Center, Zoom);
        var (x, y) = Map.Transform.ToPixel(p, Zoom);
        return new Point(x - cx + Bounds.Width / 2, y - cy + Bounds.Height / 2);
    }

    public LatLng ToLatLng(Point screen)
    {
        if (Map is null) return default;
        var (cx, cy) = Map.Transform.ToPixel(Center, Zoom);
        return Map.Transform.FromPixel(cx + screen.X - Bounds.Width / 2, cy + screen.Y - Bounds.Height / 2, Zoom);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MapProperty)
        {
            StopFlight();
            _requestedTiles.Clear();
            _hover = null;
            if (Map is not null)
            {
                Center = Map.Center;
                Zoom = Map.InitialZoom;
            }
            InvalidateVisual();
        }
        else if (change.Property == FloorProperty)
        {
            _requestedTiles.Clear();
        }
        else if (change.Property == BoundsProperty)
        {
            InvalidateVisual();
        }
    }

    // ---------------------------------------------------------------- rysowanie
    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        ctx.FillRectangle(Background, new Rect(size));
        var map = Map;
        if (map is null) return;

        using (ctx.PushClip(new Rect(size)))
        {
            var tz = (int)Math.Clamp(Math.Round(Zoom), 2, map.MaxNativeZoom);
            // warstwa bazowa zawsze; wybrane piętro (tunele, podziemia, dolne piętro) na wierzchu — jak w MetaForge
            DrawLayer(ctx, map, null, tz, size);
            var floor = Floor;
            if (floor is not null && !ReferenceEquals(floor, map.BaseFloor) && floor.Id != map.BaseFloor?.Id)
            {
                // powierzchnia przyciemniona, żeby było jasne, które piętro jest wybrane (MetaForge: brightness 0.3)
                ctx.FillRectangle(BaseDim, new Rect(size));
                DrawLayer(ctx, map, floor, tz, size);
            }
            DrawMarkers(ctx, map, size);
            DrawPins(ctx, map, size);
            DrawHoverLabel(ctx);
        }
    }

    private void DrawLayer(DrawingContext ctx, MapDefinition map, MapFloor? layer, int tz, Size size)
    {
        // zgrubny podkład tylko wtedy, gdy brakuje jeszcze dokładnych kafelków (mniej rysowania przy przesuwaniu)
        if (tz != 2 && DrawTiles(ctx, map, layer, tz, size, dryRun: true))
        {
            DrawTiles(ctx, map, layer, tz, size);
            return;
        }
        DrawTiles(ctx, map, layer, 2, size);
        if (tz != 2) DrawTiles(ctx, map, layer, tz, size);
    }

    /// <summary>Rysuje kafelki widocznego obszaru. dryRun = tylko sprawdza, czy wszystkie są już w pamięci.</summary>
    private bool DrawTiles(DrawingContext ctx, MapDefinition map, MapFloor? layer, int tz, Size size, bool dryRun = false)
    {
        var ts = (double)map.TileSize;
        var images = ImageCache.Shared;
        if (images is null) return false;
        var f = Math.Pow(2, Zoom - tz);
        var (cx, cy) = map.Transform.ToPixel(Center, tz);

        var (bx1, by1) = map.Transform.ToPixel(map.Bounds.Min, tz);
        var (bx2, by2) = map.Transform.ToPixel(map.Bounds.Max, tz);
        var minX = Math.Max(Math.Min(bx1, bx2), cx - size.Width / 2 / f);
        var maxX = Math.Min(Math.Max(bx1, bx2), cx + size.Width / 2 / f);
        var minY = Math.Max(Math.Min(by1, by2), cy - size.Height / 2 / f);
        var maxY = Math.Min(Math.Max(by1, by2), cy + size.Height / 2 / f);
        if (maxX <= minX || maxY <= minY) return true;

        var tx1 = Math.Max(0, (int)Math.Floor(minX / ts));
        var tx2 = (int)Math.Floor((maxX - 0.001) / ts);
        var ty1 = Math.Max(0, (int)Math.Floor(minY / ts));
        var ty2 = (int)Math.Floor((maxY - 0.001) / ts);
        if ((tx2 - tx1 + 1) * (ty2 - ty1 + 1) > 400) return false; // bezpiecznik

        var tileSize = ts * f;
        for (var tx = tx1; tx <= tx2; tx++)
        for (var ty = ty1; ty <= ty2; ty++)
        {
            var url = map.TileUrl(tz, tx, ty, layer);
            var bmp = images.Peek(url);
            if (dryRun)
            {
                if (bmp is null && !images.IsKnownMissing(url)) return false;
                continue;
            }
            if (bmp is null)
            {
                if (!images.IsKnownMissing(url) && _requestedTiles.Add(url))
                {
                    // po wczytaniu zwalniamy wpis, żeby kafelek wyrzucony z pamięci dało się wczytać ponownie
                    _ = images.GetAsync(url).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
                    {
                        _requestedTiles.Remove(url);
                        InvalidateVisual();
                    }), TaskScheduler.Default);
                }
                continue;
            }
            var x = (tx * ts - cx) * f + size.Width / 2;
            var y = (ty * ts - cy) * f + size.Height / 2;
            ctx.DrawImage(bmp, new Rect(bmp.Size), new Rect(x, y, tileSize + 0.6, tileSize + 0.6));
        }
        return true;
    }

    private void DrawMarkers(DrawingContext ctx, MapDefinition map, Size size)
    {
        _drawn.Clear();
        var markers = Markers;
        if (markers is null) return;

        var (cx, cy) = map.Transform.ToPixel(Center, Zoom);
        var hw = size.Width / 2;
        var hh = size.Height / 2;
        var pad = 20.0;

        foreach (var m in markers)
        {
            var (x, y) = map.Transform.ToPixel(m.Marker.Position, Zoom);
            var p = new Point(x - cx + hw, y - cy + hh);
            if (p.X < -pad || p.Y < -pad || p.X > size.Width + pad || p.Y > size.Height + pad) continue;
            _drawn.Add((m, p));
        }

        // Dużo punktów na ekranie → kropki; mniej → pełne ikony.
        _badges = _drawn.Count < 900 || Zoom >= map.InitialZoom + 1.2;

        // Najpierw zwykłe, potem przyciemnione pod spodem, na końcu questy na wierzchu.
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (Math.Abs(scale - _badgeScale) > 0.01)
        {
            _badgeScale = scale;
            ClearBadgeCache();
        }
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var (m, p) in _drawn)
            {
                var inPass = pass switch
                {
                    0 => m.Dimmed,
                    1 => !m.Dimmed && m.Shape == MarkerShape.Circle,
                    _ => !m.Dimmed && m.Shape == MarkerShape.Diamond,
                };
                if (inPass) DrawMarker(ctx, m, p);
            }
        }
    }

    private void ClearBadgeCache()
    {
        foreach (var (bmp, _) in _badgeCache.Values) bmp.Dispose();
        _badgeCache.Clear();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopFlight();
        ClearBadgeCache();
    }

    /// <summary>Marker narysowany raz do bitmapy, potem tylko kopiowany.</summary>
    private (RenderTargetBitmap Bmp, double Size) Badge(BadgeKey key)
    {
        if (_badgeCache.TryGetValue(key, out var hit)) return hit;

        var size = key.Dot ? 14.0 : key.Radius * 2 + (key.Shape == MarkerShape.Diamond ? 8 : 6);
        var px = Math.Max(1, (int)Math.Ceiling(size * _badgeScale));
        var bmp = new RenderTargetBitmap(new PixelSize(px, px), new Vector(96, 96));
        using (var dc = bmp.CreateDrawingContext())
        using (dc.PushTransform(Matrix.CreateScale(_badgeScale, _badgeScale)))
        using (dc.PushOpacity(key.Dimmed ? 0.45 : 1))
        {
            var c = new Point(size / 2, size / 2);
            var fill = BrushOf(key.Color);
            if (key.Dot)
            {
                dc.DrawEllipse(fill, MarkerOutline, c, 4.5, 4.5);
            }
            else
            {
                var r = key.Radius;
                var pen = key.Highlight ? HighlightPen : MarkerOutline;
                if (key.Shape == MarkerShape.Diamond)
                {
                    var diamond = new StreamGeometry();
                    using (var g = diamond.Open())
                    {
                        g.BeginFigure(new Point(c.X, c.Y - r), true);
                        g.LineTo(new Point(c.X + r, c.Y));
                        g.LineTo(new Point(c.X, c.Y + r));
                        g.LineTo(new Point(c.X - r, c.Y));
                        g.EndFigure(true);
                    }
                    dc.DrawGeometry(fill, pen, diamond);
                }
                else
                {
                    dc.DrawEllipse(fill, pen, c, r, r);
                }
                if (key.Icon is not null)
                {
                    var iconSize = (key.Shape == MarkerShape.Diamond ? r - 2 : r) * 1.25;
                    var s = iconSize / MarkerIconData.ViewBox;
                    using (dc.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(c.X - iconSize / 2, c.Y - iconSize / 2)))
                        dc.DrawGeometry(IconInk, null, key.Icon);
                }
            }
        }
        if (_badgeCache.Count > 3000) ClearBadgeCache();
        return _badgeCache[key] = (bmp, size);
    }

    private void DrawMarker(DrawingContext ctx, MarkerVisual m, Point p)
    {
        var isHover = ReferenceEquals(m, _hover);
        var dot = !_badges && m.Shape == MarkerShape.Circle && !isHover && !m.Highlighted;
        var r = isHover ? 13.5 : 11.5;
        if (m.Shape == MarkerShape.Diamond) r += 2;

        var key = dot
            ? new BadgeKey(null, m.Color, MarkerShape.Circle, 4.5, false, m.Dimmed, true)
            : new BadgeKey(m.Icon, m.Color, m.Shape, r, m.Highlighted, m.Dimmed, false);
        var (bmp, size) = Badge(key);
        ctx.DrawImage(bmp, new Rect(0, 0, bmp.PixelSize.Width, bmp.PixelSize.Height),
            new Rect(p.X - size / 2, p.Y - size / 2, size, size));

        if (!m.Favorite) return;
        if (dot) ctx.DrawEllipse(FavoriteBrush, null, new Point(p.X + 4, p.Y - 4), 2.5, 2.5);
        else ctx.DrawEllipse(FavoriteBrush, MarkerOutline, new Point(p.X + r * 0.75, p.Y - r * 0.75), 4, 4);
    }

    private void DrawPins(DrawingContext ctx, MapDefinition map, Size size)
    {
        var pins = Pins;
        if (pins is null) return;
        foreach (var pin in pins)
        {
            if (Floor is not null && pin.FloorId is not null && pin.FloorId != Floor.Id) continue;
            var p = ToScreen(new LatLng(pin.Lat, pin.Lng));
            if (p.X < -30 || p.Y < -30 || p.X > size.Width + 30 || p.Y > size.Height + 30) continue;
            const double w = 26;
            var s = w / 24.0;
            using (ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(p.X - w / 2, p.Y - w)))
                ctx.DrawGeometry(PinFill, PinOutline, UiIcons.Pin);
        }
    }

    private void DrawHoverLabel(DrawingContext ctx)
    {
        string? text = null;
        Point at = default;
        if (_hoverPin is not null)
        {
            text = string.IsNullOrWhiteSpace(_hoverPin.Note) ? Loc.T("MyPinLabel") : _hoverPin.Note;
            at = ToScreen(new LatLng(_hoverPin.Lat, _hoverPin.Lng)) - new Point(0, 26);
        }
        else if (_hover is not null)
        {
            text = _hover.Label;
            var found = _drawn.FirstOrDefault(d => ReferenceEquals(d.Marker, _hover));
            if (found.Marker is null) return;
            at = found.P - new Point(0, 16);
        }
        if (string.IsNullOrEmpty(text)) return;

        var ft = new FormattedText(text.Length > 80 ? text[..80] + "…" : text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, LabelFace, 13, LabelText);
        var box = new Rect(at.X - ft.Width / 2 - 8, at.Y - ft.Height - 6, ft.Width + 16, ft.Height + 6);
        ctx.DrawRectangle(LabelBack, LabelBorder, box, 2, 2);
        ctx.DrawText(ft, new Point(box.X + 8, box.Y + 3));
    }

    // ---------------------------------------------------------------- trafienia
    private MarkerVisual? HitMarker(Point p)
    {
        MarkerVisual? best = null;
        var bestD = double.MaxValue;
        var radius = _badges ? 14.0 : 8.0;
        for (var i = _drawn.Count - 1; i >= 0; i--)
        {
            var (m, mp) = _drawn[i];
            var r = m.Shape == MarkerShape.Diamond ? radius + 4 : radius;
            var d = Math.Sqrt(Math.Pow(mp.X - p.X, 2) + Math.Pow(mp.Y - p.Y, 2));
            if (d <= r && d < bestD)
            {
                best = m;
                bestD = d;
            }
        }
        return best;
    }

    private MapPin? HitPin(Point p)
    {
        if (Pins is null) return null;
        foreach (var pin in Pins.Reverse())
        {
            if (Floor is not null && pin.FloorId is not null && pin.FloorId != Floor.Id) continue;
            var sp = ToScreen(new LatLng(pin.Lat, pin.Lng));
            if (Math.Abs(sp.X - p.X) <= 13 && p.Y <= sp.Y + 2 && p.Y >= sp.Y - 28) return pin;
        }
        return null;
    }

    // ---------------------------------------------------------------- wejście
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pt = e.GetCurrentPoint(this);
        var pos = pt.Position;
        _pressPoint = _lastPoint = pos;
        _moved = false;

        if (pt.Properties.IsRightButtonPressed)
        {
            MapRightClicked?.Invoke(this, new MapPointerEventArgs(ToLatLng(pos), pos));
            e.Handled = true;
            return;
        }
        if (!pt.Properties.IsLeftButtonPressed) return;

        StopFlight();
        _draggingPin = HitPin(pos);
        _panning = _draggingPin is null;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_panning || _draggingPin is not null)
        {
            if (!_moved && Math.Abs(pos.X - _pressPoint.X) + Math.Abs(pos.Y - _pressPoint.Y) > 4) _moved = true;
            if (!_moved) return;
            if (_draggingPin is not null)
            {
                var ll = ToLatLng(pos);
                _draggingPin.Lat = ll.Lat;
                _draggingPin.Lng = ll.Lng;
                InvalidateVisual();
            }
            else if (Map is not null)
            {
                var d = pos - _lastPoint;
                var (cx, cy) = Map.Transform.ToPixel(Center, Zoom);
                Center = ClampCenter(Map.Transform.FromPixel(cx - d.X, cy - d.Y, Zoom));
                RaiseViewChanged(final: false);
            }
            _lastPoint = pos;
            return;
        }

        var pin = HitPin(pos);
        var hover = pin is null ? HitMarker(pos) : null;
        if (!ReferenceEquals(hover, _hover) || !ReferenceEquals(pin, _hoverPin))
        {
            _hover = hover;
            _hoverPin = pin;
            Cursor = hover is not null || pin is not null ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pos = e.GetPosition(this);
        var wasPanning = _panning;
        var pin = _draggingPin;
        _panning = false;
        _draggingPin = null;
        e.Pointer.Capture(null);

        if (pin is not null)
        {
            if (_moved) PinMoved?.Invoke(this, new PinEventArgs(pin, pos));
            else PinClicked?.Invoke(this, new PinEventArgs(pin, pos));
            return;
        }
        if (!wasPanning) return;
        if (_moved)
        {
            ViewChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        var hit = HitMarker(pos);
        if (hit is not null) MarkerClicked?.Invoke(this, new MarkerClickEventArgs(hit, pos));
        else BackgroundClicked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover is null && _hoverPin is null) return;
        _hover = null;
        _hoverPin = null;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Map is null) return;
        e.Handled = true;
        var pos = e.GetPosition(this);
        var baseZoom = _wheelActive ? _wheelTarget : Zoom;
        var target = Math.Clamp(baseZoom + e.Delta.Y * 0.5, MinZoom, MaxZoom);
        if (Math.Abs(target - Zoom) < 1e-6) return;

        // płynne dojście do docelowego zoomu; punkt pod kursorem zostaje pod kursorem
        _wheelTarget = target;
        _wheelScreen = pos;
        _wheelAnchor = ToLatLng(pos);
        if (_wheelActive) return;   // animacja już trwa — tylko nowy cel

        StopFlight();
        _wheelActive = true;
        var last = 0.0;
        Animate(ms =>
        {
            var dt = Math.Max(1, ms - last);
            last = ms;
            var k = 1 - Math.Exp(-dt / 70.0);
            Zoom += (_wheelTarget - Zoom) * k;
            var done = Math.Abs(_wheelTarget - Zoom) < 0.003;
            if (done) Zoom = _wheelTarget;
            var map = Map;
            if (map is not null)
            {
                var (ax, ay) = map.Transform.ToPixel(_wheelAnchor, Zoom);
                Center = ClampCenter(map.Transform.FromPixel(ax - (_wheelScreen.X - Bounds.Width / 2),
                    ay - (_wheelScreen.Y - Bounds.Height / 2), Zoom));
            }
            return done;
        });
    }
}
