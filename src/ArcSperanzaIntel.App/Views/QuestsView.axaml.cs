using ArcSperanzaIntel.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Views;

public partial class QuestsView : UserControl
{
    private QuestsViewModel? _vm;
    private bool _initialFitDone;

    public QuestsView()
    {
        InitializeComponent();
        Graph.NodeClicked += (_, e) => _vm?.Select(e.QuestId);
        Graph.NodeToggleClicked += (_, e) => _vm?.ToggleQuest(e.QuestId);
        Graph.BackgroundClicked += (_, _) => _vm?.ClearSelection();

        // Enter w wyszukiwarce = pierwsza podpowiedź
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && _vm is not null)
            {
                _vm.PickFirstCommand.Execute(null);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        // Pierwsze wejście w zakładkę: pokaż cały graf (albo quest, do którego ktoś chciał przelecieć).
        Graph.SizeChanged += (_, e) =>
        {
            if (_initialFitDone || e.NewSize.Width < 50) return;
            _initialFitDone = true;
            var pending = _pendingFly;
            _pendingFly = null;
            Dispatcher.UIThread.Post(() =>
            {
                if (pending is not null) Graph.FlyTo(pending);
                else Graph.FitAll();
            }, DispatcherPriority.Background);
        };
    }

    private string? _pendingFly;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
        {
            _vm.FlyToRequested -= OnFlyTo;
            _vm.FitRequested -= OnFit;
        }
        _vm = DataContext as QuestsViewModel;
        if (_vm is null) return;
        _vm.FlyToRequested += OnFlyTo;
        _vm.FitRequested += OnFit;
    }

    private void OnFlyTo(string questId)
    {
        if (!_initialFitDone)
        {
            _pendingFly = questId; // graf jeszcze nie ma rozmiaru — przelot po pierwszym ułożeniu
            return;
        }
        // po przełączeniu zakładki graf musi mieć już rozmiar
        Dispatcher.UIThread.Post(() => Graph.FlyTo(questId), DispatcherPriority.Background);
    }

    private void OnFit() => Graph.FitAll();
}
