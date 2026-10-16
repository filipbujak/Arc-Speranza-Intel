using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.App.Services;

/// <summary>Rozpoznawanie rodzaju itemu po id (np. schematy rysujemy jak w grze: białe na błękitnym).</summary>
public static class ItemKinds
{
    /// <summary>Ustawiane po załadowaniu danych.</summary>
    public static Func<string, Item?> Lookup { get; set; } = _ => null;

    public static bool IsBlueprintType(string? type) =>
        string.Equals(type, "Blueprint", StringComparison.OrdinalIgnoreCase);

    /// <summary>Projekt mebla do kryjówki — w grze kafelek z wzorem krzyżyków.</summary>
    public static bool IsDesignType(string? type) =>
        string.Equals(type, "Design", StringComparison.OrdinalIgnoreCase);

    public static bool IsBlueprint(string? itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (Lookup(itemId) is { } item) return IsBlueprintType(item.Type);
        return itemId.EndsWith("-blueprint", StringComparison.Ordinal);
    }
}
