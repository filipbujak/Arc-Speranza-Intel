using ArcSperanzaIntel.Core.Localization;
using System.Collections.ObjectModel;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Util;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>Zakładka Itemy: wiki wszystkich przedmiotów w sekcjach po kategoriach, filtry po lewej, wyszukiwarka.</summary>
public sealed partial class ItemsViewModel : ObservableObject
{
    /// <summary>Wewnętrzny znacznik "brak rzadkości" (wyświetlany jako Brak / None).</summary>
    public const string NoRarity = "~none";

    private readonly MainViewModel _main;
    private readonly Dictionary<string, ItemCardViewModel> _cards = [];

    public ItemsViewModel(MainViewModel main)
    {
        _main = main;
        var items = main.Services.Data.Items;
        foreach (var i in items) _cards[i.Id] = new ItemCardViewModel(i, this);

        Categories = items.GroupBy(i => i.Type).OrderBy(g => g.Key)
            .Select(g => new CategoryFilterViewModel(g.Key, g.Count(), !State.HiddenItemTypes.Contains(g.Key), this))
            .ToList();
        Rarities = items.Select(i => i.Rarity ?? NoRarity).Distinct()
            .OrderBy(r => Palette.RarityRank(r == NoRarity ? null : r))
            .Select(r => new RarityFilterViewModel(r, Palette.Rarity(r == NoRarity ? null : r), this))
            .ToList();
        main.Services.Workshop.Changed += (_, _) => OnWorkshopChanged();
        UpdateBlueprintCounter();
        Rebuild();
    }

    internal Core.Workshop.WorkshopBook Workshop => _main.Services.Workshop;

    // ---------------------------------------------------------------- schematy
    [ObservableProperty] private bool _onlyMissingBlueprints;
    [ObservableProperty] private string _blueprintCounter = "";

    partial void OnOnlyMissingBlueprintsChanged(bool value) => Rebuild();

    private void UpdateBlueprintCounter() =>
        BlueprintCounter = Loc.F("BlueprintCounter", Workshop.FoundCount, Workshop.Blueprints.Count);

    private void OnWorkshopChanged()
    {
        foreach (var c in _cards.Values.Where(c => c.IsBlueprint)) c.RefreshFound();
        UpdateBlueprintCounter();
        Selected?.RefreshWorkshop();
        if (OnlyMissingBlueprints) Rebuild();
    }

    private UserState State => _main.Services.State;
    public WantedItemsViewModel? Wanted => _main.Wanted;

    public IReadOnlyList<CategoryFilterViewModel> Categories { get; }
    public IReadOnlyList<RarityFilterViewModel> Rarities { get; }
    /// <summary>Sekcje (kategorie) po filtrach — źródło dla <see cref="Rows"/>.</summary>
    private readonly List<ItemSectionViewModel> _sections = [];

    /// <summary>
    /// Płaska lista wierszy do wirtualizacji: nagłówek sekcji, potem wiersze po <see cref="Columns"/> kart.
    /// Widok tworzy kontrolki tylko dla wierszy na ekranie (zamiast 1000+ kart naraz).
    /// </summary>
    public ObservableCollection<object> Rows { get; } = [];

    private int _columns = 6;

    /// <summary>Ile kart mieści się w wierszu — ustawia widok przy zmianie szerokości.</summary>
    public int Columns
    {
        get => _columns;
        set
        {
            value = Math.Max(1, value);
            if (value == _columns) return;
            _columns = value;
            BuildRows();
        }
    }

    private void BuildRows()
    {
        var rows = new List<object>();
        foreach (var sec in _sections)
        {
            rows.Add(sec);
            for (var i = 0; i < sec.Items.Count; i += _columns)
                rows.Add(new ItemRowViewModel(sec.Items.Skip(i).Take(_columns).ToList()));
        }
        Rows.Clear();
        foreach (var r in rows) Rows.Add(r);
    }

    /// <summary>Indeks wiersza z daną kartą (do przewinięcia) albo -1.</summary>
    public int RowIndexOf(ItemCardViewModel card)
    {
        for (var i = 0; i < Rows.Count; i++)
            if (Rows[i] is ItemRowViewModel r && r.Items.Contains(card)) return i;
        return -1;
    }

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _onlyFavorites;
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private ItemDetailViewModel? _selected;

    /// <summary>Prośba do widoku: przewiń do karty itemu.</summary>
    public event Action<ItemCardViewModel>? ScrollToRequested;

    partial void OnSearchTextChanged(string value) => Rebuild();
    partial void OnOnlyFavoritesChanged(bool value) => Rebuild();

    internal void CategoryChanged(CategoryFilterViewModel c)
    {
        if (c.IsOn) State.HiddenItemTypes.Remove(c.Name); else State.HiddenItemTypes.Add(c.Name);
        _main.Services.RequestSave();
        Rebuild();
    }

    internal void FilterChanged() => Rebuild();

    [RelayCommand]
    private void AllCategories() => SetCategories(true);

    [RelayCommand]
    private void NoCategories() => SetCategories(false);

    // ---------------------------------------------------------------- wyszukiwarka kategorii
    [ObservableProperty] private string _categorySearch = "";

    partial void OnCategorySearchChanged(string value)
    {
        var term = value.Trim();
        foreach (var c in Categories) c.IsShown = term.Length == 0 || TextFormat.ContainsLoose(c.Name, term);
    }

    /// <summary>"Wszystkie" / "Żadne" działają na kategoriach widocznych po wyszukaniu.</summary>
    private void SetCategories(bool on)
    {
        foreach (var c in Categories.Where(c => c.IsShown))
        {
            c.SetSilently(on);
            if (on) State.HiddenItemTypes.Remove(c.Name);
            else State.HiddenItemTypes.Add(c.Name);
        }
        _main.Services.RequestSave();
        Rebuild();
    }

    private void Rebuild()
    {
        var term = SearchText.Trim();
        var shownTypes = Categories.Where(c => c.IsOn).Select(c => c.Name).ToHashSet();
        var activeRarities = Rarities.Where(r => r.IsOn).Select(r => r.Key).ToHashSet();
        var favs = State.FavoriteItems;

        var visible = _cards.Values.Where(c =>
            (term.Length > 0 || shownTypes.Contains(c.Item.Type)) && // wyszukiwanie przeszukuje wszystkie kategorie
            (activeRarities.Count == 0 || activeRarities.Contains(c.Item.Rarity ?? NoRarity)) &&
            (!OnlyFavorites || favs.Contains(c.Item.Id)) &&
            (!OnlyMissingBlueprints || (c.IsBlueprint && !Workshop.IsFound(c.Item.Id))) &&
            (term.Length == 0 || TextFormat.ContainsLoose(c.Item.Name, term) || TextFormat.ContainsLoose(c.Item.Subcategory, term)));

        _sections.Clear();
        var total = 0;
        // schematy na końcu listy
        foreach (var g in visible.GroupBy(c => c.Item.Type)
                     .OrderBy(g => Services.ItemKinds.IsBlueprintType(g.Key) ? 1 : 0).ThenBy(g => g.Key))
        {
            var cards = g.OrderBy(c => Palette.RarityRank(c.Item.Rarity)).ThenBy(c => c.Item.Name).ToList();
            total += cards.Count;
            _sections.Add(new ItemSectionViewModel(g.Key.ToUpperInvariant(), cards));
        }
        CountText = Loc.F("ItemCount", total);
        BuildRows();
    }

    public void Select(ItemCardViewModel card)
    {
        foreach (var c in _cards.Values) c.IsSelected = false;
        card.IsSelected = true;
        Selected = new ItemDetailViewModel(card.Item, this, _main);
    }

    /// <summary>Otwiera item (np. z panelu Szukane itemy) i przewija do niego.</summary>
    public void Focus(string itemId)
    {
        if (!_cards.TryGetValue(itemId, out var card)) return;
        var visible = _sections.Any(s => s.Items.Contains(card));
        if (!visible)
        {
            SearchText = card.Item.Name;
        }
        Select(card);
        ScrollToRequested?.Invoke(card);
    }

    public void RefreshSelected()
    {
        if (Selected is not null) Selected = new ItemDetailViewModel(Selected.Item, this, _main);
    }

    internal void CloseDetail()
    {
        foreach (var c in _cards.Values) c.IsSelected = false;
        Selected = null;
    }

    internal bool IsFavorite(string id) => State.FavoriteItems.Contains(id);

    internal void ToggleFavorite(string id)
    {
        if (!State.FavoriteItems.Remove(id)) State.FavoriteItems.Add(id);
        _main.Services.RequestSave();
        if (_cards.TryGetValue(id, out var card)) card.IsFavorite = IsFavorite(id);
        if (OnlyFavorites) Rebuild();
    }
}

/// <summary>Jeden wiersz kart w siatce itemów.</summary>
public sealed class ItemRowViewModel(IReadOnlyList<ItemCardViewModel> items)
{
    public IReadOnlyList<ItemCardViewModel> Items { get; } = items;
}

public sealed class ItemSectionViewModel(string title, IReadOnlyList<ItemCardViewModel> items)
{
    public string Title { get; } = title;
    public IReadOnlyList<ItemCardViewModel> Items { get; } = items;
    public string CountText { get; } = items.Count.ToString();
}

public sealed partial class ItemCardViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;

    public ItemCardViewModel(Item item, ItemsViewModel owner)
    {
        Item = item;
        _owner = owner;
        RarityBrush = Palette.Brush(Palette.Rarity(item.Rarity));
        _isFavorite = owner.IsFavorite(item.Id);
        if (IsBlueprint) _isFound = owner.Workshop.IsFound(item.Id);
    }

    public bool IsBlueprint => Services.ItemKinds.IsBlueprintType(Item.Type);
    public bool IsDesign => Services.ItemKinds.IsDesignType(Item.Type);

    /// <summary>Schemat oznaczony jako znaleziony (znaczek na karcie).</summary>
    [ObservableProperty] private bool _isFound;

    internal void RefreshFound() => IsFound = _owner.Workshop.IsFound(Item.Id);

    public Item Item { get; }
    public string Name => Item.Name;
    public string? IconUrl => Item.IconUrl;
    public IBrush RarityBrush { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isFavorite;

    [RelayCommand]
    private void Select() => _owner.Select(this);
}

public sealed partial class CategoryFilterViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;
    private bool _silent;

    public CategoryFilterViewModel(string name, int count, bool on, ItemsViewModel owner)
    {
        Name = name;
        Count = count;
        _isOn = on;
        _owner = owner;
    }

    public string Name { get; }
    public int Count { get; }

    [ObservableProperty] private bool _isOn;

    /// <summary>Pasuje do wyszukiwarki kategorii.</summary>
    [ObservableProperty] private bool _isShown = true;

    partial void OnIsOnChanged(bool value)
    {
        if (!_silent) _owner.CategoryChanged(this);
    }

    internal void SetSilently(bool on)
    {
        _silent = true;
        IsOn = on;
        _silent = false;
    }
}

public sealed partial class RarityFilterViewModel(string name, Color color, ItemsViewModel owner) : ObservableObject
{
    public string Key { get; } = name;
    public string Name { get; } = name == ItemsViewModel.NoRarity ? Loc.T("None") : name;
    public IBrush ColorBrush { get; } = Palette.Brush(color);

    [ObservableProperty] private bool _isOn;

    partial void OnIsOnChanged(bool value) => owner.FilterChanged();
}

/// <summary>"Gunsmith → poziom 3 ×5" — klik otwiera stację w Warsztacie.</summary>
public sealed class StationNeedViewModel(Core.Workshop.StationNeed need, MainViewModel main)
{
    public string Text { get; } = Loc.F("StationNeed", need.Station.Name, need.Level, need.Quantity);
    public IRelayCommand OpenCommand { get; } = new RelayCommand(() => main.ShowStation(need.Station.Id));
}

/// <summary>Panel szczegółów itemu: opis, statystyki, gdzie potrzebny, kto sprzedaje, ulubione, dodaj do szukanych.</summary>
public sealed partial class ItemDetailViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;
    private readonly MainViewModel _main;

    public bool IsBlueprint => Services.ItemKinds.IsBlueprintType(Item.Type);
    public bool IsDesign => Services.ItemKinds.IsDesignType(Item.Type);

    public ItemDetailViewModel(Item item, ItemsViewModel owner, MainViewModel main)
    {
        Item = item;
        _owner = owner;
        _main = main;
        RarityBrush = Palette.Brush(Palette.Rarity(item.Rarity));
        RarityGlow = Palette.Glow(Palette.Rarity(item.Rarity));
        _isFavorite = owner.IsFavorite(item.Id);
        _isMarked = main.Services.Wanted.IsMarked(item.Id);

        var book = main.Services.Quests;
        UsedIn = main.Services.Wanted.QuestsNeeding(item.Id)
            .Select(q => new QuestLinkViewModel(q.Id, $"{q.Name}  (×{q.RequiredItems.First(r => r.ItemId == item.Id).Quantity})",
                book.IsDone(q.Id), main.ShowQuestInTree))
            .ToList();
        BuildWorkshopInfo();
        var ingredients = item.Recipes.FirstOrDefault(r => r.Ingredients.Count > 0)?.Ingredients ?? item.Components;
        CraftComponents = ingredients.Select(c => Chip(c, main)).ToList();
        RecycleComponents = item.RecycleComponents.Select(c => Chip(c, main)).ToList();
        SalvageComponents = item.SalvageComponents.Select(c => Chip(c, main)).ToList();
        RecycleFrom = item.RecycleFrom.Select(c => Chip(c, main)).ToList();
        var ws = main.Services.Workshop;
        var upgradesTo = new List<ItemChipViewModel>();
        if (ws.UpgradeOf(item.Id) is { } up)
        {
            upgradesTo.Add(Chip(new ItemStack(up.To.Id, up.To.Name, up.To.IconUrl, up.To.Rarity, 1), main));
            UpgradeCost = up.Cost.Select(c => Chip(c, main)).ToList();
        }
        UpgradesTo = upgradesTo;
        var upgradedFrom = new List<ItemChipViewModel>();
        if (ws.UpgradedFrom(item.Id) is { } from)
            upgradedFrom.Add(Chip(new ItemStack(from.From.Id, from.From.Name, from.From.IconUrl, from.From.Rarity, 1), main));
        UpgradedFrom = upgradedFrom;
        SoldBy = main.Services.Data.Traders
            .SelectMany(t => t.Offers.Where(o => o.ItemId == item.Id).Select(o => $"{t.Name} — {TextFormat.Number(o.Price)} {(t.Currency == TraderCurrency.Seeds ? Loc.T("SeedsWord") : Loc.T("CoinsWord"))}"))
            .ToList();
    }

    public Item Item { get; }
    public string Name => Item.Name;
    public string? IconUrl => Item.IconUrl;
    public string Rarity => Item.Rarity ?? "—";
    public IBrush RarityBrush { get; }

    /// <summary>Poświata w kolorze rzadkości za górą panelu szczegółów.</summary>
    public IBrush RarityGlow { get; }
    public string TypeLine => Item.Subcategory is null ? Item.Type : $"{Item.Type} · {Item.Subcategory}";
    public string? Description => Item.Description;
    public bool HasDescription => Item.Description is not null;
    public string? FlavorText => Item.FlavorText;
    public bool HasFlavor => Item.FlavorText is not null;
    public string ValueText => Item.Value is { } v ? TextFormat.Number(v) : "—";
    public string? Workbench => Item.Workbench;
    /// <summary>Surowy tekst "workbench" z MetaForge pokazujemy tylko, gdy nie wiemy lepiej (CraftedAt).</summary>
    public bool HasWorkbench => Item.Workbench is not null && _main.Services.Workshop.CraftedAt(Item.Id) is null
        && _main.Services.Workshop.UpgradedFrom(Item.Id) is null;
    public IReadOnlyList<ItemStat> Stats => Item.Stats;
    public bool HasStats => Item.Stats.Count > 0;
    public IReadOnlyList<QuestLinkViewModel> UsedIn { get; }
    public bool HasUsedIn => UsedIn.Count > 0;
    public IReadOnlyList<string> SoldBy { get; }
    public bool HasSoldBy => SoldBy.Count > 0;

    // ---------------------------------------------------------------- craft / recykling
    public IReadOnlyList<ItemChipViewModel> CraftComponents { get; }
    public bool HasCraftComponents => CraftComponents.Count > 0;
    public IReadOnlyList<ItemChipViewModel> RecycleComponents { get; }
    public bool HasRecycleComponents => RecycleComponents.Count > 0;
    public IReadOnlyList<ItemChipViewModel> SalvageComponents { get; }
    public bool HasSalvageComponents => SalvageComponents.Count > 0;
    public IReadOnlyList<ItemChipViewModel> RecycleFrom { get; }
    public bool HasRecycleFrom => RecycleFrom.Count > 0;
    /// <summary>Następny tier broni.</summary>
    public IReadOnlyList<ItemChipViewModel> UpgradesTo { get; }
    public IReadOnlyList<ItemChipViewModel> UpgradeCost { get; } = [];
    public bool HasUpgradeCost => UpgradeCost.Count > 0;
    public bool HasUpgradesTo => UpgradesTo.Count > 0;
    public IReadOnlyList<ItemChipViewModel> UpgradedFrom { get; }
    public bool HasUpgradedFrom => UpgradedFrom.Count > 0;

    private static ItemChipViewModel Chip(ItemStack s, MainViewModel main)
    {
        var item = main.Services.Data.FindItem(s.ItemId);
        return new ItemChipViewModel(s.ItemId, item?.Name ?? s.Name, s.IconUrl ?? item?.IconUrl, s.Rarity ?? item?.Rarity, s.Quantity, main.ShowItem);
    }

    // ---------------------------------------------------------------- warsztat
    public ObservableCollection<StationNeedViewModel> NeededInWorkshop { get; } = [];
    [ObservableProperty] private bool _hasWorkshopNeeds;
    [ObservableProperty] private string? _craftedAtText;
    [ObservableProperty] private bool _hasCraftedAt;
    [ObservableProperty] private string? _blueprintStatusText;
    [ObservableProperty] private bool _hasBlueprintStatus;

    /// <summary>Dla schematu: znaleziony / nie (zmiana od razu zapisuje).</summary>
    [ObservableProperty] private bool _isFound;
    private bool _loadingFound;

    partial void OnIsFoundChanged(bool value)
    {
        if (!_loadingFound && IsBlueprint) _main.Services.Workshop.SetFound(Item.Id, value);
    }

    internal void RefreshWorkshop() => BuildWorkshopInfo();

    private void BuildWorkshopInfo()
    {
        var ws = _main.Services.Workshop;
        NeededInWorkshop.Clear();
        foreach (var n in ws.NeededFor(Item.Id))
            NeededInWorkshop.Add(new StationNeedViewModel(n, _main));
        HasWorkshopNeeds = NeededInWorkshop.Count > 0;

        var at = ws.CraftedAt(Item.Id);
        CraftedAtText = at is { } a
            ? (a.Station.IsBasic ? Loc.T("BasicBench") : a.Station.Name) + (a.Level is { } l ? " · " + Loc.F("LevelShort", l) : "")
            : null;
        HasCraftedAt = CraftedAtText is not null;

        var bp = ws.BlueprintFor(Item.Id);
        BlueprintStatusText = bp is null ? null : ws.IsFound(bp.Id) ? Loc.T("BlueprintHave") : Loc.T("BlueprintMissing");
        HasBlueprintStatus = BlueprintStatusText is not null;

        _loadingFound = true;
        IsFound = IsBlueprint && ws.IsFound(Item.Id);
        _loadingFound = false;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteText))]
    private bool _isFavorite;

    public string FavoriteText => IsFavorite ? Loc.T("InFavorites") : Loc.T("AddFavorite");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarkText))]
    private bool _isMarked;

    public string MarkText => IsMarked ? Loc.T("InWanted") : Loc.T("AddWanted");

    [RelayCommand]
    private void ToggleFavorite()
    {
        _owner.ToggleFavorite(Item.Id);
        IsFavorite = _owner.IsFavorite(Item.Id);
    }

    [RelayCommand]
    private void Close() => _owner.CloseDetail();

    [RelayCommand]
    private void ToggleMarked()
    {
        var wanted = _main.Services.Wanted;
        if (wanted.IsMarked(Item.Id)) wanted.Unmark(Item.Id);
        else
        {
            wanted.Mark(Item.Id);
            _main.Toast(Loc.T("AddedToWanted") + Item.Name, ToastKind.Ok);
        }
        IsMarked = wanted.IsMarked(Item.Id);
    }
}
