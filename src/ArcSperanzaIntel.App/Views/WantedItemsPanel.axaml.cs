using ArcSperanzaIntel.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace ArcSperanzaIntel.App.Views;

public partial class WantedItemsPanel : UserControl
{
    public WantedItemsPanel()
    {
        InitializeComponent();
        // prawy przycisk na nagłówku też rozwija / chowa
        Header.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Right && DataContext is WantedItemsViewModel vm)
                vm.ToggleCommand.Execute(null);
        };
    }
}
