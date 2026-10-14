using System.Text.RegularExpressions;
using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Workshop;

/// <summary>Przedmiot craftowany na stacji; Level = wymagany poziom stacji (null = MetaForge go nie podaje).</summary>
public sealed record StationCraft(Item Item, int? Level);

/// <summary>Poziom stacji, na który potrzebny jest przedmiot (do szczegółów itemu).</summary>
public sealed record StationNeed(Station Station, int Level, int Quantity);

/// <summary>
/// Warsztat: poziomy stacji gracza, wymagania na kolejne poziomy, co się na stacji craftuje
/// i schematy (znalezione / nieznalezione).
/// </summary>
public sealed partial class WorkshopBook
{
    private readonly UserState _state;
    private readonly Dictionary<string, Item> _items;
    private readonly Dictionary<string, List<StationCraft>> _crafts = [];
    private readonly Dictionary<string, (string StationId, int? Level)> _workbenchOf = [];
    private readonly Dictionary<string, Item> _blueprintFor = [];   // id przedmiotu → schemat
    private readonly Dictionary<string, Item> _craftedBy = [];      // id schematu → przedmiot

    public WorkshopBook(IReadOnlyList<Station> stations, IReadOnlyList<Item> items, UserState state)
    {
        _state = state;
        _items = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());

        // 1) crafting z /hideout (dokładny poziom)
        foreach (var s in stations)
        foreach (var lvl in s.Levels)
        foreach (var id in lvl.Crafts)
            if (_items.TryGetValue(id, out var item)) AddCraft(s.Id, item, lvl.Level);

        // 2) pole "workbench" przedmiotów (np. "Gunsmith III", "Weapon Bench 2", "Refiner")
        foreach (var item in items)
        {
            if (ParseWorkbench(item.Workbench) is not { } wb) continue;
            if (wb.StationId != Station.BasicId && stations.All(s => s.Id != wb.StationId)) continue;
            AddCraft(wb.StationId, item, wb.Level);
        }

        var all = stations.ToList();
        if (_crafts.ContainsKey(Station.BasicId))
            all.Add(new Station { Id = Station.BasicId, Name = "Basic Bench", MaxLevel = 0 });
        Stations = all;

        // 3) schematy: "x-blueprint" / "x-recipe" → x, albo po nazwie "X Blueprint" → X
        var byName = items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var bp in items.Where(IsBlueprint))
        {
            Item? target = null;
            foreach (var suffix in new[] { "-blueprint", "-recipe" })
                if (target is null && bp.Id.EndsWith(suffix, StringComparison.Ordinal) && _items.TryGetValue(bp.Id[..^suffix.Length], out var t) && !IsBlueprint(t))
                    target = t;
            if (target is null && bp.Name.EndsWith(" Blueprint", StringComparison.OrdinalIgnoreCase)
                && byName.TryGetValue(bp.Name[..^" Blueprint".Length], out var byN) && !IsBlueprint(byN))
                target = byN;
            if (target is null) continue;
            _craftedBy[bp.Id] = target;
            _blueprintFor.TryAdd(target.Id, bp);
        }
        Blueprints = items.Where(IsBlueprint).OrderBy(i => i.Name).ToList();
    }

    public IReadOnlyList<Station> Stations { get; }
    public IReadOnlyList<Item> Blueprints { get; }

    public event EventHandler? Changed;

    public static bool IsBlueprint(Item item) => string.Equals(item.Type, "Blueprint", StringComparison.OrdinalIgnoreCase);

    private void AddCraft(string stationId, Item item, int? level)
    {
        if (!_crafts.TryGetValue(stationId, out var list)) _crafts[stationId] = list = [];
        var existing = list.FindIndex(c => c.Item.Id == item.Id);
        if (existing >= 0)
        {
            // /hideout ma pierwszeństwo; z pola workbench uzupełniamy tylko brakujący poziom
            if (list[existing].Level is null && level is not null)
            {
                list[existing] = list[existing] with { Level = level };
                _workbenchOf[item.Id] = (stationId, level);
            }
            return;
        }
        list.Add(new StationCraft(item, level));
        _workbenchOf.TryAdd(item.Id, (stationId, level));
    }

    // ---------------------------------------------------------------- poziomy
    public Station? Get(string id) => Stations.FirstOrDefault(s => s.Id == id);

    /// <summary>Poziom gracza; domyślnie 0 (niezbudowana) — gracz sam ustawia, co ma.</summary>
    public int LevelOf(Station s) =>
        s.IsBasic ? 0 : Math.Clamp(_state.StationLevels.TryGetValue(s.Id, out var l) ? l : 0, 0, s.MaxLevel);

    public void SetLevel(Station s, int level)
    {
        if (s.IsBasic) return;
        var clamped = Math.Clamp(level, 0, s.MaxLevel);
        if (clamped == LevelOf(s)) return;
        _state.StationLevels[s.Id] = clamped;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsMaxed(Station s) => !s.IsBasic && LevelOf(s) >= s.MaxLevel;

    /// <summary>Następny poziom do zbudowania albo null (maks. poziom / Basic Bench).</summary>
    public StationLevel? NextLevel(Station s) => s.IsBasic || IsMaxed(s) ? null : s.Level(LevelOf(s) + 1);

    // ---------------------------------------------------------------- crafting
    public IReadOnlyList<StationCraft> CraftsAt(string stationId) =>
        _crafts.TryGetValue(stationId, out var list)
            ? list.OrderBy(c => c.Level ?? 99).ThenBy(c => c.Item.Name).ToList()
            : [];

    /// <summary>Gdzie się craftuje dany przedmiot (stacja + poziom) albo null.</summary>
    public (Station Station, int? Level)? CraftedAt(string itemId) =>
        _workbenchOf.TryGetValue(itemId, out var wb) && Get(wb.StationId) is { } s ? (s, wb.Level) : null;

    /// <summary>Na które kolejne poziomy stacji (wyższe niż obecny) potrzebny jest przedmiot.</summary>
    public IReadOnlyList<StationNeed> NeededFor(string itemId)
    {
        var list = new List<StationNeed>();
        foreach (var s in Stations)
        {
            var current = LevelOf(s);
            foreach (var lvl in s.Levels.Where(l => l.Level > current))
            foreach (var r in lvl.RequiredItems.Where(r => r.ItemId == itemId))
                list.Add(new StationNeed(s, lvl.Level, r.Quantity));
        }
        return list;
    }

    // ---------------------------------------------------------------- schematy
    public Item? BlueprintFor(string itemId) => _blueprintFor.GetValueOrDefault(itemId);
    public Item? CraftedBy(string blueprintId) => _craftedBy.GetValueOrDefault(blueprintId);

    public bool IsFound(string blueprintId) => _state.FoundBlueprints.Contains(blueprintId);

    public void SetFound(string blueprintId, bool found)
    {
        var changed = found ? _state.FoundBlueprints.Add(blueprintId) : _state.FoundBlueprints.Remove(blueprintId);
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public int FoundCount => Blueprints.Count(b => IsFound(b.Id));

    // ---------------------------------------------------------------- pole "workbench"
    /// <summary>
    /// Zamienia tekst MetaForge ("Gunsmith III", "Weapon Bench 2", "Explosive Bench 1 or Basic Bench",
    /// "Med Stations 1", "Refiner") na id stacji i poziom. Pierwsza stacja z listy "or" wygrywa.
    /// </summary>
    public static (string StationId, int? Level)? ParseWorkbench(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var first = text.Split(" or ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        var m = LevelSuffix().Match(first);
        int? level = null;
        var name = first;
        if (m.Success)
        {
            name = first[..m.Index].Trim();
            level = m.Groups[1].Value.ToUpperInvariant() switch
            {
                "I" => 1, "II" => 2, "III" => 3, "IV" => 4, "V" => 5, "VI" => 6,
                var d when int.TryParse(d, out var n) => n,
                _ => null,
            };
        }
        var id = name.ToLowerInvariant() switch
        {
            "weapon bench" or "gunsmith" => "weapon-bench",
            "equipment bench" or "gear bench" => "equipment-bench",
            "explosive bench" or "explosives bench" or "explosives station" => "explosives-bench",
            "med station" or "med stations" or "medical lab" => "med-station",
            "utility bench" or "utility station" => "utility-bench",
            "refiner" => "refiner",
            "research station" => "research-station",
            "basic bench" or "workbench" => Station.BasicId,
            _ => null,
        };
        return id is null ? null : (id, id == Station.BasicId ? null : level);
    }

    [GeneratedRegex(@"\s+(\d+|I{1,3}|IV|V|VI)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex LevelSuffix();
}
