using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>
/// Ramka karty: po najechaniu żółta obwódka „rysuje się” wokół karty, a potem krąży z krótką przerwą
/// o ostrych końcach — jakby miała się zaraz domknąć. Wybrana karta (IsActive) ma pełną żółtą ramkę.
/// Bez najechania — zwykła cienka linia.
/// </summary>
public sealed class FlowBorder : Decorator
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<FlowBorder>();

    public static readonly StyledProperty<IBrush?> IdleBrushProperty =
        AvaloniaProperty.Register<FlowBorder, IBrush?>(nameof(IdleBrush), new SolidColorBrush(Color.Parse("#22262D")));

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<FlowBorder, double>(nameof(Radius), 8);

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<FlowBorder, double>(nameof(Thickness), 1.5);

    /// <summary>Stałe podświetlenie (np. wybrana karta) — pełna żółta ramka.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<FlowBorder, bool>(nameof(IsActive));

    private static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#FFC72C"));
    private const double DrawInMs = 240;   // czas „rysowania” obwódki po najechaniu
    private const double Coverage = 0.86;  // jaka część obwodu jest zamalowana (reszta = przerwa)
    private const double Speed = 0.16;     // px na ms — prędkość krążenia przerwy
    private TimeSpan _hoverStart;

    private static readonly Stopwatch Clock = Stopwatch.StartNew(); // jeden zegar dla wszystkich kart
    private IPen? _idlePen;
    private bool _frameRequested;

    static FlowBorder()
    {
        AffectsRender<FlowBorder>(BackgroundProperty, IdleBrushProperty, RadiusProperty, ThicknessProperty, IsActiveProperty);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? IdleBrush { get => GetValue(IdleBrushProperty); set => SetValue(IdleBrushProperty, value); }
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    private bool Flowing => IsPointerOver;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IdleBrushProperty || change.Property == ThicknessProperty) _idlePen = null;
        if (change.Property == IsPointerOverProperty && IsPointerOver) _hoverStart = Clock.Elapsed;
        if (change.Property == IsPointerOverProperty || change.Property == IsActiveProperty)
        {
            InvalidateVisual();
            if (Flowing) RequestFrame();
        }
    }

    private void RequestFrame()
    {
        if (_frameRequested) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        _frameRequested = true;
        top.RequestAnimationFrame(_ =>
        {
            _frameRequested = false;
            if (!Flowing) return;
            InvalidateVisual();
            RequestFrame();
        });
    }

    public override void Render(DrawingContext ctx)
    {
        var t = Thickness;
        var rect = new Rect(Bounds.Size).Deflate(t / 2);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        IPen? pen;
        if (Flowing)
        {
            ctx.DrawRectangle(Background, IdlePen(t), rect, Radius, Radius);
            // obwód zaokrąglonego prostokąta; kreska = Coverage obwodu, przerwa krąży
            var r = Math.Min(Radius, Math.Min(rect.Width, rect.Height) / 2);
            var perimeter = 2 * (rect.Width + rect.Height) - (8 - 2 * Math.PI) * r;
            var ms = (Clock.Elapsed - _hoverStart).TotalMilliseconds;
            var grow = Math.Clamp(ms / DrawInMs, 0, 1);
            var ease = 1 - Math.Pow(1 - grow, 3);
            var dash = Math.Max(0.01, perimeter * Coverage * ease);
            var gap = Math.Max(0.01, perimeter - dash);
            var offset = ms * Speed % perimeter;
            var strokeT = t + 0.5;
            var dashes = new DashStyle([dash / strokeT, gap / strokeT], -offset / strokeT);
            ctx.DrawRectangle(null, new Pen(Yellow, strokeT, dashes, PenLineCap.Flat), rect, Radius, Radius);
            return;
        }
        pen = IsActive ? new Pen(Yellow, t) : IdlePen(t);
        ctx.DrawRectangle(Background, pen, rect, Radius, Radius);
    }

    /// <summary>Zwykła ramka: ten sam obiekt przy każdym rysowaniu (setki kart = mniej śmieci dla GC).</summary>
    private IPen? IdlePen(double t) => _idlePen ??= IdleBrush is null ? null : new Pen(IdleBrush, t);
}
