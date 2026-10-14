using ArcSperanzaIntel.App.Controls;
using ArcSperanzaIntel.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Views;

public partial class ShopsView : UserControl
{
    private ShopsViewModel? _vm;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };

    public ShopsView()
    {
        InitializeComponent();
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && _vm is not null)
            {
                _vm.GoToFirstCommand.Execute(null);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        _clock.Tick += (_, _) => _vm?.Ermal?.UpdateCountdown();
        AttachedToVisualTree += (_, _) => _clock.Start();
        DetachedFromVisualTree += (_, _) => _clock.Stop();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.ScrollToRequested -= OnScrollTo;
        _vm = DataContext as ShopsViewModel;
        if (_vm is not null) _vm.ScrollToRequested += OnScrollTo;
    }

    private void OnScrollTo(object offer) => SmoothScroll.ToDataContext(Scroller, offer, "offer", 160);
}
