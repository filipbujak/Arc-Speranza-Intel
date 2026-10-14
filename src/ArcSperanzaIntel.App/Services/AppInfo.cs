using System.Reflection;

namespace ArcSperanzaIntel.App.Services;

/// <summary>Wersja aplikacji i adres repozytorium (źródło aktualizacji).</summary>
public static class AppInfo
{
    /// <summary>Repozytorium na GitHubie — z jego Releases instalator pobiera aktualizacje.</summary>
    public const string RepoUrl = "https://github.com/OWNER/arc-speranza-intel";

    public static string Version { get; } =
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0")
        .Split('+')[0];
}
