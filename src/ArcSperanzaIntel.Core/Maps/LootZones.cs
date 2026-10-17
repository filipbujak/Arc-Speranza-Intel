namespace ArcSperanzaIntel.Core.Maps;

/// <summary>
/// Strefy lootu z gry (Industrial, Medical, Residential…). MetaForge podaje je jako tekst rozdzielony przecinkami:
/// przy przedmiocie (<c>loot_area</c> — gdzie go szukać) i przy kontenerze na mapie (<c>lootAreas</c> — co w nim jest).
/// Kształtów stref (wielokątów) MetaForge nie podaje, więc strefę pokazujemy przez kontenery tej kategorii.
/// </summary>
public static class LootZones
{
    /// <summary>Znane strefy w stałej kolejności (do listy filtrów).</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "Industrial", "Mechanical", "Electrical", "Technological", "Medical", "Security",
        "Commercial", "Residential", "Old World", "Exodus", "Nature", "ARC", "Raider",
    ];

    /// <summary>„Electrical, Commercial, Residential” → [Electrical, Commercial, Residential] w ujednoliconej pisowni.</summary>
    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var list = new List<string>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var known = All.FirstOrDefault(z => string.Equals(z, part, StringComparison.OrdinalIgnoreCase)) ?? part;
            if (!list.Contains(known)) list.Add(known);
        }
        return list;
    }

    /// <summary>Kolejność do sortowania (nieznane na końcu).</summary>
    public static int OrderOf(string zone)
    {
        for (var i = 0; i < All.Count; i++)
            if (All[i] == zone) return i;
        return All.Count;
    }
}
