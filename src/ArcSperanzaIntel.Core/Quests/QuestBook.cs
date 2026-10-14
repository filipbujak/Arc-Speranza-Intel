using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Quests;

public enum QuestStatus { Locked, Available, Done }

/// <summary>Wynik zmiany postępu — do komunikatu dla użytkownika.</summary>
public sealed record QuestChange(string QuestId, bool NowDone, IReadOnlyList<string> AlsoChanged);

/// <summary>
/// Graf questów i postęp gracza.
/// Zasady: ukończenie questu zaznacza wszystkie wymagane wcześniej; odznaczenie cofa wszystkie zależne.
/// Subtaski: komplet zrobionych → quest ukończony automatycznie; odznaczenie subtaska ukończonego questu cofa quest.
/// </summary>
public sealed class QuestBook
{
    private readonly Dictionary<string, Quest> _byId;
    private readonly Dictionary<string, List<string>> _parents = [];
    private readonly Dictionary<string, List<string>> _children = [];
    private readonly QuestProgress _progress;

    public IReadOnlyList<Quest> Quests { get; }
    public QuestGraphLayout Layout { get; }

    /// <summary>Zmiana stanu dowolnego questu lub subtaska.</summary>
    public event EventHandler? Changed;

    public QuestBook(IReadOnlyList<Quest> quests, QuestProgress progress)
    {
        Quests = quests;
        _progress = progress;
        _byId = quests.GroupBy(q => q.Id).ToDictionary(g => g.Key, g => g.First());
        foreach (var q in _byId.Values) _children[q.Id] = [];
        foreach (var q in _byId.Values)
        {
            var pre = q.PrerequisiteIds.Where(id => id != q.Id && _byId.ContainsKey(id)).Distinct().ToList();
            _parents[q.Id] = pre;
            foreach (var p in pre) _children[p].Add(q.Id);
        }
        Layout = QuestGraphLayout.Build(_byId.Values.ToList(), _parents);
    }

    public Quest? Get(string id) => _byId.GetValueOrDefault(id);
    public bool Contains(string id) => _byId.ContainsKey(id);
    public IReadOnlyList<string> ParentsOf(string id) => _parents.GetValueOrDefault(id) ?? [];
    public IReadOnlyList<string> ChildrenOf(string id) => _children.GetValueOrDefault(id) ?? [];

    public bool IsDone(string id) => _progress.Completed.ContainsKey(id);

    public QuestStatus StatusOf(string id)
    {
        if (IsDone(id)) return QuestStatus.Done;
        return ParentsOf(id).All(IsDone) ? QuestStatus.Available : QuestStatus.Locked;
    }

    public (int Done, int Available, int Locked, int Total) Counts()
    {
        int done = 0, available = 0, locked = 0;
        foreach (var q in _byId.Values)
        {
            switch (StatusOf(q.Id))
            {
                case QuestStatus.Done: done++; break;
                case QuestStatus.Available: available++; break;
                default: locked++; break;
            }
        }
        return (done, available, locked, _byId.Count);
    }

    public IEnumerable<Quest> WithStatus(QuestStatus status) => _byId.Values.Where(q => StatusOf(q.Id) == status);

    public HashSet<string> Ancestors(string id) => Walk(id, _parents);
    public HashSet<string> Descendants(string id) => Walk(id, _children);

    private static HashSet<string> Walk(string id, Dictionary<string, List<string>> edges)
    {
        var seen = new HashSet<string>();
        var stack = new Stack<string>(edges.GetValueOrDefault(id) ?? []);
        while (stack.Count > 0)
        {
            var next = stack.Pop();
            if (!seen.Add(next)) continue;
            foreach (var e in edges.GetValueOrDefault(next) ?? []) stack.Push(e);
        }
        return seen;
    }

    // ---------------------------------------------------------------- questy
    public QuestChange Complete(string id)
    {
        if (!_byId.ContainsKey(id)) return new QuestChange(id, false, []);
        var now = DateTimeOffset.Now;
        var extra = Ancestors(id).Where(a => !IsDone(a)).ToList();
        foreach (var a in extra) MarkDone(a, now);
        MarkDone(id, now);
        Changed?.Invoke(this, EventArgs.Empty);
        return new QuestChange(id, true, extra);
    }

    public QuestChange Uncomplete(string id)
    {
        var extra = Descendants(id).Where(IsDone).ToList();
        foreach (var d in extra) MarkUndone(d);
        MarkUndone(id);
        Changed?.Invoke(this, EventArgs.Empty);
        return new QuestChange(id, false, extra);
    }

    public QuestChange Toggle(string id) => IsDone(id) ? Uncomplete(id) : Complete(id);

    private void MarkDone(string id, DateTimeOffset when)
    {
        _progress.Completed[id] = when;
        var n = _byId[id].Objectives.Count;
        if (n > 0) _progress.Objectives[id] = Enumerable.Repeat(true, n).ToList();
    }

    private void MarkUndone(string id)
    {
        _progress.Completed.Remove(id);
        _progress.Objectives.Remove(id);
    }

    // ---------------------------------------------------------------- subtaski
    public IReadOnlyList<bool> ObjectiveStates(string id)
    {
        var q = Get(id);
        if (q is null) return [];
        var stored = _progress.Objectives.GetValueOrDefault(id);
        var done = IsDone(id);
        return Enumerable.Range(0, q.Objectives.Count)
            .Select(i => done || (stored is not null && i < stored.Count && stored[i]))
            .ToList();
    }

    public bool IsObjectiveDone(string id, int index)
    {
        var states = ObjectiveStates(id);
        return index >= 0 && index < states.Count && states[index];
    }

    /// <summary>Ustawia subtask. Zwraca zmianę questu, jeśli przez to quest się ukończył albo cofnął.</summary>
    public QuestChange? SetObjective(string id, int index, bool done)
    {
        var q = Get(id);
        if (q is null || index < 0 || index >= q.Objectives.Count) return null;

        var states = ObjectiveStates(id).ToList();
        states[index] = done;

        if (!done && IsDone(id))
        {
            // Cofnięcie subtaska ukończonego questu: quest (i zależne) wracają do "dostępny", reszta subtasków zostaje.
            var change = Uncomplete(id);
            _progress.Objectives[id] = states;
            Changed?.Invoke(this, EventArgs.Empty);
            return change;
        }

        _progress.Objectives[id] = states;
        if (done && states.All(s => s) && !IsDone(id)) return Complete(id);

        Changed?.Invoke(this, EventArgs.Empty);
        return null;
    }

    public void ResetAll()
    {
        _progress.Completed.Clear();
        _progress.Objectives.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
