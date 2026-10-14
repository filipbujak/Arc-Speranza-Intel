using ArcSperanzaIntel.App.Controls;
using ArcSperanzaIntel.App.ViewModels;
using ArcSperanzaIntel.Core.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Views;

/// <summary>Łączy MapCanvas z ViewModelem: zdarzenia myszy, przeloty, pozycjonowanie dymków przy markerach.</summary>
public partial class MapsView : UserControl
{
    private MapsViewModel? _vm;
    private LatLng? _cardAnchor;
    private LatLng? _pinAnchor;

    public MapsView()
    {
        InitializeComponent();

        Canvas.MarkerClicked += (_, e) =>
        {
            _vm?.OpenMarker(e.Marker);
            _cardAnchor = e.Marker.Marker.Position;
            Dispatcher.UIThread.Post(PositionCards, DispatcherPriority.Render);
        };
        Canvas.PinClicked += (_, e) =>
        {
            _vm?.OpenPin(e.Pin);
            _pinAnchor = new LatLng(e.Pin.Lat, e.Pin.Lng);
            Dispatcher.UIThread.Post(PositionCards, DispatcherPriority.Render);
        };
        Canvas.PinMoved += (_, _) => _vm?.PinMoved();
        Canvas.BackgroundClicked += (_, _) => _vm?.CloseCards();
        Canvas.MapRightClicked += (_, e) => _ = _vm?.AddPinAsync(e.Position);
        Canvas.ViewChanging += (_, _) => PositionCards();
        Canvas.ViewChanged += (_, _) => _vm?.SaveView(Canvas.Center, Canvas.Zoom);

        ZoomIn.Click += (_, _) => Canvas.ZoomBy(0.75);
        ZoomOut.Click += (_, _) => Canvas.ZoomBy(-0.75);

        MarkerCard.SizeChanged += (_, _) => PositionCards();
        PinCard.SizeChanged += (_, _) => PositionCards();
        Overlay.SizeChanged += (_, _) => PositionCards();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.SetViewRequested -= OnSetView;
            _vm.FlyToRequested -= OnFlyTo;
            _vm.FlyToBoundsRequested -= OnFlyToBounds;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }
        _vm = DataContext as MapsViewModel;
        if (_vm is null) return;
        _vm.SetViewRequested += OnSetView;
        _vm.FlyToRequested += OnFlyTo;
        _vm.FlyToBoundsRequested += OnFlyToBounds;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapsViewModel.SelectedCard))
        {
            if (_vm?.SelectedCard is { } card) _cardAnchor = card.Marker.Position;
            Dispatcher.UIThread.Post(PositionCards, DispatcherPriority.Render);
        }
        else if (e.PropertyName == nameof(MapsViewModel.SelectedPin))
        {
            if (_vm?.SelectedPin is { } pin) _pinAnchor = new LatLng(pin.Pin.Lat, pin.Pin.Lng);
            Dispatcher.UIThread.Post(PositionCards, DispatcherPriority.Render);
        }
    }

    private void OnSetView(LatLng center, double zoom) =>
        // poczekaj, aż kontrolka dostanie nową mapę (wiązanie Map ustawia środek domyślny)
        Dispatcher.UIThread.Post(() => Canvas.SetView(center, zoom), DispatcherPriority.Background);

    private void OnFlyTo(LatLng center, double zoom) =>
        Dispatcher.UIThread.Post(() => Canvas.FlyTo(center, zoom), DispatcherPriority.Background);

    private void OnFlyToBounds(LatLngBounds bounds) =>
        Dispatcher.UIThread.Post(() => Canvas.FlyToBounds(bounds), DispatcherPriority.Background);

    /// <summary>Dymek stoi obok markera i przesuwa się razem z mapą; nie wychodzi poza ekran.</summary>
    private void PositionCards()
    {
        Place(MarkerCard, _vm?.SelectedCard is not null ? _cardAnchor : null);
        Place(PinCard, _vm?.SelectedPin is not null ? _pinAnchor : null);
    }

    private void Place(Control card, LatLng? anchor)
    {
        if (anchor is null || !card.IsVisible) return;
        var p = Canvas.ToScreen(anchor.Value);
        var w = card.Bounds.Width > 0 ? card.Bounds.Width : 320;
        var h = card.Bounds.Height > 0 ? card.Bounds.Height : 200;
        var area = Overlay.Bounds.Size;
        if (area.Height > 0) card.MaxHeight = Math.Max(160, area.Height - 16);

        var x = p.X + 22;
        if (x + w > area.Width - 8) x = p.X - w - 22; // po lewej, gdy brak miejsca
        var y = p.Y - 30;
        y = Math.Clamp(y, 8, Math.Max(8, area.Height - h - 8));
        x = Math.Clamp(x, 8, Math.Max(8, area.Width - w - 8));

        Avalonia.Controls.Canvas.SetLeft(card, x);
        Avalonia.Controls.Canvas.SetTop(card, y);
    }
}
