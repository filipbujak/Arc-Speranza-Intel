using System.ComponentModel;
using System.Globalization;

namespace ArcSperanzaIntel.Core.Localization;

public enum AppLanguage { Polish, English, German, French, Spanish, PortugueseBrazil, Russian, Japanese, ChineseSimplified }

/// <summary>Język interfejsu: kod zapisywany w stanie, nazwa we własnym języku (do listy) i kultura do liczb i dat.</summary>
public sealed record LanguageInfo(AppLanguage Language, string Code, string NativeName, string CultureName)
{
    public CultureInfo Culture => CultureInfo.GetCultureInfo(CultureName);
}

/// <summary>Obsługiwane języki. Polski i angielski są w <see cref="Texts"/>, reszta w osobnych plikach (brak tekstu = angielski).</summary>
public static class Languages
{
    public static IReadOnlyList<LanguageInfo> All { get; } =
    [
        new(AppLanguage.English, "en", "English", "en-GB"),
        new(AppLanguage.Polish, "pl", "Polski", "pl-PL"),
        new(AppLanguage.German, "de", "Deutsch", "de-DE"),
        new(AppLanguage.French, "fr", "Français", "fr-FR"),
        new(AppLanguage.Spanish, "es", "Español", "es-ES"),
        new(AppLanguage.PortugueseBrazil, "pt-BR", "Português (Brasil)", "pt-BR"),
        new(AppLanguage.Russian, "ru", "Русский", "ru-RU"),
        new(AppLanguage.Japanese, "ja", "日本語", "ja-JP"),
        new(AppLanguage.ChineseSimplified, "zh-CN", "简体中文", "zh-CN"),
    ];

    public static LanguageInfo Of(AppLanguage language) => All.First(l => l.Language == language);

    public static LanguageInfo? FromCode(string? code) =>
        string.IsNullOrEmpty(code) ? null : All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Język Windows → nasz język (np. pt-PT też dostaje portugalski brazylijski); nieobsługiwany → angielski.</summary>
    public static AppLanguage FromCulture(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "pl" => AppLanguage.Polish,
        "de" => AppLanguage.German,
        "fr" => AppLanguage.French,
        "es" => AppLanguage.Spanish,
        "pt" => AppLanguage.PortugueseBrazil,
        "ru" => AppLanguage.Russian,
        "ja" => AppLanguage.Japanese,
        "zh" => AppLanguage.ChineseSimplified,
        _ => AppLanguage.English,
    };

    /// <summary>Teksty danego języka poza polskim i angielskim (klucz → tekst).</summary>
    internal static IReadOnlyDictionary<string, string>? Extra(AppLanguage language) => language switch
    {
        AppLanguage.German => TextsDe.All,
        AppLanguage.French => TextsFr.All,
        AppLanguage.Spanish => TextsEs.All,
        AppLanguage.PortugueseBrazil => TextsPtBr.All,
        AppLanguage.Russian => TextsRu.All,
        AppLanguage.Japanese => TextsJa.All,
        AppLanguage.ChineseSimplified => TextsZh.All,
        _ => null,
    };
}

/// <summary>
/// Teksty interfejsu w wybranym języku. XAML wiąże się przez indeksator (Loc.Instance["Klucz"]),
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
            Culture = Languages.Of(value).Culture;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Kultura do liczb i dat w bieżącym języku.</summary>
    public static CultureInfo Culture { get; private set; } = Languages.Of(AppLanguage.Polish).Culture;

    public static bool IsPolish => Instance.Language == AppLanguage.Polish;

    public string this[string key] => Get(key);

    public string Get(string key) => Get(key, _language);

    /// <summary>Tekst w podanym języku (np. pytanie o restart w języku docelowym). Brak tłumaczenia = angielski.</summary>
    public string Get(string key, AppLanguage language)
    {
        if (!Texts.All.TryGetValue(key, out var t)) return key;
        return language switch
        {
            AppLanguage.Polish => t.Pl,
            AppLanguage.English => t.En,
            _ => Languages.Extra(language) is { } extra && extra.TryGetValue(key, out var s) ? s : t.En,
        };
    }

    public static string T(string key) => Instance.Get(key);

    public static string F(string key, params object?[] args) =>
        string.Format(Culture, Instance.Get(key), args);

    /// <summary>Język systemu Windows, jeśli go obsługujemy; w przeciwnym razie angielski.</summary>
    public static AppLanguage SystemDefault() => Languages.FromCulture(CultureInfo.CurrentUICulture);
}
