namespace ArcSperanzaIntel.Core.Models;

public readonly record struct LatLng(double Lat, double Lng);

public readonly record struct LatLngBounds(LatLng Min, LatLng Max)
{
    public LatLng Center => new((Min.Lat + Max.Lat) / 2, (Min.Lng + Max.Lng) / 2);

    public static LatLngBounds From(IEnumerable<LatLng> points)
    {
        double minLat = double.MaxValue, minLng = double.MaxValue, maxLat = double.MinValue, maxLng = double.MinValue;
        foreach (var p in points)
        {
            minLat = Math.Min(minLat, p.Lat); maxLat = Math.Max(maxLat, p.Lat);
            minLng = Math.Min(minLng, p.Lng); maxLng = Math.Max(maxLng, p.Lng);
        }
        return new LatLngBounds(new LatLng(minLat, minLng), new LatLng(maxLat, maxLng));
    }
}

/// <summary>
/// Piętro / warstwa mapy (Stella Montis, tunele Spaceportu, podziemia Blue Gate).
/// Bit = 1 &lt;&lt; indeks warstwy w MetaForge; marker jest na piętrze, gdy (zlayers &amp; Bit) != 0.
/// TileUrlTemplate = kafelki tej warstwy ({z} {x} {y}).
/// </summary>
public sealed record MapFloor(string Id, string Name, int Bit, string TileUrlTemplate);

/// <summary>
/// Przeliczanie współrzędnych jak w Leaflet CRS.Simple z własną transformacją:
/// piksel = 2^zoom · (a·lng + b, c·lat + d).
/// </summary>
public sealed class CoordinateTransform(double a, double b, double c, double d)
{
    public double A { get; } = a;
    public double B { get; } = b;
    public double C { get; } = c;
    public double D { get; } = d;

    public (double X, double Y) ToPixel(LatLng p, double zoom)
    {
        var s = Math.Pow(2, zoom);
        return (s * (A * p.Lng + B), s * (C * p.Lat + D));
    }

    public LatLng FromPixel(double x, double y, double zoom)
    {
        var s = Math.Pow(2, zoom);
        return new LatLng((y / s - D) / C, (x / s - B) / A);
    }
}

/// <summary>Definicja mapy: kafelki, układ współrzędnych, granice, piętra.</summary>
public sealed class MapDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ShortName { get; init; }

    /// <summary>Szablon adresu kafelków warstwy bazowej z {z} {x} {y}.</summary>
    public required string TileUrlTemplate { get; init; }

    /// <summary>Rozmiar kafelka w układzie Leaflet (MetaForge: 256 dla Dam i Stella Montis, 512 dla reszty).</summary>
    public int TileSize { get; init; } = 512;

    public required CoordinateTransform Transform { get; init; }
    public required LatLngBounds Bounds { get; init; }
    public int MaxNativeZoom { get; init; } = 5;
    public required LatLng Center { get; init; }
    public double InitialZoom { get; init; } = 3;
    /// <summary>Piętra; pierwsze na liście to warstwa bazowa (zawsze rysowana pod spodem, jak w MetaForge).</summary>
    public IReadOnlyList<MapFloor> Floors { get; init; } = [];

    /// <summary>Nazwy, pod którymi mapa występuje w opisach questów (małe litery).</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public bool HasFloors => Floors.Count > 0;

    public MapFloor? BaseFloor => Floors.Count > 0 ? Floors[0] : null;

    /// <summary>Adres kafelka warstwy bazowej (layer = null) albo podanego piętra.</summary>
    public string TileUrl(int z, int x, int y, MapFloor? layer = null) =>
        (layer?.TileUrlTemplate ?? BaseFloor?.TileUrlTemplate ?? TileUrlTemplate)
            .Replace("{z}", z.ToString())
            .Replace("{x}", x.ToString())
            .Replace("{y}", y.ToString());

    /// <summary>Czy marker leży na danym piętrze (mapy bez pięter: zawsze tak).</summary>
    public bool IsOnFloor(MapMarker marker, MapFloor? floor) =>
        floor is null || (marker.ZLayers & floor.Bit) != 0;

    public override string ToString() => Name;
}
