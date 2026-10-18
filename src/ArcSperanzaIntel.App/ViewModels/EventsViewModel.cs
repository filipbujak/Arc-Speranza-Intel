using ArcSperanzaIntel.Core.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using ArcSperanzaIntel.Core.Maps;
using ArcSperanzaIntel.Core.Models;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>
/// Zakładka Wydarzenia: co trwa teraz i co nadchodzi, z filtrem na mapę.
/// Harmonogram z MetaForge (region Europa) sięga ok. 1,5 doby do przodu.
/// </summary>
public sealed partial class EventsViewModel : ObservableObject
{
    public const string AllMaps = "all";
    private static CultureInfo Pl => Loc.Culture;
    private readonly MainViewModel _main;
    private DateTimeOffset _lastFetch = DateTimeOffset.MinValue;

    public EventsViewModel(MainViewModel main)
    {
        _main = main;
        var selected = main.Services.State.EventsMapId ?? AllMaps;
        MapFilters = new[] { new EventMapFilterViewModel(AllMaps, Loc.T("AllMaps"), this) }
            .Concat(MapCatalog.All.Select(m => new EventMapFilterViewModel(m.Id, m.Name, this)))
            .ToList();
        _selectedMapId = MapFilters.Any(f => f.MapId == selected) ? selected : AllMaps;
        foreach (var f in MapFilters) f.IsActive = f.MapId == _selectedMapId;
        _selectedMode = main.Services.State.EventsMode;
        Rebuild();
    }

    public IReadOnlyList<EventMapFilterViewModel> MapFilters { get; }

    /// <summary>Tryby (rodzaje wydarzeń) z harmonogramu; pierwszy = wszystkie.</summary>
    public ObservableCollection<EventModeFilterViewModel> ModeFilters { get; } = [];

    [ObservableProperty] private string? _selectedMode;
    // ---------------------------------------------------------------- rozkład: mapy × godziny
    /// <summary>Ile godzin widać naraz; strony przewijają o tyle samo.</summary>
    public const int HoursPerPage = 6;

    public ObservableCollection<EventHourViewModel> Hours { get; } = [];
    public ObservableCollection<EventRowViewModel> Rows { get; } = [];

    [ObservableProperty] private int _page;
    [ObservableProperty] private bool _canPrevPage;
    [ObservableProperty] private bool _canNextPage;
    [ObservableProperty] private string _pageText = "";
    [ObservableProperty] private string _nowText = "";
    [ObservableProperty] private bool _hasLive;

    [RelayCommand]
    private void PrevPage()
    {
        if (Page <= 0) return;
        Page--;
        Rebuild();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanNextPage) return;
        Page++;
        Rebuild();
    }

    [RelayCommand]
    private void NowPage()
    {
        Page = 0;
        Rebuild();
    }

    [ObservableProperty] private string _selectedMapId;
    [ObservableProperty] private bool _noData;
    [ObservableProperty] private string _sourceText = "";
    [ObservableProperty] private bool _isRefreshing;

    /// <summary>Lista trybów z informacją, gdzie trwają teraz i kiedy następny raz.</summary>
    private void RebuildModes(List<GameEvent> live, DateTimeOffset now)
    {
        ModeFilters.Clear();
        ModeFilters.Add(new EventModeFilterViewModel(null, Loc.T("AllModes"), null, "", SelectedMode is null, this));
        foreach (var g in live.GroupBy(e => e.Name).OrderBy(g => g.Key))
        {
            var activeMaps = g.Where(e => e.IsActive(now)).Select(MapLabel).Distinct().ToList();
            string info;
            if (activeMaps.Count > 0) info = Loc.F("ModeNowOn", string.Join(", ", activeMaps));
            else
            {
                var next = g.Where(e => e.IsUpcoming(now)).OrderBy(e => e.Start).First();
                info = Loc.F("ModeNext", Clock(next.Start, now), MapLabel(next));
            }
            ModeFilters.Add(new EventModeFilterViewModel(g.Key, g.Key, g.First().IconUrl, info, SelectedMode == g.Key, this,
                activeMaps.Count > 0));
        }
        // tryb zapisany, którego nie ma już w harmonogramie — wracamy do wszystkich
        if (SelectedMode is not null && ModeFilters.All(m => m.Mode != SelectedMode))
        {
            SelectedMode = null;
            ModeFilters[0].IsActive = true;
        }
    }

    private static string MapLabel(GameEvent e) => e.MapId is { } id ? MapCatalog.Get(id).ShortName : e.MapName;

    internal void SelectMode(string? mode)
    {
        SelectedMode = mode;
        _main.Services.State.EventsMode = mode;
        _main.Services.RequestSave();
        Rebuild();
    }

    internal void SelectMap(string mapId)
    {
        SelectedMapId = mapId;
        foreach (var f in MapFilters) f.IsActive = f.MapId == mapId;
        _main.Services.State.EventsMapId = mapId;
        _main.Services.RequestSave();
        Rebuild();
    }

    /// <summary>Wołane po wejściu na zakładkę i co 30 s: odświeża liczniki, co 30 min dociąga harmonogram.</summary>
    public async Task TickAsync()
    {
        if (DateTimeOffset.UtcNow - _lastFetch > TimeSpan.FromMinutes(30))
        {
            _lastFetch = DateTimeOffset.UtcNow;
            await ReloadAsync(force: false);
            return;
        }
        Rebuild();
    }

    [RelayCommand]
    private Task Refresh() => ReloadAsync(force: true);

    /// <summary>Po zmianie regionu w ustawieniach: inny harmonogram (osobny cache na region).</summary>
    public Task RegionChangedAsync()
    {
        _lastFetch = DateTimeOffset.UtcNow;
        return ReloadAsync(force: false);
    }

    private async Task ReloadAsync(bool force)
    {
        IsRefreshing = true;
        try
        {
            await _main.Services.Data.LoadEventsAsync(force, CancellationToken.None);
        }
        finally
        {
            IsRefreshing = false;
        }
        Rebuild();
    }

    private void Rebuild()
    {
        var data = _main.Services.Data;
        var now = DateTimeOffset.UtcNow;
        var live = data.Events.Where(e => e.End > now).ToList();
        RebuildModes(live, now);

        // kolumny: pełne godziny od bieżącej, strona po HoursPerPage
        var local = now.ToLocalTime();
        var firstHour = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, 0, 0, local.Offset);
        var lastEnd = live.Count > 0 ? live.Max(e => e.End) : now;
        var maxPage = Math.Max(0, (int)Math.Ceiling((lastEnd - firstHour).TotalHours / HoursPerPage) - 1);
        Page = Math.Clamp(Page, 0, maxPage);
        var pageStart = firstHour.AddHours(Page * HoursPerPage);

        Hours.Clear();
        var slots = new List<(DateTimeOffset From, DateTimeOffset To)>();
        for (var i = 0; i < HoursPerPage; i++)
        {
            var from = pageStart.AddHours(i);
            var to = from.AddHours(1);
            slots.Add((from, to));
            var isNow = from <= now && now < to;
            Hours.Add(new EventHourViewModel(isNow ? Loc.F("EventsHourNow", from.ToString("HH:mm", Pl)) : HourLabel(from, now), isNow));
        }

        // wiersze: mapy (filtr mapy zostawia jedną); tryb nie ukrywa, tylko przygasza resztę
        Rows.Clear();
        var maps = MapCatalog.All.Where(m => SelectedMapId == AllMaps || m.Id == SelectedMapId).ToList();
        for (var r = 0; r < maps.Count; r++)
        {
            var map = maps[r];
            var cells = slots.Select(slot => new EventCellViewModel(
                slot.From <= now && now < slot.To,
                live.Where(e => e.MapId == map.Id && e.Start < slot.To && e.End > slot.From)
                    .OrderBy(e => e.Name)
                    .Select(e => new EventChipViewModel(e, SelectedMode is null || e.Name == SelectedMode, this))
                    .ToList())).ToList();
            Rows.Add(new EventRowViewModel(map.Name, Palette.Brush(MapTint(r)), cells));
        }

        var liveNow = live.Count(e => e.IsActive(now) && (SelectedMapId == AllMaps || e.MapId == SelectedMapId));
        HasLive = liveNow > 0;
        var nextChange = firstHour.AddHours(1) - now;
        NowText = Loc.F("EventsNowLine", local.ToString("HH:mm", Pl), liveNow, Span(nextChange));
        CanPrevPage = Page > 0;
        CanNextPage = Page < maxPage;
        PageText = $"{pageStart:HH:mm} – {pageStart.AddHours(HoursPerPage):HH:mm}";
        NoData = data.Events.Count == 0;
        SourceText = data.EventsSavedAt is { } t
            ? Loc.F("EventsSource", t.ToLocalTime().ToString("dd.MM HH:mm", Pl), EventRegionOptionViewModel.NameOf(_main.Services.Repository.EventRegion))
            : Loc.F("EventsSourceShort", EventRegionOptionViewModel.NameOf(_main.Services.Repository.EventRegion));
    }

    /// <summary>„18:00”, a po północy z dniem: „jutro 01:00”.</summary>
    private static string HourLabel(DateTimeOffset from, DateTimeOffset now) =>
        from.Date == now.ToLocalTime().Date ? from.ToString("HH:mm", Pl) : Clock(from, now);

    private static readonly Avalonia.Media.Color[] MapTints =
    [
        Avalonia.Media.Color.Parse("#3FD07A"), Avalonia.Media.Color.Parse("#36B5E6"), Avalonia.Media.Color.Parse("#E88629"),
        Avalonia.Media.Color.Parse("#FFC72C"), Avalonia.Media.Color.Parse("#B57BFF"), Avalonia.Media.Color.Parse("#26BF8F"),
        Avalonia.Media.Color.Parse("#F0503F"),
    ];

    private static Avalonia.Media.Color MapTint(int i) => MapTints[i % MapTints.Length];

    internal void OpenMap(string? mapId)
    {
        if (mapId is not null) _main.OpenMap(mapId);
    }

    internal static string Span(TimeSpan t)
    {
        if (t.TotalMinutes < 1) return "<1 min";
        if (t.TotalHours < 1) return $"{(int)t.TotalMinutes} min";
        if (t.TotalDays < 1) return t.Minutes == 0 ? $"{(int)t.TotalHours} h" : $"{(int)t.TotalHours} h {t.Minutes} min";
        return $"{(int)t.TotalDays} d {t.Hours} h";
    }

    internal static string Clock(DateTimeOffset t, DateTimeOffset now)
    {
        var local = t.ToLocalTime();
        var today = now.ToLocalTime().Date;
        var day = local.Date == today ? Loc.T("Today") : local.Date == today.AddDays(1) ? Loc.T("Tomorrow") : local.ToString("ddd dd.MM", Pl);
        return $"{day} {local:HH:mm}";
    }
}

public sealed partial class EventModeFilterViewModel(string? mode, string name, string? iconUrl, string info, bool active,
    EventsViewModel owner, bool isLive = false) : ObservableObject
{
    public string? Mode { get; } = mode;
    public string Name { get; } = name;
    public string? IconUrl { get; } = iconUrl;
    public bool HasIcon => IconUrl is not null;
    /// <summary>Kolor wydarzenia — obwódka okrągłej ikony (jak w siatce godzin).</summary>
    public Avalonia.Media.IBrush Accent { get; } = new Avalonia.Media.SolidColorBrush(EventColors.For(name));
    public string Info { get; } = info;
    public bool HasInfo => Info.Length > 0;
    public bool IsLive { get; } = isLive;

    [ObservableProperty] private bool _isActive = active;

    [RelayCommand]
    private void Select() => owner.SelectMode(Mode);
}

public sealed partial class EventMapFilterViewModel(string mapId, string name, EventsViewModel owner) : ObservableObject
{
    public string MapId { get; } = mapId;
    public string Name { get; } = name;

    [ObservableProperty] private bool _isActive;

    [RelayCommand]
    private void Select() => owner.SelectMap(MapId);
}

/// <summary>Nagłówek kolumny godziny.</summary>
public sealed class EventHourViewModel(string label, bool isNow)
{
    public string Label { get; } = label;
    public bool IsNow { get; } = isNow;
    public IBrush Foreground { get; } = Palette.Brush(isNow ? Avalonia.Media.Color.Parse("#FF8A7A") : Palette.Muted);
}

/// <summary>Wiersz rozkładu: mapa i jej komórki godzin.</summary>
public sealed class EventRowViewModel(string map, IBrush tint, IReadOnlyList<EventCellViewModel> cells)
{
    public string Map { get; } = map;
    public IBrush Tint { get; } = tint;
    public IReadOnlyList<EventCellViewModel> Cells { get; } = cells;
}

/// <summary>Komórka: jedna mapa w jednej godzinie (zwykle 0–2 wydarzenia).</summary>
public sealed class EventCellViewModel(bool isNow, IReadOnlyList<EventChipViewModel> events)
{
    private static readonly IBrush NowBack = new SolidColorBrush(Avalonia.Media.Color.FromArgb(0x10, 0xF0, 0x50, 0x3F));

    public bool IsNow { get; } = isNow;
    public IBrush Background { get; } = isNow ? NowBack : Brushes.Transparent;
    public IReadOnlyList<EventChipViewModel> Events { get; } = events;
}

/// <summary>Wydarzenie w komórce rozkładu: kolor trybu, klik otwiera mapę.</summary>
public sealed partial class EventChipViewModel
{
    private readonly string? _mapId;
    private readonly EventsViewModel _owner;

    public EventChipViewModel(GameEvent e, bool matchesFilter, EventsViewModel owner)
    {
        _owner = owner;
        _mapId = e.MapId;
        Name = e.Name;
        IconUrl = e.IconUrl;
        var color = EventColors.For(e.Name);
        Accent = Palette.Brush(color);
        Fill = Palette.Brush(Avalonia.Media.Color.FromArgb(0x30, color.R, color.G, color.B));
        Opacity = matchesFilter ? 1 : 0.22;
        var map = e.MapId is { } id ? MapCatalog.Get(id).Name : e.MapName;
        Tooltip = $"{e.Name}\n{map} · {e.Start.ToLocalTime():HH:mm} – {e.End.ToLocalTime():HH:mm}";
    }

    public string Name { get; }
    public string? IconUrl { get; }
    public bool HasIcon => IconUrl is not null;
    public IBrush Accent { get; }
    public IBrush Fill { get; }
    public double Opacity { get; }
    public string Tooltip { get; }

    [RelayCommand]
    private void OpenMap() => _owner.OpenMap(_mapId);
}

/// <summary>Stały kolor dla każdego rodzaju wydarzenia (znane z gry + kolor z nazwy dla nowych).</summary>
public static class EventColors
{
    private static readonly Dictionary<string, Avalonia.Media.Color> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Night Raid"] = Avalonia.Media.Color.Parse("#7C86FF"),
        ["ARC Frigate"] = Avalonia.Media.Color.Parse("#F0503F"),
        ["Matriarch"] = Avalonia.Media.Color.Parse("#CC3099"),
        ["Electromagnetic Storm"] = Avalonia.Media.Color.Parse("#36B5E6"),
        ["Hurricane"] = Avalonia.Media.Color.Parse("#5FC9D6"),
        ["Lush Blooms"] = Avalonia.Media.Color.Parse("#3FD07A"),
        ["Prospecting Probes"] = Avalonia.Media.Color.Parse("#FFC72C"),
        ["Bird City"] = Avalonia.Media.Color.Parse("#E88629"),
        ["Beachcombing"] = Avalonia.Media.Color.Parse("#26BF8F"),
        ["Close Scrutiny"] = Avalonia.Media.Color.Parse("#9AA3AE"),
        ["Redirection"] = Avalonia.Media.Color.Parse("#B57BFF"),
        ["Harvester"] = Avalonia.Media.Color.Parse("#FF7A45"),
        ["Cold Snap"] = Avalonia.Media.Color.Parse("#9ED8FF"),
        ["Husk Graveyard"] = Avalonia.Media.Color.Parse("#C9A27A"),
        ["Uncovered Caches"] = Avalonia.Media.Color.Parse("#A3E05A"),
    };

    private static readonly Avalonia.Media.Color[] Spare =
    [
        Avalonia.Media.Color.Parse("#E8C547"), Avalonia.Media.Color.Parse("#57C4AD"), Avalonia.Media.Color.Parse("#D97AD9"),
        Avalonia.Media.Color.Parse("#6FA8FF"), Avalonia.Media.Color.Parse("#F29E6B"), Avalonia.Media.Color.Parse("#8FD16A"),
    ];

    public static Avalonia.Media.Color For(string name)
    {
        if (Known.TryGetValue(name, out var c)) return c;
        var h = 0;
        foreach (var ch in name.ToLowerInvariant()) h = h * 31 + ch;
        return Spare[(h & 0x7fffffff) % Spare.Length];
    }
}
