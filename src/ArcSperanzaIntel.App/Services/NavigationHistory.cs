namespace ArcSperanzaIntel.App.Services;

/// <summary>
/// Historia „stron” jak w przeglądarce: Wstecz / Dalej. Nowa strona po cofnięciu ucina gałąź „do przodu”.
/// </summary>
public sealed class NavigationHistory<T> where T : class
{
    private const int Limit = 100;
    private readonly List<T> _entries = [];
    private int _index = -1;

    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index >= 0 && _index < _entries.Count - 1;

    /// <summary>Zapisuje bieżącą stronę (ta sama co ostatnia = nic nie robi).</summary>
    public void Record(T entry)
    {
        if (_index >= 0 && EqualityComparer<T>.Default.Equals(_entries[_index], entry)) return;
        if (_index < _entries.Count - 1) _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(entry);
        if (_entries.Count > Limit) _entries.RemoveAt(0);
        _index = _entries.Count - 1;
    }

    public T? Back() => CanGoBack ? _entries[--_index] : default;

    public T? Forward() => CanGoForward ? _entries[++_index] : default;
}
