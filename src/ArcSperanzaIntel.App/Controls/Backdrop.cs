using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>
/// Tło pod zakładkami zamiast płaskiej czerni: bardzo delikatne ukośne linie i dwie miękkie poświaty
/// w barwach ARC (żółta z lewej u góry, błękitna z prawej u dołu). Rysuje się tylko przy zmianie rozmiaru.
/// </summary>
public sealed class Backdrop : Control
{
    private static readonly IBrush Base = new SolidColorBrush(Color.Parse("#0D0F12"));
    private static readonly IPen Line = new Pen(new SolidColorBrush(Color.FromArgb(0x05, 0xFF, 0xFF, 0xFF)), 1.5);

    private static readonly IBrush WarmGlow = new RadialGradientBrush
    {
        Center = new RelativePoint(0.12, -0.15, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(0.12, -0.15, RelativeUnit.Relative),
        Radius = 0.75,
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x13, 0xFF, 0xC7, 0x2C), 0),
            new GradientStop(Color.FromArgb(0x00, 0xFF, 0xC7, 0x2C), 1),
        },
    };

    private static readonly IBrush CoolGlow = new RadialGradientBrush
    {
        Center = new RelativePoint(1.05, 1.1, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(1.05, 1.1, RelativeUnit.Relative),
        Radius = 0.7,
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0x10, 0x36, 0xB5, 0xE6), 0),
            new GradientStop(Color.FromArgb(0x00, 0x36, 0xB5, 0xE6), 1),
        },
    };

    public Backdrop()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext ctx)
    {
        var r = new Rect(Bounds.Size);
        if (r.Width <= 0 || r.Height <= 0) return;
        ctx.FillRectangle(Base, r);
        using (ctx.PushClip(r))
        {
            // ukośne linie co 9 px (135°)
            const double step = 9;
            for (var x = -r.Height; x < r.Width; x += step)
                ctx.DrawLine(Line, new Point(x, r.Height), new Point(x + r.Height, 0));
        }
        ctx.FillRectangle(WarmGlow, r);
        ctx.FillRectangle(CoolGlow, r);
    }
}
