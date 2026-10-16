using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>
/// Wjazd zakładki z boku: nowa zakładka dalej w pasku wjeżdża z prawej, wcześniejsza — z lewej.
/// Krótki ruch (40 px, ~180 ms) połączony z rozjaśnieniem; samo przesunięcie (RenderTransform) nie przelicza układu.
/// </summary>
public sealed class TabSlide
{
    private const double Distance = 40;
    private const double DurationMs = 180;

    private readonly Panel _host;
    private Control? _target;
    private Stopwatch? _clock;
    private double _from;

    public TabSlide(Panel host) => _host = host;

    /// <summary>Uruchamia wjazd dziecka o danym indeksie; kierunek wynika z porównania z poprzednim indeksem.</summary>
    public void Play(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || toIndex < 0 || toIndex >= _host.Children.Count) return;
        Finish();
        _target = _host.Children[toIndex];
        _from = toIndex > fromIndex ? Distance : -Distance;
        _clock = Stopwatch.StartNew();
        Apply(0);
        Tick();
    }

    private void Tick()
    {
        if (_target is null || _clock is null) return;
        var top = TopLevel.GetTopLevel(_host);
        if (top is null) { Finish(); return; }
        top.RequestAnimationFrame(_ =>
        {
            if (_target is null || _clock is null) return;
            var p = Math.Clamp(_clock.Elapsed.TotalMilliseconds / DurationMs, 0, 1);
            if (p >= 1) { Finish(); return; }
            Apply(1 - Math.Pow(1 - p, 3)); // ease-out
            Tick();
        });
    }

    private void Apply(double eased)
    {
        if (_target is null) return;
        _target.RenderTransform = new TranslateTransform(_from * (1 - eased), 0);
        _target.Opacity = 0.35 + 0.65 * eased;
    }

    private void Finish()
    {
        if (_target is not null)
        {
            _target.RenderTransform = null;
            _target.Opacity = 1;
        }
        _target = null;
        _clock = null;
    }
}
