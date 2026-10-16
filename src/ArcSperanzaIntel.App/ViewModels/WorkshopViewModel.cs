using System.Collections.ObjectModel;
using ArcSperanzaIntel.App.Services;
using ArcSperanzaIntel.Core.Localization;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Quests;
using ArcSperanzaIntel.Core.Workshop;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>
/// Zakładka Warsztat: stacje z poziomami gracza, wymagania na następny poziom (trafiają do Szukanych),
/// co się na stacji craftuje (z wymaganym poziomem) i schematy do odhaczania.
/// </summary>
public sealed partial class WorkshopViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public WorkshopViewModel(MainViewModel main)
    {
        _main = main;
        Stations = Book.Stations.Select(s => new StationRowViewModel(s, this)).ToList();
        Book.Changed += (_, _) => Refresh();
        main.Services.Wanted.Changed += (_, _) => Detail?.RefreshTracking();
        if (Stations.Count > 0) Select(Stations[0].Station.Id);
        UpdateCounter();
    }

    internal WorkshopBook Book => _main.Services.Workshop;
    internal WantedItemsService Wanted => _main.Services.Wanted;
    public WantedItemsViewModel? WantedPanel => _main.Wanted;

    public IReadOnlyList<StationRowViewModel> Stations { get; }
    public bool HasStations => Stations.Count > 0;
    public bool NoStations => Stations.Count == 0;

    [ObservableProperty] private StationDetailViewModel? _detail;
    [ObservableProperty] private bool _onlyMissingBlueprints;
    [ObservableProperty] private string _blueprintCounter = "";

    partial void OnOnlyMissingBlueprintsChanged(bool value) => Detail?.RebuildBlueprints();

    public void Select(string stationId)
    {
        var row = Stations.FirstOrDefault(r => r.Station.Id == stationId);
        if (row is null) return;
        foreach (var r in Stations) r.IsSelected = ReferenceEquals(r, row);
        Detail = new StationDetailViewModel(row.Station, this);
    }

    private void Refresh()
    {
        foreach (var r in Stations) r.Update();
        Detail?.Rebuild();
        UpdateCounter();
    }

    private void UpdateCounter() =>
        BlueprintCounter = Loc.F("BlueprintCounter", Book.FoundCount, Book.Blueprints.Count);

    internal void OpenItem(string itemId) => _main.ShowItem(itemId);

    internal static string StationName(Station s) => s.IsBasic ? Loc.T("BasicBench") : s.Name;
}

public sealed partial class StationRowViewModel : ObservableObject
{
    private readonly WorkshopViewModel _owner;

    public StationRowViewModel(Station station, WorkshopViewModel owner)
    {
        Station = station;
        _owner = owner;
        Update();
    }

    public Station Station { get; }
    public string Name => WorkshopViewModel.StationName(Station);
    public Geometry Icon => Icons.StationIcons.For(Station.Id);
    public IBrush IconBrush => Palette.Brush(Icons.StationIcons.ColorOf(Station.Id));
    public IBrush IconBack => Palette.Brush(Color.FromArgb(0x26, Icons.StationIcons.ColorOf(Station.Id).R,
        Icons.StationIcons.ColorOf(Station.Id).G, Icons.StationIcons.ColorOf(Station.Id).B));

    [ObservableProperty] private string _levelText = "";
    [ObservableProperty] private bool _isMaxed;
    [ObservableProperty] private bool _isNotMaxed;
    [ObservableProperty] private bool _isSelected;

    internal void Update()
    {
        var book = _owner.Book;
        IsMaxed = book.IsMaxed(Station);
        IsNotMaxed = !IsMaxed;
        LevelText = Station.IsBasic ? "—" : $"{book.LevelOf(Station)}/{Station.MaxLevel}";
    }

    [RelayCommand]
    private void Select() => _owner.Select(Station.Id);
}

public sealed partial class StationDetailViewModel : ObservableObject
{
    private readonly WorkshopViewModel _owner;

    public StationDetailViewModel(Station station, WorkshopViewModel owner)
    {
        Station = station;
        _owner = owner;
        Rebuild();
    }

    public Station Station { get; }
    public string Name => WorkshopViewModel.StationName(Station).ToUpperInvariant();
    public Geometry Icon => Icons.StationIcons.For(Station.Id);
    public IBrush IconBrush => Palette.Brush(Icons.StationIcons.ColorOf(Station.Id));
    public bool HasLevels => !Station.IsBasic;
    public bool IsBasic => Station.IsBasic;

    private string GroupKey => WantedItemsService.StationKeyPrefix + Station.Id;

    public ObservableCollection<LevelPipViewModel> Pips { get; } = [];
    public ObservableCollection<ItemChipViewModel> NextItems { get; } = [];
    public ObservableCollection<string> NextConditions { get; } = [];
    public ObservableCollection<CraftLevelGroupViewModel> Crafts { get; } = [];
    public ObservableCollection<BlueprintRowViewModel> Blueprints { get; } = [];
    public ObservableCollection<UpgradeChainViewModel> Upgrades { get; } = [];

    [ObservableProperty] private string _levelText = "";
    [ObservableProperty] private string _nextTitle = "";
    [ObservableProperty] private bool _hasNext;
    [ObservableProperty] private bool _isMaxed;
    [ObservableProperty] private bool _canDowngrade;
    [ObservableProperty] private bool _hasCrafts;
    [ObservableProperty] private int _hiddenByBlueprint;
    [ObservableProperty] private string _hiddenText = "";
    [ObservableProperty] private bool _hasHidden;
    [ObservableProperty] private bool _noCrafts;
    [ObservableProperty] private bool _hasBlueprints;
    [ObservableProperty] private bool _hasUpgrades;
    [ObservableProperty] private string _stationBlueprintCounter = "";
    [ObservableProperty] private bool _isTracked;
    [ObservableProperty] private string _trackText = "";

    internal void Rebuild()
    {
        var book = _owner.Book;
        var level = book.LevelOf(Station);

        Pips.Clear();
        for (var i = 1; i <= Station.MaxLevel; i++) Pips.Add(new LevelPipViewModel(i <= level));
        LevelText = Station.IsBasic ? Loc.T("BasicBenchInfo") : Loc.F("StationLevelOf", level, Station.MaxLevel);
        IsMaxed = book.IsMaxed(Station);
        CanDowngrade = !Station.IsBasic && level > Station.MinLevel;

        NextItems.Clear();
        NextConditions.Clear();
        var next = book.NextLevel(Station);
        HasNext = next is not null;
        if (next is not null)
        {
            NextTitle = Loc.F("UpgradeTo", next.Level);
            foreach (var r in next.RequiredItems)
            {
                var item = ItemKinds.Lookup(r.ItemId);
                NextItems.Add(new ItemChipViewModel(r.ItemId, item?.Name ?? r.Name, item?.IconUrl, item?.Rarity, r.Quantity, _owner.OpenItem));
            }
            foreach (var c in next.Conditions)
                NextConditions.Add(c.Id == "rounds-played" ? Loc.F("CondRounds", c.Quantity) : $"{Core.Util.TextFormat.Pretty(c.Id)}: {c.Quantity}");
        }

        Crafts.Clear();
        // przedmiot ze schematem pojawia się dopiero po oznaczeniu schematu jako znalezionego
        var all = book.CraftsAt(Station.Id);
        var craftable = all.Where(c => !book.IsLockedByBlueprint(c)).ToList();
        HiddenByBlueprint = all.Count - craftable.Count;
        HiddenText = Loc.F("HiddenByBlueprint", HiddenByBlueprint);
        HasHidden = HiddenByBlueprint > 0;
        foreach (var g in craftable.GroupBy(c => c.Level))
        {
            var unlocked = Station.IsBasic || g.Key is null || g.Key <= level;
            var title = g.Key is null ? Loc.T("CraftAnyLevel") : Loc.F("CraftLevel", g.Key);
            Crafts.Add(new CraftLevelGroupViewModel(title, unlocked, g.Select(c => new CraftItemViewModel(c, book, _owner)).ToList()));
        }
        HasCrafts = Crafts.Count > 0;
        NoCrafts = !HasCrafts && HiddenByBlueprint == 0;

        // ulepszenia broni (I → II → III → IV) dla tego, co da się tu craftować
        Upgrades.Clear();
        foreach (var c in craftable)
        {
            var chain = book.UpgradeChain(c.Item.Id);
            if (chain.Count > 0) Upgrades.Add(new UpgradeChainViewModel(c.Item, chain, _owner));
        }
        HasUpgrades = Upgrades.Count > 0;

        RebuildBlueprints();
        RefreshTracking();
    }

    internal void RebuildBlueprints()
    {
        var book = _owner.Book;
        var all = book.CraftsAt(Station.Id)
            .SelectMany(c => c.BlueprintIds.Count > 0
                ? c.BlueprintIds.Select(id => ItemKinds.Lookup(id))
                : new[] { book.BlueprintFor(c.Item.Id) })
            .Where(b => b is not null && WorkshopBook.IsBlueprint(b))
            .Select(b => b!)
            .DistinctBy(b => b.Id)
            .OrderBy(b => b.Name)
            .ToList();
        Blueprints.Clear();
        foreach (var bp in all.Where(b => !_owner.OnlyMissingBlueprints || !book.IsFound(b.Id)))
            Blueprints.Add(new BlueprintRowViewModel(bp, book, _owner));
        HasBlueprints = all.Count > 0;
        StationBlueprintCounter = Loc.F("BlueprintCounter", all.Count(b => book.IsFound(b.Id)), all.Count);
    }

    internal void RefreshTracking()
    {
        IsTracked = _owner.Wanted.TrackStations && _owner.Wanted.IsTracked(GroupKey);
        TrackText = IsTracked ? Loc.T("StationTracked") : Loc.T("StationNotTracked");
    }

    [RelayCommand]
    private void Upgrade() => _owner.Book.SetLevel(Station, _owner.Book.LevelOf(Station) + 1);

    [RelayCommand]
    private void Downgrade() => _owner.Book.SetLevel(Station, _owner.Book.LevelOf(Station) - 1);

    [RelayCommand]
    private void ToggleTracked()
    {
        if (!_owner.Wanted.TrackStations)
        {
            _owner.Wanted.TrackStations = true;
            _owner.Wanted.SetTracked(GroupKey, true);
        }
        else _owner.Wanted.SetTracked(GroupKey, !_owner.Wanted.IsTracked(GroupKey));
        RefreshTracking();
    }
}

public sealed class LevelPipViewModel(bool filled)
{
    public bool Filled { get; } = filled;
    public bool Empty => !Filled;
}

/// <summary>Przedmioty odblokowane na danym poziomie stacji.</summary>
public sealed class CraftLevelGroupViewModel(string title, bool unlocked, IReadOnlyList<CraftItemViewModel> items)
{
    public string Title { get; } = title;
    public bool IsUnlocked { get; } = unlocked;
    public bool IsLocked => !unlocked;
    public double Opacity => unlocked ? 1 : 0.5;
    public IReadOnlyList<CraftItemViewModel> Items { get; } = items;
}

public sealed partial class CraftItemViewModel : ObservableObject
{
    private readonly Item _item;
    private readonly WorkshopViewModel _owner;

    public CraftItemViewModel(StationCraft craft, WorkshopBook book, WorkshopViewModel owner)
    {
        _item = craft.Item;
        _owner = owner;
        var bp = craft.BlueprintIds.Select(id => ItemKinds.Lookup(id)).FirstOrDefault(b => b is not null) ?? book.BlueprintFor(_item.Id);
        HasBlueprint = bp is not null;
        BlueprintFound = bp is not null && book.IsFound(bp.Id);
        BlueprintTip = bp is null ? "" : BlueprintFound ? Loc.T("BlueprintHave") : Loc.T("BlueprintMissing");
        var ingredients = craft.Ingredients.Count > 0 ? craft.Ingredients : _item.Components;
        var parts = ingredients.Select(c => $"{c.Quantity}× {ItemKinds.Lookup(c.ItemId)?.Name ?? c.Name}").ToList();
        Tooltip = parts.Count == 0 ? _item.Name : _item.Name + "\n" + Loc.T("CraftNeeds") + ": " + string.Join(", ", parts);
    }

    public string Tooltip { get; }

    public string Name => _item.Name;
    public string? IconUrl => _item.IconUrl;
    public bool IsBlueprint => WorkshopBook.IsBlueprint(_item);
    public bool IsDesign => ItemKinds.IsDesignType(_item.Type);
    public IBrush RarityBrush => Palette.Brush(Palette.Rarity(_item.Rarity));
    public bool HasBlueprint { get; }
    public bool BlueprintFound { get; }
    public bool BlueprintMissing => HasBlueprint && !BlueprintFound;
    public string BlueprintTip { get; }

    [RelayCommand]
    private void Open() => _owner.OpenItem(_item.Id);
}

/// <summary>Łańcuch ulepszeń broni: Bettina I → II → III → IV, z kosztem każdego kroku.</summary>
public sealed class UpgradeChainViewModel(Item start, IReadOnlyList<UpgradeStep> steps, WorkshopViewModel owner)
{
    public string Name { get; } = start.Name;
    public string? IconUrl { get; } = start.IconUrl;
    public IBrush RarityBrush { get; } = Palette.Brush(Palette.Rarity(start.Rarity));
    public IReadOnlyList<UpgradeStepViewModel> Steps { get; } = steps.Select(s => new UpgradeStepViewModel(s, owner)).ToList();
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenCommand { get; } = new RelayCommand(() => owner.OpenItem(start.Id));
}

public sealed class UpgradeStepViewModel(UpgradeStep step, WorkshopViewModel owner)
{
    public string Name { get; } = step.To.Name;
    public string? IconUrl { get; } = step.To.IconUrl;
    public IBrush RarityBrush { get; } = Palette.Brush(Palette.Rarity(step.To.Rarity));
    public string CostText { get; } = string.Join(", ", step.Cost.Select(c => $"{c.Quantity}× {ItemKinds.Lookup(c.ItemId)?.Name ?? c.Name}"));
    public string Tooltip => Name + "\n" + Loc.T("UpgradeCost") + ": " + CostText;
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenCommand { get; } = new RelayCommand(() => owner.OpenItem(step.To.Id));
}

/// <summary>Schemat do odhaczenia jako znaleziony.</summary>
public sealed partial class BlueprintRowViewModel : ObservableObject
{
    private readonly Item _blueprint;
    private readonly WorkshopBook _book;
    private readonly WorkshopViewModel _owner;

    public BlueprintRowViewModel(Item blueprint, WorkshopBook book, WorkshopViewModel owner)
    {
        _blueprint = blueprint;
        _book = book;
        _owner = owner;
        _isFound = book.IsFound(blueprint.Id);
    }

    public string Name => _blueprint.Name;
    public string? IconUrl => _blueprint.IconUrl;
    public IBrush RarityBrush => Palette.Brush(Palette.Rarity(_blueprint.Rarity));

    [ObservableProperty] private bool _isFound;

    partial void OnIsFoundChanged(bool value) => _book.SetFound(_blueprint.Id, value);

    [RelayCommand]
    private void Open() => _owner.OpenItem(_blueprint.Id);
}
