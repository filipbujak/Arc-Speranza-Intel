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
    public bool IsPolish
    {
        get => Loc.Instance.Language == AppLanguage.Polish;
        set
        {
            if (value) Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = _main.ApplyLanguageAsync(AppLanguage.Polish));
        }
    }

    public bool IsEnglish
    {
        get => Loc.Instance.Language == AppLanguage.English;
        set
        {
            if (value) Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = _main.ApplyLanguageAsync(AppLanguage.English));
        }
    }

    [ObservableProperty] private bool _alwaysOnTop;
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
        if (gesture is not null && row.Action == KeybindAction.ToggleWindow && e.KeyModifiers == KeyModifiers.None &&
            e.Key is not (>= Key.F1 and <= Key.F24))
        {
            _main.Toast(Loc.T("GlobalNeedsModifier"), ToastKind.Error);
            await _main.RegisterHotkeyAsync();
            return true;
        }

        Binds.Set(row.Action, gesture);
        _main.Services.RequestSave();
        row.Refresh();
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
        IsGlobal = action == KeybindAction.ToggleWindow;
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
