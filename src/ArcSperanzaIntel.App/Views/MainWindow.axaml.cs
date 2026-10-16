using Avalonia;
using ArcSperanzaIntel.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using ArcSperanzaIntel.App.Controls;

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
        SizeChanged += (_, _) => UpdateHeaderMode();
        Opened += (_, _) => UpdateHeaderMode();

        // własny pasek okna: przeciąganie za puste miejsce paska, dwuklik = maksymalizuj / przywróć
        TitleBar.PointerPressed += OnTitleBarPressed;
        TitleBar.DoubleTapped += (_, e) =>
        {
            if (IsOnControl(e.Source)) return;
            ToggleMaximize();
        };
    }

    /// <summary>Klik w przycisk, zakładkę itp. nie przeciąga okna.</summary>
    private static bool IsOnControl(object? source)
    {
        for (var v = source as Avalonia.Visual; v is not null; v = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v))
        {
            if (v is Button or TextBox or ToggleButton) return true;
            if (v is Border { Name: "TitleBar" }) return false;
        }
        return false;
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.ClickCount > 1 || IsOnControl(e.Source)) return;
        BeginMoveDrag(e);
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty || change.Property == OffScreenMarginProperty)
        {
            // zmaksymalizowane okno bez ramki wystaje kilka pikseli poza ekran — chowamy to marginesem
            var max = WindowState == WindowState.Maximized;
            RootGrid.Margin = max ? OffScreenMargin : default;
            MaxIcon.Data = Avalonia.Media.Geometry.Parse(max
                ? "M2.6,0.6 L9.4,0.6 L9.4,7.4 M0.6,2.6 L7.4,2.6 L7.4,9.4 L0.6,9.4 Z"
                : "M0.6,0.6 L9.4,0.6 L9.4,9.4 L0.6,9.4 Z");
        }
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private double _fullTabsWidth;
    private TabSlide? _tabSlideField;
    private TabSlide Slide => _tabSlideField ??= new TabSlide(TabHost);

    /// <summary>
    /// Pasek zakładek nie mieści się w wąskim oknie — wtedy zostają same ikony (klasa "compact"),
    /// a przy jeszcze węższym znika też napis przy logo ("narrow").
    /// </summary>
    private void UpdateHeaderMode()
    {
        if (Bounds.Width <= 0) return;
        if (!Classes.Contains("compact"))
        {
            TabsPanel.Measure(Size.Infinity);
            _fullTabsWidth = Math.Max(_fullTabsWidth, TabsPanel.DesiredSize.Width);
        }
        LogoPanel.Measure(Size.Infinity);
        RightPanel.Measure(Size.Infinity);
        WindowButtons.Measure(Size.Infinity);
        var available = Bounds.Width - LogoPanel.DesiredSize.Width - RightPanel.DesiredSize.Width - WindowButtons.DesiredSize.Width - 48;
        var compact = _fullTabsWidth > available;
        Classes.Set("compact", compact);
        Classes.Set("narrow", compact && Bounds.Width < 1180);
    }

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
            var lastTab = vm.Tab;
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.Tab))
                {
                    // kolejność dzieci TabHost = kolejność AppTab
                    Slide.Play((int)lastTab, (int)vm.Tab);
                    lastTab = vm.Tab;
                }
                if (args.PropertyName == nameof(MainViewModel.Dialog) && vm.Dialog?.HasInput == true)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => DialogInput.Focus());
            };
        }
    }
}
