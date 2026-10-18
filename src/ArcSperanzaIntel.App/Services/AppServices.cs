using ArcSperanzaIntel.Core.Workshop;
using ArcSperanzaIntel.Core.Localization;
using ArcSperanzaIntel.Core.Api;
using ArcSperanzaIntel.Core.Maps;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Quests;
using ArcSperanzaIntel.Core.Storage;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Services;

/// <summary>
/// Korzeń kompozycji: tworzy i trzyma wszystkie serwisy aplikacji.
/// QuestBook, indeks map i szukane itemy powstają dopiero po załadowaniu danych (<see cref="BuildDomain"/>).
/// </summary>
public sealed class AppServices : IDisposable
{
    private readonly DispatcherTimer _saveTimer;
    private Window? _window;

    public AppPaths Paths { get; }
    public IStateStore Store { get; }
    public UserState State { get; private set; }
    public MetaForgeHttp Http { get; }
    public JsonFileCache ApiCache { get; }
    public GameData Data { get; }
    public GameDataRepository Repository { get; }

    /// <summary>Zgłoszenia na GitHubie i informacje diagnostyczne.</summary>
    public ReportService Reports { get; }
    public ImageCache Images { get; }
    public Keybinds Keybinds { get; private set; }
    public GlobalHotkey Hotkey { get; } = new();

    /// <summary>Drugi skrót globalny: pokaż / schowaj overlay szukanych.</summary>
    public GlobalHotkey OverlayHotkey { get; } = new();

    /// <summary>Globalny skrót: zablokuj / odblokuj overlay (zablokowany = mysz go ignoruje, kursor się nie pokazuje).</summary>
    public GlobalHotkey OverlayLockHotkey { get; } = new();

    /// <summary>Overlay z szukanymi przedmiotami (tworzony po załadowaniu danych).</summary>
    public OverlayService? Overlay { get; private set; }
    public UpdateService Updates { get; } = new();
    public QuestMapIndex MapIndex { get; } = new(MapCatalog.All);

    public QuestBook Quests { get; private set; } = null!;
    public WantedItemsService Wanted { get; private set; } = null!;
    public WorkshopBook Workshop { get; private set; } = null!;

    public AppServices()
    {
        Paths = new AppPaths();
        Paths.EnsureCreated();
        Store = new JsonStateStore(Paths.StateFile);
        State = Store.Load();
        Loc.Instance.Language = Languages.FromCode(State.Language)?.Language ?? Loc.SystemDefault();
        Http = new MetaForgeHttp();
        ApiCache = new JsonFileCache(Paths.ApiCacheDir);
        Repository = new GameDataRepository(Http, ApiCache) { EventRegion = EventRegions.Resolve(State.EventRegion) };
        Data = new GameData(Repository);
        Reports = new ReportService(() => Repository.EventRegion);
        Images = new ImageCache(Paths.ImageCacheDir);
        Keybinds = new Keybinds(State.Keybinds);
        Data.MarkersLoaded += (_, mapId) => MapIndex.AddMarkers(Data.MarkersOf(mapId));
        ItemKinds.Lookup = Data.FindItem;

        _saveTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(600), DispatcherPriority.Background, (_, _) => FlushSave());
        _saveTimer.Stop();
    }

    /// <summary>Tworzy obiekty domenowe po załadowaniu questów i itemów.</summary>
    public void BuildDomain()
    {
        Quests = new QuestBook(Data.Quests, State.Progress);
        Workshop = new WorkshopBook(Data.Stations, Data.Items, State);
        Wanted = new WantedItemsService(Quests, Workshop, State, Data.FindItem);
        Overlay?.Dispose();
        Overlay = new OverlayService(this);
        Quests.Changed += (_, _) => RequestSave();
        Wanted.Changed += (_, _) => RequestSave();
        Workshop.Changed += (_, _) =>
        {
            RequestSave();
            Wanted.Invalidate();
        };
    }

    /// <summary>Zapis z opóźnieniem — wiele szybkich zmian = jeden zapis na dysk.</summary>
    public void RequestSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void FlushSave()
    {
        _saveTimer.Stop();
        try
        {
            if (_window is not null) SaveWindowPlacement(_window);
            Store.Save(State);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "Save");
        }
    }

    // ---------------------------------------------------------------- okno
    public void AttachWindow(Window window)
    {
        _window = window;
        if (State.Window is { } w && w.Width > 400 && w.Height > 300)
        {
            window.Width = w.Width;
            window.Height = w.Height;
            window.Position = new PixelPoint(w.X, w.Y);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            if (w.Maximized) window.WindowState = WindowState.Maximized;
        }
        window.Topmost = State.AlwaysOnTop;
        // zapamiętujemy ostatni stan sprzed minimalizacji — skrót przywraca dokładnie to,
        // co przywróciłoby kliknięcie na pasku zadań (a nie stan zapisany przy ostatnim zapisie)
        _restoreState = window.WindowState;
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty && e.NewValue is WindowState s && s != WindowState.Minimized)
                _restoreState = s;
        };
        window.Closing += (_, _) => FlushSave();
        Hotkey.Pressed += () => Dispatcher.UIThread.Post(ToggleWindow);
        OverlayHotkey.Pressed += () => Dispatcher.UIThread.Post(() => Overlay?.Toggle());
        OverlayLockHotkey.Pressed += () => Dispatcher.UIThread.Post(() => Overlay?.ToggleLock());
    }

    private void SaveWindowPlacement(Window w)
    {
        if (w.WindowState == WindowState.Minimized) return;
        var max = w.WindowState == WindowState.Maximized;
        var prev = State.Window;
        State.Window = max && prev is not null
            ? prev with { Maximized = true }
            : new WindowPlacement(w.Position.X, w.Position.Y, w.Width, w.Height, max);
    }

    private WindowState _restoreState = WindowState.Normal;

    /// <summary>Globalny skrót: schowaj, jeśli okno jest z przodu; w przeciwnym razie wyciągnij na wierzch.</summary>
    public void ToggleWindow()
    {
        if (_window is null) return;
        if (_window.IsActive && _window.WindowState != WindowState.Minimized)
        {
            _window.WindowState = WindowState.Minimized;
            return;
        }
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = _restoreState;
        _window.Show();
        _window.Activate();
        // chwilowe "zawsze na wierzchu" wyciąga okno nad grę w trybie okna bez ramek
        _window.Topmost = true;
        _window.Topmost = State.AlwaysOnTop;
    }

    public void SetAlwaysOnTop(bool on)
    {
        State.AlwaysOnTop = on;
        if (_window is not null) _window.Topmost = on;
        RequestSave();
    }

    // ---------------------------------------------------------------- reset
    /// <summary>Usuwa postęp, ustawienia i cache, potem uruchamia aplikację od nowa.</summary>
    public void ResetEverythingAndRestart()
    {
        _saveTimer.Stop();
        Store.Delete();
        ApiCache.Clear();
        Images.ClearDisk();
        Restart();
    }

    /// <summary>Zapisuje stan i uruchamia aplikację od nowa (np. po zmianie języka).</summary>
    public void SaveAndRestart()
    {
        FlushSave();
        Restart();
    }

    private void Restart()
    {
        Hotkey.Dispose(); // zwalniamy skróty globalne, zanim nowy proces spróbuje je zarejestrować
        OverlayHotkey.Dispose();
        OverlayLockHotkey.Dispose();
        Overlay?.Dispose();
        var exe = Environment.ProcessPath;
        if (exe is not null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        Environment.Exit(0);
    }

    public void Dispose()
    {
        FlushSave();
        Hotkey.Dispose();
        OverlayHotkey.Dispose();
        OverlayLockHotkey.Dispose();
        Overlay?.Dispose();
        Images.Dispose();
        Http.Dispose();
    }
}
