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
        Rebuild();
    }

    private UserState State => _main.Services.State;
    public WantedItemsViewModel? Wanted => _main.Wanted;

    public IReadOnlyList<CategoryFilterViewModel> Categories { get; }
    public IReadOnlyList<RarityFilterViewModel> Rarities { get; }
    public ObservableCollection<ItemSectionViewModel> Sections { get; } = [];

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
            (term.Length == 0 || TextFormat.ContainsLoose(c.Item.Name, term) || TextFormat.ContainsLoose(c.Item.Subcategory, term)));

        Sections.Clear();
        var total = 0;
        foreach (var g in visible.GroupBy(c => c.Item.Type).OrderBy(g => g.Key))
        {
            var cards = g.OrderBy(c => Palette.RarityRank(c.Item.Rarity)).ThenBy(c => c.Item.Name).ToList();
            total += cards.Count;
            Sections.Add(new ItemSectionViewModel(g.Key.ToUpperInvariant(), cards));
        }
        CountText = Loc.F("ItemCount", total);
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
        var visible = Sections.Any(s => s.Items.Contains(card));
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
    }

    public bool IsBlueprint => Services.ItemKinds.IsBlueprintType(Item.Type);

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

/// <summary>Panel szczegółów itemu: opis, statystyki, gdzie potrzebny, kto sprzedaje, ulubione, dodaj do szukanych.</summary>
public sealed partial class ItemDetailViewModel : ObservableObject
{
    private readonly ItemsViewModel _owner;
    private readonly MainViewModel _main;

    public bool IsBlueprint => Services.ItemKinds.IsBlueprintType(Item.Type);

    public ItemDetailViewModel(Item item, ItemsViewModel owner, MainViewModel main)
    {
        Item = item;
        _owner = owner;
        _main = main;
        RarityBrush = Palette.Brush(Palette.Rarity(item.Rarity));
        _isFavorite = owner.IsFavorite(item.Id);
        _isMarked = main.Services.Wanted.IsMarked(item.Id);

        var book = main.Services.Quests;
        UsedIn = main.Services.Wanted.QuestsNeeding(item.Id)
            .Select(q => new QuestLinkViewModel(q.Id, $"{q.Name}  (×{q.RequiredItems.First(r => r.ItemId == item.Id).Quantity})",
                book.IsDone(q.Id), main.ShowQuestInTree))
            .ToList();
        SoldBy = main.Services.Data.Traders
            .SelectMany(t => t.Offers.Where(o => o.ItemId == item.Id).Select(o => $"{t.Name} — {TextFormat.Number(o.Price)} {(t.Currency == TraderCurrency.Seeds ? Loc.T("SeedsWord") : Loc.T("CoinsWord"))}"))
            .ToList();
    }

    public Item Item { get; }
    public string Name => Item.Name;
    public string? IconUrl => Item.IconUrl;
    public string Rarity => Item.Rarity ?? "—";
    public IBrush RarityBrush { get; }
    public string TypeLine => Item.Subcategory is null ? Item.Type : $"{Item.Type} · {Item.Subcategory}";
    public string? Description => Item.Description;
    public bool HasDescription => Item.Description is not null;
    public string? FlavorText => Item.FlavorText;
    public bool HasFlavor => Item.FlavorText is not null;
    public string ValueText => Item.Value is { } v ? TextFormat.Number(v) : "—";
    public string? Workbench => Item.Workbench;
    public bool HasWorkbench => Item.Workbench is not null;
    public IReadOnlyList<ItemStat> Stats => Item.Stats;
    public bool HasStats => Item.Stats.Count > 0;
    public IReadOnlyList<QuestLinkViewModel> UsedIn { get; }
    public bool HasUsedIn => UsedIn.Count > 0;
    public IReadOnlyList<string> SoldBy { get; }
    public bool HasSoldBy => SoldBy.Count > 0;

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
