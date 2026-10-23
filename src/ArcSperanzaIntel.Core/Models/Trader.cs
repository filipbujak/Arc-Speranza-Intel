namespace ArcSperanzaIntel.Core.Models;

/// <summary>Czym handlarz każe płacić. Celeste bierze nasiona (Assorted Seeds), reszta monety.</summary>
public enum TraderCurrency { Coins, Seeds }

/// <summary>Handlarz i jego oferta.</summary>
public sealed class Trader
{
    public required string Name { get; init; }
    public string? PortraitUrl { get; init; }
    public TraderCurrency Currency { get; init; } = TraderCurrency.Coins;
    public IReadOnlyList<TraderOffer> Offers { get; init; } = [];
}

/// <summary>Przedmiot w sklepie handlarza. MetaForge nie podaje ilości sztuk — tylko cenę.</summary>
public sealed record TraderOffer(
    string ItemId,
    string Name,
    string? IconUrl,
    string? Rarity,
    string? Type,
    string? Description,
    int Price,
    int? Value)
{
    /// <summary>Druga możliwa zapłata (np. 1× Free Loadout Augment u Lance'a). Null = tylko cena główna.</summary>
    public AltPrice? AltPrice { get; init; }
}

/// <summary>Zapłata przedmiotem zamiast waluty: ile sztuk którego przedmiotu.</summary>
public sealed record AltPrice(string ItemId, string Name, int Amount);

/// <summary>Wszystko z endpointu /traders: stali handlarze + Ermal.</summary>
public sealed record TradersData(IReadOnlyList<Trader> Traders, ErmalStock? Ermal);

/// <summary>
/// Ermal — wędrowny handlarz z tygodniową rotacją (od 25 poziomu). Oferty "kupuje się" monetami
/// albo oddając przedmioty o łącznej wartości równej cenie. Do tego stałe usługi (Expedition Vault, Stash Expansion).
/// </summary>
public sealed class ErmalStock
{
    public IReadOnlyList<ErmalOffer> Offers { get; init; } = [];
    public IReadOnlyList<ErmalService> Services { get; init; } = [];

    /// <summary>Koniec bieżącej rotacji (najwcześniejszy koniec oferty).</summary>
    public DateTimeOffset? RotationEnd => Offers.Where(o => o.End is not null).Select(o => o.End).Min();
}

public sealed record ErmalOffer(
    string Id,
    string Name,
    int Cost,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    ErmalItem? Item,
    IReadOnlyList<ErmalItem> Accepted);

public sealed record ErmalService(
    string Id,
    string Name,
    string? Description,
    int Cost,
    IReadOnlyList<ErmalItem> Accepted);

/// <summary>Przedmiot przyjmowany przez Ermala (albo nagroda). IsCategory = np. "Any Firearm".</summary>
public sealed record ErmalItem(
    string Id,
    string Name,
    string? IconUrl,
    string? Rarity,
    string? Type,
    int Value,
    int? Amount,
    bool IsCategory);
