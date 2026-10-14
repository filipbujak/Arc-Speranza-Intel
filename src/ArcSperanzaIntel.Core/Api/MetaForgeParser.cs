using System.Globalization;
using System.Text.Json;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Util;

namespace ArcSperanzaIntel.Core.Api;

/// <summary>Zamienia surowy JSON z MetaForge na modele. Odporny na brakujące pola i null.</summary>
public static class MetaForgeParser
{
    private const string TraderPortraitBase = "https://static.metaforge.app/arc-raiders/custom/";

    // ---------------------------------------------------------------- questy
    public static IReadOnlyList<Quest> ParseQuests(string jsonArray)
    {
        using var doc = JsonDocument.Parse(jsonArray);
        var list = new List<Quest>();
        foreach (var q in doc.RootElement.EnumerateArray())
        {
            var id = Str(q, "id");
            var name = Str(q, "name");
            if (id is null || name is null) continue;

            var prereqs = new List<string>();
            if (q.TryGetProperty("manifest", out var manifest) && manifest.ValueKind == JsonValueKind.Object &&
                manifest.TryGetProperty("prerequisites", out var pre) && pre.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in pre.EnumerateArray())
                    if (Str(p, "quest_id") is { } pid && pid != id && !prereqs.Contains(pid)) prereqs.Add(pid);
            }

            Point2? pos = null;
            if (q.TryGetProperty("position", out var position) && position.ValueKind == JsonValueKind.Object)
            {
                var x = Num(position, "x");
                var y = Num(position, "y");
                if (x is not null && y is not null && !(x == 0 && y == 0)) pos = new Point2(x.Value, y.Value);
            }

            var maps = new List<string>();
            if (q.TryGetProperty("locations", out var locs) && locs.ValueKind == JsonValueKind.Array)
                foreach (var l in locs.EnumerateArray())
                    if (Str(l, "map") is { } m && !maps.Contains(m)) maps.Add(m);

            var currency = new List<CurrencyReward>();
            if (q.TryGetProperty("additional_rewards", out var add) && add.ValueKind == JsonValueKind.Array)
                foreach (var r in add.EnumerateArray())
                    if (Str(r, "kind") == "currency" && Str(r, "name") is { } cn)
                        currency.Add(new CurrencyReward(cn, (int)(Num(r, "quantity") ?? 0)));

            list.Add(new Quest
            {
                Id = id,
                Name = name.Trim(),
                Trader = Str(q, "trader_name") ?? "",
                Description = (Str(q, "description") ?? "").Replace("\r\n", "\n").Trim(),
                ImageUrl = Str(q, "image"),
                GuideUrl = Str(q, "guide_url"),
                Objectives = StrArray(q, "objectives"),
                PrerequisiteIds = prereqs,
                RequiredItems = Stacks(q, "required_items"),
                Rewards = Stacks(q, "rewards"),
                GrantedItems = Stacks(q, "granted_items"),
                CurrencyRewards = currency,
                LocationMapIds = maps,
                GraphPosition = pos,
            });
        }
        return list;
    }

    private static IReadOnlyList<ItemStack> Stacks(JsonElement parent, string prop)
    {
        var list = new List<ItemStack>();
        if (!parent.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var s in arr.EnumerateArray())
        {
            var itemId = Str(s, "item_id");
            JsonElement item = default;
            var hasItem = s.TryGetProperty("item", out item) && item.ValueKind == JsonValueKind.Object;
            itemId ??= hasItem ? Str(item, "id") : null;
            if (itemId is null) continue;
            list.Add(new ItemStack(
                itemId,
                (hasItem ? Str(item, "name") : null) ?? TextFormat.Pretty(itemId),
                hasItem ? Str(item, "icon") : null,
                hasItem ? NullIfEmpty(Str(item, "rarity")) : null,
                (int)(Num(s, "quantity") ?? 1)));
        }
        return list;
    }

    // ---------------------------------------------------------------- itemy
    public static IReadOnlyList<Item> ParseItems(string jsonArray)
    {
        using var doc = JsonDocument.Parse(jsonArray);
        var list = new List<Item>();
        foreach (var i in doc.RootElement.EnumerateArray())
        {
            var id = Str(i, "id");
            var name = Str(i, "name");
            if (id is null || name is null) continue;
            list.Add(new Item
            {
                Id = id,
                Name = name.Trim(),
                Type = NullIfEmpty(Str(i, "item_type")) ?? "Other",
                Subcategory = NullIfEmpty(Str(i, "subcategory")),
                Rarity = NullIfEmpty(Str(i, "rarity")),
                Description = NullIfEmpty(Str(i, "description")),
                FlavorText = NullIfEmpty(Str(i, "flavor_text")),
                IconUrl = NullIfEmpty(Str(i, "icon")),
                Value = Num(i, "value") is { } v ? (int)v : null,
                Workbench = NullIfEmpty(Str(i, "workbench")),
                AmmoType = NullIfEmpty(Str(i, "ammo_type")),
                Stats = Stats(i),
            });
        }
        return list;
    }

    private static IReadOnlyList<ItemStat> Stats(JsonElement item)
    {
        var list = new List<ItemStat>();
        if (!item.TryGetProperty("stat_block", out var sb) || sb.ValueKind != JsonValueKind.Object) return list;
        foreach (var p in sb.EnumerateObject())
        {
            string? value = p.Value.ValueKind switch
            {
                JsonValueKind.Number when p.Value.GetDouble() != 0 => TextFormat.Number(p.Value.GetDouble()),
                JsonValueKind.String when !string.IsNullOrWhiteSpace(p.Value.GetString()) => p.Value.GetString(),
                JsonValueKind.True => "Yes",
                _ => null,
            };
            if (value is not null) list.Add(new ItemStat(TextFormat.Pretty(p.Name), value));
        }
        return list;
    }

    // ---------------------------------------------------------------- handlarze
    private const string StaticBase = "https://static.metaforge.app/arc-raiders/";

    public static TradersData ParseTraders(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<Trader>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            foreach (var t in data.EnumerateObject())
            {
                if (t.Value.ValueKind != JsonValueKind.Array) continue;
                var offers = new List<TraderOffer>();
                foreach (var o in t.Value.EnumerateArray())
                {
                    var id = Str(o, "id");
                    var name = Str(o, "name");
                    if (id is null || name is null) continue;
                    offers.Add(new TraderOffer(
                        id, name.Trim(), NullIfEmpty(Str(o, "icon")), NullIfEmpty(Str(o, "rarity")),
                        NullIfEmpty(Str(o, "item_type")), NullIfEmpty(Str(o, "description")),
                        (int)(Num(o, "trader_price") ?? 0), Num(o, "value") is { } v ? (int)v : null));
                }
                list.Add(new Trader
                {
                    Name = t.Name,
                    PortraitUrl = TraderPortraitBase + t.Name.ToLowerInvariant() + ".webp",
                    Currency = t.Name.Equals("Celeste", StringComparison.OrdinalIgnoreCase) ? TraderCurrency.Seeds : TraderCurrency.Coins,
                    Offers = offers,
                });
            }
        }

        ErmalStock? ermal = null;
        if (doc.RootElement.TryGetProperty("ermal", out var e) && e.ValueKind == JsonValueKind.Object)
        {
            var offers = new List<ErmalOffer>();
            if (e.TryGetProperty("offers", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in arr.EnumerateArray())
                {
                    var item = o.TryGetProperty("item", out var it) && it.ValueKind == JsonValueKind.Object ? ErmalItemOf(it) : null;
                    var name = item?.Name ?? Str(o, "offer_title");
                    if (name is null) continue;
                    offers.Add(new ErmalOffer(
                        Str(o, "id") ?? name, name.Trim(), (int)(Num(o, "coin_cost") ?? 0),
                        Date(o, "offer_start"), Date(o, "offer_end"), item, ErmalItems(o, "accepted_items")));
                }
            }
            var services = new List<ErmalService>();
            if (e.TryGetProperty("permanent_services", out var sv) && sv.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in sv.EnumerateArray())
                {
                    var name = Str(o, "name");
                    if (name is null) continue;
                    services.Add(new ErmalService(Str(o, "id") ?? name, name.Trim(), NullIfEmpty(Str(o, "description")),
                        (int)(Num(o, "coin_cost") ?? 0), ErmalItems(o, "accepted_items")));
                }
            }
            if (offers.Count > 0 || services.Count > 0) ermal = new ErmalStock { Offers = offers, Services = services };
        }
        return new TradersData(list, ermal);
    }

    private static IReadOnlyList<ErmalItem> ErmalItems(JsonElement parent, string prop)
    {
        var list = new List<ErmalItem>();
        if (!parent.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var x in arr.EnumerateArray())
            if (ErmalItemOf(x) is { } i) list.Add(i);
        return list;
    }

    private static ErmalItem? ErmalItemOf(JsonElement x)
    {
        var id = Str(x, "id");
        var name = Str(x, "name");
        if (id is null || name is null) return null;
        var icon = NullIfEmpty(Str(x, "icon"));
        // kategorie ("Any Firearm") mają ścieżkę względną bez rozszerzenia — tych nie da się pobrać
        if (icon is not null && !icon.StartsWith("http", StringComparison.OrdinalIgnoreCase)) icon = null;
        return new ErmalItem(id, name.Trim(), icon, NullIfEmpty(Str(x, "rarity")), NullIfEmpty(Str(x, "item_type")),
            (int)(Num(x, "value") ?? 0), Num(x, "amount") is { } a ? (int)a : null,
            x.TryGetProperty("is_category", out var c) && c.ValueKind == JsonValueKind.True);
    }

    private static DateTimeOffset? Date(JsonElement e, string prop) =>
        Str(e, prop) is { } s && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d
            : null;

    // ---------------------------------------------------------------- wydarzenia
    /// <summary>Harmonogram wydarzeń: {data:[{name,map,icon,startTime,endTime}]}, czasy w ms od 1970 (UTC).</summary>
    public static IReadOnlyList<GameEvent> ParseEvents(string json, Func<string, string?> mapIdOf)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<GameEvent>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return list;
        foreach (var x in data.EnumerateArray())
        {
            var name = Str(x, "name");
            var map = Str(x, "map");
            var start = Num(x, "startTime");
            var end = Num(x, "endTime");
            if (name is null || map is null || start is not > 0 || end is not > 0) continue;
            list.Add(new GameEvent(name.Trim(), map.Trim(), mapIdOf(map), NullIfEmpty(Str(x, "icon")),
                DateTimeOffset.FromUnixTimeMilliseconds((long)start.Value),
                DateTimeOffset.FromUnixTimeMilliseconds((long)end.Value)));
        }
        list.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : string.CompareOrdinal(a.MapName, b.MapName));
        return list;
    }

    // ---------------------------------------------------------------- markery
    public static IReadOnlyList<MapMarker> ParseMarkers(string mapId, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<MapMarker>();
        if (!doc.RootElement.TryGetProperty("allData", out var all) || all.ValueKind != JsonValueKind.Array) return list;
        foreach (var m in all.EnumerateArray())
        {
            var id = Str(m, "id");
            var cat = Str(m, "category");
            var lat = Num(m, "lat");
            var lng = Num(m, "lng");
            if (id is null || cat is null || lat is null || lng is null) continue;
            var z = Num(m, "zlayers");
            list.Add(new MapMarker(
                id, mapId, lat.Value, lng.Value,
                z is null ? int.MaxValue : (int)Math.Clamp(z.Value, int.MinValue, int.MaxValue),
                cat.Trim(),
                (Str(m, "subcategory") ?? "").Trim(),
                NullIfEmpty(StrOrJoin(m, "instanceName")),
                m.TryGetProperty("behindLockedDoor", out var bl) && bl.ValueKind == JsonValueKind.True,
                NullIfEmpty(StrOrJoin(m, "lootAreas"))));
        }
        return list;
    }

    // ---------------------------------------------------------------- pomocnicze
    private static string? Str(JsonElement e, string prop) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string? StrOrJoin(JsonElement e, string prop)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Array => string.Join(", ", v.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString())),
            _ => null,
        };
    }

    private static double? Num(JsonElement e, string prop)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }

    private static IReadOnlyList<string> StrArray(JsonElement e, string prop)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Array) return [];
        return v.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
            .Select(x => x.GetString()!.Trim())
            .ToList();
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
