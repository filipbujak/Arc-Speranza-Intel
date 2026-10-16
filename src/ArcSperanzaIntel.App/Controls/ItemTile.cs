using ArcSperanzaIntel.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ArcSperanzaIntel.App.Controls;

public enum RarityTier { None, Common, Uncommon, Rare, Epic, Legendary, Amplified }

/// <summary>
/// Kafelek przedmiotu jak w ekwipunku ARC Raiders (proporcje i kolory wzięte z kafelków ARC Raiders Wiki,
/// które odwzorowują grę):
/// <list type="bullet">
/// <item>ramka z gradientem: kolor rzadkości w lewym dolnym rogu → szary,</item>
/// <item>granatowe pole, poświata rzadkości od lewego dolnego rogu,</item>
/// <item>zakrzywiony znak rzadkości w lewym dolnym rogu (większy dla wyższej rzadkości),</item>
/// <item>schemat: granatowa siatka; Amplified: pomarańczowe pasy u dołu; projekt mebla (Design): wzór z krzyżyków.</item>
/// </list>
/// Wszystko rysowane kodem — jeden wygląd w całej aplikacji, strojenie tylko tutaj.
/// </summary>
public sealed class ItemTile : Decorator
{
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<ItemTile, string?>(nameof(Url));

    /// <summary>Kolor rzadkości (binding do RarityBrush) — z niego rozpoznajemy też samą rzadkość.</summary>
    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<ItemTile, IBrush?>(nameof(Accent));

    public static readonly StyledProperty<bool> IsBlueprintProperty =
        AvaloniaProperty.Register<ItemTile, bool>(nameof(IsBlueprint));

    /// <summary>Projekt mebla do kryjówki (MetaForge: item_type "Design").</summary>
    public static readonly StyledProperty<bool> IsDesignProperty =
        AvaloniaProperty.Register<ItemTile, bool>(nameof(IsDesign));

    public static readonly StyledProperty<int> DecodeWidthProperty =
        AvaloniaProperty.Register<ItemTile, int>(nameof(DecodeWidth), 128);

    // ---- wygląd (wartości z wiki dla kafelka 120 px)
    private const double Ref = 120;
    private static readonly Color Frame = Color.Parse("#3A3F49");
    private static readonly Color BlueprintFrame = Color.Parse("#6A7080");
    private static readonly Color Body = Color.Parse("#050D24");
    private static readonly Color BpTop = Color.Parse("#08112C");
    private static readonly Color BpBottom = Color.Parse("#0B1A44");
    private static readonly Color BpLine = Color.Parse("#343778");
    private static readonly Color BpLineFaint = Color.Parse("#1C2A5E");

    private readonly RemoteImage _image = new();
    private readonly CornerMark _mark = new() { IsHitTestVisible = false };

    static ItemTile()
    {
        AffectsRender<ItemTile>(AccentProperty, IsBlueprintProperty, IsDesignProperty);
    }

    public ItemTile()
    {
        ClipToBounds = true;
        var panel = new Panel();
        panel.Children.Add(_image);
        panel.Children.Add(_mark); // znak rzadkości leży nad ikoną, jak w grze
        Child = panel;
    }

    public string? Url { get => GetValue(UrlProperty); set => SetValue(UrlProperty, value); }
    public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public bool IsBlueprint { get => GetValue(IsBlueprintProperty); set => SetValue(IsBlueprintProperty, value); }
    public bool IsDesign { get => GetValue(IsDesignProperty); set => SetValue(IsDesignProperty, value); }
    public int DecodeWidth { get => GetValue(DecodeWidthProperty); set => SetValue(DecodeWidthProperty, value); }

    private Color AccentColor => (Accent as ISolidColorBrush)?.Color ?? Palette.Rarity(null);
    private RarityTier Tier => IsBlueprint ? RarityTier.None : Palette.TierOf(AccentColor);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UrlProperty) _image.Url = Url;
        else if (change.Property == DecodeWidthProperty) _image.DecodeWidth = DecodeWidth;
        else if (change.Property == AccentProperty || change.Property == IsBlueprintProperty)
            _mark.Set(Tier, AccentColor);
        else if (change.Property == BoundsProperty)
        {
            var s = Scale;
            var inset = Math.Round(Math.Min(Bounds.Width, Bounds.Height) * 0.08) + 2 * s;
            _image.Margin = new Thickness(inset);
        }
    }

    private double Scale => Math.Max(0.2, Math.Min(Bounds.Width, Bounds.Height) / Ref);

    public override void Render(DrawingContext ctx)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        var s = Scale;
        var outer = new Rect(size);
        var radius = 10 * s;
        var c = AccentColor;
        var tier = Tier;

        // ramka: gradient od lewego dolnego rogu ("to top right")
        var frameStart = IsBlueprint ? BlueprintFrame : tier == RarityTier.Common || tier == RarityTier.None ? Color.Parse("#7A7F88") : c;
        var frame = Cache.Get(("frame", frameStart, tier == RarityTier.Rare), () => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(frameStart, tier == RarityTier.Rare ? 0.3 : 0),
                new GradientStop(Frame, 0.65),
            },
        });
        ctx.DrawRectangle(frame, null, outer, radius, radius);

        var border = Math.Max(1, 2 * s);
        var body = outer.Deflate(border);
        var bodyRadius = Math.Max(1, radius - border);
        using (ctx.PushClip(new RoundedRect(body, bodyRadius)))
        {
            if (IsBlueprint) DrawBlueprint(ctx, body, s);
            else
            {
                ctx.FillRectangle(Cache.Solid(Body), body);
                if (tier == RarityTier.Amplified) DrawAmplified(ctx, body, c);
                else DrawGlow(ctx, body, c, tier);
                if (IsDesign) DrawDesign(ctx, body, c, s);
            }
        }
    }

    /// <summary>Poświata: od prawego górnego rogu przezroczysta, w stronę lewego dolnego kolor rzadkości (~30%).</summary>
    private static void DrawGlow(DrawingContext ctx, Rect r, Color c, RarityTier tier)
    {
        var alpha = tier switch
        {
            RarityTier.Rare => 0.34,
            RarityTier.Epic => 0.32,
            RarityTier.Common or RarityTier.None => 0.25,
            _ => 0.3,
        };
        var tint = tier is RarityTier.Common or RarityTier.None ? Color.Parse("#999FA5") : c;
        var glow = Cache.Get(("glow", tint, alpha), () => new RadialGradientBrush
        {
            Center = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(1, 0, RelativeUnit.Relative),
            Radius = 1.42,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, tint.R, tint.G, tint.B), 0.35),
                new GradientStop(Color.FromArgb((byte)(alpha * 255), tint.R, tint.G, tint.B), 1),
            },
        });
        ctx.FillRectangle(glow, r);
    }

    /// <summary>Schemat: granatowe pole z siatką (grubsze linie co ćwiartkę, cienkie pomiędzy).</summary>
    private static void DrawBlueprint(DrawingContext ctx, Rect r, double s)
    {
        var back = Cache.Get(("bp", BpTop, 0), () => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(BpTop, 0), new GradientStop(BpBottom, 0.55), new GradientStop(BpTop, 1) },
        });
        ctx.FillRectangle(back, r);
        var major = new Pen(Cache.Solid(BpLine), Math.Max(1, 1.6 * s));
        var minor = new Pen(Cache.Solid(BpLineFaint), Math.Max(0.6, 0.8 * s));
        var step = r.Width / 8;
        for (var i = 1; i < 8; i++)
        {
            var pen = i % 2 == 0 ? major : minor;
            var x = r.X + i * step;
            var y = r.Y + i * (r.Height / 8);
            ctx.DrawLine(pen, new Point(x, r.Y), new Point(x, r.Bottom));
            ctx.DrawLine(pen, new Point(r.X, y), new Point(r.Right, y));
        }
    }

    /// <summary>Amplified: pomarańcz od dołu i cztery pasy o rosnącej jasności.</summary>
    private static void DrawAmplified(DrawingContext ctx, Rect r, Color c)
    {
        var up = Cache.Get(("amp", c, 0), () => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(97, c.R, c.G, c.B), 0),
                new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 0.87),
            },
        });
        ctx.FillRectangle(up, r);
        var h = r.Height * 0.38;
        var top = r.Bottom - h;
        (double Y, double H, double A)[] bars = [(0, 9, 0.25), (10, 8, 0.5), (19, 7, 0.75), (28, 8, 1)];
        foreach (var (y, bh, a) in bars)
            ctx.FillRectangle(Cache.Solid(Color.FromArgb((byte)(a * 255 * 0.55), c.R, c.G, c.B)),
                new Rect(r.X, top + y / 36 * h, r.Width, bh / 36 * h));
    }

    /// <summary>Projekt mebla: siatka krzyżyków w kolorze rzadkości, wygaszona przy brzegach.</summary>
    private static void DrawDesign(DrawingContext ctx, Rect r, Color c, double s)
    {
        var step = 22 * s;
        var pen = new Pen(Cache.Solid(Color.FromArgb(46, c.R, c.G, c.B)), Math.Max(0.6, 0.9 * s));
        var dot = Cache.Solid(Color.FromArgb(120, c.R, c.G, c.B));
        var arm = 2.6 * s;
        for (var x = r.X + r.Width / 2 % step; x < r.Right; x += step)
            ctx.DrawLine(pen, new Point(x, r.Y), new Point(x, r.Bottom));
        for (var y = r.Bottom - step / 2; y > r.Y; y -= step)
            ctx.DrawLine(pen, new Point(r.X, y), new Point(r.Right, y));
        for (var x = r.X + r.Width / 2 % step; x < r.Right; x += step)
        for (var y = r.Bottom - step / 2; y > r.Y; y -= step)
        {
            ctx.FillRectangle(dot, new Rect(x - arm, y - 0.5 * s, arm * 2, 1.2 * s));
            ctx.FillRectangle(dot, new Rect(x - 0.5 * s, y - arm, 1.2 * s, arm * 2));
        }
    }

    /// <summary>Zakrzywiony znak rzadkości w lewym dolnym rogu — osobna warstwa nad ikoną.</summary>
    private sealed class CornerMark : Control
    {
        private RarityTier _tier;
        private Color _color;

        public void Set(RarityTier tier, Color color)
        {
            _tier = tier;
            _color = color;
            InvalidateVisual();
        }

        public override void Render(DrawingContext ctx)
        {
            // wiki: ramka 20 px, promień 40 (common) … 60 (legendary), róg wysunięty o 21 px poza pole
            var outerR = _tier switch
            {
                RarityTier.Common => 40,
                RarityTier.Uncommon => 45,
                RarityTier.Rare => 50,
                RarityTier.Epic => 55,
                RarityTier.Legendary => 60,
                _ => 0,
            };
            if (outerR == 0 || Bounds.Width <= 0) return;
            var s = Math.Min(Bounds.Width, Bounds.Height) / Ref;
            var color = _tier switch
            {
                RarityTier.Common => Color.Parse("#6C6C6C"),
                RarityTier.Epic => Color.Parse("#AF2E86"),
                _ => _color,
            };
            var h = Bounds.Height;
            var border = Math.Max(1, 2 * s);
            var body = new Rect(Bounds.Size).Deflate(border);
            using var clip = ctx.PushClip(new RoundedRect(body, Math.Max(1, 10 * s - border)));
            var cx = (-21 + outerR) * s;
            var cy = h - (-21 + outerR) * s;
            var ro = outerR * s;
            var ri = (outerR - 20) * s;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                // ćwiartka pierścienia skierowana w lewy dolny róg
                c.BeginFigure(new Point(cx, cy + ro), true);
                c.ArcTo(new Point(cx - ro, cy), new Size(ro, ro), 0, false, SweepDirection.Clockwise);
                c.LineTo(new Point(cx - ri, cy));
                c.ArcTo(new Point(cx, cy + ri), new Size(ri, ri), 0, false, SweepDirection.CounterClockwise);
                c.EndFigure(true);
            }
            ctx.DrawGeometry(Cache.Solid(color), null, g);
        }
    }

    private static class Cache
    {
        private static readonly Dictionary<Color, IBrush> Brushes = [];
        private static readonly Dictionary<object, IBrush> Gradients = [];

        /// <summary>Pędzle z jednostkami względnymi można współdzielić między kafelkami — tworzymy je raz.</summary>
        public static IBrush Get(object key, Func<IBrush> make)
        {
            if (!Gradients.TryGetValue(key, out var b)) Gradients[key] = b = make();
            return b;
        }

        public static IBrush Solid(Color c)
        {
            if (!Brushes.TryGetValue(c, out var b)) Brushes[c] = b = new SolidColorBrush(c);
            return b;
        }
    }
}
