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

    public OverlayWindow()
    {
        InitializeComponent();
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        DragBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        ResizeGrip.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginResizeDrag(WindowEdge.SouthEast, e);
                e.Handled = true;
            }
        };
        Opened += (_, _) => MakeNonActivating();
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
