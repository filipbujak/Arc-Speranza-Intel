using ArcSperanzaIntel.Core.Localization;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Workshop;

namespace ArcSperanzaIntel.Core.Quests;

public sealed record WantedItem(string ItemId, string Name, string? IconUrl, string? Rarity, int Quantity);

public enum WantedGroupKind { Manual, Quest, Station }

/// <summary>Grupa szukanych itemów: "Marked By Me", quest albo następny poziom stacji warsztatu.</summary>
public sealed record WantedGroup(string Key, string Title, WantedGroupKind Kind, bool IsTracked, IReadOnlyList<WantedItem> Items)
{
    public bool IsManual => Kind == WantedGroupKind.Manual;
}

/// <summary>
/// Liczy panel "Szukane itemy": itemy dodane ręcznie, wymagane itemy dostępnych questów
/// i itemy na następny poziom każdej stacji. Questy / stacje można wyłączyć w całości albo grupami.
/// </summary>
public sealed class WantedItemsService(QuestBook book, WorkshopBook workshop, UserState state, Func<string, Item?> findItem)
{
    public const string StationKeyPrefix = "station:";

    public const string ManualGroupKey = "marked-by-me";
    public const string ManualGroupTitle = "Marked By Me";

    public event EventHandler? Changed;

    public IReadOnlyList<WantedGroup> Groups()
    {
        var groups = new List<WantedGroup>();

        if (state.MarkedItems.Count > 0)
        {
            var manual = state.MarkedItems
                .Select(kv =>
                {
                    var item = findItem(kv.Key);
                    return new WantedItem(kv.Key, item?.Name ?? kv.Key, item?.IconUrl, item?.Rarity, kv.Value);
                })
                .OrderBy(i => i.Name)
                .ToList();
            groups.Add(new WantedGroup(ManualGroupKey, ManualGroupTitle, WantedGroupKind.Manual, true, manual));
        }

        if (state.TrackQuestItems)
        {
            foreach (var q in book.WithStatus(QuestStatus.Available).OrderBy(q => q.Name))
            {
                if (q.RequiredItems.Count == 0) continue;
                var items = q.RequiredItems.Select(Resolve).ToList();
                groups.Add(new WantedGroup(q.Id, q.Name, WantedGroupKind.Quest, IsTracked(q.Id), items));
            }
        }

        if (state.TrackStationItems)
        {
            foreach (var st in workshop.Stations)
            {
                if (workshop.NextLevel(st) is not { RequiredItems.Count: > 0 } next) continue;
                var key = StationKeyPrefix + st.Id;
                groups.Add(new WantedGroup(key, Loc.F("StationGroupTitle", st.Name, next.Level), WantedGroupKind.Station,
                    IsTracked(key), next.RequiredItems.Select(Resolve).ToList()));
            }
        }
        return groups;
    }

    private WantedItem Resolve(ItemStack s)
    {
        var item = findItem(s.ItemId);
        return new WantedItem(s.ItemId, item?.Name ?? s.Name, s.IconUrl ?? item?.IconUrl, s.Rarity ?? item?.Rarity, s.Quantity);
    }

    // ---------------------------------------------------------------- śledzenie
    public bool IsTracked(string groupKey) => !state.UntrackedWantedGroups.Contains(groupKey);

    public void SetTracked(string groupKey, bool tracked)
    {
        var changed = tracked ? state.UntrackedWantedGroups.Remove(groupKey) : state.UntrackedWantedGroups.Add(groupKey);
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TrackQuests
    {
        get => state.TrackQuestItems;
        set
        {
            if (state.TrackQuestItems == value) return;
            state.TrackQuestItems = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool TrackStations
    {
        get => state.TrackStationItems;
        set
        {
            if (state.TrackStationItems == value) return;
            state.TrackStationItems = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Coś się zmieniło poza serwisem (np. poziom stacji) — przelicz panel.</summary>
    public void Invalidate() => Changed?.Invoke(this, EventArgs.Empty);

    public bool IsMarked(string itemId) => state.MarkedItems.ContainsKey(itemId);

    public void Mark(string itemId, int quantity = 1)
    {
        state.MarkedItems[itemId] = Math.Max(1, quantity);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ChangeQuantity(string itemId, int delta)
    {
        if (!state.MarkedItems.TryGetValue(itemId, out var q)) return;
        var next = q + delta;
        if (next <= 0) state.MarkedItems.Remove(itemId);
        else state.MarkedItems[itemId] = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Unmark(string itemId)
    {
        if (state.MarkedItems.Remove(itemId)) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>W których dostępnych questach item jest potrzebny.</summary>
    public IReadOnlyList<Quest> QuestsNeeding(string itemId) =>
        book.Quests.Where(q => q.RequiredItems.Any(r => r.ItemId == itemId)).ToList();
}
