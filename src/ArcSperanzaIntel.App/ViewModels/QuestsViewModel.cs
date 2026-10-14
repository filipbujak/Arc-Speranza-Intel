using ArcSperanzaIntel.Core.Localization;
using System.Collections.ObjectModel;
using ArcSperanzaIntel.App.Controls;
using ArcSperanzaIntel.Core.Models;
using ArcSperanzaIntel.Core.Quests;
using ArcSperanzaIntel.Core.Util;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArcSperanzaIntel.App.ViewModels;

/// <summary>Zakładka Questy: duży graf, wyszukiwarka z podpowiedziami, filtr handlarzy, szczegóły questu.</summary>
public sealed partial class QuestsViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private string? _selectedId;

    public QuestsViewModel(MainViewModel main)
    {
        _main = main;
        var traders = Book.Quests.Select(q => q.Trader).Where(t => t.Length > 0).Distinct().OrderBy(t => t);
        Traders = traders.Select(t => new TraderChipViewModel(t, this)).ToList();
        Edges = Book.Quests.SelectMany(q => Book.ParentsOf(q.Id).Select(p => new QuestEdge(p, q.Id))).ToList();
        RefreshAll();
    }

    private QuestBook Book => _main.Services.Quests;
    public WantedItemsViewModel? Wanted => _main.Wanted;

    /// <summary>Prośby do widoku: przelot do questu, dopasowanie całego grafu.</summary>
    public event Action<string>? FlyToRequested;
    public event Action? FitRequested;

    // ---------------------------------------------------------------- graf
    [ObservableProperty] private IReadOnlyList<QuestNodeVisual> _nodes = [];
    public IReadOnlyList<QuestEdge> Edges { get; }
    public IReadOnlyList<TraderChipViewModel> Traders { get; }

    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _countsText = "";
    public ObservableCollection<QuestListItemViewModel> AvailableQuests { get; } = [];

    public void RefreshAll()
    {
        var activeTraders = Traders.Where(t => t.IsOn).Select(t => t.Name).ToHashSet();
        var filterTraders = activeTraders.Count > 0 && activeTraders.Count < Traders.Count;
        var term = SearchText.Trim();

        var nodes = new List<QuestNodeVisual>(Book.Quests.Count);
        foreach (var q in Book.Quests)
        {
            if (!Book.Layout.TryGet(q.Id, out var p)) continue;
            var states = Book.ObjectiveStates(q.Id);
            var maps = _main.Services.MapIndex.MapsFor(q).Select(m => m.ShortName);
            nodes.Add(new QuestNodeVisual
            {
                Id = q.Id,
                Name = q.Name,
                Subtitle = q.Trader + (maps.Any() ? "  ·  " + string.Join(", ", maps) : ""),
                TraderColor = Palette.Trader(q.Trader),
                Status = Book.StatusOf(q.Id),
                X = p.X,
                Y = p.Y,
                ObjectivesDone = states.Count(s => s),
                ObjectivesTotal = states.Count,
                Dimmed = filterTraders && !activeTraders.Contains(q.Trader),
                Selected = q.Id == _selectedId,
                Matched = term.Length >= 2 && TextFormat.ContainsLoose(q.Name, term),
            });
        }
        Nodes = nodes;

        var (done, available, locked, total) = Book.Counts();
        ProgressPercent = total == 0 ? 0 : 100.0 * done / total;
        ProgressText = $"{done} / {total}";
        CountsText = Loc.F("QuestCounts", available, locked);

        AvailableQuests.Clear();
        foreach (var q in Book.WithStatus(QuestStatus.Available).OrderBy(q => q.Trader).ThenBy(q => q.Name))
            AvailableQuests.Add(new QuestListItemViewModel(q, this));

        if (Selected is not null && _selectedId is not null) Selected = BuildDetail(_selectedId);
    }

    // ---------------------------------------------------------------- wybór i akcje
    [ObservableProperty] private QuestDetailViewModel? _selected;

    public void Select(string id)
    {
        _selectedId = id;
        Selected = BuildDetail(id);
        RefreshAll();
    }

    public void Focus(string id)
    {
        Select(id);
        FlyToRequested?.Invoke(id);
    }

    public void ClearSelection()
    {
        _selectedId = null;
        Selected = null;
        RefreshAll();
    }

    private QuestDetailViewModel? BuildDetail(string id)
    {
        var q = Book.Get(id);
        return q is null ? null : new QuestDetailViewModel(q, this, _main);
    }

    public void ToggleQuest(string id)
    {
        _main.ReportQuestChange(Book.Toggle(id));
        _main.OnQuestProgressChanged();
    }

    [RelayCommand]
    private void Fit() => FitRequested?.Invoke();

    internal void TraderFilterChanged() => RefreshAll();

    // ---------------------------------------------------------------- wyszukiwarka z podpowiedziami
    [ObservableProperty] private string _searchText = "";
    public ObservableCollection<QuestListItemViewModel> Suggestions { get; } = [];
    [ObservableProperty] private bool _hasSuggestions;

    partial void OnSearchTextChanged(string value)
    {
        Suggestions.Clear();
        var term = value.Trim();
        if (term.Length >= 2)
        {
            foreach (var q in Book.Quests
                         .Where(q => TextFormat.ContainsLoose(q.Name, term) || TextFormat.ContainsLoose(q.Trader, term))
                         .OrderBy(q => q.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(q => q.Name)
                         .Take(12))
                Suggestions.Add(new QuestListItemViewModel(q, this));
        }
        HasSuggestions = Suggestions.Count > 0;
        RefreshAll();
    }

    /// <summary>Klik w podpowiedź: płynny przelot i przybliżenie do questu.</summary>
    internal void Pick(string id)
    {
        HasSuggestions = false;
        Focus(id);
    }

    [RelayCommand]
    private void PickFirst()
    {
        if (Suggestions.Count > 0) Pick(Suggestions[0].Id);
    }

    internal string StatusOf(string id) => Palette.StatusText(Book.StatusOf(id));
    internal IBrush StatusBrush(string id) => Palette.Brush(Palette.Status(Book.StatusOf(id)));
}

public sealed partial class TraderChipViewModel(string name, QuestsViewModel owner) : ObservableObject
{
    public string Name { get; } = name;
    public IBrush ColorBrush { get; } = Palette.Brush(Palette.Trader(name));

    [ObservableProperty] private bool _isOn;

    partial void OnIsOnChanged(bool value) => owner.TraderFilterChanged();
}

public sealed partial class QuestListItemViewModel(Quest quest, QuestsViewModel owner) : ObservableObject
{
    public string Id { get; } = quest.Id;
    public string Name { get; } = quest.Name;
    public string Trader { get; } = quest.Trader;
    public IBrush TraderBrush { get; } = Palette.Brush(Palette.Trader(quest.Trader));
    public string Status { get; } = owner.StatusOf(quest.Id);
    public IBrush StatusBrush { get; } = owner.StatusBrush(quest.Id);

    [RelayCommand]
    private void Open() => owner.Pick(Id);
}

/// <summary>Box szczegółów questu: subtaski, itemy, nagrody, wymagania, przejście do mapy.</summary>
public sealed partial class QuestDetailViewModel : ObservableObject
{
    private readonly QuestsViewModel _owner;
    private readonly MainViewModel _main;

    public QuestDetailViewModel(Quest quest, QuestsViewModel owner, MainViewModel main)
    {
        Quest = quest;
        _owner = owner;
        _main = main;
        var book = main.Services.Quests;
        var status = book.StatusOf(quest.Id);

        StatusText = Palette.StatusText(status);
        StatusBrush = Palette.Brush(Palette.Status(status));
        IsDone = status == QuestStatus.Done;
        TraderBrush = Palette.Brush(Palette.Trader(quest.Trader));

        var states = book.ObjectiveStates(quest.Id);
        Objectives = quest.Objectives.Select((o, i) => new ObjectiveViewModel(i, o, states[i], SetObjective)).ToList();
        RequiredItems = quest.RequiredItems.Select(Chip).ToList();
        Rewards = quest.Rewards.Select(Chip).ToList();
        GrantedItems = quest.GrantedItems.Select(Chip).ToList();
        CurrencyText = string.Join("   ", quest.CurrencyRewards.Select(c => $"+{TextFormat.Number(c.Quantity)} {c.Name}"));

        Prerequisites = book.ParentsOf(quest.Id).Select(id => Link(book, id)).ToList();
        Unlocks = book.ChildrenOf(quest.Id).Select(id => Link(book, id)).ToList();

        var index = main.Services.MapIndex;
        Maps = index.MapsFor(quest)
            .Select(m => new MapChoiceViewModel(m.Name, index.HasMarkersOn(quest.Id, m.Id), () => main.ShowQuestOnMap(quest.Id, m.Id)))
            .ToList();
    }

    private ItemChipViewModel Chip(ItemStack s) =>
        new(s.ItemId, s.Name, s.IconUrl ?? _main.Services.Data.FindItem(s.ItemId)?.IconUrl, s.Rarity, s.Quantity, _main.ShowItem);

    private QuestLinkViewModel Link(QuestBook book, string id) =>
        new(id, book.Get(id)?.Name ?? id, book.IsDone(id), _owner.Focus);

    public Quest Quest { get; }
    public string Name => Quest.Name;
    public string Trader => Quest.Trader;
    public string Description => Quest.Description;
    public bool HasDescription => Quest.Description.Length > 0;
    public string? ImageUrl => Quest.ImageUrl;
    public bool HasImage => Quest.ImageUrl is not null;
    public string StatusText { get; }
    public IBrush StatusBrush { get; }
    public IBrush TraderBrush { get; }
    public bool IsDone { get; }
    public string ToggleText => IsDone ? Loc.T("UncheckQuestBtn") : Loc.T("CompleteQuestBtn");

    public IReadOnlyList<ObjectiveViewModel> Objectives { get; }
    public bool HasObjectives => Objectives.Count > 0;
    public IReadOnlyList<ItemChipViewModel> RequiredItems { get; }
    public bool HasRequiredItems => RequiredItems.Count > 0;
    public IReadOnlyList<ItemChipViewModel> Rewards { get; }
    public IReadOnlyList<ItemChipViewModel> GrantedItems { get; }
    public bool HasGrantedItems => GrantedItems.Count > 0;
    public bool HasRewards => Rewards.Count > 0 || CurrencyText.Length > 0;
    public string CurrencyText { get; }
    public IReadOnlyList<QuestLinkViewModel> Prerequisites { get; }
    public bool HasPrerequisites => Prerequisites.Count > 0;
    public IReadOnlyList<QuestLinkViewModel> Unlocks { get; }
    public bool HasUnlocks => Unlocks.Count > 0;
    public IReadOnlyList<MapChoiceViewModel> Maps { get; }

    [ObservableProperty] private bool _isMapChoiceOpen;

    private void SetObjective(int index, bool done)
    {
        var change = _main.Services.Quests.SetObjective(Quest.Id, index, done);
        if (change is not null) _main.ReportQuestChange(change);
        _main.OnQuestProgressChanged();
    }

    [RelayCommand]
    private void ToggleQuest() => _owner.ToggleQuest(Quest.Id);

    /// <summary>Jedna mapa → od razu; kilka → wybór; brak → komunikat.</summary>
    [RelayCommand]
    private void GoToMap()
    {
        if (Maps.Count == 0)
        {
            _main.Toast(Loc.T("QuestNoMap"));
            return;
        }
        if (Maps.Count == 1)
        {
            Maps[0].GoCommand.Execute(null);
            return;
        }
        IsMapChoiceOpen = !IsMapChoiceOpen;
    }

    [RelayCommand]
    private void Close() => _owner.ClearSelection();

    [RelayCommand]
    private void OpenGuide()
    {
        if (Quest.GuideUrl is not null) MainViewModel.OpenUrl("https://metaforge.app" + Quest.GuideUrl);
    }

    public bool HasGuide => Quest.GuideUrl is not null;
}

public sealed class MapChoiceViewModel(string name, bool hasMarkers, Action go)
{
    public string Name { get; } = name;
    public string Hint { get; } = hasMarkers ? Loc.T("HintMarkers") : Loc.T("HintMentioned");
    public IRelayCommand GoCommand { get; } = new RelayCommand(go);
}
