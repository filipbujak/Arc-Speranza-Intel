using ArcSperanzaIntel.Core.Maps;
using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Api;

public interface IGameDataSource
{
    Task<DataResult<IReadOnlyList<Quest>>> GetQuestsAsync(bool forceRefresh, CancellationToken ct);
    Task<DataResult<IReadOnlyList<Item>>> GetItemsAsync(bool forceRefresh, CancellationToken ct);
    Task<DataResult<TradersData>> GetTradersAsync(bool forceRefresh, CancellationToken ct);
    Task<DataResult<IReadOnlyList<GameEvent>>> GetEventsAsync(bool forceRefresh, CancellationToken ct);
    Task<DataResult<IReadOnlyList<Station>>> GetHideoutAsync(bool forceRefresh, CancellationToken ct);
    Task<DataResult<IReadOnlyList<MapMarker>>> GetMarkersAsync(string mapId, bool forceRefresh, CancellationToken ct);
}

public enum DataOrigin { Network, Cache, StaleCache }

/// <summary>Wynik pobrania danych: same dane + skąd przyszły (sieć / cache / stary cache po błędzie).</summary>
public sealed record DataResult<T>(T Data, DataOrigin Origin, DateTimeOffset SavedAt, string? Error = null);

/// <summary>
/// Dane z MetaForge z cache na dysku. Świeży cache (domyślnie 12 h) → bez sieci.
/// Błąd sieci → ostatni zapisany cache. Brak cache i sieci → wyjątek.
/// </summary>
public sealed class GameDataRepository(MetaForgeHttp http, JsonFileCache cache, TimeSpan? maxAge = null) : IGameDataSource
{
    private readonly TimeSpan _maxAge = maxAge ?? TimeSpan.FromHours(12);

    public Task<DataResult<IReadOnlyList<Quest>>> GetQuestsAsync(bool forceRefresh, CancellationToken ct) =>
        LoadAsync("quests", forceRefresh, c => http.GetAllPagesAsync("arc-raiders/quests", c), MetaForgeParser.ParseQuests, ct);

    public Task<DataResult<IReadOnlyList<Item>>> GetItemsAsync(bool forceRefresh, CancellationToken ct) =>
        LoadAsync("items-v2", forceRefresh, c => http.GetAllPagesAsync("arc-raiders/items?includeComponents=true", c), MetaForgeParser.ParseItems, ct);

    public Task<DataResult<TradersData>> GetTradersAsync(bool forceRefresh, CancellationToken ct) =>
        LoadAsync("traders", forceRefresh, c => http.GetStringAsync("arc-raiders/traders", c), MetaForgeParser.ParseTraders, ct);

    public Task<DataResult<IReadOnlyList<Station>>> GetHideoutAsync(bool forceRefresh, CancellationToken ct) =>
        LoadAsync("hideout", forceRefresh, c => http.GetStringAsync("arc-raiders/hideout", c), MetaForgeParser.ParseHideout, ct);

    /// <summary>Harmonogram wydarzeń (ok. 1,5 doby do przodu) — krótki cache, bo szybko się dezaktualizuje.</summary>
    public Task<DataResult<IReadOnlyList<GameEvent>>> GetEventsAsync(bool forceRefresh, CancellationToken ct) =>
        LoadAsync("events", forceRefresh, c => http.GetStringAsync("arc-raiders/events-schedule?region=europe", c),
            json => MetaForgeParser.ParseEvents(json, MapCatalog.IdForEventMap), ct, TimeSpan.FromMinutes(30));

    public Task<DataResult<IReadOnlyList<MapMarker>>> GetMarkersAsync(string mapId, bool forceRefresh, CancellationToken ct) =>
        LoadAsync("map-" + mapId, forceRefresh,
            c => http.GetStringAsync($"game-map-data?tableID=arc_map_data&mapID={Uri.EscapeDataString(mapId)}", c),
            json => MetaForgeParser.ParseMarkers(mapId, json), ct);

    private async Task<DataResult<T>> LoadAsync<T>(
        string key, bool force, Func<CancellationToken, Task<string>> fetch, Func<string, T> parse, CancellationToken ct,
        TimeSpan? maxAge = null)
    {
        var cached = cache.TryRead(key);
        if (cached is not null && !force && DateTimeOffset.UtcNow - cached.SavedAt < (maxAge ?? _maxAge))
        {
            try
            {
                return new DataResult<T>(parse(cached.Json), DataOrigin.Cache, cached.SavedAt);
            }
            catch (Exception)
            {
                // uszkodzony plik cache — pobieramy od nowa
            }
        }

        try
        {
            var json = await fetch(ct).ConfigureAwait(false);
            var data = parse(json); // najpierw parsujemy, żeby nie zapisać śmieci
            cache.Write(key, json);
            return new DataResult<T>(data, DataOrigin.Network, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && cached is not null)
        {
            return new DataResult<T>(parse(cached.Json), DataOrigin.StaleCache, cached.SavedAt, ex.Message);
        }
    }
}
