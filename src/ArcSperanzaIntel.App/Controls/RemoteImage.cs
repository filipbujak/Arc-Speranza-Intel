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

    /// <summary>Schemat: rysujemy jak w grze — biały rysunek na błękitnym tle z siatką.</summary>
    public static readonly StyledProperty<bool> IsBlueprintProperty =
        AvaloniaProperty.Register<RemoteImage, bool>(nameof(IsBlueprint));

    private static readonly IBrush BlueprintBack = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse("#2F86C9"), 0), new GradientStop(Color.Parse("#1C5E99"), 1) },
    };

    private static readonly IPen BlueprintGrid = new Pen(new SolidColorBrush(Color.Parse("#30FFFFFF")), 1);
    private static readonly IBrush BlueprintInk = Brushes.White;

    static RemoteImage()
    {
        AffectsRender<RemoteImage>(IsBlueprintProperty);
    }

    public bool IsBlueprint
    {
        get => GetValue(IsBlueprintProperty);
        set => SetValue(IsBlueprintProperty, value);
    }

    public override void Render(DrawingContext ctx)
    {
        if (!IsBlueprint || Source is not Bitmap bmp)
        {
            base.Render(ctx);
            return;
        }

        var area = new Rect(Bounds.Size);
        if (area.Width <= 0 || area.Height <= 0) return;
        ctx.DrawRectangle(BlueprintBack, null, area, 2, 2);

        var step = Math.Max(6, Math.Min(area.Width, area.Height) / 6);
        for (var x = step; x < area.Width; x += step)
            ctx.DrawLine(BlueprintGrid, new Point(Math.Round(x) + 0.5, 0), new Point(Math.Round(x) + 0.5, area.Height));
        for (var y = step; y < area.Height; y += step)
            ctx.DrawLine(BlueprintGrid, new Point(0, Math.Round(y) + 0.5), new Point(area.Width, Math.Round(y) + 0.5));

        // biała sylwetka ikony: maska z kanału alfa obrazka
        var size = bmp.Size;
        var scale = Math.Min(area.Width / size.Width, area.Height / size.Height) * 0.86;
        var w = size.Width * scale;
        var h = size.Height * scale;
        var dest = new Rect((area.Width - w) / 2, (area.Height - h) / 2, w, h);
        using (ctx.PushOpacityMask(new ImageBrush(bmp) { Stretch = Stretch.Fill }, dest))
            ctx.FillRectangle(BlueprintInk, dest);
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
            Source = Url is null ? null : ImageCache.Shared?.Peek(Url);
            if (Source is not null) _loadedUrl = Url;
            else if (_inViewport) TryLoad();
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
        Dispatcher.UIThread.Post(() => { if (Url == url) Source = bmp; });
    }
}
