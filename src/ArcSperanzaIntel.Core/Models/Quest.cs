namespace ArcSperanzaIntel.Core.Models;

/// <summary>Quest z MetaForge: cele (subtaski), wymagania, nagrody i pozycja w grafie.</summary>
public sealed class Quest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Trader { get; init; } = "";
    public string Description { get; init; } = "";
    public string? ImageUrl { get; init; }
    public string? GuideUrl { get; init; }

    /// <summary>Widoczne cele questu w kolejności z gry. Indeks celu = indeks subtaska.</summary>
    public IReadOnlyList<string> Objectives { get; init; } = [];

    /// <summary>Questy, które trzeba ukończyć wcześniej.</summary>
    public IReadOnlyList<string> PrerequisiteIds { get; init; } = [];

    public IReadOnlyList<ItemStack> RequiredItems { get; init; } = [];
    public IReadOnlyList<ItemStack> Rewards { get; init; } = [];
    public IReadOnlyList<ItemStack> GrantedItems { get; init; } = [];
    public IReadOnlyList<CurrencyReward> CurrencyRewards { get; init; } = [];

    /// <summary>Mapy wskazane wprost w danych questu (pole "locations").</summary>
    public IReadOnlyList<string> LocationMapIds { get; init; } = [];

    /// <summary>Pozycja węzła w grafie questów z MetaForge (null = brak, liczona automatycznie).</summary>
    public Point2? GraphPosition { get; init; }

    public override string ToString() => Name;
}

/// <summary>Przedmiot z ilością (wymagany, nagroda, startowy).</summary>
public sealed record ItemStack(string ItemId, string Name, string? IconUrl, string? Rarity, int Quantity);

/// <summary>Nagroda walutowa (XP, Coins, Raider Tokens).</summary>
public sealed record CurrencyReward(string Name, int Quantity);

public readonly record struct Point2(double X, double Y);
