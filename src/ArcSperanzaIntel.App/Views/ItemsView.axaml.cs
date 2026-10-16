using ArcSperanzaIntel.App.Controls;
using ArcSperanzaIntel.App.ViewModels;
using Avalonia.Controls;

namespace ArcSperanzaIntel.App.Views;

public partial class ItemsView : UserControl
{
    private ItemsViewModel? _vm;

    private const double CardWidth = 132; // karta 124 + margines 8 (ItemsView.axaml)
    private const double SideMargin = 40; // margines siatki 20 + 20

    public ItemsView()
    {
        InitializeComponent();
        Scroller.SizeChanged += (_, _) => UpdateColumns();
    }

    private void UpdateColumns()
    {
        if (_vm is null || Scroller.Bounds.Width <= 0) return;
        _vm.Columns = (int)Math.Max(1, Math.Floor((Scroller.Bounds.Width - SideMargin - 12) / CardWidth));
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.ScrollToRequested -= OnScrollTo;
        _vm = DataContext as ItemsViewModel;
        if (_vm is not null) _vm.ScrollToRequested += OnScrollTo;
        UpdateColumns();
    }

    /// <summary>Wiersz z kartą może jeszcze nie istnieć (wirtualizacja) — najpierw go tworzymy, potem płynnie dosuwamy.</summary>
    private void OnScrollTo(ItemCardViewModel card)
    {
        var index = _vm?.RowIndexOf(card) ?? -1;
        if (index >= 0) RowsHost.ScrollIntoView(index);
        SmoothScroll.ToDataContext(Scroller, card, "itemcard");
    }
}
