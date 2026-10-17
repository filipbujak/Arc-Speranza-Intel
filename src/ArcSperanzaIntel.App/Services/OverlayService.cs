using ArcSperanzaIntel.App.ViewModels;
using ArcSperanzaIntel.App.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ArcSperanzaIntel.App.Services;

/// <summary>
/// Overlay z szukanymi przedmiotami: osobne okienko zawsze na wierzchu (widać je nad grą w trybie okna bez ramek).
/// Nie czyta gry ani ekranu i nic do niej nie wstrzykuje — to zwykłe okno Windows.
/// </summary>
public sealed class OverlayService : IDisposable
{
    private readonly AppServices _services;
    private readonly OverlayViewModel _vm;
    private OverlayWindow? _window;
    private bool _disposed;

    public OverlayService(AppServices services)
    {
        _services = services;
        _vm = new OverlayViewModel(services.Wanted, Math.Clamp(services.State.Overlay.Opacity, 0.2, 1));
        services.Wanted.Changed += OnWantedChanged;
    }

    public bool IsVisible => _window?.IsVisible == true;

    private void OnWantedChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(_vm.Rebuild);

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Show();
    }

    public void Show()
    {
        _window ??= Create();
        _vm.Rebuild();
        SetLocked(true); // po każdym pokazaniu zablokowany — w grze mysz ma go ignorować
        _window.Show();
        // chwilowe przełączenie Topmost wyciąga okno nad grę w trybie okna bez ramek
        _window.Topmost = false;
        _window.Topmost = true;
    }

    public void Hide()
    {
        if (_window is null) return;
        Remember(_window);
        _window.Hide();
    }

    /// <summary>Skrót „zablokuj / odblokuj”. Na schowanym overlayu najpierw go pokazuje (odblokowany, do ustawienia).</summary>
    public void ToggleLock()
    {
        if (!IsVisible)
        {
            Show();
            SetLocked(false);
            return;
        }
        SetLocked(_vm.IsUnlocked);
    }

    private void SetLocked(bool locked)
    {
        _vm.IsUnlocked = !locked;
        _window?.SetLocked(locked);
        if (locked && _window is not null) Remember(_window);
    }

    /// <summary>Przezroczystość z ustawień (0.2–1), od razu widoczna na overlayu.</summary>
    public void SetOpacity(double value)
    {
        value = Math.Clamp(value, 0.2, 1);
        _services.State.Overlay.Opacity = value;
        _vm.PanelOpacity = value;
        _services.RequestSave();
    }

    /// <summary>Przywraca domyślne miejsce (prawy górny róg ekranu) — gdy overlay „uciekł” poza ekran.</summary>
    public void ResetPosition()
    {
        var st = _services.State.Overlay;
        st.X = null;
        st.Y = null;
        if (_window is not null) Place(_window);
        _services.RequestSave();
    }

    private OverlayWindow Create()
    {
        var st = _services.State.Overlay;
        var w = new OverlayWindow
        {
            DataContext = _vm,
            Width = Math.Max(200, st.Width),
            Height = Math.Max(140, st.Height),
        };
        Place(w);
        w.PositionChanged += (_, _) => Remember(w);
        w.SizeChanged += (_, _) => Remember(w);
        w.Closing += (_, e) =>
        {
            // Alt+F4 na overlayu tylko go chowa; zamknięcie aplikacji zamyka go naprawdę
            if (_disposed || e.CloseReason != WindowCloseReason.WindowClosing) return;
            e.Cancel = true;
            Hide();
        };
        return w;
    }

    private void Place(Window w)
    {
        var st = _services.State.Overlay;
        if (st.X is { } x && st.Y is { } y && IsOnScreen(w, x, y))
        {
            w.Position = new PixelPoint(x, y);
            return;
        }
        var screen = w.Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        w.Position = new PixelPoint(area.Right - (int)(w.Width * scale) - 24, area.Y + 80);
    }

    private static bool IsOnScreen(Window w, int x, int y) =>
        w.Screens.All.Any(s => s.WorkingArea.Contains(new PixelPoint(x + 20, y + 20)));

    private void Remember(Window w)
    {
        if (!w.IsVisible) return;
        var st = _services.State.Overlay;
        st.X = w.Position.X;
        st.Y = w.Position.Y;
        st.Width = w.ClientSize.Width;
        st.Height = w.ClientSize.Height;
        _services.RequestSave();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _services.Wanted.Changed -= OnWantedChanged;
        var w = _window;
        _window = null;
        w?.Close();
    }
}
