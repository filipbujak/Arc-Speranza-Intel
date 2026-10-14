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
    public static readonly Color LockedQuest = Color.Parse("#7A7462");

    private static readonly Dictionary<Color, IBrush> Brushes = [];

    public static IBrush Brush(Color c)
    {
        if (!Brushes.TryGetValue(c, out var b)) Brushes[c] = b = new SolidColorBrush(c);
        return b;
    }

    public static Color Rarity(string? rarity) => rarity?.ToLowerInvariant() switch
    {
        "common" => Color.Parse("#8E949E"),
        "uncommon" => Color.Parse("#45C46A"),
        "rare" => Color.Parse("#3E9BE8"),
        "epic" => Color.Parse("#B05CE8"),
        "legendary" => Color.Parse("#F2B230"),
        "amplified" => Color.Parse("#F0503F"),
        _ => Color.Parse("#4A515D"),
    };

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

    public ObjectiveViewModel(int index, string text, bool done, Action<int, bool> set)
    {
        Index = index;
        Text = text;
        _isDone = done;
        _set = set;
    }

    public int Index { get; }
    public string Text { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText), nameof(TextOpacity), nameof(IsNotDone))]
    private bool _isDone;

    public bool IsNotDone => !IsDone;
    public string ButtonText => IsDone ? Loc.T("Undo") : Loc.T("Done");
    public double TextOpacity => IsDone ? 0.45 : 1;

    [RelayCommand]
    private void Toggle() => _set(Index, !IsDone);
}
