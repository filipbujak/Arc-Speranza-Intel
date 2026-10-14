using ArcSperanzaIntel.App.Controls;
using ArcSperanzaIntel.App.ViewModels;
using Avalonia.Controls;

namespace ArcSperanzaIntel.App.Views;

public partial class ItemsView : UserControl
{
    private ItemsViewModel? _vm;

    public ItemsView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.ScrollToRequested -= OnScrollTo;
        _vm = DataContext as ItemsViewModel;
        if (_vm is not null) _vm.ScrollToRequested += OnScrollTo;
    }

    private void OnScrollTo(ItemCardViewModel card) => SmoothScroll.ToDataContext(Scroller, card, "itemcard");
}
