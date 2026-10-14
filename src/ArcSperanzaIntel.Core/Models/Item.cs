namespace ArcSperanzaIntel.Core.Models;

/// <summary>Przedmiot z bazy MetaForge.</summary>
public sealed class Item
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Type { get; init; } = "Other";
    public string? Subcategory { get; init; }
    public string? Rarity { get; init; }
    public string? Description { get; init; }
    public string? FlavorText { get; init; }
    public string? IconUrl { get; init; }
    public int? Value { get; init; }
    public string? Workbench { get; init; }
    public string? AmmoType { get; init; }
    public IReadOnlyList<ItemStat> Stats { get; init; } = [];

    /// <summary>Z czego się craftuje (MetaForge: components).</summary>
    public IReadOnlyList<ItemStack> Components { get; init; } = [];

    /// <summary>Na co się rozkłada przy recyklingu (MetaForge: recycle_components).</summary>
    public IReadOnlyList<ItemStack> RecycleComponents { get; init; } = [];

    public override string ToString() => Name;
}

/// <summary>Jedna statystyka przedmiotu, już w czytelnej postaci.</summary>
public sealed record ItemStat(string Name, string Value);
