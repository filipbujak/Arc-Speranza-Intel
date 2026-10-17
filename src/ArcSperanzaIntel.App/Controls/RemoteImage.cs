using ArcSperanzaIntel.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>
/// Obrazek z adresu URL. Ładuje się dopiero, gdy pojawi się na ekranie (ważne przy 1000+ ikonach itemów).
/// </summary>
public sealed class RemoteImage : Image
{
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<RemoteImage, string?>(nameof(Url));

    /// <summary>Szerokość dekodowania w pikselach (0 = pełny rozmiar).</summary>
    public static readonly StyledProperty<int> DecodeWidthProperty =
        AvaloniaProperty.Register<RemoteImage, int>(nameof(DecodeWidth), 128);

    /// <summary>Schemat: przedmiot na błękitnym tle z białą siatką, jak w grze.</summary>
    public static readonly StyledProperty<bool> IsBlueprintProperty =
        AvaloniaProperty.Register<RemoteImage, bool>(nameof(IsBlueprint));

    private static readonly IBrush BlueprintBack = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse("#2F86C9"), 0), new GradientStop(Color.Parse("#1C5E99"), 1) },
    };

    private static readonly IPen BlueprintGrid = new Pen(new SolidColorBrush(Color.Parse("#45FFFFFF")), 1);

    private static readonly Dictionary<string, Bitmap> BlueprintCache = [];

    public bool IsBlueprint
    {
        get => GetValue(IsBlueprintProperty);
        set => SetValue(IsBlueprintProperty, value);
    }

    /// <summary>
    /// Image.Render jest zapieczętowany, więc schemat rysujemy raz do osobnej bitmapy:
    /// błękitne tło z siatką + ikona przedmiotu w normalnych kolorach.
    /// </summary>
    private static Bitmap Blueprint(string url, Bitmap bmp)
    {
        if (BlueprintCache.TryGetValue(url, out var hit)) return hit;
        var px = bmp.PixelSize;
        var rtb = new RenderTargetBitmap(px, new Vector(96, 96));
        var area = new Rect(0, 0, px.Width, px.Height);
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.DrawRectangle(BlueprintBack, null, area, px.Width * 0.04, px.Width * 0.04);
            var step = Math.Max(6, Math.Min(area.Width, area.Height) / 6);
            for (var x = step; x < area.Width; x += step)
                ctx.DrawLine(BlueprintGrid, new Point(Math.Round(x) + 0.5, 0), new Point(Math.Round(x) + 0.5, area.Height));
            for (var y = step; y < area.Height; y += step)
                ctx.DrawLine(BlueprintGrid, new Point(0, Math.Round(y) + 0.5), new Point(area.Width, Math.Round(y) + 0.5));
            // sam przedmiot w normalnych kolorach, na błękitnym tle z siatką
            ctx.DrawImage(bmp, new Rect(0, 0, px.Width, px.Height), area.Deflate(area.Width * 0.08));
        }
        if (BlueprintCache.Count > 400) BlueprintCache.Clear();
        BlueprintCache[url] = rtb;
        return rtb;
    }

    /// <summary>Ustawia obraz — dla schematu wersję błękitną.</summary>
    private void Show(string url, Bitmap? bmp)
    {
        Source = bmp is not null && IsBlueprint ? Blueprint(url, bmp) : bmp;
    }

    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public int DecodeWidth
    {
        get => GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    private bool _inViewport;
    private string? _loadedUrl;

    public RemoteImage()
    {
        EffectiveViewportChanged += OnViewportChanged;
    }

    protected override Type StyleKeyOverride => typeof(Image);

    private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        _inViewport = e.EffectiveViewport.Width > 0 && e.EffectiveViewport.Height > 0;
        if (_inViewport) TryLoad();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UrlProperty)
        {
            _loadedUrl = null;
            var cached = Url is null ? null : ImageCache.Shared?.Peek(Url, DecodeWidth);
            if (cached is not null && Url is not null)
            {
                Show(Url, cached);
                _loadedUrl = Url;
            }
            else
            {
                Source = null;
                if (_inViewport) TryLoad();
            }
        }
        else if (change.Property == DecodeWidthProperty && _loadedUrl is not null)
        {
            // inny rozmiar = inny wpis w cache; ładujemy ponownie, żeby nie rozciągać mniejszej wersji
            _loadedUrl = null;
            if (_inViewport) TryLoad();
        }
        else if (change.Property == IsBlueprintProperty && Url is { } u && ImageCache.Shared?.Peek(u, DecodeWidth) is { } bmp)
        {
            Show(u, bmp);
        }
    }

    private async void TryLoad()
    {
        var url = Url;
        if (string.IsNullOrEmpty(url) || url == _loadedUrl || ImageCache.Shared is null) return;
        _loadedUrl = url;
        Bitmap? bmp;
        try
        {
            bmp = await ImageCache.Shared.GetAsync(url, DecodeWidth);
        }
        catch (Exception)
        {
            return;
        }
        if (Url != url) return; // kontrolka dostała już inny adres
        Dispatcher.UIThread.Post(() => { if (Url == url) Show(url, bmp); });
    }
}
