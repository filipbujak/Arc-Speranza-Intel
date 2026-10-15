using System.Text.RegularExpressions;
using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Workshop;

/// <summary>
/// Przedmiot craftowany na stacji. Level = wymagany poziom stacji (null = MetaForge go nie podaje),
/// Ingredients = składniki z przepisu, BlueprintIds = schematy potrzebne do craftu.
/// </summary>
public sealed record StationCraft(Item Item, int? Level, IReadOnlyList<ItemStack> Ingredients, IReadOnlyList<string> BlueprintIds);

/// <summary>Poziom stacji, na który potrzebny jest przedmiot (do szczegółów itemu).</summary>
public sealed record StationNeed(Station Station, int Level, int Quantity);

/// <summary>Jeden krok ulepszenia broni: z czego → w co, za jakie przedmioty.</summary>
public sealed record UpgradeStep(Item From, Item To, IReadOnlyList<ItemStack> Cost);

/// <summary>
/// Warsztat: poziomy stacji gracza, wymagania na kolejne poziomy, co się na stacji craftuje,
/// ulepszenia broni i schematy (znalezione / nieznalezione).
/// </summary>
public sealed partial class WorkshopBook
{
    private readonly UserState _state;
    private readonly Dictionary<string, Item> _items;
    private readonly Dictionary<string, List<StationCraft>> _crafts = [];
    private readonly Dictionary<string, (string StationId, int? Level)> _workbenchOf = [];
    private readonly Dictionary<string, Item> _blueprintFor = [];   // id przedmiotu → schemat
    private readonly Dictionary<string, Item> _craftedBy = [];      // id schematu → przedmiot
    private readonly Dictionary<string, UpgradeStep> _upgradeOf = []; // id przedmiotu → ulepszenie do następnego tieru
    private readonly HashSet<string> _upgradeTargets = [];            // tiery II+ (powstają z ulepszenia, nie z craftu)

    public WorkshopBook(IReadOnlyList<Station> stations, IReadOnlyList<Item> items, UserState state)
    {
        _state = state;
        _items = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());

        // 0) ulepszenia (Bettina I → II → III → IV)
        foreach (var item in items)
        {
            if (item.Upgrade is not { } up) continue;
            if (!_items.TryGetValue(up.Outputs[0].ItemId, out var to) || to.Id == item.Id) continue;
            _upgradeOf.TryAdd(item.Id, new UpgradeStep(item, to, up.Inputs.Where(i => i.ItemId != item.Id).ToList()));
            _upgradeTargets.Add(to.Id);
        }

        // 1) przepisy z relationships.recipes (stacja + poziom + składniki + schemat) — główne źródło
        foreach (var item in items)
        foreach (var r in item.Recipes)
        {
            if (ParseStation(r.Station, stations) is not { } wb) continue;
            AddCraft(new StationCraft(item, wb.Level, r.Ingredients, r.BlueprintIds), wb.StationId);
        }

        // 2) /hideout crafts — tylko dla przedmiotów bez przepisu
        foreach (var s in stations)
        foreach (var lvl in s.Levels)
        foreach (var id in lvl.Crafts)
            if (_items.TryGetValue(id, out var item) && item.Recipes.Count == 0)
                AddCraft(new StationCraft(item, lvl.Level, item.Components, []), s.Id);

        // 3) pole "workbench" — ostatnia deska ratunku; tiery z ulepszeń pomijamy (nie craftuje się ich)
        foreach (var item in items)
        {
            // schematy mają w "workbench" stację przedmiotu, który odblokowują — to nie jest ich craft
            if (item.Recipes.Count > 0 || _upgradeTargets.Contains(item.Id) || IsBlueprint(item)) continue;
            if (ParseStation(item.Workbench, stations) is not { } wb) continue;
            AddCraft(new StationCraft(item, wb.Level, item.Components, []), wb.StationId);
        }

        var all = stations.ToList();
        if (_crafts.ContainsKey(Station.BasicId))
            all.Add(new Station { Id = Station.BasicId, Name = "Basic Bench", MaxLevel = 0 });
        Stations = all;

        LinkBlueprints(items);
        Blueprints = items.Where(IsBlueprint).OrderBy(i => i.Name).ToList();
    }

    /// <summary>
    /// Schemat ↔ przedmiot. Kolejność: blueprintUnlocks schematu, schemat z przepisu przedmiotu,
    /// "x-blueprint" → x, nazwa "X Blueprint" → "X" albo "X I" (broń z tierami).
    /// </summary>
    private void LinkBlueprints(IReadOnlyList<Item> items)
    {
        void Link(Item bp, Item target)
        {
            if (IsBlueprint(target) || !IsBlueprint(bp)) return;
            _craftedBy.TryAdd(bp.Id, target);
            _blueprintFor.TryAdd(target.Id, bp);
        }

        foreach (var bp in items.Where(IsBlueprint))
        foreach (var id in bp.BlueprintUnlocks)
            if (_items.TryGetValue(id, out var t)) Link(bp, t);

        foreach (var item in items)
        foreach (var r in item.Recipes)
        foreach (var id in r.BlueprintIds)
            if (_items.TryGetValue(id, out var bp)) Link(bp, item);

        var byName = items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var bp in items.Where(IsBlueprint).Where(b => !_craftedBy.ContainsKey(b.Id)))
        {
            if (bp.Id.EndsWith("-blueprint", StringComparison.Ordinal) && _items.TryGetValue(bp.Id[..^"-blueprint".Length], out var t))
            {
                Link(bp, t);
                continue;
            }
            if (!bp.Name.EndsWith(" Blueprint", StringComparison.OrdinalIgnoreCase)) continue;
            var baseName = bp.Name[..^" Blueprint".Length].Trim();
            if (byName.TryGetValue(baseName, out var byN) || byName.TryGetValue(baseName + " I", out byN)) Link(bp, byN);
        }
    }

    public IReadOnlyList<Station> Stations { get; }
    public IReadOnlyList<Item> Blueprints { get; }

    public event EventHandler? Changed;

    public static bool IsBlueprint(Item item) => string.Equals(item.Type, "Blueprint", StringComparison.OrdinalIgnoreCase);

    private void AddCraft(StationCraft craft, string stationId)
    {
        if (!_crafts.TryGetValue(stationId, out var list)) _crafts[stationId] = list = [];
        var existing = list.FindIndex(c => c.Item.Id == craft.Item.Id);
        if (existing >= 0)
        {
            // pierwsze źródło wygrywa; uzupełniamy tylko brakujący poziom
            if (list[existing].Level is null && craft.Level is not null)
            {
                list[existing] = list[existing] with { Level = craft.Level };
                _workbenchOf[craft.Item.Id] = (stationId, craft.Level);
            }
            return;
        }
        list.Add(craft);
        _workbenchOf.TryAdd(craft.Item.Id, (stationId, craft.Level));
    }

    // ---------------------------------------------------------------- poziomy
    public Station? Get(string id) => Stations.FirstOrDefault(s => s.Id == id);

    /// <summary>Poziom gracza; domyślnie poziom startowy stacji (0, a dla Scrappy/Outpost 1).</summary>
    public int LevelOf(Station s) =>
        s.IsBasic ? 0 : Math.Clamp(_state.StationLevels.TryGetValue(s.Id, out var l) ? l : s.MinLevel, s.MinLevel, s.MaxLevel);

    public void SetLevel(Station s, int level)
    {
        if (s.IsBasic) return;
        var clamped = Math.Clamp(level, s.MinLevel, s.MaxLevel);
        if (clamped == LevelOf(s)) return;
        _state.StationLevels[s.Id] = clamped;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool IsMaxed(Station s) => !s.IsBasic && LevelOf(s) >= s.MaxLevel;

    /// <summary>Następny poziom do zbudowania albo null (maks. poziom / Basic Bench).</summary>
    public StationLevel? NextLevel(Station s) => s.IsBasic || IsMaxed(s) ? null : s.Level(LevelOf(s) + 1);

    // ---------------------------------------------------------------- crafting
    /// <summary>Crafty stacji ze znanym poziomem (bez grupy „poziom nieznany”).</summary>
    public IReadOnlyList<StationCraft> CraftsAt(string stationId) =>
        _crafts.TryGetValue(stationId, out var list)
            ? list.Where(c => c.Level is not null || stationId == Station.BasicId)
                .OrderBy(c => c.Level ?? 0).ThenBy(c => c.Item.Name).ToList()
            : [];

    /// <summary>Gdzie się craftuje dany przedmiot (stacja + poziom) albo null.</summary>
    public (Station Station, int? Level)? CraftedAt(string itemId) =>
        _workbenchOf.TryGetValue(itemId, out var wb) && Get(wb.StationId) is { } s ? (s, wb.Level) : null;

    /// <summary>Czy do craftu potrzebny jest schemat, którego gracz nie oznaczył jako znaleziony.</summary>
    public bool IsLockedByBlueprint(StationCraft craft)
    {
        var ids = craft.BlueprintIds.Count > 0
            ? craft.BlueprintIds
            : BlueprintFor(craft.Item.Id) is { } bp ? [bp.Id] : [];
        return ids.Count > 0 && !ids.Any(IsFound);
    }

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

    // ---------------------------------------------------------------- ulepszenia
    public UpgradeStep? UpgradeOf(string itemId) => _upgradeOf.GetValueOrDefault(itemId);

    /// <summary>Cały łańcuch ulepszeń od danego przedmiotu (I → II → III → IV).</summary>
    public IReadOnlyList<UpgradeStep> UpgradeChain(string itemId)
    {
        var list = new List<UpgradeStep>();
        var seen = new HashSet<string> { itemId };
        while (UpgradeOf(itemId) is { } step && seen.Add(step.To.Id))
        {
            list.Add(step);
            itemId = step.To.Id;
        }
        return list;
    }

    /// <summary>Z czego powstaje dany tier przez ulepszenie (np. Bettina II ← Bettina I) albo null.</summary>
    public UpgradeStep? UpgradedFrom(string itemId) => _upgradeOf.Values.FirstOrDefault(u => u.To.Id == itemId);

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

    // ---------------------------------------------------------------- nazwy stacji
    /// <summary>
    /// Zamienia tekst MetaForge ("Gunsmith Level 3", "Gunsmith III", "Weapon Bench 2",
    /// "Explosive Bench 1 or Basic Bench", "Med Stations 1", "Refiner") na id stacji i poziom.
    /// Pierwsza stacja z listy "or" wygrywa. Nieznane nazwy porównujemy z nazwami stacji z /hideout.
    /// </summary>
    public static (string StationId, int? Level)? ParseStation(string? text, IReadOnlyList<Station>? stations = null)
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
            "explosive bench" or "explosives bench" or "explosives station" or "explosive station" => "explosives-bench",
            "med station" or "med stations" or "medical lab" or "medical station" => "med-station",
            "utility bench" or "utility station" => "utility-bench",
            "refiner" => "refiner",
            "research station" => "research-station",
            "scrappy" => "scrappy",
            "outpost" => "outpost",
            "basic bench" or "workbench" or "work bench" => Station.BasicId,
            _ => stations?.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))?.Id,
        };
        return id is null ? null : (id, id == Station.BasicId ? null : level);
    }

    /// <summary>Stara nazwa (testy, zgodność).</summary>
    public static (string StationId, int? Level)? ParseWorkbench(string? text) => ParseStation(text);

    [GeneratedRegex(@"\s+(?:level\s+|lvl\.?\s*|poziom\s+)?(\d+|I{1,3}|IV|V|VI)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex LevelSuffix();
}
