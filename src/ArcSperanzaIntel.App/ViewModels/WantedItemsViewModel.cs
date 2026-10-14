using System.Collections.ObjectModel;
using ArcSperanzaIntel.Core.Quests;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>Panel "Szukane itemy" — wspólny dla zakładek Mapy, Questy i Itemy.</summary>
public sealed partial class WantedItemsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public WantedItemsViewModel(MainViewModel main)
    {
        _main = main;
        _isExpanded = main.Services.State.WantedPanelExpanded;
        main.Services.Wanted.Changed += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<WantedGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BodyMaxHeight), nameof(IsCollapsed))]
    private bool _isExpanded;

    [ObservableProperty] private int _totalItems;
    [ObservableProperty] private bool _isEmpty;

    public bool IsCollapsed => !IsExpanded;

    /// <summary>Wysokość treści — animowana w widoku (płynne rozwijanie).</summary>
    public double BodyMaxHeight => IsExpanded ? 620 : 0;

    partial void OnIsExpandedChanged(bool value)
    {
        _main.Services.State.WantedPanelExpanded = value;
        _main.Services.RequestSave();
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    public void Refresh()
    {
        var s = _main.Services;
        var groups = s.Wanted.Groups();
        Groups.Clear();
        foreach (var g in groups)
        {
            Groups.Add(new WantedGroupViewModel(g, s.State.CollapsedWantedGroups.Contains(g.Key), this));
        }
        TotalItems = groups.Sum(g => g.Items.Count);
        IsEmpty = groups.Count == 0;
    }

    internal void SetGroupCollapsed(string key, bool collapsed)
    {
        var set = _main.Services.State.CollapsedWantedGroups;
        if (collapsed) set.Add(key); else set.Remove(key);
        _main.Services.RequestSave();
    }

    internal void ChangeQuantity(string itemId, int delta) => _main.Services.Wanted.ChangeQuantity(itemId, delta);
    internal void Remove(string itemId) => _main.Services.Wanted.Unmark(itemId);
    internal void OpenItem(string itemId) => _main.ShowItem(itemId);
    internal void OpenQuest(string questId) => _main.ShowQuestInTree(questId);
}

public sealed partial class WantedGroupViewModel : ObservableObject
{
    private readonly WantedItemsViewModel _owner;

    public WantedGroupViewModel(WantedGroup group, bool collapsed, WantedItemsViewModel owner)
    {
        _owner = owner;
        Key = group.Key;
        Title = group.Title;
        IsManual = group.IsManual;
        _isCollapsed = collapsed;
        Items = group.Items.Select(i => new WantedItemViewModel(i, group.IsManual, owner)).ToList();
    }

    public string Key { get; }
    public string Title { get; }
    public bool IsManual { get; }
    public bool IsQuest => !IsManual;
    public IReadOnlyList<WantedItemViewModel> Items { get; }
    public IBrush TitleBrush => Palette.Brush(IsManual ? Palette.Blue : Palette.Yellow);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private bool _isCollapsed;

    public bool IsOpen => !IsCollapsed;

    partial void OnIsCollapsedChanged(bool value) => _owner.SetGroupCollapsed(Key, value);

    [RelayCommand]
    private void ToggleCollapsed() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    private void OpenQuest()
    {
        if (IsQuest) _owner.OpenQuest(Key);
    }
}

public sealed partial class WantedItemViewModel(WantedItem item, bool isManual, WantedItemsViewModel owner) : ObservableObject
{
    public string Name { get; } = item.Name;
    public string? IconUrl { get; } = item.IconUrl;
    public bool IsBlueprint { get; } = Services.ItemKinds.IsBlueprint(item.ItemId);
    public string QuantityText { get; } = "×" + item.Quantity;
    public bool IsManual { get; } = isManual;
    public IBrush RarityBrush { get; } = Palette.Brush(Palette.Rarity(item.Rarity));

    [RelayCommand] private void Plus() => owner.ChangeQuantity(item.ItemId, 1);
    [RelayCommand] private void Minus() => owner.ChangeQuantity(item.ItemId, -1);
    [RelayCommand] private void Remove() => owner.Remove(item.ItemId);
    [RelayCommand] private void Open() => owner.OpenItem(item.ItemId);
}
