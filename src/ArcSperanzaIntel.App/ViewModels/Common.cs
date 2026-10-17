using ArcSperanzaIntel.Core.Localization;
using ArcSperanzaIntel.Core.Quests;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>Kolory ARC używane w ViewModelach (rzadkość, handlarze, statusy).</summary>
public static class Palette
{
    public static readonly Color Yellow = Color.Parse("#FFC72C");
    public static readonly Color Green = Color.Parse("#3FD07A");
    public static readonly Color Red = Color.Parse("#F0503F");
    public static readonly Color Blue = Color.Parse("#36B5E6");
    public static readonly Color Muted = Color.Parse("#9298A3");
    public static readonly Color Text = Color.Parse("#ECE7DC");
    public static readonly Color LockedQuest = Color.Parse("#7A7462");

    private static readonly Dictionary<Color, IBrush> Brushes = [];

    private static readonly Dictionary<Color, IBrush> Glows = [];

    /// <summary>Miękka poświata od góry w danym kolorze (panel szczegółów itemu).</summary>
    public static IBrush Glow(Color c)
    {
        if (!Glows.TryGetValue(c, out var b))
            Glows[c] = b = new RadialGradientBrush
            {
                Center = new Avalonia.RelativePoint(0.5, 0, Avalonia.RelativeUnit.Relative),
                GradientOrigin = new Avalonia.RelativePoint(0.5, 0, Avalonia.RelativeUnit.Relative),
                Radius = 0.85,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x40, c.R, c.G, c.B), 0),
                    new GradientStop(Color.FromArgb(0x00, c.R, c.G, c.B), 1),
                },
            };
        return b;
    }

    public static IBrush Brush(Color c)
    {
        if (!Brushes.TryGetValue(c, out var b)) Brushes[c] = b = new SolidColorBrush(c);
        return b;
    }

    // kolory rzadkości z gry (te same co na ARC Raiders Wiki)
    private static readonly Color CommonColor = Color.Parse("#8A8F98");
    private static readonly Color UncommonColor = Color.Parse("#26BF57");
    private static readonly Color RareColor = Color.Parse("#00A9F2");
    private static readonly Color EpicColor = Color.Parse("#CC3099");
    private static readonly Color LegendaryColor = Color.Parse("#FFC600");
    private static readonly Color AmplifiedColor = Color.Parse("#E88629");
    private static readonly Color NoRarityColor = Color.Parse("#4A515D");

    public static Color Rarity(string? rarity) => rarity?.ToLowerInvariant() switch
    {
        "common" => CommonColor,
        "uncommon" => UncommonColor,
        "rare" => RareColor,
        "epic" => EpicColor,
        "legendary" => LegendaryColor,
        "amplified" => AmplifiedColor,
        _ => NoRarityColor,
    };

    /// <summary>Rzadkość z koloru (kafelki dostają tylko RarityBrush).</summary>
    public static Controls.RarityTier TierOf(Color c) =>
        c == CommonColor ? Controls.RarityTier.Common
        : c == UncommonColor ? Controls.RarityTier.Uncommon
        : c == RareColor ? Controls.RarityTier.Rare
        : c == EpicColor ? Controls.RarityTier.Epic
        : c == LegendaryColor ? Controls.RarityTier.Legendary
        : c == AmplifiedColor ? Controls.RarityTier.Amplified
        : Controls.RarityTier.None;

    public static int RarityRank(string? rarity) => rarity?.ToLowerInvariant() switch
    {
        "amplified" => 0,
        "legendary" => 1,
        "epic" => 2,
        "rare" => 3,
        "uncommon" => 4,
        "common" => 5,
        _ => 6,
    };

    public static Color Trader(string? name) => name switch
    {
        "Shani" => Color.Parse("#36B5E6"),
        "Celeste" => Color.Parse("#FFC72C"),
        "TianWen" => Color.Parse("#FF7A45"),
        "Apollo" => Color.Parse("#3FD07A"),
        "Lance" => Color.Parse("#B57BFF"),
        "Ermal" => Color.Parse("#F0503F"),
        _ => Muted,
    };

    public static Color Status(QuestStatus s) => s switch
    {
        QuestStatus.Done => Green,
        QuestStatus.Available => Yellow,
        _ => LockedQuest,
    };

    public static string StatusText(QuestStatus s) => s switch
    {
        QuestStatus.Done => Loc.T("StatusDoneUpper"),
        QuestStatus.Available => Loc.T("StatusAvailableUpper"),
        _ => Loc.T("StatusLockedUpper"),
    };
}

public enum ToastKind { Info, Ok, Error }

public sealed class ToastViewModel(string text, ToastKind kind)
{
    public string Text { get; } = text;
    public IBrush Accent { get; } = Palette.Brush(kind switch
    {
        ToastKind.Ok => Palette.Green,
        ToastKind.Error => Palette.Red,
        _ => Palette.Yellow,
    });
}

/// <summary>Okno potwierdzenia / pytania z polem tekstowym (nakładka w głównym oknie).</summary>
public sealed partial class DialogViewModel : ObservableObject
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public required string Title { get; init; }
    public string Message { get; init; } = "";
    public string ConfirmText { get; init; } = "OK";
    public bool IsDanger { get; init; }
    public bool HasInput { get; init; }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    [ObservableProperty] private string _inputText = "";

    public Task<string?> Result => _tcs.Task;

    [RelayCommand]
    private void Confirm() => _tcs.TrySetResult(InputText ?? "");

    [RelayCommand]
    private void Cancel() => _tcs.TrySetResult(null);
}

/// <summary>Przedmiot z ilością (wymagany, nagroda) do wyświetlenia z ikoną.</summary>
public sealed class ItemChipViewModel(string itemId, string name, string? iconUrl, string? rarity, int quantity, Action<string>? open = null)
{
    public string ItemId { get; } = itemId;
    public string Name { get; } = name;
    public string? IconUrl { get; } = iconUrl;
    public bool IsBlueprint { get; } = Services.ItemKinds.IsBlueprint(itemId);
    public string QuantityText { get; } = "×" + quantity;
    public IBrush RarityBrush { get; } = Palette.Brush(Palette.Rarity(rarity));
    public IRelayCommand OpenCommand { get; } = new RelayCommand(() => open?.Invoke(itemId));
}

/// <summary>Link do innego questu (wymaga / odblokowuje).</summary>
public sealed class QuestLinkViewModel(string id, string name, bool done, Action<string> open)
{
    public string Name { get; } = name;
    public string Mark { get; } = done ? "✔" : "○";
    public IBrush MarkBrush { get; } = Palette.Brush(done ? Palette.Green : Palette.Muted);
    public IRelayCommand OpenCommand { get; } = new RelayCommand(() => open(id));
}

/// <summary>Subtask questu z przyciskiem "Zrobione".</summary>
public sealed partial class ObjectiveViewModel : ObservableObject
{
    private readonly Action<int, bool> _set;

    public ObjectiveViewModel(int index, string text, bool done, Action<int, bool> set, bool isHere = false)
    {
        Index = index;
        Text = text;
        _isDone = done;
        _set = set;
        IsHere = isHere;
    }

    public int Index { get; }
    public string Text { get; }

    /// <summary>Ten podpunkt dotyczy klikniętego znacznika na mapie — wyróżniony żółtym, pogrubionym tekstem.</summary>
    public bool IsHere { get; }
    public FontWeight TextWeight => IsHere ? FontWeight.Bold : FontWeight.Normal;
    public IBrush TextBrush => IsHere ? Palette.Brush(Palette.Yellow) : Palette.Brush(Palette.Text);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText), nameof(TextOpacity), nameof(IsNotDone))]
    private bool _isDone;

    public bool IsNotDone => !IsDone;
    public string ButtonText => IsDone ? Loc.T("Undo") : Loc.T("Done");
    public double TextOpacity => IsDone ? 0.45 : 1;

    [RelayCommand]
    private void Toggle() => _set(Index, !IsDone);
}

/// <summary>Kolor każdej strefy lootu (te same w filtrze mapy i w szczegółach itemu).</summary>
public static class LootZoneColors
{
    private static readonly Dictionary<string, Color> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Industrial"] = Color.Parse("#E88629"),
        ["Mechanical"] = Color.Parse("#C9A27A"),
        ["Electrical"] = Color.Parse("#FFC72C"),
        ["Technological"] = Color.Parse("#36B5E6"),
        ["Medical"] = Color.Parse("#FF6B81"),
        ["Security"] = Color.Parse("#7C86FF"),
        ["Commercial"] = Color.Parse("#B57BFF"),
        ["Residential"] = Color.Parse("#3FD07A"),
        ["Old World"] = Color.Parse("#A3E05A"),
        ["Exodus"] = Color.Parse("#5FC9D6"),
        ["Nature"] = Color.Parse("#26BF57"),
        ["ARC"] = Color.Parse("#F0503F"),
        ["Raider"] = Color.Parse("#9AA3AE"),
    };

    public static Color For(string zone) => Known.TryGetValue(zone, out var c) ? c : Palette.Muted;
}

/// <summary>Chip strefy lootu: kolorowa kropka + nazwa (+ liczba kontenerów na mapie). Klik = przełącz / pokaż na mapie.</summary>
public sealed partial class LootZoneChipViewModel(string zone, int? count, bool active, Action<string> click) : ObservableObject
{
    public string Zone { get; } = zone;
    public string Name { get; } = zone;
    public IBrush ColorBrush { get; } = Palette.Brush(LootZoneColors.For(zone));
    public string CountText { get; } = count is { } c ? c.ToString() : "";
    public bool HasCount => count is not null;

    [ObservableProperty] private bool _isActive = active;

    [RelayCommand]
    private void Click() => click(Zone);
}
