using System.ComponentModel;
using System.Globalization;

namespace ArcSperanzaIntel.Core.Localization;

public enum AppLanguage { Polish, English }

/// <summary>
/// Teksty interfejsu po polsku i angielsku. XAML wiąże się przez indeksator (Loc.Instance["Klucz"]),
/// więc zmiana języka od razu odświeża wszystkie napisy; kod woła Loc.T("Klucz") / Loc.F("Klucz", args).
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private AppLanguage _language = AppLanguage.Polish;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Po zmianie języka (po odświeżeniu napisów w XAML).</summary>
    public event EventHandler? LanguageChanged;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            Culture = CultureFor(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Kultura do liczb i dat w bieżącym języku.</summary>
    public static CultureInfo Culture { get; private set; } = CultureFor(AppLanguage.Polish);

    public static bool IsPolish => Instance.Language == AppLanguage.Polish;

    public string this[string key] => Get(key);

    public string Get(string key) =>
        Texts.All.TryGetValue(key, out var t) ? (_language == AppLanguage.Polish ? t.Pl : t.En) : key;

    public static string T(string key) => Instance.Get(key);

    public static string F(string key, params object?[] args) =>
        string.Format(Culture, Instance.Get(key), args);

    /// <summary>Język systemu: polski Windows → polski, każdy inny → angielski.</summary>
    public static AppLanguage SystemDefault() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl" ? AppLanguage.Polish : AppLanguage.English;

    private static CultureInfo CultureFor(AppLanguage l) =>
        CultureInfo.GetCultureInfo(l == AppLanguage.Polish ? "pl-PL" : "en-GB");
}
