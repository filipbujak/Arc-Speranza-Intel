using ArcSperanzaIntel.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ArcSperanzaIntel.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // tunelowanie: skróty działają niezależnie od tego, która kontrolka ma fokus
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        // boczne przyciski myszy (mouse4 = wstecz, mouse5 = dalej), jak w przeglądarce
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private async void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;

        if (vm.Settings?.IsCapturing == true)
        {
            e.Handled = true;
            await vm.Settings.CaptureAsync(e);
            return;
        }

        if (vm.Dialog is { } dialog)
        {
            if (e.Key == Key.Escape)
            {
                dialog.CancelCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                dialog.ConfirmCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        // Alt+← / Alt+→ — wstecz / dalej
        if (e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.Left or Key.Right)
        {
            (e.Key == Key.Left ? vm.GoBackCommand : vm.GoForwardCommand).Execute(null);
            e.Handled = true;
            return;
        }

        if (vm.HandleKey(e)) e.Handled = true;
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is not { Dialog: null } vm) return;
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsXButton1Pressed)
        {
            vm.GoBackCommand.Execute(null);
            e.Handled = true;
        }
        else if (props.IsXButton2Pressed)
        {
            vm.GoForwardCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is { } vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.Dialog) && vm.Dialog?.HasInput == true)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DialogInput.Focus());
            };
        }
    }
}
