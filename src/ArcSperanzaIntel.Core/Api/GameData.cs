using ArcSperanzaIntel.Core.Localization;
using ArcSperanzaIntel.Core.Maps;
using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Api;

/// <summary>Załadowane dane gry w pamięci: questy, itemy, handlarze i markery (ładowane per mapa).</summary>
public sealed class GameData(IGameDataSource source)
{
    private readonly Dictionary<string, IReadOnlyList<MapMarker>> _markers = [];
    private readonly Dictionary<string, Item> _itemsById = [];

    public IReadOnlyList<Quest> Quests { get; private set; } = [];
    public IReadOnlyList<Item> Items { get; private set; } = [];
    public IReadOnlyList<Trader> Traders { get; private set; } = [];
    public ErmalStock? Ermal { get; private set; }
    public IReadOnlyList<Station> Stations { get; private set; } = [];
    public IReadOnlyList<GameEvent> Events { get; private set; } = [];

    /// <summary>Kiedy ostatnio udało się wczytać harmonogram wydarzeń.</summary>
    public DateTimeOffset? EventsSavedAt { get; private set; }

    /// <summary>Ostatni opis źródła danych (do paska statusu).</summary>
    public string SourceInfo { get; private set; } = "";

    public event EventHandler<string>? Warning;
    public event EventHandler<string>? MarkersLoaded;

    public Item? FindItem(string id) => _itemsById.GetValueOrDefault(id);

    public bool HasMarkers(string mapId) => _markers.ContainsKey(mapId);
    public IReadOnlyList<MapMarker> MarkersOf(string mapId) => _markers.GetValueOrDefault(mapId) ?? [];

    /// <summary>Ładuje questy, itemy i handlarzy. Błąd jednego źródła nie blokuje reszty.</summary>
    public async Task LoadCoreAsync(bool force, CancellationToken ct)
    {
        var quests = Guard(source.GetQuestsAsync(force, ct), Loc.T("WhatQuests"));
        var items = Guard(source.GetItemsAsync(force, ct), Loc.T("WhatItems"));
        var traders = Guard(source.GetTradersAsync(force, ct), Loc.T("WhatTraders"));
        var hideout = Guard(source.GetHideoutAsync(force, ct), Loc.T("WhatWorkshop"));
        await Task.WhenAll(quests, items, traders, hideout).ConfigureAwait(false);
        if (hideout.Result is { } h) Stations = h.Data;

        if (quests.Result is { } q) Quests = q.Data;
        if (items.Result is { } i)
        {
            Items = i.Data;
            _itemsById.Clear();
            foreach (var item in Items) _itemsById.TryAdd(item.Id, item);
        }
        if (traders.Result is { } t)
        {
            Traders = t.Data.Traders;
            Ermal = t.Data.Ermal;
        }

        var saved = quests.Result?.SavedAt;
        SourceInfo = saved is null ? Loc.T("NoData") : Loc.F("DataFrom", saved.Value.ToLocalTime());
    }

    /// <summary>Harmonogram wydarzeń; błąd sieci nie blokuje aplikacji (zostaje poprzednia lista).</summary>
    public async Task<IReadOnlyList<GameEvent>> LoadEventsAsync(bool force, CancellationToken ct)
    {
        var r = await Guard(source.GetEventsAsync(force, ct), Loc.T("WhatEvents")).ConfigureAwait(false);
        if (r is not null)
        {
            Events = r.Data;
            EventsSavedAt = r.SavedAt;
        }
        return Events;
    }

    public async Task<IReadOnlyList<MapMarker>> LoadMarkersAsync(string mapId, bool force, CancellationToken ct)
    {
        if (!force && _markers.TryGetValue(mapId, out var cached)) return cached;
        var r = await Guard(source.GetMarkersAsync(mapId, force, ct), Loc.T("WhatMarkers") + MapCatalog.Get(mapId).ShortName).ConfigureAwait(false);
        if (r is null) return MarkersOf(mapId);
        _markers[mapId] = r.Data;
        MarkersLoaded?.Invoke(this, mapId);
        return r.Data;
    }

    private async Task<DataResult<T>?> Guard<T>(Task<DataResult<T>> task, string what)
    {
        try
        {
            var r = await task.ConfigureAwait(false);
            if (r.Origin == DataOrigin.StaleCache)
                Warning?.Invoke(this, Loc.F("OfflineData", what));
            return r;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Warning?.Invoke(this, Loc.F("FetchFailed", what, ex.Message));
            return null;
        }
    }
}
