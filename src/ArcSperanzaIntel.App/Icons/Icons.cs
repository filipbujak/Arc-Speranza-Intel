using ArcSperanzaIntel.Core.Models;
using Avalonia.Media;

namespace ArcSperanzaIntel.App.Icons;

/// <summary>Geometrie ikon interfejsu do użycia w XAML: Data="{x:Static icons:UiIcons.Map}".</summary>
public static class UiIcons
{
    public static Geometry Map { get; } = Parse(UiIconData.Map);
    public static Geometry Quests { get; } = Parse(UiIconData.Quests);
    public static Geometry Items { get; } = Parse(UiIconData.Items);
    public static Geometry Shops { get; } = Parse(UiIconData.Shops);
    public static Geometry Events { get; } = Parse(UiIconData.Events);
    public static Geometry Workshop { get; } = Parse(UiIconData.Workshop);
    public static Geometry StationScrappy { get; } = Parse(UiIconData.StationScrappy);
    public static Geometry StationGunsmith { get; } = Parse(UiIconData.StationGunsmith);
    public static Geometry StationGear { get; } = Parse(UiIconData.StationGear);
    public static Geometry StationMedical { get; } = Parse(UiIconData.StationMedical);
    public static Geometry StationExplosives { get; } = Parse(UiIconData.StationExplosives);
    public static Geometry StationUtility { get; } = Parse(UiIconData.StationUtility);
    public static Geometry StationRefiner { get; } = Parse(UiIconData.StationRefiner);
    public static Geometry StationResearch { get; } = Parse(UiIconData.StationResearch);
    public static Geometry StationOutpost { get; } = Parse(UiIconData.StationOutpost);
    public static Geometry StationBasic { get; } = Parse(UiIconData.StationBasic);
    public static Geometry Timer { get; } = Parse(UiIconData.Timer);
    public static Geometry Swap { get; } = Parse(UiIconData.Swap);
    public static Geometry Settings { get; } = Parse(UiIconData.Settings);
    public static Geometry Search { get; } = Parse(UiIconData.Search);
    public static Geometry Star { get; } = Parse(UiIconData.Star);
    public static Geometry StarOutline { get; } = Parse(UiIconData.StarOutline);
    public static Geometry ChevronDown { get; } = Parse(UiIconData.ChevronDown);
    public static Geometry ChevronUp { get; } = Parse(UiIconData.ChevronUp);
    public static Geometry ChevronRight { get; } = Parse(UiIconData.ChevronRight);
    public static Geometry Eye { get; } = Parse(UiIconData.Eye);
    public static Geometry EyeOff { get; } = Parse(UiIconData.EyeOff);
    public static Geometry Plus { get; } = Parse(UiIconData.Plus);
    public static Geometry Minus { get; } = Parse(UiIconData.Minus);
    public static Geometry Check { get; } = Parse(UiIconData.Check);
    public static Geometry Close { get; } = Parse(UiIconData.Close);
    public static Geometry Pin { get; } = Parse(UiIconData.Pin);
    public static Geometry Crosshair { get; } = Parse(UiIconData.Crosshair);
    public static Geometry Target { get; } = Parse(UiIconData.Target);
    public static Geometry Layers { get; } = Parse(UiIconData.Layers);
    public static Geometry Fit { get; } = Parse(UiIconData.Fit);
    public static Geometry Reset { get; } = Parse(UiIconData.Reset);
    public static Geometry Refresh { get; } = Parse(UiIconData.Refresh);
    public static Geometry Keyboard { get; } = Parse(UiIconData.Keyboard);
    public static Geometry Trash { get; } = Parse(UiIconData.Trash);
    public static Geometry Edit { get; } = Parse(UiIconData.Edit);
    public static Geometry Goto { get; } = Parse(UiIconData.Goto);
    public static Geometry Lock { get; } = Parse(UiIconData.Lock);
    public static Geometry Info { get; } = Parse(UiIconData.Info);
    public static Geometry Coin { get; } = Parse(UiIconData.Coin);
    public static Geometry Bag { get; } = Parse(UiIconData.Bag);

    internal static Geometry Parse(string data)
    {
        try
        {
            return Geometry.Parse(data);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "Icon parse");
            return new EllipseGeometry(new Avalonia.Rect(4, 4, 16, 16));
        }
    }
}

/// <summary>Ikony markerów mapy (game-icons.net) z kolorem kategorii.</summary>
public static class MarkerIcons
{
    private static readonly Dictionary<string, Geometry> Cache = [];

    public static readonly IReadOnlyDictionary<string, Color> CategoryColors = new Dictionary<string, Color>
    {
        [MarkerCategories.Quests] = Color.Parse("#FFC72C"),
        [MarkerCategories.Locations] = Color.Parse("#36B5E6"),
        [MarkerCategories.Containers] = Color.Parse("#E8913A"),
        [MarkerCategories.Arc] = Color.Parse("#F0503F"),
        [MarkerCategories.Events] = Color.Parse("#B57BFF"),
        [MarkerCategories.Nature] = Color.Parse("#3FD07A"),
        ["custom"] = Color.Parse("#ECE7DC"),
        ["favorites"] = Color.Parse("#FFC72C"),
    };

    public static Color ColorOf(string category) =>
        CategoryColors.TryGetValue(category, out var c) ? c : Color.Parse("#9298A3");

    /// <summary>Geometria ikony w układzie 512×512 dla klucza "kategoria/podkategoria" (z zapasem na kategorię).</summary>
    public static Geometry For(string category, string subcategory)
    {
        var key = category + "/" + subcategory;
        if (Cache.TryGetValue(key, out var g)) return g;

        var data = MarkerIconData.Paths.GetValueOrDefault(key)
                   ?? (subcategory.EndsWith("_husk", StringComparison.Ordinal) ? MarkerIconData.Paths.GetValueOrDefault("containers/husk") : null)
                   ?? MarkerIconData.Paths.GetValueOrDefault(category)
                   ?? MarkerIconData.Paths.GetValueOrDefault(category switch
                   {
                       MarkerCategories.Locations => "locations/player_spawn",
                       MarkerCategories.Containers => "containers/base_container",
                       MarkerCategories.Quests => "quests",
                       _ => "events",
                   })
                   ?? "";
        g = data.Length > 0 ? UiIcons.Parse(data) : new EllipseGeometry(new Avalonia.Rect(96, 96, 320, 320));
        Cache[key] = g;
        return g;
    }

    public static Geometry Pin => For("pin", "");
}

/// <summary>Ikony i kolory stacji warsztatu (rysowane, bo MetaForge ma tylko zdjęcia).</summary>
public static class StationIcons
{
    public static Geometry For(string stationId) => stationId switch
    {
        "scrappy" => UiIcons.StationScrappy,
        "weapon-bench" => UiIcons.StationGunsmith,
        "equipment-bench" => UiIcons.StationGear,
        "med-station" => UiIcons.StationMedical,
        "explosives-bench" => UiIcons.StationExplosives,
        "utility-bench" => UiIcons.StationUtility,
        "refiner" => UiIcons.StationRefiner,
        "research-station" => UiIcons.StationResearch,
        "outpost" => UiIcons.StationOutpost,
        _ => UiIcons.StationBasic,
    };

    public static Color ColorOf(string stationId) => stationId switch
    {
        "scrappy" => Color.Parse("#FFC72C"),
        "weapon-bench" => Color.Parse("#F0503F"),
        "equipment-bench" => Color.Parse("#36B5E6"),
        "med-station" => Color.Parse("#3FD07A"),
        "explosives-bench" => Color.Parse("#FF7A45"),
        "utility-bench" => Color.Parse("#B57BFF"),
        "refiner" => Color.Parse("#4FE3D2"),
        "research-station" => Color.Parse("#7FA8FF"),
        "outpost" => Color.Parse("#E0C27A"),
        _ => Color.Parse("#9298A3"),
    };
}
