using ArcSperanzaIntel.Core.Localization;
using System.Collections.ObjectModel;
using ArcSperanzaIntel.App.Services;
using ArcSperanzaIntel.Core.Maps;
using ArcSperanzaIntel.Core.Quests;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

public enum AppTab { Maps, Quests, Items, Shops, Workshop, Events, Settings }

/// <summary>Okno główne: zakładki, ładowanie danych, nawigacja między zakładkami, toasty i okna dialogowe.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    public AppServices Services { get; }

    public MainViewModel(AppServices services)
    {
        Services = services;
        services.Data.Warning += (_, msg) => Dispatcher.UIThread.Post(() => Toast(msg, ToastKind.Error));
    }

    // ---------------------------------------------------------------- zakładki
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMapsTab), nameof(IsQuestsTab), nameof(IsItemsTab), nameof(IsShopsTab), nameof(IsWorkshopTab), nameof(IsEventsTab), nameof(IsSettingsTab))]
    private AppTab _tab = AppTab.Maps;

    public bool IsMapsTab => Tab == AppTab.Maps;
    public bool IsQuestsTab => Tab == AppTab.Quests;
    public bool IsItemsTab => Tab == AppTab.Items;
    public bool IsShopsTab => Tab == AppTab.Shops;
    public bool IsEventsTab => Tab == AppTab.Events;
    public bool IsWorkshopTab => Tab == AppTab.Workshop;
    public bool IsSettingsTab => Tab == AppTab.Settings;

    partial void OnTabChanged(AppTab value)
    {
        if (value == AppTab.Events) _ = Events?.TickAsync();
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        if (Enum.TryParse<AppTab>(tab, out var t)) Tab = t;
    }

    // ---------------------------------------------------------------- strony
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _loadingText = Loc.T("Starting");
    [ObservableProperty] private string _statusText = "";

    [ObservableProperty] private MapsViewModel? _maps;
    [ObservableProperty] private QuestsViewModel? _quests;
    [ObservableProperty] private ItemsViewModel? _items;
    [ObservableProperty] private ShopsViewModel? _shops;
    [ObservableProperty] private EventsViewModel? _events;
    [ObservableProperty] private WorkshopViewModel? _workshop;
    [ObservableProperty] private SettingsViewModel? _settings;
    [ObservableProperty] private WantedItemsViewModel? _wanted;

    public async Task InitializeAsync()
    {
        try
        {
            LoadingText = Loc.T("LoadingCore");
            await Services.Data.LoadCoreAsync(force: false, CancellationToken.None);
            if (Services.Data.Quests.Count == 0)
            {
                LoadingText = Loc.T("LoadFailed");
                return;
            }

            Services.BuildDomain();
            Wanted = new WantedItemsViewModel(this);
            Maps = new MapsViewModel(this);
            Quests = new QuestsViewModel(this);
            Items = new ItemsViewModel(this);
            Shops = new ShopsViewModel(this);
            Events = new EventsViewModel(this);
            Workshop = new WorkshopViewModel(this);
            Settings = new SettingsViewModel(this);

            IsLoading = false;
            StatusText = Services.Data.SourceInfo;
            await Maps.LoadCurrentMapAsync();
            await RegisterHotkeyAsync();
            _ = PreloadAllMarkersAsync();
            _ = CheckUpdatesQuietlyAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "Init");
            LoadingText = Loc.T("StartError") + ex.Message;
        }
    }

    /// <summary>Zmiana języka: zapis w stanie i restart (wszystkie teksty budują się od nowa).</summary>
    public async Task ApplyLanguageAsync(AppLanguage language)
    {
        var code = language == AppLanguage.Polish ? "pl" : "en";
        if (Services.State.Language == code && Loc.Instance.Language == language) return;
        Services.State.Language = code;
        Services.FlushSave();
        // pytanie w języku docelowym, bo to on będzie po restarcie
        var pl = language == AppLanguage.Polish;
        var ok = await ConfirmAsync(
            pl ? "Język: polski" : "Language: English",
            pl ? "Aplikacja uruchomi się ponownie w nowym języku." : "The app will restart in the new language.",
            pl ? "Uruchom ponownie" : "Restart");
        if (ok) Services.SaveAndRestart();
    }

    /// <summary>Po starcie: jeśli jest nowsza wersja na GitHubie, mówimy o tym raz (instalacja w Ustawieniach).</summary>
    private async Task CheckUpdatesQuietlyAsync()
    {
        try
        {
            var v = await Services.Updates.CheckAsync();
            if (v is null) return;
            Dispatcher.UIThread.Post(() => Settings?.ShowPendingUpdate(v));
            // automatycznie: pobierz w tle, zainstaluj po zamknięciu aplikacji
            await Services.Updates.DownloadAndApplyOnExitAsync();
            Dispatcher.UIThread.Post(() => Toast(Loc.F("UpdateReady", v), ToastKind.Ok));
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "UpdateCheck");
        }
    }

    /// <summary>Markery wszystkich map w tle — potrzebne, żeby wiedzieć, na których mapach są questy.</summary>
    private async Task PreloadAllMarkersAsync()
    {
        foreach (var map in MapCatalog.All)
        {
            try
            {
                await Services.Data.LoadMarkersAsync(map.Id, force: false, CancellationToken.None);
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex, "Preload " + map.Id);
            }
        }
        Dispatcher.UIThread.Post(() =>
        {
            Quests?.RefreshAll();
            Maps?.RefreshQuestCounts();
        });
    }

    public async Task RegisterHotkeyAsync()
    {
        var error = await Services.Hotkey.RegisterAsync(Services.Keybinds.Get(KeybindAction.ToggleWindow));
        if (error is not null) Toast(error, ToastKind.Error);
    }

    /// <summary>Po zmianie postępu questów odświeżamy wszystkie zależne widoki.</summary>
    public void OnQuestProgressChanged()
    {
        Quests?.RefreshAll();
        Maps?.OnQuestProgressChanged();
        Wanted?.Refresh();
        Items?.RefreshSelected();
    }

    /// <summary>Komunikat po zmianie questu (z liczbą automatycznie oznaczonych).</summary>
    public void ReportQuestChange(QuestChange change)
    {
        var q = Services.Quests.Get(change.QuestId);
        if (q is null) return;
        if (change.NowDone)
            Toast("✔ " + q.Name + (change.AlsoChanged.Count > 0 ? Loc.F("PlusEarlier", change.AlsoChanged.Count) : ""), ToastKind.Ok);
        else
            Toast(Loc.T("Unchecked") + q.Name + (change.AlsoChanged.Count > 0 ? Loc.F("PlusDependent", change.AlsoChanged.Count) : ""));
    }

    // ---------------------------------------------------------------- nawigacja
    public void ShowQuestInTree(string questId)
    {
        Tab = AppTab.Quests;
        Quests?.Focus(questId);
    }

    public void ShowQuestOnMap(string questId, string mapId)
    {
        Tab = AppTab.Maps;
        _ = Maps?.ShowQuestAsync(questId, mapId);
    }

    public void OpenMap(string mapId)
    {
        Tab = AppTab.Maps;
        Maps?.SelectMap(MapCatalog.Get(mapId));
    }

    public void ShowStation(string stationId)
    {
        Tab = AppTab.Workshop;
        Workshop?.Select(stationId);
    }

    public void ShowItem(string itemId)
    {
        Tab = AppTab.Items;
        Items?.Focus(itemId);
    }

    // ---------------------------------------------------------------- skróty w aplikacji
    public bool HandleKey(KeyEventArgs e)
    {
        if (Settings?.IsCapturing == true) return false;
        var action = Services.Keybinds.Match(e);
        if (action is null) return false;
        Tab = action switch
        {
            KeybindAction.TabMaps => AppTab.Maps,
            KeybindAction.TabQuests => AppTab.Quests,
            KeybindAction.TabItems => AppTab.Items,
            KeybindAction.TabShops => AppTab.Shops,
            KeybindAction.TabEvents => AppTab.Events,
            KeybindAction.TabWorkshop => AppTab.Workshop,
            KeybindAction.TabSettings => AppTab.Settings,
            _ => Tab,
        };
        return true;
    }

    // ---------------------------------------------------------------- toasty i dialogi
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    public void Toast(string text, ToastKind kind = ToastKind.Info)
    {
        var t = new ToastViewModel(text, kind);
        Toasts.Add(t);
        while (Toasts.Count > 4) Toasts.RemoveAt(0);
        DispatcherTimer.RunOnce(() => Toasts.Remove(t), TimeSpan.FromSeconds(3.5));
    }

    [ObservableProperty] private DialogViewModel? _dialog;

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", bool danger = false)
    {
        var d = new DialogViewModel { Title = title, Message = message, ConfirmText = confirmText, IsDanger = danger };
        Dialog = d;
        var r = await d.Result;
        Dialog = null;
        return r is not null;
    }

    public async Task<string?> PromptAsync(string title, string message, string initial = "", string? confirmText = null)
    {
        var d = new DialogViewModel { Title = title, Message = message, ConfirmText = confirmText ?? Loc.T("Save"), HasInput = true, InputText = initial };
        Dialog = d;
        var r = await d.Result;
        Dialog = null;
        return r;
    }

    [RelayCommand]
    private void OpenMetaForge() => OpenUrl("https://metaforge.app/arc-raiders");

    public static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "OpenUrl");
        }
    }
}
