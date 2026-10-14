using ArcSperanzaIntel.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Views;

public partial class EventsView : UserControl
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };

    public EventsView()
    {
        InitializeComponent();
        // liczniki "za 12 min" odświeżamy tylko, gdy zakładka jest widoczna
        _clock.Tick += (_, _) =>
        {
            if (IsEffectivelyVisible && DataContext is EventsViewModel vm) _ = vm.TickAsync();
        };
        AttachedToVisualTree += (_, _) => _clock.Start();
        DetachedFromVisualTree += (_, _) => _clock.Stop();
    }
}
