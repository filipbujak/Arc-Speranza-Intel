namespace ArcSperanzaIntel.Core.Api;

/// <summary>
/// Region serwerów dla harmonogramu wydarzeń. Embark prowadzi osobny harmonogram na region,
/// a MetaForge zwraca go po parametrze <c>region</c> (nieznana wartość = europe).
/// Potwierdzone w API: europe, north-america, oceania.
/// </summary>
public static class EventRegions
{
    public const string Europe = "europe";
    public const string NorthAmerica = "north-america";
    public const string Oceania = "oceania";

    public static IReadOnlyList<string> All { get; } = [Europe, NorthAmerica, Oceania];

    public static bool IsKnown(string? region) => region is not null && All.Contains(region);

    /// <summary>Wybór użytkownika, a gdy go nie ma (null = „automatycznie”) — region ze strefy czasowej Windows.</summary>
    public static string Resolve(string? stored) => IsKnown(stored) ? stored! : Detect(TimeZoneInfo.Local);

    /// <summary>Australia / Nowa Zelandia → oceania, obie Ameryki (UTC−2:30 i dalej na zachód) → north-america, reszta → europe.</summary>
    public static string Detect(TimeZoneInfo zone)
    {
        var id = zone.Id;
        if (id.Contains("Australia", StringComparison.OrdinalIgnoreCase) || id.StartsWith("AUS ", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Tasmania", StringComparison.OrdinalIgnoreCase) || id.Contains("New Zealand", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("Pacific/Auckland", StringComparison.OrdinalIgnoreCase) || id.Contains("Lord Howe", StringComparison.OrdinalIgnoreCase))
            return Oceania;
        return zone.BaseUtcOffset <= TimeSpan.FromHours(-2.5) ? NorthAmerica : Europe;
    }
}
