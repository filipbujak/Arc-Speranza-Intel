namespace ArcSperanzaIntel.Core.Models;

/// <summary>Wszystko, co użytkownik zmienia i co trzeba zapisać na dysku.</summary>
public sealed class UserState
{
    public int Version { get; set; } = 1;

    /// <summary>"pl" / "en"; null = język systemu przy pierwszym uruchomieniu.</summary>
    public string? Language { get; set; }

    public QuestProgress Progress { get; set; } = new();

    /// <summary>Ulubione markery (id markera).</summary>
    public HashSet<string> FavoriteMarkers { get; set; } = [];

    /// <summary>Ulubione przedmioty (id itemu).</summary>
    public HashSet<string> FavoriteItems { get; set; } = [];

    /// <summary>Własne pinezki per mapa.</summary>
    public Dictionary<string, List<MapPin>> Pins { get; set; } = [];

    /// <summary>Itemy dodane ręcznie do "Szukanych" (grupa Marked By Me): id → ilość.</summary>
    public Dictionary<string, int> MarkedItems { get; set; } = [];

    /// <summary>Nadpisania domyślnych filtrów mapy: klucz filtra → widoczny.</summary>
    public Dictionary<string, bool> MapFilters { get; set; } = [];

    public bool OnlyFavoriteMarkers { get; set; }

    public string SelectedMapId { get; set; } = "dam";
    public Dictionary<string, string> SelectedFloors { get; set; } = [];
    public Dictionary<string, MapViewState> MapViews { get; set; } = [];

    /// <summary>Akcja → skrót (np. "Ctrl+F").</summary>
    public Dictionary<string, string> Keybinds { get; set; } = [];

    public bool AlwaysOnTop { get; set; }
    public bool WantedPanelExpanded { get; set; } = true;
    public HashSet<string> CollapsedWantedGroups { get; set; } = [];

    /// <summary>Ukryte kategorie itemów w zakładce Itemy.</summary>
    public HashSet<string> HiddenItemTypes { get; set; } = [];

    public HashSet<string> HiddenTraders { get; set; } = [];

    /// <summary>Poziomy stacji warsztatu: id stacji → poziom (brak wpisu = 0, niezbudowana).</summary>
    public Dictionary<string, int> StationLevels { get; set; } = [];

    /// <summary>Schematy oznaczone jako znalezione (id itemu-schematu).</summary>
    public HashSet<string> FoundBlueprints { get; set; } = [];

    /// <summary>Panel Szukane: czy pokazywać itemy z questów / ze stacji warsztatu.</summary>
    public bool TrackQuestItems { get; set; } = true;
    public bool TrackStationItems { get; set; } = true;

    /// <summary>Grupy w panelu Szukane wyłączone "okiem" (klucz grupy).</summary>
    public HashSet<string> UntrackedWantedGroups { get; set; } = [];

    /// <summary>Filtr mapy w zakładce Wydarzenia ("all" = wszystkie).</summary>
    public string? EventsMapId { get; set; }

    /// <summary>Filtr rodzaju wydarzenia (nazwa, np. "Night Raid"); null = wszystkie.</summary>
    public string? EventsMode { get; set; }

    public WindowPlacement? Window { get; set; }
}

public sealed class QuestProgress
{
    /// <summary>Ukończone questy: id → kiedy.</summary>
    public Dictionary<string, DateTimeOffset> Completed { get; set; } = [];

    /// <summary>Stan subtasków: id questu → zrobione cele (po indeksie).</summary>
    public Dictionary<string, List<bool>> Objectives { get; set; } = [];
}

public sealed class MapPin
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string Note { get; set; } = "";
    public string? FloorId { get; set; }
}

public sealed record MapViewState(double Lat, double Lng, double Zoom);

public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool Maximized);
