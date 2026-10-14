namespace ArcSperanzaIntel.Core.Models;

/// <summary>Stacja warsztatu (Gunsmith, Gear Bench…) z poziomami z MetaForge /hideout.</summary>
public sealed class Station
{
    /// <summary>Id stacji "Warsztat podstawowy" (Basic Bench) — nie ma jej w /hideout, nie ma poziomów.</summary>
    public const string BasicId = "basic-bench";

    public required string Id { get; init; }
    public required string Name { get; init; }
    public int MaxLevel { get; init; }
    public IReadOnlyList<StationLevel> Levels { get; init; } = [];
    public string? IconUrl { get; init; }

    public bool IsBasic => Id == BasicId;

    public StationLevel? Level(int level) => Levels.FirstOrDefault(l => l.Level == level);
}

/// <summary>
/// Poziom stacji: co trzeba oddać, żeby go zbudować (RequiredItems), dodatkowe warunki
/// (np. rozegrane rundy) i id przedmiotów, które odblokowuje do craftowania.
/// </summary>
public sealed record StationLevel(
    int Level,
    IReadOnlyList<ItemStack> RequiredItems,
    IReadOnlyList<StationCondition> Conditions,
    IReadOnlyList<string> Crafts);

/// <summary>Warunek odblokowania poziomu, który nie jest przedmiotem (np. "rounds-played" ×3).</summary>
public sealed record StationCondition(string Id, int Quantity);
