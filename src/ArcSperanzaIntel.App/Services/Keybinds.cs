using ArcSperanzaIntel.Core.Localization;
using Avalonia.Input;

namespace ArcSperanzaIntel.App.Services;

public enum KeybindAction
{
    ToggleWindow,
    ToggleOverlay,
    TabMaps,
    TabQuests,
    TabItems,
    TabShops,
    TabWorkshop,
    TabEvents,
    TabSettings,
}

/// <summary>Skróty klawiszowe: domyślne, zapis do stanu, dopasowanie do naciśniętych klawiszy.</summary>
public sealed class Keybinds(Dictionary<string, string> store)
{
    public static readonly IReadOnlyDictionary<KeybindAction, string> Defaults = new Dictionary<KeybindAction, string>
    {
        [KeybindAction.ToggleWindow] = "Ctrl+F",
        [KeybindAction.ToggleOverlay] = "Ctrl+Shift+O",
        [KeybindAction.TabMaps] = "Ctrl+D1",
        [KeybindAction.TabQuests] = "Ctrl+D2",
        [KeybindAction.TabItems] = "Ctrl+D3",
        [KeybindAction.TabShops] = "Ctrl+D4",
        [KeybindAction.TabWorkshop] = "Ctrl+D5",
        [KeybindAction.TabEvents] = "Ctrl+D6",
        [KeybindAction.TabSettings] = "Ctrl+D7",
    };

    public static string Describe(KeybindAction a) => a switch
    {
        KeybindAction.ToggleWindow => Loc.T("KbToggle"),
        KeybindAction.ToggleOverlay => Loc.T("KbOverlay"),
        KeybindAction.TabMaps => Loc.T("KbMaps"),
        KeybindAction.TabQuests => Loc.T("KbQuests"),
        KeybindAction.TabItems => Loc.T("KbItems"),
        KeybindAction.TabShops => Loc.T("KbTraders"),
        KeybindAction.TabEvents => Loc.T("KbEvents"),
        KeybindAction.TabWorkshop => Loc.T("KbWorkshop"),
        KeybindAction.TabSettings => Loc.T("Settings"),
        _ => a.ToString(),
    };

    public event EventHandler<KeybindAction>? Changed;

    public KeyGesture? Get(KeybindAction action)
    {
        var text = store.GetValueOrDefault(action.ToString()) ?? Defaults[action];
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : KeyGesture.Parse(text);
        }
        catch (Exception)
        {
            return KeyGesture.Parse(Defaults[action]);
        }
    }

    public void Set(KeybindAction action, KeyGesture? gesture)
    {
        store[action.ToString()] = gesture?.ToString() ?? "";
        Changed?.Invoke(this, action);
    }

    public void ResetAll()
    {
        store.Clear();
        foreach (var a in Enum.GetValues<KeybindAction>()) Changed?.Invoke(this, a);
    }

    /// <summary>Akcja w aplikacji dla naciśniętego klawisza (bez ToggleWindow — ten jest globalny).</summary>
    public KeybindAction? Match(KeyEventArgs e)
    {
        foreach (var a in Enum.GetValues<KeybindAction>())
        {
            if (IsGlobal(a)) continue;
            if (Get(a) is { } g && g.Matches(e)) return a;
        }
        return null;
    }

    /// <summary>Skróty działające w całym Windows (także w grze), rejestrowane przez RegisterHotKey.</summary>
    public static bool IsGlobal(KeybindAction a) => a is KeybindAction.ToggleWindow or KeybindAction.ToggleOverlay;

    /// <summary>Czytelny opis skrótu, np. "Ctrl+1" zamiast "Ctrl+D1".</summary>
    public static string Pretty(KeyGesture? g)
    {
        if (g is null) return Loc.T("NoShortcut");
        var key = g.Key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(g.Key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num" + (int)(g.Key - Key.NumPad0),
            _ => g.Key.ToString(),
        };
        var parts = new List<string>();
        if (g.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (g.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (g.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (g.KeyModifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Win");
        parts.Add(key);
        return string.Join(" + ", parts);
    }
}
