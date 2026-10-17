using ArcSperanzaIntel.Core.Localization;
using System.Collections.ObjectModel;
using ArcSperanzaIntel.App.Controls;
using ArcSperanzaIntel.App.Icons;
using ArcSperanzaIntel.Core.Maps;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Quests;
using ArcSperanzaIntel.Core.Util;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>Zakładka Mapy: wybór mapy i piętra, filtry, markery, questy na mapie, ulubione, własne pinezki.</summary>
public sealed partial class MapsViewModel : ObservableObject
{
    public const string QuestAvailableKey = "quests/available";
    public const string QuestLockedKey = "quests/locked";
    public const string QuestDoneKey = "quests/done";
    public const string PinsKey = "custom/pin";

    private static readonly string[] CategoryOrder =
        [MarkerCategories.Quests, MarkerCategories.Locations, MarkerCategories.Arc, MarkerCategories.Containers, MarkerCategories.Events, MarkerCategories.Nature];

    private static readonly IReadOnlyDictionary<string, string> CategoryTitles = new Dictionary<string, string>
    {
        [MarkerCategories.Quests] = "CatQuests",
        [MarkerCategories.Locations] = "CatLocations",
        [MarkerCategories.Arc] = "CatArc",
        [MarkerCategories.Containers] = "CatContainers",
        [MarkerCategories.Events] = "CatEvents",
        [MarkerCategories.Nature] = "CatNature",
        ["custom"] = "CatMine",
    };

    private readonly MainViewModel _main;
    private string? _highlightQuestId;

    public MapsViewModel(MainViewModel main)
    {
        _main = main;
        var s = main.Services.State;
        MapOptions = MapCatalog.All.Select(m => new MapOptionViewModel(m, this)).ToList();
        _selectedMap = MapCatalog.Get(s.SelectedMapId);
        _onlyFavorites = s.OnlyFavoriteMarkers;
        UpdateMapOptionStates();
    }

    private Core.Api.GameData Data => _main.Services.Data;
    private QuestBook Book => _main.Services.Quests;
    private UserState State => _main.Services.State;
    public WantedItemsViewModel? Wanted => _main.Wanted;

    // ---------------------------------------------------------------- mapa i piętro
    public IReadOnlyList<MapOptionViewModel> MapOptions { get; }

    [ObservableProperty] private MapDefinition _selectedMap;
    [ObservableProperty] private MapFloor? _currentFloor;
    [ObservableProperty] private bool _isLoadingMarkers;
    public ObservableCollection<FloorOptionViewModel> FloorOptions { get; } = [];
    public bool HasFloors => SelectedMap.HasFloors;

    /// <summary>Prośby do widoku: ustaw widok, przeleć do punktu / prostokąta.</summary>
    public event Action<LatLng, double>? SetViewRequested;
    public event Action<LatLng, double>? FlyToRequested;
    public event Action<LatLngBounds>? FlyToBoundsRequested;

    public void SelectMap(MapDefinition map)
    {
        if (map.Id == SelectedMap.Id) return;
        SelectedMap = map;
        _ = LoadCurrentMapAsync();
    }

    partial void OnSelectedMapChanged(MapDefinition value)
    {
        State.SelectedMapId = value.Id;
        _main.Services.RequestSave();
        _highlightQuestId = null;
        CloseCards();
        UpdateMapOptionStates();
        OnPropertyChanged(nameof(HasFloors));
    }

    private void UpdateMapOptionStates()
    {
        foreach (var o in MapOptions) o.IsActive = o.Map.Id == SelectedMap.Id;
    }

    public async Task LoadCurrentMapAsync()
    {
        var map = SelectedMap;
        BuildFloors(map);
        IsLoadingMarkers = true;
        try
        {
            await Data.LoadMarkersAsync(map.Id, force: false, CancellationToken.None);
        }
        finally
        {
            IsLoadingMarkers = false;
        }
        if (SelectedMap.Id != map.Id) return; // użytkownik zdążył zmienić mapę

        var v = State.MapViews.GetValueOrDefault(map.Id);
        SetViewRequested?.Invoke(v is null ? map.Center : new LatLng(v.Lat, v.Lng), v?.Zoom ?? map.InitialZoom);
        BuildFilters();
        RebuildVisuals();
        RefreshPins();
        RefreshQuestCounts();
    }

    public void ForceReloadCurrentMap()
    {
        _ = ReloadAsync();
        async Task ReloadAsync()
        {
            await Data.LoadMarkersAsync(SelectedMap.Id, force: true, CancellationToken.None);
            await LoadCurrentMapAsync();
        }
    }

    private void BuildFloors(MapDefinition map)
    {
        FloorOptions.Clear();
        if (!map.HasFloors)
        {
            CurrentFloor = null;
            return;
        }
        var id = State.SelectedFloors.GetValueOrDefault(map.Id) ?? map.Floors[0].Id;
        CurrentFloor = map.Floors.FirstOrDefault(f => f.Id == id) ?? map.Floors[0];
        foreach (var f in map.Floors) FloorOptions.Add(new FloorOptionViewModel(f, f.Id == CurrentFloor.Id, this));
    }

    public void SelectFloor(MapFloor floor)
    {
        State.SelectedFloors[SelectedMap.Id] = floor.Id;
        _main.Services.RequestSave();
        CurrentFloor = floor;
        foreach (var o in FloorOptions) o.IsActive = o.Floor.Id == floor.Id;
        CloseCards();
        BuildFilters();
        RebuildVisuals();
        RefreshPins();
    }

    public void SaveView(LatLng center, double zoom)
    {
        State.MapViews[SelectedMap.Id] = new MapViewState(center.Lat, center.Lng, zoom);
        _main.Services.RequestSave();
    }

    /// <summary>Odznaki na wyborze map: ile dostępnych questów czeka na danej mapie.</summary>
    public void RefreshQuestCounts()
    {
        var available = Book.WithStatus(QuestStatus.Available).ToList();
        foreach (var o in MapOptions)
            o.QuestCount = available.Count(q => _main.Services.MapIndex.IsOn(q, o.Map));
    }

    // ---------------------------------------------------------------- filtry
    public ObservableCollection<FilterGroupViewModel> FilterGroups { get; } = [];

    [ObservableProperty] private bool _onlyFavorites;
    [ObservableProperty] private int _visibleCount;

    partial void OnOnlyFavoritesChanged(bool value)
    {
        State.OnlyFavoriteMarkers = value;
        _main.Services.RequestSave();
        RebuildVisuals();
    }

    internal static bool DefaultOn(string key)
    {
        var slash = key.IndexOf('/');
        var cat = slash < 0 ? key : key[..slash];
        var sub = slash < 0 ? "" : key[(slash + 1)..];
        return cat switch
        {
            MarkerCategories.Quests => key == QuestAvailableKey,
            MarkerCategories.Arc => true,
            "custom" => true,
            MarkerCategories.Locations => sub != "field_crate",
            MarkerCategories.Containers => sub is "weapon_case" or "raider_cache" or "frozen_raider_cache" or "security_breach"
                or "hurricane_cache" or "combat_supplies" or "med_crate" or "ammo_crate" or "arc_courier" or "arc_probe"
                or "baron_husk" or "deforester_husk" or "arc_husk",
            MarkerCategories.Events => sub is not ("bird" or "piano" or "ship_model" or "snow_pile" or "dig_spot" or "mine"
                or "security_camera" or "car_alarm"),
            _ => false,
        };
    }

    internal bool IsOn(string key) => State.MapFilters.TryGetValue(key, out var v) ? v : DefaultOn(key);

    internal void SetFilter(string key, bool on, bool rebuild = true)
    {
        State.MapFilters[key] = on;
        _main.Services.RequestSave();
        if (rebuild) RebuildVisuals();
        if (key == PinsKey) RefreshPins();
    }

    private string QuestKey(MapMarker m) => Book.StatusOf(m.Subcategory) switch
    {
        QuestStatus.Done => QuestDoneKey,
        QuestStatus.Locked => QuestLockedKey,
        _ => QuestAvailableKey,
    };

    private IEnumerable<MapMarker> MarkersOnFloor() =>
        Data.MarkersOf(SelectedMap.Id).Where(m => SelectedMap.IsOnFloor(m, CurrentFloor));

    private void BuildFilters()
    {
        var counts = new Dictionary<string, int>();
        foreach (var m in MarkersOnFloor())
        {
            var key = m.IsQuest ? QuestKey(m) : m.Key;
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var expanded = FilterGroups.Where(g => g.IsExpanded).Select(g => g.Category).ToHashSet();
        var firstBuild = FilterGroups.Count == 0;
        FilterGroups.Clear();

        // Questy: zawsze trzy pozycje
        var questItems = new[]
        {
            (QuestAvailableKey, Loc.T("FilterAvailable")), (QuestLockedKey, Loc.T("FilterLocked")), (QuestDoneKey, Loc.T("FilterDone")),
        }.Select(t => new FilterItemViewModel(t.Item1, t.Item2, counts.GetValueOrDefault(t.Item1),
            MarkerIcons.For(MarkerCategories.Quests, ""), MarkerIcons.ColorOf(MarkerCategories.Quests), IsOn(t.Item1), this)).ToList();
        FilterGroups.Add(new FilterGroupViewModel(MarkerCategories.Quests, Loc.T(CategoryTitles[MarkerCategories.Quests]),
            MarkerIcons.ColorOf(MarkerCategories.Quests), questItems, firstBuild || expanded.Contains(MarkerCategories.Quests), this));

        foreach (var cat in CategoryOrder.Skip(1).Concat(counts.Keys.Select(k => k.Split('/')[0]).Distinct().Except(CategoryOrder)))
        {
            var keys = counts.Keys.Where(k => k.StartsWith(cat + "/", StringComparison.Ordinal) && !k.StartsWith("quests/", StringComparison.Ordinal))
                .OrderByDescending(k => counts[k]).ToList();
            if (keys.Count == 0) continue;
            var items = keys.Select(k =>
            {
                var sub = k[(cat.Length + 1)..];
                return new FilterItemViewModel(k, TextFormat.Pretty(sub), counts[k], MarkerIcons.For(cat, sub), MarkerIcons.ColorOf(cat), IsOn(k), this);
            }).ToList();
            FilterGroups.Add(new FilterGroupViewModel(cat, CategoryTitles.GetValueOrDefault(cat) is { } titleKey ? Loc.T(titleKey) : TextFormat.Pretty(cat),
                MarkerIcons.ColorOf(cat), items, firstBuild ? cat is MarkerCategories.Locations or MarkerCategories.Arc : expanded.Contains(cat), this));
        }

        var pins = State.Pins.GetValueOrDefault(SelectedMap.Id)?.Count ?? 0;
        FilterGroups.Add(new FilterGroupViewModel("custom", Loc.T(CategoryTitles["custom"]), MarkerIcons.ColorOf("custom"),
            [new FilterItemViewModel(PinsKey, Loc.T("MyPins"), pins, UiIcons.Pin, MarkerIcons.ColorOf("custom"), IsOn(PinsKey), this, iconViewBox: 24)],
            true, this));
    }

    [RelayCommand]
    private void ShowAll() => SetAll(true);

    [RelayCommand]
    private void HideAll() => SetAll(false);

    [RelayCommand]
    private void ResetFilters()
    {
        State.MapFilters.Clear();
        _main.Services.RequestSave();
        BuildFilters();
        RebuildVisuals();
        RefreshPins();
    }

    private void SetAll(bool on)
    {
        foreach (var g in FilterGroups)
        foreach (var i in g.Items)
            i.SetSilently(on);
        RebuildVisuals();
        RefreshPins();
    }

    internal void SetGroup(FilterGroupViewModel group, bool on)
    {
        foreach (var i in group.Items) i.SetSilently(on);
        RebuildVisuals();
        RefreshPins();
    }

    // ---------------------------------------------------------------- markery do narysowania
    [ObservableProperty] private IReadOnlyList<MarkerVisual> _markerVisuals = [];
    [ObservableProperty] private IReadOnlyList<MapPin> _pins = [];

    public void RebuildVisuals()
    {
        var list = new List<MarkerVisual>();
        var favs = State.FavoriteMarkers;
        foreach (var m in MarkersOnFloor())
        {
            var fav = favs.Contains(m.Id);
            if (OnlyFavorites && !fav) continue;
            if (m.IsQuest)
            {
                var key = QuestKey(m);
                var highlighted = m.Subcategory == _highlightQuestId;
                if (!IsOn(key) && !highlighted) continue;
                var status = Book.StatusOf(m.Subcategory);
                var q = Book.Get(m.Subcategory);
                list.Add(new MarkerVisual(m, Palette.Status(status), MarkerIcons.For(MarkerCategories.Quests, ""), MarkerShape.Diamond,
                    (q?.Name ?? TextFormat.Pretty(m.Subcategory)) + (m.Name is null ? "" : " — " + m.Name))
                {
                    Favorite = fav,
                    Dimmed = status == QuestStatus.Done,
                    Highlighted = highlighted,
                });
                continue;
            }
            if (!IsOn(m.Key)) continue;
            list.Add(new MarkerVisual(m, MarkerIcons.ColorOf(m.Category), MarkerIcons.For(m.Category, m.Subcategory), MarkerShape.Circle,
                m.Name ?? TextFormat.Pretty(m.Subcategory)) { Favorite = fav });
        }
        MarkerVisuals = list;
        VisibleCount = list.Count;
    }

    private void RefreshPins()
    {
        Pins = IsOn(PinsKey) ? (State.Pins.GetValueOrDefault(SelectedMap.Id) ?? []).ToList() : [];
    }

    public void OnQuestProgressChanged()
    {
        BuildFilters();
        RebuildVisuals();
        RefreshQuestCounts();
        if (SelectedCard?.Quest is not null) SelectedCard.RefreshQuest();
    }

    // ---------------------------------------------------------------- wyszukiwarka markerów
    [ObservableProperty] private string _searchText = "";
    public ObservableCollection<MarkerSearchResultViewModel> SearchResults { get; } = [];
    [ObservableProperty] private bool _hasSearchResults;

    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        var term = value.Trim();
        if (term.Length >= 2)
        {
            foreach (var m in MarkersOnFloor())
            {
                var label = m.IsQuest ? Book.Get(m.Subcategory)?.Name ?? m.Subcategory : m.Name ?? TextFormat.Pretty(m.Subcategory);
                var sub = m.IsQuest ? "Quest" + (m.Name is null ? "" : " · " + m.Name) : TextFormat.Pretty(m.Subcategory);
                if (TextFormat.ContainsLoose(label, term) || TextFormat.ContainsLoose(sub, term) || TextFormat.ContainsLoose(m.Name, term))
                {
                    SearchResults.Add(new MarkerSearchResultViewModel(m, label, sub, MarkerIcons.ColorOf(m.Category), this));
                    if (SearchResults.Count >= 60) break;
                }
            }
        }
        HasSearchResults = SearchResults.Count > 0;
    }

    internal void GoToMarker(MapMarker m)
    {
        if (m.IsQuest)
        {
            var key = QuestKey(m);
            if (!IsOn(key)) SetFilter(key, true, rebuild: false);
        }
        else if (!IsOn(m.Key)) SetFilter(m.Key, true, rebuild: false);
        BuildFilters();
        RebuildVisuals();
        SearchText = "";
        FlyToRequested?.Invoke(m.Position, Math.Max(SelectedMap.InitialZoom + 2, 5));
        var v = MarkerVisuals.FirstOrDefault(x => x.Marker.Id == m.Id);
        if (v is not null) OpenMarker(v);
    }

    // ---------------------------------------------------------------- dymek markera
    [ObservableProperty] private MarkerCardViewModel? _selectedCard;
    [ObservableProperty] private PinCardViewModel? _selectedPin;

    public void OpenMarker(MarkerVisual v)
    {
        SelectedPin = null;
        SelectedCard = new MarkerCardViewModel(v.Marker, this, _main);
    }

    public void OpenPin(MapPin pin)
    {
        SelectedCard = null;
        SelectedPin = new PinCardViewModel(pin, this);
    }

    public void CloseCards()
    {
        SelectedCard = null;
        SelectedPin = null;
    }

    internal bool IsFavorite(string markerId) => State.FavoriteMarkers.Contains(markerId);

    internal void ToggleFavorite(string markerId)
    {
        if (!State.FavoriteMarkers.Remove(markerId)) State.FavoriteMarkers.Add(markerId);
        _main.Services.RequestSave();
        RebuildVisuals();
    }

    internal void HideType(string key)
    {
        SetFilter(key, false, rebuild: false);
        BuildFilters();
        RebuildVisuals();
        CloseCards();
    }

    // ---------------------------------------------------------------- pinezki
    public async Task AddPinAsync(LatLng at)
    {
        var note = await _main.PromptAsync(Loc.T("NewPin"), Loc.T("NewPinHelp"), "", Loc.T("Add"));
        if (note is null) return;
        var list = State.Pins.TryGetValue(SelectedMap.Id, out var l) ? l : State.Pins[SelectedMap.Id] = [];
        list.Add(new MapPin { Lat = at.Lat, Lng = at.Lng, Note = note.Trim(), FloorId = CurrentFloor?.Id });
        if (!IsOn(PinsKey)) SetFilter(PinsKey, true, rebuild: false);
        _main.Services.RequestSave();
        RefreshPins();
        BuildFilters();
        _main.Toast(Loc.T("PinAdded"), ToastKind.Ok);
    }

    public void PinMoved()
    {
        _main.Services.RequestSave();
        RefreshPins();
    }

    internal async Task EditPinAsync(MapPin pin)
    {
        var note = await _main.PromptAsync(Loc.T("EditPin"), "", pin.Note);
        if (note is null) return;
        pin.Note = note.Trim();
        _main.Services.RequestSave();
        RefreshPins();
        if (SelectedPin?.Pin == pin) SelectedPin = new PinCardViewModel(pin, this);
    }

    internal void DeletePin(MapPin pin)
    {
        if (State.Pins.TryGetValue(SelectedMap.Id, out var list)) list.Remove(pin);
        _main.Services.RequestSave();
        SelectedPin = null;
        RefreshPins();
        BuildFilters();
    }

    // ---------------------------------------------------------------- questy na mapie
    /// <summary>Przełącza mapę (jeśli trzeba), włącza markery questu, podświetla i przelatuje do nich.</summary>
    public async Task ShowQuestAsync(string questId, string mapId)
    {
        if (SelectedMap.Id != mapId)
        {
            SelectedMap = MapCatalog.Get(mapId);
            await LoadCurrentMapAsync();
        }
        var markers = Data.MarkersOf(mapId).Where(m => m.IsQuest && m.Subcategory == questId).ToList();
        if (markers.Count == 0)
        {
            _main.Toast(Loc.T("NoQuestMarkers"));
            return;
        }
        if (SelectedMap.HasFloors && CurrentFloor is not null && !markers.Any(m => SelectedMap.IsOnFloor(m, CurrentFloor)))
        {
            var floor = SelectedMap.Floors.FirstOrDefault(f => markers.Any(m => SelectedMap.IsOnFloor(m, f)));
            if (floor is not null) SelectFloor(floor);
        }
        _highlightQuestId = questId;
        RebuildVisuals();
        if (markers.Count == 1) FlyToRequested?.Invoke(markers[0].Position, Math.Max(SelectedMap.InitialZoom + 2, 5));
        else FlyToBoundsRequested?.Invoke(LatLngBounds.From(markers.Select(m => m.Position)));
        var first = MarkerVisuals.FirstOrDefault(v => v.Marker.Subcategory == questId);
        if (first is not null) OpenMarker(first);
    }

    internal QuestBook Quests => Book;
    internal MainViewModel Main => _main;
}

// ======================================================================== elementy listy

public sealed partial class MapOptionViewModel(MapDefinition map, MapsViewModel owner) : ObservableObject
{
    public MapDefinition Map { get; } = map;
    public string Name { get; } = map.Name;

    [ObservableProperty] private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuests))]
    private int _questCount;

    public bool HasQuests => QuestCount > 0;

    [RelayCommand]
    private void Select() => owner.SelectMap(Map);
}

public sealed partial class FloorOptionViewModel(MapFloor floor, bool active, MapsViewModel owner) : ObservableObject
{
    public MapFloor Floor { get; } = floor;
    public string Name { get; } = Loc.T(floor.Name);

    [ObservableProperty] private bool _isActive = active;

    [RelayCommand]
    private void Select() => owner.SelectFloor(Floor);
}

public sealed partial class FilterGroupViewModel : ObservableObject
{
    private readonly MapsViewModel _owner;

    public FilterGroupViewModel(string category, string title, Color color, IReadOnlyList<FilterItemViewModel> items, bool expanded, MapsViewModel owner)
    {
        _owner = owner;
        Category = category;
        Title = title.ToUpperInvariant();
        ColorBrush = Palette.Brush(color);
        Items = items;
        TotalCount = items.Sum(i => i.Count);
        _isExpanded = expanded;
    }

    public string Category { get; }
    public string Title { get; }
    public IBrush ColorBrush { get; }
    public IReadOnlyList<FilterItemViewModel> Items { get; }
    public int TotalCount { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronAngle))]
    private bool _isExpanded;

    public double ChevronAngle => IsExpanded ? 0 : -90;

    [RelayCommand] private void Toggle() => IsExpanded = !IsExpanded;
    [RelayCommand] private void ShowAll() => _owner.SetGroup(this, true);
    [RelayCommand] private void HideAll() => _owner.SetGroup(this, false);
}

public sealed partial class FilterItemViewModel : ObservableObject
{
    private readonly MapsViewModel _owner;
    private bool _silent;

    public FilterItemViewModel(string key, string label, int count, Geometry icon, Color color, bool on, MapsViewModel owner, double iconViewBox = 512)
    {
        _owner = owner;
        Key = key;
        Label = label;
        Count = count;
        Icon = icon;
        IconBrush = Palette.Brush(color);
        IconViewBox = iconViewBox;
        _isOn = on;
    }

    public string Key { get; }
    public string Label { get; }
    public int Count { get; }
    public Geometry Icon { get; }
    public IBrush IconBrush { get; }
    public double IconViewBox { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowOpacity))]
    private bool _isOn;

    public double RowOpacity => IsOn ? 1 : 0.38;

    partial void OnIsOnChanged(bool value)
    {
        if (_silent) return;
        _owner.SetFilter(Key, value);
    }

    /// <summary>Zmiana bez przebudowy mapy (przy "pokaż/ukryj wszystko").</summary>
    internal void SetSilently(bool on)
    {
        _silent = true;
        IsOn = on;
        _silent = false;
        _owner.SetFilter(Key, on, rebuild: false);
    }

    [RelayCommand]
    private void Toggle() => IsOn = !IsOn;
}

public sealed partial class MarkerSearchResultViewModel(MapMarker marker, string label, string sub, Color color, MapsViewModel owner) : ObservableObject
{
    public string Label { get; } = label;
    public string Sub { get; } = sub;
    public IBrush ColorBrush { get; } = Palette.Brush(color);

    [RelayCommand]
    private void Go() => owner.GoToMarker(marker);
}

/// <summary>Dymek klikniętego markera (zwykłego albo questowego z subtaskami).</summary>
public sealed partial class MarkerCardViewModel : ObservableObject
{
    private readonly MapsViewModel _owner;
    private readonly MainViewModel _main;

    public MarkerCardViewModel(MapMarker marker, MapsViewModel owner, MainViewModel main)
    {
        Marker = marker;
        _owner = owner;
        _main = main;
        _isFavorite = owner.IsFavorite(marker.Id);
        Quest = marker.IsQuest ? owner.Quests.Get(marker.Subcategory) : null;
        Title = Quest?.Name ?? marker.Name ?? TextFormat.Pretty(marker.Subcategory);
        Subtitle = marker.IsQuest
            ? (marker.Name ?? Loc.T("QuestSpot"))
            : TextFormat.Pretty(marker.Category) + " · " + TextFormat.Pretty(marker.Subcategory);
        Loot = marker.LootAreas is null ? null : "Loot: " + marker.LootAreas;
        AccentBrush = Palette.Brush(MarkerIcons.ColorOf(marker.Category));
        RefreshQuest();
    }

    public MapMarker Marker { get; }
    public Quest? Quest { get; }
    public bool IsQuest => Quest is not null;
    public bool IsNotQuest => Quest is null;
    public string Title { get; }
    public string Subtitle { get; }
    public string? Loot { get; }
    public bool HasLoot => Loot is not null;
    public bool IsLocked => Marker.BehindLockedDoor;
    public IBrush AccentBrush { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteText))]
    private bool _isFavorite;

    public string FavoriteText => IsFavorite ? Loc.T("FavOn") : Loc.T("FavOff");

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private IBrush _statusBrush = Palette.Brush(Palette.Muted);
    [ObservableProperty] private string _questButtonText = "";
    [ObservableProperty] private string _traderText = "";
    public ObservableCollection<ObjectiveViewModel> Objectives { get; } = [];
    [ObservableProperty] private bool _hasObjectives;

    public void RefreshQuest()
    {
        if (Quest is null) return;
        var book = _owner.Quests;
        var status = book.StatusOf(Quest.Id);
        StatusText = Palette.StatusText(status);
        StatusBrush = Palette.Brush(Palette.Status(status));
        QuestButtonText = status == QuestStatus.Done ? Loc.T("UncheckQuest") : Loc.T("CompleteWholeQuest");
        TraderText = Quest.Trader;
        var states = book.ObjectiveStates(Quest.Id);
        var here = ObjectiveMatcher.BestMatch(Quest.Objectives, Marker.Name);
        Objectives.Clear();
        for (var i = 0; i < Quest.Objectives.Count; i++)
            Objectives.Add(new ObjectiveViewModel(i, Quest.Objectives[i], states[i], SetObjective, i == here));
        HasObjectives = Objectives.Count > 0;
    }

    private void SetObjective(int index, bool done)
    {
        if (Quest is null) return;
        var change = _owner.Quests.SetObjective(Quest.Id, index, done);
        if (change is not null) _main.ReportQuestChange(change);
        _main.OnQuestProgressChanged();
        RefreshQuest();
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        _owner.ToggleFavorite(Marker.Id);
        IsFavorite = _owner.IsFavorite(Marker.Id);
    }

    [RelayCommand]
    private void ToggleQuest()
    {
        if (Quest is null) return;
        _main.ReportQuestChange(_owner.Quests.Toggle(Quest.Id));
        _main.OnQuestProgressChanged();
        RefreshQuest();
    }

    [RelayCommand]
    private void ShowInTree()
    {
        if (Quest is not null) _main.ShowQuestInTree(Quest.Id);
    }

    [RelayCommand]
    private void HideType() => _owner.HideType(Marker.Key);

    [RelayCommand]
    private void Close() => _owner.CloseCards();
}

public sealed partial class PinCardViewModel(MapPin pin, MapsViewModel owner) : ObservableObject
{
    public MapPin Pin { get; } = pin;
    public string Note { get; } = string.IsNullOrWhiteSpace(pin.Note) ? Loc.T("NoNote") : pin.Note;

    [RelayCommand] private Task Edit() => owner.EditPinAsync(Pin);
    [RelayCommand] private void Delete() => owner.DeletePin(Pin);
    [RelayCommand] private void Close() => owner.CloseCards();
}
