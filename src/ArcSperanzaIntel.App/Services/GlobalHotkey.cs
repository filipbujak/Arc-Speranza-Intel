using ArcSperanzaIntel.Core.Localization;
using System.Runtime.InteropServices;
using Avalonia.Input;

namespace ArcSperanzaIntel.App.Services;

/// <summary>
/// Globalny skrót Windows (RegisterHotKey) — działa także, gdy aktywna jest gra.
/// Rejestracja na osobnym wątku z własną pętlą komunikatów, niezależnie od okna Avalonii.
/// To zwykłe API Windows (jak w Discordzie czy OBS); nie dotyka procesu gry.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 0xA7C0;
    private const uint WmHotkey = 0x0312;
    private const uint WmQuit = 0x0012;
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8, ModNoRepeat = 0x4000;

    private Thread? _thread;
    private uint _threadId;

    public event Action? Pressed;

    /// <summary>Rejestruje skrót (podmienia poprzedni). Zwraca komunikat błędu albo null.</summary>
    public Task<string?> RegisterAsync(KeyGesture? gesture)
    {
        Unregister();
        if (gesture is null) return Task.FromResult<string?>(null);
        if (!OperatingSystem.IsWindows()) return Task.FromResult<string?>(Loc.T("HotkeyWindowsOnly"));

        var vk = ToVirtualKey(gesture.Key);
        if (vk == 0) return Task.FromResult<string?>(Loc.F("HotkeyUnsupported", gesture.Key));
        var mods = ModNoRepeat;
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Control)) mods |= ModControl;
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Shift)) mods |= ModShift;
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Alt)) mods |= ModAlt;
        if (gesture.KeyModifiers.HasFlag(KeyModifiers.Meta)) mods |= ModWin;

        var tcs = new TaskCompletionSource<string?>();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            if (!RegisterHotKey(IntPtr.Zero, HotkeyId, mods, vk))
            {
                tcs.TrySetResult(Loc.T("HotkeyTaken"));
                return;
            }
            tcs.TrySetResult(null);
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WmHotkey && (int)msg.wParam == HotkeyId)
                    Pressed?.Invoke();
            }
            UnregisterHotKey(IntPtr.Zero, HotkeyId);
        })
        {
            IsBackground = true,
            Name = "GlobalHotkey",
        };
        _thread.Start();
        return tcs.Task;
    }

    public void Unregister()
    {
        if (_thread is null) return;
        if (_threadId != 0) PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(500);
        _thread = null;
        _threadId = 0;
    }

    public void Dispose() => Unregister();

    /// <summary>Klawisz Avalonii → kod wirtualny Windows.</summary>
    private static uint ToVirtualKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (uint)('A' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (uint)('0' + (key - Key.D0)),
        >= Key.NumPad0 and <= Key.NumPad9 => (uint)(0x60 + (key - Key.NumPad0)),
        >= Key.F1 and <= Key.F24 => (uint)(0x70 + (key - Key.F1)),
        Key.Space => 0x20,
        Key.Tab => 0x09,
        Key.Insert => 0x2D,
        Key.Delete => 0x2E,
        Key.Home => 0x24,
        Key.End => 0x23,
        Key.PageUp => 0x21,
        Key.PageDown => 0x22,
        Key.OemTilde => 0xC0,
        Key.OemMinus => 0xBD,
        Key.OemPlus => 0xBB,
        Key.OemComma => 0xBC,
        Key.OemPeriod => 0xBE,
        Key.Pause => 0x13,
        Key.Scroll => 0x91,
        _ => 0,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    private static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
