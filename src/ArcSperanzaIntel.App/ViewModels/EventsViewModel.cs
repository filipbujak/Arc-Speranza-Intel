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
    public ObservableCollection<EventCardViewModel> Active { get; } = [];
    public ObservableCollection<EventSlotViewModel> Upcoming { get; } = [];

    [ObservableProperty] private string _selectedMapId;
    [ObservableProperty] private bool _hasActive;
    [ObservableProperty] private bool _noActive;
    [ObservableProperty] private bool _hasUpcoming;
    [ObservableProperty] private bool _noData;
    [ObservableProperty] private string _sourceText = "";
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _modeTitle;

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
        var events = live
            .Where(e => SelectedMapId == AllMaps || e.MapId == SelectedMapId)
            .Where(e => SelectedMode is null || e.Name == SelectedMode)
            .ToList();

        Active.Clear();
        foreach (var e in events.Where(e => e.IsActive(now)).OrderBy(e => e.End).ThenBy(e => e.MapName))
            Active.Add(new EventCardViewModel(e, now, this));

        Upcoming.Clear();
        foreach (var g in events.Where(e => e.IsUpcoming(now)).GroupBy(e => e.Start).OrderBy(g => g.Key))
            Upcoming.Add(new EventSlotViewModel(g.Key, now, g.OrderBy(e => e.MapName).Select(e => new EventCardViewModel(e, now, this))));

        ModeTitle = SelectedMode is null ? null : SelectedMode.ToUpperInvariant();
        HasActive = Active.Count > 0;
        NoActive = !HasActive && data.Events.Count > 0;
        HasUpcoming = Upcoming.Count > 0;
        NoData = data.Events.Count == 0;
        SourceText = data.EventsSavedAt is { } t
            ? Loc.F("EventsSource", t.ToLocalTime().ToString("dd.MM HH:mm", Pl))
            : Loc.T("EventsSourceShort");
    }

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

/// <summary>Jedno wydarzenie na karcie.</summary>
public sealed partial class EventCardViewModel : ObservableObject
{
    private readonly string? _mapId;
    private readonly EventsViewModel _owner;

    public EventCardViewModel(GameEvent e, DateTimeOffset now, EventsViewModel owner)
    {
        _owner = owner;
        _mapId = e.MapId;
        Name = e.Name;
        MapName = e.MapId is { } id ? MapCatalog.Get(id).Name : e.MapName;
        IconUrl = e.IconUrl;
        HoursText = $"{e.Start.ToLocalTime():HH:mm} – {e.End.ToLocalTime():HH:mm}";
        var total = (e.End - e.Start).TotalSeconds;
        Progress = e.IsActive(now) && total > 0 ? Math.Clamp((now - e.Start).TotalSeconds / total * 100, 0, 100) : 0;
        TimeText = e.IsActive(now)
            ? Loc.F("EndsIn", EventsViewModel.Span(e.End - now))
            : Loc.F("StartsIn", EventsViewModel.Span(e.Start - now));
        AccentBrush = Palette.Brush(AccentFor(e.Name));
    }

    public string Name { get; }
    public string MapName { get; }
    public string? IconUrl { get; }
    public string HoursText { get; }
    public string TimeText { get; }
    public double Progress { get; }
    public IBrush AccentBrush { get; }

    [RelayCommand]
    private void OpenMap() => _owner.OpenMap(_mapId);

    /// <summary>Kolor wg charakteru wydarzenia: groźne na czerwono, łupy na zielono, pogoda na niebiesko.</summary>
    private static Avalonia.Media.Color AccentFor(string name) => name.ToLowerInvariant() switch
    {
        var n when n.Contains("matriarch") || n.Contains("harvester") || n.Contains("frigate") || n.Contains("night") => Palette.Red,
        var n when n.Contains("storm") || n.Contains("hurricane") || n.Contains("cold") => Palette.Blue,
        var n when n.Contains("loot") || n.Contains("cache") || n.Contains("bloom") || n.Contains("beach") || n.Contains("probe") => Palette.Green,
        _ => Palette.Yellow,
    };
}

/// <summary>Godzina startu + wydarzenia, które wtedy ruszają.</summary>
public sealed class EventSlotViewModel(DateTimeOffset start, DateTimeOffset now, IEnumerable<EventCardViewModel> events)
{
    public string Header { get; } = EventsViewModel.Clock(start, now).ToUpperInvariant();
    public string InText { get; } = Loc.F("StartsIn", EventsViewModel.Span(start - now));
    public IReadOnlyList<EventCardViewModel> Events { get; } = events.ToList();
}
