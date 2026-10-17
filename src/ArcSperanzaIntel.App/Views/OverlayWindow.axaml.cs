using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;

namespace ArcSperanzaIntel.App.Views;

/// <summary>
/// Okienko overlaya: bez ramki, zawsze na wierzchu, nie kradnie fokusu grze. Przesuwane za pasek, rozmiar za róg.
/// Klik w overlay nie aktywuje go (WS_EX_NOACTIVATE), więc gra zostaje na pierwszym planie i się nie minimalizuje.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExLayered = 0x00080000L;

    private static readonly Cursor NoCursor = new(StandardCursorType.None);
    private bool _locked;

    public OverlayWindow()
    {
        InitializeComponent();
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        DragBar.PointerPressed += (_, e) =>
        {
            if (!_locked && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        ResizeGrip.PointerPressed += (_, e) =>
        {
            if (!_locked && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginResizeDrag(WindowEdge.SouthEast, e);
                e.Handled = true;
            }
        };
        Opened += (_, _) =>
        {
            MakeNonActivating();
            ApplyLock();
        };
    }

    /// <summary>
    /// Zablokowany overlay: kursor nad nim jest ukryty, okno nie reaguje na mysz (brak dymków i przeciągania),
    /// a jeśli okno jest warstwowe — kliknięcia przechodzą do gry. Dzięki temu przy dwóch monitorach
    /// niewidoczny kursor gry nie „wyskakuje” jako strzałka, gdy przejedzie nad overlayem.
    /// </summary>
    public void SetLocked(bool locked)
    {
        _locked = locked;
        ApplyLock();
    }

    private void ApplyLock()
    {
        Cursor = _locked ? NoCursor : Cursor.Default;
        Root.IsHitTestVisible = !_locked;
        SetClickThrough(_locked);
    }

    /// <summary>
    /// WS_EX_TRANSPARENT działa tylko z WS_EX_LAYERED. Dokładamy go wyłącznie wtedy, gdy okno już jest warstwowe —
    /// wymuszenie warstwowości na przezroczystym oknie Avalonii mogłoby je ukryć. Bez tego zostaje ukryty kursor.
    /// </summary>
    private void SetClickThrough(bool on)
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero) return;
        try
        {
            var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            if ((style & WsExLayered) == 0) return;
            var next = on ? style | WsExTransparent : style & ~WsExTransparent;
            if (next != style) SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(next));
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "OverlayClickThrough");
        }
    }

    /// <summary>
    /// Zwykłe style okna Windows: nie aktywuje się po kliknięciu i nie ma go w Alt+Tab.
    /// Nie dotyka gry — zmienia tylko nasze okno.
    /// </summary>
    private void MakeNonActivating()
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero) return;
        try
        {
            var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style | WsExNoActivate | WsExToolWindow));
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "OverlayNoActivate");
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
