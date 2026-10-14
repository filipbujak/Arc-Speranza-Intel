using System.Text.RegularExpressions;
using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Quests;

/// <summary>Na których mapach robi się dany quest: wg markerów MetaForge, pola "locations" i nazwy mapy w celach.</summary>
public sealed class QuestMapIndex
{
    private readonly Dictionary<string, HashSet<string>> _markerMaps = [];
    private readonly IReadOnlyList<MapDefinition> _maps;

    public QuestMapIndex(IReadOnlyList<MapDefinition> maps) => _maps = maps;

    public event EventHandler? Changed;

    /// <summary>Dopisuje markery questów z jednej mapy.</summary>
    public void AddMarkers(IEnumerable<MapMarker> markers)
    {
        foreach (var m in markers)
        {
            if (!m.IsQuest || string.IsNullOrEmpty(m.Subcategory)) continue;
            if (!_markerMaps.TryGetValue(m.Subcategory, out var set)) _markerMaps[m.Subcategory] = set = [];
            set.Add(m.MapId);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool HasMarkersOn(string questId, string mapId) =>
        _markerMaps.TryGetValue(questId, out var set) && set.Contains(mapId);

    /// <summary>Wszystkie mapy questu (najpierw te z markerami).</summary>
    public IReadOnlyList<MapDefinition> MapsFor(Quest q)
    {
        var withMarkers = _markerMaps.GetValueOrDefault(q.Id) ?? [];
        var result = new List<MapDefinition>();
        foreach (var map in _maps)
        {
            if (withMarkers.Contains(map.Id) || q.LocationMapIds.Contains(map.Id) || MentionedInText(q, map))
                result.Add(map);
        }
        return result.OrderBy(m => withMarkers.Contains(m.Id) ? 0 : 1).ToList();
    }

    public bool IsOn(Quest q, MapDefinition map) => MapsFor(q).Any(m => m.Id == map.Id);

    private static bool MentionedInText(Quest q, MapDefinition map)
    {
        var text = (string.Join(" ", q.Objectives) + " " + q.Description).ToLowerInvariant();
        foreach (var alias in map.Aliases)
            if (Regex.IsMatch(text, @"\b" + Regex.Escape(alias) + @"\b")) return true; // "dam" ≠ "damage"
        return false;
    }
}
