using ArcSperanzaIntel.Core.Localization;
using ArcSperanzaIntel.App.Services;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>Ustawienia: skróty klawiszowe, okno na wierzchu, odświeżenie danych, reset aplikacji, informacje.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        Keybinds = Enum.GetValues<KeybindAction>().Select(a => new KeybindRowViewModel(a, this)).ToList();
        _alwaysOnTop = main.Services.State.AlwaysOnTop;
        DataFolder = main.Services.Paths.DataDir;
        CacheFolder = main.Services.Paths.CacheDir;
        OverlayOpacity = Math.Round(Math.Clamp(main.Services.State.Overlay.Opacity, 0.2, 1) * 100);
    }

    private Services.Keybinds Binds => _main.Services.Keybinds;

    public IReadOnlyList<KeybindRowViewModel> Keybinds { get; }
    public string DataFolder { get; }
    public string CacheFolder { get; }
    public string Version => "ARC Speranza Intel " + AppInfo.Version;

    // ---------------------------------------------------------------- aktualizacje
    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private bool _isCheckingUpdates;

    /// <summary>Wołane po starcie, gdy w tle znaleziono nową wersję.</summary>
    internal void ShowPendingUpdate(string version)
    {
        HasUpdate = true;
        UpdateStatus = Loc.F("UpdateAvailable", version);
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        var updates = _main.Services.Updates;
        if (!updates.IsInstalled)
        {
            UpdateStatus = Loc.T("UpdateNotInstalled");
            return;
        }
        IsCheckingUpdates = true;
        try
        {
            var v = await updates.CheckAsync();
            if (v is null)
            {
                HasUpdate = false;
                UpdateStatus = Loc.T("UpToDate");
            }
            else ShowPendingUpdate(v);
        }
        catch (Exception ex)
        {
            UpdateStatus = Loc.F("UpdateFailed", ex.Message);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        IsCheckingUpdates = true;
        UpdateStatus = Loc.T("Downloading");
        try
        {
            _main.Services.FlushSave();
            await _main.Services.Updates.DownloadAndRestartAsync();
        }
        catch (Exception ex)
        {
            UpdateStatus = Loc.F("UpdateFailed", ex.Message);
            IsCheckingUpdates = false;
        }
    }

    // ---------------------------------------------------------------- język
    /// <summary>Lista języków (nazwy we własnym języku); wybór = zapis i restart w nowym języku.</summary>
    public IReadOnlyList<LanguageInfo> LanguageOptions => Languages.All;

    public LanguageInfo SelectedLanguage
    {
        get => Languages.Of(Loc.Instance.Language);
        set
        {
            if (value is null || value.Language == Loc.Instance.Language) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = _main.ApplyLanguageAsync(value.Language));
        }
    }

    // ---------------------------------------------------------------- region wydarzeń
    private IReadOnlyList<EventRegionOptionViewModel>? _eventRegions;

    /// <summary>„Automatycznie” + potwierdzone regiony MetaForge.</summary>
    public IReadOnlyList<EventRegionOptionViewModel> EventRegions => _eventRegions ??= BuildEventRegions();

    private IReadOnlyList<EventRegionOptionViewModel> BuildEventRegions()
    {
        var stored = _main.Services.State.EventRegion;
        var detected = Core.Api.EventRegions.Detect(TimeZoneInfo.Local);
        var auto = new EventRegionOptionViewModel(null, Loc.F("EventRegionAuto", EventRegionOptionViewModel.NameOf(detected)),
            !Core.Api.EventRegions.IsKnown(stored), SelectEventRegion);
        return [auto, .. Core.Api.EventRegions.All.Select(r =>
            new EventRegionOptionViewModel(r, EventRegionOptionViewModel.NameOf(r), stored == r, SelectEventRegion))];
    }

    private void SelectEventRegion(string? region)
    {
        var services = _main.Services;
        if (services.State.EventRegion == region) return;
        services.State.EventRegion = region;
        services.RequestSave();
        services.Repository.EventRegion = Core.Api.EventRegions.Resolve(region);
        foreach (var option in EventRegions) option.IsSelected = option.Value == region;
        if (_main.Events is { } events) _ = events.RegionChangedAsync();
    }

    [ObservableProperty] private bool _alwaysOnTop;

    // ---------------------------------------------------------------- overlay
    /// <summary>Przezroczystość overlaya w procentach (20–100).</summary>
    [ObservableProperty] private double _overlayOpacity;

    partial void OnOverlayOpacityChanged(double value)
    {
        _main.Services.Overlay?.SetOpacity(value / 100);
        OverlayOpacityText = $"{Math.Round(value)}%";
    }

    [ObservableProperty] private string _overlayOpacityText = "";

    public string OverlayHotkeyText => Loc.F("OverlayHelp", GestureText(KeybindAction.ToggleOverlay), GestureText(KeybindAction.LockOverlay));

    [RelayCommand]
    private void ToggleOverlay() => _main.Services.Overlay?.Toggle();

    [RelayCommand]
    private void ResetOverlayPosition() => _main.Services.Overlay?.ResetPosition();
    [ObservableProperty] private bool _isRefreshing;

    partial void OnAlwaysOnTopChanged(bool value) => _main.Services.SetAlwaysOnTop(value);

    // ---------------------------------------------------------------- skróty
    public KeybindRowViewModel? Capturing { get; private set; }
    public bool IsCapturing => Capturing is not null;

    internal void StartCapture(KeybindRowViewModel row)
    {
        if (Capturing is not null) Capturing.IsCapturing = false;
        Capturing = row;
        row.IsCapturing = true;
        _main.Services.Hotkey.Unregister(); // żeby stary skrót globalny nie przechwycił klawiszy
        _main.Services.OverlayHotkey.Unregister();
        _main.Services.OverlayLockHotkey.Unregister();
    }

    /// <summary>Klawisz naciśnięty w trakcie przypisywania. Esc = anuluj, Backspace = usuń skrót.</summary>
    public async Task<bool> CaptureAsync(KeyEventArgs e)
    {
        var row = Capturing;
        if (row is null) return false;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
            return true; // czekamy na właściwy klawisz

        Capturing = null;
        row.IsCapturing = false;
        if (e.Key == Key.Escape)
        {
            await _main.RegisterHotkeyAsync();
            return true;
        }

        var gesture = e.Key == Key.Back ? null : new KeyGesture(e.Key, e.KeyModifiers);
        if (gesture is not null && Services.Keybinds.IsGlobal(row.Action) && e.KeyModifiers == KeyModifiers.None &&
            e.Key is not (>= Key.F1 and <= Key.F24))
        {
            _main.Toast(Loc.T("GlobalNeedsModifier"), ToastKind.Error);
            await _main.RegisterHotkeyAsync();
            return true;
        }

        Binds.Set(row.Action, gesture);
        _main.Services.RequestSave();
        row.Refresh();
        OnPropertyChanged(nameof(OverlayHotkeyText));
        await _main.RegisterHotkeyAsync();
        _main.Toast($"{Services.Keybinds.Describe(row.Action)}: {Services.Keybinds.Pretty(gesture)}", ToastKind.Ok);
        return true;
    }

    internal string GestureText(KeybindAction a) => Services.Keybinds.Pretty(Binds.Get(a));

    [RelayCommand]
    private async Task ResetKeybinds()
    {
        Binds.ResetAll();
        foreach (var k in Keybinds) k.Refresh();
        _main.Services.RequestSave();
        await _main.RegisterHotkeyAsync();
        _main.Toast(Loc.T("KeybindsRestored"), ToastKind.Ok);
    }

    // ---------------------------------------------------------------- dane i reset
    [RelayCommand]
    private async Task RefreshData()
    {
        IsRefreshing = true;
        try
        {
            await _main.Services.Data.LoadCoreAsync(force: true, CancellationToken.None);
            _main.Maps?.ForceReloadCurrentMap();
            _main.StatusText = _main.Services.Data.SourceInfo;
            _main.Toast(Loc.T("DataRefreshed"), ToastKind.Ok);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ResetApp()
    {
        var ok = await _main.ConfirmAsync(
            Loc.T("ResetApp"),
            Loc.T("ResetAppConfirm"),
            Loc.T("ResetEverything"), danger: true);
        if (ok) _main.Services.ResetEverythingAndRestart();
    }

    [RelayCommand]
    private async Task ResetQuests()
    {
        var ok = await _main.ConfirmAsync(Loc.T("ResetQuestsTitle"), Loc.T("ResetQuestsConfirm"), Loc.T("ResetQuests"), danger: true);
        if (!ok) return;
        _main.Services.Quests.ResetAll();
        _main.OnQuestProgressChanged();
        _main.Toast(Loc.T("QuestsCleared"));
    }

    [RelayCommand]
    private void OpenDataFolder() => MainViewModel.OpenUrl(DataFolder);

    [RelayCommand]
    private void OpenMetaForge() => MainViewModel.OpenUrl("https://metaforge.app/arc-raiders");

    [RelayCommand]
    private void OpenRepo() => MainViewModel.OpenUrl(AppInfo.RepoUrl);

    // ---------------------------------------------------------------- zgłoszenia
    [RelayCommand]
    private void ReportBug() => MainViewModel.OpenUrl(_main.Services.Reports.BugUrl());

    [RelayCommand]
    private void ReportIdea() => MainViewModel.OpenUrl(_main.Services.Reports.IdeaUrl());

    [RelayCommand]
    private void ReportTranslation() => MainViewModel.OpenUrl(_main.Services.Reports.TranslationUrl());

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(ReportService.LogsFolder);
        MainViewModel.OpenUrl(ReportService.LogsFolder);
    }

    /// <summary>Dla osób bez konta na GitHubie: te same informacje do schowka (np. na Discorda).</summary>
    [RelayCommand]
    private async Task CopyDiagnostics()
    {
        var window = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window?.Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(_main.Services.Reports.Diagnostics());
        _main.Toast(Loc.T("DiagnosticsCopied"), ToastKind.Ok);
    }

    [RelayCommand]
    private void OpenGameIcons() => MainViewModel.OpenUrl("https://game-icons.net");
}

public sealed partial class KeybindRowViewModel : ObservableObject
{
    private readonly SettingsViewModel _owner;

    public KeybindRowViewModel(KeybindAction action, SettingsViewModel owner)
    {
        Action = action;
        _owner = owner;
        Description = Keybinds.Describe(action);
        IsGlobal = Services.Keybinds.IsGlobal(action);
        _gestureText = owner.GestureText(action);
    }

    public KeybindAction Action { get; }
    public string Description { get; }
    public bool IsGlobal { get; }

    [ObservableProperty] private string _gestureText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    private bool _isCapturing;

    public string ButtonText => IsCapturing ? Loc.T("PressShortcut") : GestureText;

    partial void OnGestureTextChanged(string value) => OnPropertyChanged(nameof(ButtonText));

    public void Refresh() => GestureText = _owner.GestureText(Action);

    [RelayCommand]
    private void StartCapture() => _owner.StartCapture(this);
}

/// <summary>Jedna opcja regionu wydarzeń (RadioButton w ustawieniach).</summary>
public sealed partial class EventRegionOptionViewModel(string? value, string label, bool selected, Action<string?> select) : ObservableObject
{
    public string? Value { get; } = value;
    public string Label { get; } = label;

    [ObservableProperty] private bool _isSelected = selected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value) select(Value);
    }

    public static string NameOf(string region) => region switch
    {
        Core.Api.EventRegions.NorthAmerica => Loc.T("EventRegionNorthAmerica"),
        Core.Api.EventRegions.Oceania => Loc.T("EventRegionOceania"),
        _ => Loc.T("EventRegionEurope"),
    };
}
