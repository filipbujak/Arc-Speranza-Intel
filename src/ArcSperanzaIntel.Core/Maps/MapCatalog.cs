using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Maps;

/// <summary>
/// Definicje 7 map odczytane z konfiguracji interaktywnych map MetaForge (adresy kafelków, rozmiar kafelka,
/// warstwy/piętra i transformacje Leaflet liczone z worldExtent/tileExtent).
/// Po aktualizacji gry MetaForge może zmienić wersję w adresie kafelków (np. "20260112-q85") — wtedy podmień ją tutaj.
/// </summary>
public static class MapCatalog
{
    private const string Static = "https://static.metaforge.app/arc-raiders/maps/";

    public static IReadOnlyList<MapDefinition> All { get; } =
    [
        new()
        {
            Id = "dam", Name = "Dam Battlegrounds", ShortName = "Dam",
            TileUrlTemplate = Static + "dam/20260112-q85/{z}/{x}/{y}.webp",
            Transform = new(0.02899823907870071, -0.5500098883744897, 0.029003656496347927, -0.2453962393344682),
            Bounds = new(new(8.46, 18.97), new(8800.46, 8812.6)),
            TileSize = 256, MaxNativeZoom = 5, Center = new(2560, 4096), InitialZoom = 3.25,
            Aliases = ["dam battlegrounds", "the dam", "dam"],
        },
        new()
        {
            Id = "spaceport", Name = "The Spaceport", ShortName = "Spaceport",
            TileUrlTemplate = Static + "spaceport/20260114-q85/{z}/{x}/{y}.webp",
            Transform = new(0.05754430970149254, 0.41474755130596463, 0.057570375981248076, 4.204424533065776),
            Bounds = new(new(-73.03, -7.21), new(8803.06, 8872.91)),
            MaxNativeZoom = 5, Center = new(2400, 3600), InitialZoom = 3,
            Floors =
            [
                new("surface", "FloorSurface", 2, Static + "spaceport/20260114-q85/{z}/{x}/{y}.webp"),
                new("tunnels", "FloorTunnels", 1, Static + "spaceport/20260114-q85/underground/{z}/{x}/{y}.webp"),
            ],
            Aliases = ["spaceport", "acerra spaceport"],
        },
        new()
        {
            Id = "buried-city", Name = "Buried City", ShortName = "Buried City",
            TileUrlTemplate = Static + "buried-city/20260112-q85/{z}/{x}/{y}.webp",
            Transform = new(0.03125, 0, 0.03125, 0),
            Bounds = new(new(0, 0), new(16352, 16352)),
            MaxNativeZoom = 5, Center = new(4800, 6800), InitialZoom = 2.75,
            Aliases = ["buried city"],
        },
        new()
        {
            Id = "blue-gate", Name = "The Blue Gate", ShortName = "Blue Gate",
            TileUrlTemplate = Static + "blue-gate/20260112-q85/{z}/{x}/{y}.webp",
            Transform = new(0.031557386447043756, -2.4780212715357095, 0.03158019364427784, -1.175068101010055),
            Bounds = new(new(37.21, 78.52), new(16218.24, 16271.25)),
            MaxNativeZoom = 5, Center = new(4700, 7500), InitialZoom = 2.5,
            Floors =
            [
                new("surface", "FloorSurface", 2, Static + "blue-gate/20260112-q85/{z}/{x}/{y}.webp"),
                new("underground", "FloorUnderground", 1, Static + "blue-gate/20260112-q85/underground/{z}/{x}/{y}.webp"),
            ],
            Aliases = ["blue gate"],
        },
        new()
        {
            Id = "stella-montis", Name = "Stella Montis", ShortName = "Stella Montis",
            TileUrlTemplate = Static + "stella-montis/v3/default/{z}/{x}_{y}.webp",
            Transform = new(0.0352558095952024, -13.055565967016491, 0.03522865853658536, -7.731432926829257),
            Bounds = new(new(219.46, 370.31), new(7457.89, 7603.16)),
            TileSize = 256, MaxNativeZoom = 5, Center = new(2500, 4000), InitialZoom = 3,
            Floors =
            [
                new("default", "FloorTop", 2, Static + "stella-montis/v3/default/{z}/{x}_{y}.webp"),
                new("b1", "FloorBottom", 1, Static + "stella-montis/v3/b1/{z}/{x}_{y}.webp"),
            ],
            Aliases = ["stella montis"],
        },
        new()
        {
            Id = "riven-tides", Name = "Riven Tides", ShortName = "Riven Tides",
            TileUrlTemplate = Static + "riven-tides/20260428-q85/{z}/{x}/{y}.webp",
            Transform = new(0.03125, 0, 0.03125, 0),
            Bounds = new(new(0, 0), new(16352, 16352)),
            MaxNativeZoom = 5, Center = new(4800, 6800), InitialZoom = 2.75,
            Aliases = ["riven tides"],
        },
        new()
        {
            Id = "pendola-pass", Name = "Pendola Pass", ShortName = "Pendola Pass",
            TileUrlTemplate = Static + "pendola-pass/20261005b-satellite/{z}/{x}/{y}.webp",
            Transform = new(0.03125, 0, 0.03125, 0),
            Bounds = new(new(3162, 626), new(13220, 15758)),
            MaxNativeZoom = 4, Center = new(8191, 8192), InitialZoom = 2.75,
            Aliases = ["pendola pass", "pendola"],
        },
    ];

    public static MapDefinition Get(string id) =>
        All.FirstOrDefault(m => m.Id == id) ?? All[0];

    public static bool Exists(string id) => All.Any(m => m.Id == id);

    /// <summary>Id mapy dla nazwy z harmonogramu wydarzeń ("Dam", "Spaceport", "Blue Gate"…).</summary>
    public static string? IdForEventMap(string name)
    {
        var n = name.Trim().ToLowerInvariant();
        return All.FirstOrDefault(m => m.ShortName.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)
                                       || m.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)
                                       || m.Aliases.Contains(n))?.Id;
    }
}
