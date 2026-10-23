using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Traders;

/// <summary>
/// Ceny, których MetaForge nie podaje w /traders, a które są w grze: Lance wymienia augmenty Mk. 1
/// (Combat, Looting, Tactical) jeden za jeden za Free Loadout Augment — obok zwykłej ceny w monetach.
/// Źródła: arcraiders.wiki (Free Loadout Augment) i arctracker.io (Lance).
/// Jeśli kiedyś MetaForge zacznie to podawać, wystarczy usunąć wpis stąd.
/// </summary>
public static class AlternativePrices
{
    public const string FreeLoadoutAugmentId = "free-loadout-augment";
    private const string FreeLoadoutAugmentName = "Free Loadout Augment";

    private static readonly Dictionary<(string Trader, string ItemId), AltPrice> Known = new()
    {
        [("Lance", "combat-mk-1")] = new AltPrice(FreeLoadoutAugmentId, FreeLoadoutAugmentName, 1),
        [("Lance", "looting-mk-1")] = new AltPrice(FreeLoadoutAugmentId, FreeLoadoutAugmentName, 1),
        [("Lance", "tactical-mk-1")] = new AltPrice(FreeLoadoutAugmentId, FreeLoadoutAugmentName, 1),
    };

    public static AltPrice? For(string trader, string itemId)
    {
        foreach (var (key, price) in Known)
            if (key.Trader.Equals(trader, StringComparison.OrdinalIgnoreCase)
                && key.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                return price;
        return null;
    }
}
