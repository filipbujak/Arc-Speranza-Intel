using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>
/// Ramka karty z efektem jak w ARC Raiders: po najechaniu obwódka w czterech kolorach ARC
/// (czerwony, żółty, zielony, błękitny) płynie wokół karty. Bez najechania — zwykła cienka linia.
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

    /// <summary>Stałe podświetlenie (np. wybrana karta) — obwódka płynie także bez najechania.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<FlowBorder, bool>(nameof(IsActive));

    private static readonly Color[] ArcColors =
        [Color.Parse("#F0503F"), Color.Parse("#FFC72C"), Color.Parse("#3FD07A"), Color.Parse("#36B5E6"), Color.Parse("#F0503F")];

    private static GradientStops ArcStops()
    {
        var stops = new GradientStops();
        for (var i = 0; i < ArcColors.Length; i++) stops.Add(new GradientStop(ArcColors[i], i / (double)(ArcColors.Length - 1)));
        return stops;
    }

    private const double Period = 220;     // długość jednego pełnego cyklu kolorów w px
    private const double Speed = 0.09;     // px na ms

    private readonly Stopwatch _clock = Stopwatch.StartNew();
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

    private bool Flowing => IsPointerOver || IsActive;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
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

        IBrush? stroke = IdleBrush;
        if (Flowing)
        {
            var offset = _clock.Elapsed.TotalMilliseconds * Speed % Period;
            stroke = new LinearGradientBrush
            {
                GradientStops = ArcStops(),
                SpreadMethod = GradientSpreadMethod.Repeat,
                StartPoint = new RelativePoint(offset, offset * 0.35, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(offset + Period, (offset + Period) * 0.35, RelativeUnit.Absolute),
            };
        }
        var pen = stroke is null ? null : new Pen(stroke, t);
        ctx.DrawRectangle(Background, pen, rect, Radius, Radius);
    }
}
