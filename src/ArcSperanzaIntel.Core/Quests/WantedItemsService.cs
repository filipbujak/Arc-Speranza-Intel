using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Quests;

public sealed record WantedItem(string ItemId, string Name, string? IconUrl, string? Rarity, int Quantity);

/// <summary>Grupa szukanych itemów: quest albo "Marked By Me".</summary>
public sealed record WantedGroup(string Key, string Title, bool IsManual, IReadOnlyList<WantedItem> Items);

/// <summary>Liczy panel "Szukane itemy": wymagane itemy dostępnych questów + itemy dodane ręcznie.</summary>
public sealed class WantedItemsService(QuestBook book, UserState state, Func<string, Item?> findItem)
{
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
            groups.Add(new WantedGroup(ManualGroupKey, ManualGroupTitle, true, manual));
        }

        foreach (var q in book.WithStatus(QuestStatus.Available).OrderBy(q => q.Name))
        {
            if (q.RequiredItems.Count == 0) continue;
            var items = q.RequiredItems
                .Select(s => new WantedItem(s.ItemId, s.Name, s.IconUrl ?? findItem(s.ItemId)?.IconUrl, s.Rarity, s.Quantity))
                .ToList();
            groups.Add(new WantedGroup(q.Id, q.Name, false, items));
        }
        return groups;
    }

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
