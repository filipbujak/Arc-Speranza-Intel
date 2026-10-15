using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>
/// Kafelek przedmiotu w stylu ekwipunku ARC Raiders: ciemne pole, od dołu poświata w kolorze rzadkości,
/// pasek rzadkości na dole i ikona na środku. Jeden wygląd w całej aplikacji (Itemy, Warsztat, Handlarze,
/// Questy, Szukane, overlay) — kolory i proporcje zmienia się tylko tutaj.
/// </summary>
public sealed class ItemTile : Border
{
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<ItemTile, string?>(nameof(Url));

    /// <summary>Kolor rzadkości (zwykle binding do RarityBrush z ViewModelu).</summary>
    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<ItemTile, IBrush?>(nameof(Accent));

    public static readonly StyledProperty<bool> IsBlueprintProperty =
        AvaloniaProperty.Register<ItemTile, bool>(nameof(IsBlueprint));

    public static readonly StyledProperty<int> DecodeWidthProperty =
        AvaloniaProperty.Register<ItemTile, int>(nameof(DecodeWidth), 128);

    // ---- wygląd (jedno miejsce do strojenia)
    private static readonly Color Base = Color.Parse("#15181D");
    private const byte GlowAlpha = 0x70;   // siła poświaty rzadkości przy dolnej krawędzi
    private const byte EdgeAlpha = 0x55;   // cienka ramka w kolorze rzadkości
    private const double StripHeight = 3;  // pasek rzadkości na dole
    private const double IconInset = 0.10; // margines ikony jako ułamek boku

    private static readonly Dictionary<Color, (IBrush Back, IBrush Edge, IBrush Strip)> Cache = [];

    private readonly RemoteImage _image = new();
    private readonly Border _strip = new() { Height = StripHeight, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom };

    public ItemTile()
    {
        ClipToBounds = true;
        CornerRadius = new CornerRadius(4);
        BorderThickness = new Thickness(1);
        var panel = new Panel();
        panel.Children.Add(_image);
        panel.Children.Add(_strip);
        Child = panel;
        ApplyAccent();
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public IBrush? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public bool IsBlueprint
    {
        get => GetValue(IsBlueprintProperty);
        set => SetValue(IsBlueprintProperty, value);
    }

    public int DecodeWidth
    {
        get => GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UrlProperty) _image.Url = Url;
        else if (change.Property == IsBlueprintProperty) _image.IsBlueprint = IsBlueprint;
        else if (change.Property == DecodeWidthProperty) _image.DecodeWidth = DecodeWidth;
        else if (change.Property == AccentProperty) ApplyAccent();
        else if (change.Property == BoundsProperty)
        {
            var inset = Math.Round(Math.Min(Bounds.Width, Bounds.Height) * IconInset);
            _image.Margin = new Thickness(inset, inset, inset, inset + StripHeight * 0.5);
        }
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private void ApplyAccent()
    {
        var color = (Accent as ISolidColorBrush)?.Color ?? Color.Parse("#4A515D");
        if (!Cache.TryGetValue(color, out var b))
        {
            var back = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Base, 0),
                    new GradientStop(Base, 0.35),
                    new GradientStop(Mix(Base, color, GlowAlpha / 255.0), 1),
                },
            };
            b = (back, new SolidColorBrush(Color.FromArgb(EdgeAlpha, color.R, color.G, color.B)), new SolidColorBrush(color));
            Cache[color] = b;
        }
        Background = b.Back;
        BorderBrush = b.Edge;
        _strip.Background = b.Strip;
    }
}
