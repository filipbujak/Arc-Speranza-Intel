using System.Collections.ObjectModel;
using ArcSperanzaIntel.Core.Localization;
using ArcSperanzaIntel.Core.Quests;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>
/// Overlay „Szukane”: wszystkie śledzone grupy zsumowane w jedną listę (ten sam przedmiot z kilku questów = jedna pozycja).
/// Tylko podgląd — liczniki zmienia się w aplikacji.
/// </summary>
public sealed partial class OverlayViewModel : ObservableObject
{
    private readonly WantedItemsService _wanted;

    public OverlayViewModel(WantedItemsService wanted, double opacity)
    {
        _wanted = wanted;
        _panelOpacity = opacity;
        Rebuild();
    }

    public ObservableCollection<OverlayItemViewModel> Items { get; } = [];

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private double _panelOpacity;

    /// <summary>Odblokowany = można przesuwać i zmieniać rozmiar (żółta ramka + znacznik). Domyślnie zablokowany.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameBrush), nameof(FrameThickness))]
    private bool _isUnlocked;

    public string UnlockedText => Loc.T("OverlayUnlocked");
    public IBrush FrameBrush => IsUnlocked ? Palette.Brush(Palette.Yellow) : DimFrame;
    public Avalonia.Thickness FrameThickness => new(IsUnlocked ? 2 : 1);
    private static readonly IBrush DimFrame = new SolidColorBrush(Color.Parse("#40FFC72C"));

    public string Title => Loc.T("OverlayTitle");
    public string EmptyText => Loc.T("OverlayEmpty");

    internal void Rebuild()
    {
        var items = _wanted.Groups()
            .Where(g => g.IsTracked)
            .SelectMany(g => g.Items.Select(i => (Item: i, Source: g.Title)))
            .GroupBy(x => x.Item.ItemId)
            .Select(g => new OverlayItemViewModel(
                g.First().Item,
                g.Sum(x => x.Item.Quantity),
                string.Join(", ", g.Select(x => x.Source).Distinct())))
            .OrderBy(i => Palette.RarityRank(i.Rarity))
            .ThenBy(i => i.Name)
            .ToList();
        Items.Clear();
        foreach (var i in items) Items.Add(i);
        IsEmpty = Items.Count == 0;
        CountText = Items.Count.ToString();
    }
}

public sealed class OverlayItemViewModel(WantedItem item, int quantity, string sources)
{
    public string Name { get; } = item.Name;
    public string? IconUrl { get; } = item.IconUrl;
    public string? Rarity { get; } = item.Rarity;
    public IBrush RarityBrush { get; } = Palette.Brush(Palette.Rarity(item.Rarity));
    public bool IsBlueprint { get; } = Services.ItemKinds.IsBlueprint(item.ItemId);
    public string QuantityText { get; } = "×" + quantity;
    public string Sources { get; } = sources;
}
