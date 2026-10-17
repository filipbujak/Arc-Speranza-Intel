namespace ArcSperanzaIntel.Core.Models;

/// <summary>Marker z interaktywnej mapy MetaForge.</summary>
public sealed record MapMarker(
    string Id,
    string MapId,
    double Lat,
    double Lng,
    int ZLayers,
    string Category,
    string Subcategory,
    string? Name,
    bool BehindLockedDoor,
    string? LootAreas)
{
    /// <summary>Klucz filtra: "kategoria/podkategoria".</summary>
    public string Key => Category + "/" + Subcategory;

    public LatLng Position => new(Lat, Lng);

    public bool IsQuest => Category == MarkerCategories.Quests;

    /// <summary>Strefy lootu kontenera jako lista (z tekstu LootAreas). Bez pola cache — rekord porównuje pola.</summary>
    public IReadOnlyList<string> LootZoneList => Maps.LootZones.Parse(LootAreas);

    public bool HasLootZone(string zone) => LootZoneList.Contains(zone);
}

public static class MarkerCategories
{
    public const string Quests = "quests";
    public const string Locations = "locations";
    public const string Containers = "containers";
    public const string Arc = "arc";
    public const string Events = "events";
    public const string Nature = "nature";
}
