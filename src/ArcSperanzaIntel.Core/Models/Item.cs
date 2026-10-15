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

    /// <summary>Z jakich przedmiotów ten wypada przy recyklingu (MetaForge: recycle_from).</summary>
    public IReadOnlyList<ItemStack> RecycleFrom { get; init; } = [];

    /// <summary>Przepisy craftingu (MetaForge: relationships.recipes) — stacja z poziomem, składniki, wymagany schemat.</summary>
    public IReadOnlyList<ItemRecipe> Recipes { get; init; } = [];

    /// <summary>Dla schematu: id przedmiotów, które odblokowuje (MetaForge: relationships.blueprintUnlocks).</summary>
    public IReadOnlyList<string> BlueprintUnlocks { get; init; } = [];

    /// <summary>Operacje na przedmiocie: rozbiórka (salvage), ulepszenie, naprawa (MetaForge: relationships.operations).</summary>
    public IReadOnlyList<ItemOperation> Operations { get; init; } = [];

    /// <summary>Co zostaje z rozbiórki w raidzie (salvage), bez samego przedmiotu.</summary>
    public IReadOnlyList<ItemStack> SalvageComponents =>
        Operations.Where(o => o.Kind == ItemOperation.Salvage).SelectMany(o => o.Outputs).Where(s => s.ItemId != Id).ToList();

    /// <summary>Ulepszenie do następnego tieru (np. Bettina I → II) albo null.</summary>
    public ItemOperation? Upgrade => Operations.FirstOrDefault(o => o.Kind == ItemOperation.UpgradeKind && o.Outputs.Count > 0);

    public override string ToString() => Name;
}

/// <summary>Przepis: na jakiej stacji (tekst MetaForge, np. "Gunsmith Level 3"), z czego i jaki schemat jest potrzebny.</summary>
public sealed record ItemRecipe(string Station, IReadOnlyList<ItemStack> Ingredients, IReadOnlyList<string> BlueprintIds, int OutputQuantity);

/// <summary>Operacja na przedmiocie (salvage / upgrade / repair / field-crafting): wejścia → wyjścia.</summary>
public sealed record ItemOperation(string Kind, IReadOnlyList<ItemStack> Inputs, IReadOnlyList<ItemStack> Outputs)
{
    public const string Salvage = "salvage";
    public const string UpgradeKind = "upgrade";
    public const string Repair = "repair";
    public const string FieldCrafting = "field-crafting";
}

/// <summary>Jedna statystyka przedmiotu, już w czytelnej postaci.</summary>
public sealed record ItemStat(string Name, string Value);
