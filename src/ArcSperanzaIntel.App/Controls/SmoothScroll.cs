using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>Płynne przewinięcie ScrollViewera do kontrolki z danym DataContext.</summary>
public static class SmoothScroll
{
    private static readonly Dictionary<ScrollViewer, DispatcherTimer> Running = [];

    public static void ToDataContext(ScrollViewer scroller, object dataContext, string requiredClass, double topMargin = 80)
    {
        // kontrolki mogą jeszcze nie istnieć (np. po zmianie filtra) — próbujemy po ułożeniu
        Dispatcher.UIThread.Post(() =>
        {
            var target = scroller.GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(c => ReferenceEquals(c.DataContext, dataContext) && c.Classes.Contains(requiredClass));
            if (target is null || scroller.Content is not Visual content) return;
            var pos = target.TranslatePoint(new Point(0, 0), content);
            if (pos is null) return;
            var maxY = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
            To(scroller, Math.Clamp(pos.Value.Y - topMargin, 0, maxY));
        }, DispatcherPriority.Background);
    }

    public static void To(ScrollViewer scroller, double y, int durationMs = 450)
    {
        if (Running.Remove(scroller, out var old)) old.Stop();
        var from = scroller.Offset.Y;
        var t0 = DateTime.UtcNow;
        var timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
        timer.Tick += (_, _) =>
        {
            var t = Math.Min(1, (DateTime.UtcNow - t0).TotalMilliseconds / durationMs);
            var e = 1 - Math.Pow(1 - t, 3); // easeOutCubic
            scroller.Offset = new Vector(scroller.Offset.X, from + (y - from) * e);
            if (t >= 1)
            {
                timer.Stop();
                Running.Remove(scroller);
            }
        };
        Running[scroller] = timer;
        timer.Start();
    }
}
