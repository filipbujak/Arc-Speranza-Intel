using ArcSperanzaIntel.Core.Localization;
using System.Collections.ObjectModel;
using ArcSperanzaIntel.App.Services;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Util;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>
/// Zakładka Handlarze: jedna długa strona — handlarz, pod nim jego itemy z ceną, pod nim kolejny handlarz,
/// na końcu Ermal (tygodniowe oferty za monety albo za oddane przedmioty).
/// Wyszukiwarka po lewej przewija płynnie do itemu i go wyróżnia.
/// </summary>
public sealed partial class ShopsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public ShopsViewModel(MainViewModel main)
    {
        _main = main;
        var data = main.Services.Data;
        CoinsIconUrl = data.FindItem("coins")?.IconUrl ?? "https://static.metaforge.app/arc-raiders/icons/coins.webp";
        SeedsIconUrl = data.FindItem("assorted-seeds")?.IconUrl;
        Traders = data.Traders
            .Select(t => new TraderSectionViewModel(t, this))
            .ToList();
        Ermal = data.Ermal is { } e ? new ErmalSectionViewModel(e, this) : null;
        var hidden = main.Services.State.HiddenTraders;
        var chips = Traders.Select(t => new TraderToggleViewModel(t, !hidden.Contains(t.Name), this)).ToList();
        if (Ermal is not null) chips.Add(new TraderToggleViewModel(Ermal, !hidden.Contains(Ermal.Name), this));
        TraderChips = chips;
        TotalText = Loc.F("TradersTotal", Traders.Sum(t => t.Offers.Count), Traders.Count)
                    + (Ermal is null ? "" : Loc.F("PlusErmal", Ermal.Offers.Count));
    }

    public string CoinsIconUrl { get; }
    public string? SeedsIconUrl { get; }

    public IReadOnlyList<TraderSectionViewModel> Traders { get; }
    public ErmalSectionViewModel? Ermal { get; }
    public bool HasErmal => Ermal is not null;
    public IReadOnlyList<TraderToggleViewModel> TraderChips { get; }
    public string TotalText { get; }
    public bool HasTraders => Traders.Count > 0;
    public bool NoTraders => Traders.Count == 0;

    /// <summary>Prośba do widoku: przewiń do karty i ją wyróżnij.</summary>
    public event Action<object>? ScrollToRequested;

    [ObservableProperty] private string _searchText = "";
    public ObservableCollection<OfferSearchResultViewModel> Results { get; } = [];
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private bool _noResults;

    partial void OnSearchTextChanged(string value)
    {
        Results.Clear();
        var term = value.Trim();
        if (term.Length >= 2)
        {
            foreach (var t in Traders.Where(t => t.IsVisible))
            foreach (var o in t.Offers.Where(o => TextFormat.ContainsLoose(o.Name, term)))
                Results.Add(new OfferSearchResultViewModel(o, t.Name, o.PriceText, o.CurrencyIconUrl, this));
            if (Ermal is { IsVisible: true })
                foreach (var o in Ermal.Offers.Where(o => TextFormat.ContainsLoose(o.Name, term)))
                    Results.Add(new OfferSearchResultViewModel(o, Ermal.Name, o.CostText, CoinsIconUrl, this));
        }
        HasResults = Results.Count > 0;
        NoResults = term.Length >= 2 && Results.Count == 0;
    }

    [RelayCommand]
    private void GoToFirst()
    {
        if (Results.Count > 0) GoTo(Results[0].Offer);
    }

    internal void GoTo(IHighlightable offer)
    {
        foreach (var t in Traders)
        foreach (var o in t.Offers)
            o.IsHighlighted = false;
        if (Ermal is not null)
            foreach (var o in Ermal.Offers)
                o.IsHighlighted = false;
        offer.IsHighlighted = true;
        ScrollToRequested?.Invoke(offer);
    }

    internal void TraderVisibilityChanged(TraderToggleViewModel chip)
    {
        var hidden = _main.Services.State.HiddenTraders;
        if (chip.IsOn) hidden.Remove(chip.Section.Name); else hidden.Add(chip.Section.Name);
        _main.Services.RequestSave();
        OnSearchTextChanged(SearchText);
    }

    internal void OpenItem(string itemId) => _main.ShowItem(itemId);
}

/// <summary>Coś, co da się wyróżnić po wyszukaniu (karta oferty).</summary>
public interface IHighlightable
{
    bool IsHighlighted { get; set; }
}

/// <summary>Sekcja na stronie handlarzy, którą można schować chipem.</summary>
public interface ITraderSection
{
    string Name { get; }
    IBrush AccentBrush { get; }
    bool IsVisible { get; set; }
}

public sealed partial class TraderSectionViewModel : ObservableObject, ITraderSection
{
    public TraderSectionViewModel(Trader trader, ShopsViewModel owner)
    {
        Name = trader.Name;
        PortraitUrl = trader.PortraitUrl;
        AccentBrush = Palette.Brush(Palette.Trader(trader.Name));
        IsSeeds = trader.Currency == TraderCurrency.Seeds;
        var currencyIcon = IsSeeds ? owner.SeedsIconUrl : owner.CoinsIconUrl;
        Offers = trader.Offers
            .OrderBy(o => o.Type).ThenBy(o => Palette.RarityRank(o.Rarity)).ThenBy(o => o.Name)
            .Select(o => new OfferCardViewModel(o, currencyIcon, IsSeeds, owner))
            .ToList();
        CountText = Loc.F("ItemCount", Offers.Count);
        CurrencyText = IsSeeds ? Loc.T("PaySeeds") : Loc.T("PayCoins");
    }

    public bool IsSeeds { get; }
    public string CurrencyText { get; }

    public string Name { get; }
    public string NameUpper => Name.ToUpperInvariant();
    public string? PortraitUrl { get; }
    public IBrush AccentBrush { get; }
    public IReadOnlyList<OfferCardViewModel> Offers { get; }
    public string CountText { get; }

    [ObservableProperty] private bool _isVisible = true;
}

public sealed partial class TraderToggleViewModel : ObservableObject
{
    private readonly ShopsViewModel _owner;

    public TraderToggleViewModel(ITraderSection section, bool on, ShopsViewModel owner)
    {
        Section = section;
        _owner = owner;
        _isOn = on;
        section.IsVisible = on;
    }

    public ITraderSection Section { get; }
    public string Name => Section.Name;
    public IBrush ColorBrush => Section.AccentBrush;

    [ObservableProperty] private bool _isOn;

    partial void OnIsOnChanged(bool value)
    {
        Section.IsVisible = value;
        _owner.TraderVisibilityChanged(this);
    }
}

public sealed partial class OfferCardViewModel(TraderOffer offer, string? currencyIconUrl, bool seeds, ShopsViewModel owner)
    : ObservableObject, IHighlightable
{
    public TraderOffer Offer { get; } = offer;
    public string Name { get; } = offer.Name;
    public string? IconUrl { get; } = offer.IconUrl;
    public string PriceText { get; } = TextFormat.Number(offer.Price);
    public string? CurrencyIconUrl { get; } = currencyIconUrl;

    /// <summary>Ikona nasion ma dużo pustego miejsca wokół — rysujemy ją większą niż monety.</summary>
    public double CurrencyIconSize { get; } = seeds ? 30 : 22;
    public bool IsBlueprint { get; } = ItemKinds.IsBlueprintType(offer.Type) || ItemKinds.IsBlueprint(offer.ItemId);
    public IBrush PriceBrush { get; } = Palette.Brush(seeds ? Palette.Green : Palette.Yellow);
    public string CurrencyTip { get; } = seeds ? Loc.T("SeedsTip") : Loc.T("CoinsTip");
    public string TypeText { get; } = offer.Type ?? "";
    public IBrush RarityBrush { get; } = Palette.Brush(Palette.Rarity(offer.Rarity));
    public string Tooltip { get; } = offer.Description ?? offer.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderThickness))]
    private bool _isHighlighted;

    public Avalonia.Thickness BorderThickness => new(IsHighlighted ? 3 : 1);

    [RelayCommand]
    private void Open() => owner.OpenItem(Offer.ItemId);
}

public sealed partial class OfferSearchResultViewModel : ObservableObject
{
    private readonly IHighlightable _target;
    private readonly ShopsViewModel _owner;

    public OfferSearchResultViewModel(OfferCardViewModel offer, string trader, string price, string? currencyIcon, ShopsViewModel owner)
        : this(offer, offer.Name, offer.IconUrl, trader, price, currencyIcon, owner) { }

    public OfferSearchResultViewModel(ErmalOfferViewModel offer, string trader, string price, string? currencyIcon, ShopsViewModel owner)
        : this(offer, offer.Name, offer.IconUrl, trader, price, currencyIcon, owner) { }

    private OfferSearchResultViewModel(IHighlightable target, string name, string? icon, string trader, string price,
        string? currencyIcon, ShopsViewModel owner)
    {
        IsBlueprint = target switch
        {
            OfferCardViewModel o => o.IsBlueprint,
            ErmalOfferViewModel e => e.IsBlueprint,
            _ => false,
        };
        RarityBrush = target switch
        {
            OfferCardViewModel o => o.RarityBrush,
            ErmalOfferViewModel e => e.RarityBrush,
            _ => Palette.Brush(Palette.Rarity(null)),
        };
        _target = target;
        _owner = owner;
        Name = name;
        IconUrl = icon;
        Trader = trader;
        PriceText = price;
        CurrencyIconUrl = currencyIcon;
    }

    public IHighlightable Offer => _target;
    public string Name { get; }
    public string Trader { get; }
    public string PriceText { get; }
    public string? IconUrl { get; }
    public string? CurrencyIconUrl { get; }
    public bool IsBlueprint { get; }
    public IBrush RarityBrush { get; }

    [RelayCommand]
    private void Go() => _owner.GoTo(_target);
}

// ==================================================================== Ermal

public sealed partial class ErmalSectionViewModel : ObservableObject, ITraderSection
{
    public ErmalSectionViewModel(ErmalStock stock, ShopsViewModel owner)
    {
        AccentBrush = Palette.Brush(Palette.Trader(Name));
        Offers = stock.Offers.Select(o => new ErmalOfferViewModel(o, owner)).ToList();
        Services = stock.Services.Select(s => new ErmalServiceViewModel(s, owner)).ToList();
        _rotationEnd = stock.RotationEnd;
        UpdateCountdown();
    }

    private readonly DateTimeOffset? _rotationEnd;

    public string Name => "Ermal";
    public string NameUpper => "ERMAL";
    public string PortraitUrl => "https://static.metaforge.app/arc-raiders/custom/ermalicon.webp";
    public IBrush AccentBrush { get; }
    public IReadOnlyList<ErmalOfferViewModel> Offers { get; }
    public IReadOnlyList<ErmalServiceViewModel> Services { get; }
    public bool HasServices => Services.Count > 0;
    public string InfoText =>
        Loc.T("ErmalInfo");

    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private string _countdownText = "";

    /// <summary>Wołane co minutę z widoku.</summary>
    public void UpdateCountdown()
    {
        if (_rotationEnd is null)
        {
            CountdownText = Loc.T("WeeklyRotation");
            return;
        }
        var left = _rotationEnd.Value - DateTimeOffset.UtcNow;
        CountdownText = left <= TimeSpan.Zero
            ? Loc.T("RotationChanged")
            : Loc.F("NextRotation", (int)left.TotalDays, left.Hours, left.Minutes);
    }
}

public sealed partial class ErmalOfferViewModel : ObservableObject, IHighlightable
{
    public ErmalOfferViewModel(ErmalOffer offer, ShopsViewModel owner)
    {
        Name = offer.Name;
        IconUrl = offer.Item?.IconUrl;
        IsBlueprint = ItemKinds.IsBlueprintType(offer.Item?.Type) || ItemKinds.IsBlueprint(offer.Item?.Id);
        RarityBrush = Palette.Brush(Palette.Rarity(offer.Item?.Rarity));
        TypeText = offer.Item?.Type ?? "";
        CostText = TextFormat.Number(offer.Cost);
        CoinsIconUrl = owner.CoinsIconUrl;
        Accepted = offer.Accepted
            .OrderByDescending(a => a.IsCategory).ThenByDescending(a => a.Value)
            .Select(a => new ErmalAcceptedViewModel(a, owner))
            .ToList();
        AcceptedHeader = Loc.F("Accepts", Accepted.Count);
        _itemId = offer.Item?.Id;
        _owner = owner;
    }

    private readonly string? _itemId;
    private readonly ShopsViewModel _owner;

    public string Name { get; }
    public string? IconUrl { get; }
    public bool IsBlueprint { get; }
    public IBrush RarityBrush { get; }
    public string TypeText { get; }
    public string CostText { get; }
    public string CoinsIconUrl { get; }
    public IReadOnlyList<ErmalAcceptedViewModel> Accepted { get; }
    public string AcceptedHeader { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BorderThickness))]
    private bool _isHighlighted;

    [ObservableProperty] private bool _isExpanded;

    public Avalonia.Thickness BorderThickness => new(IsHighlighted ? 3 : 1);

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void Open()
    {
        if (_itemId is not null) _owner.OpenItem(_itemId);
    }
}

public sealed partial class ErmalServiceViewModel : ObservableObject
{
    public ErmalServiceViewModel(ErmalService s, ShopsViewModel owner)
    {
        Name = s.Name;
        Description = s.Description ?? "";
        CostText = TextFormat.Number(s.Cost);
        CoinsIconUrl = owner.CoinsIconUrl;
        Accepted = s.Accepted
            .OrderByDescending(a => a.IsCategory).ThenByDescending(a => a.Value)
            .Select(a => new ErmalAcceptedViewModel(a, owner))
            .ToList();
        AcceptedHeader = Loc.F("Accepts", Accepted.Count);
    }

    public string Name { get; }
    public string Description { get; }
    public string CostText { get; }
    public string CoinsIconUrl { get; }
    public IReadOnlyList<ErmalAcceptedViewModel> Accepted { get; }
    public string AcceptedHeader { get; }

    [ObservableProperty] private bool _isExpanded;

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}

/// <summary>Jeden przedmiot, który Ermal przyjmuje, z wartością.</summary>
public sealed partial class ErmalAcceptedViewModel(ErmalItem item, ShopsViewModel owner) : ObservableObject
{
    public string Name { get; } = item.Name + (item.Amount is > 1 ? $" ×{item.Amount}" : "");
    public string? IconUrl { get; } = item.IconUrl;
    public bool HasIcon => IconUrl is not null;
    public bool IsBlueprint { get; } = !item.IsCategory && (ItemKinds.IsBlueprintType(item.Type) || ItemKinds.IsBlueprint(item.Id));
    public bool NoIcon => IconUrl is null;
    public string ValueText { get; } = TextFormat.Number(item.Value);
    public IBrush RarityBrush { get; } = Palette.Brush(Palette.Rarity(item.Rarity));
    public string Tip { get; } = item.IsCategory ? Loc.T("AnyOfCategory") : item.Type ?? item.Name;

    [RelayCommand]
    private void Open()
    {
        if (!item.IsCategory) owner.OpenItem(item.Id);
    }
}
