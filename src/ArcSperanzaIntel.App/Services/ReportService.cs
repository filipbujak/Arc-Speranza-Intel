using System.Runtime.InteropServices;
using System.Text;
using ArcSperanzaIntel.Core.Localization;
using ArcSperanzaIntel.Core.Util;

namespace ArcSperanzaIntel.App.Services;

/// <summary>
/// Zgłoszenia: gotowe formularze na GitHubie (błąd, pomysł, tłumaczenie) i informacje diagnostyczne do skopiowania.
/// Do zgłoszenia trafia wersja, język, region, system i końcówka crash.log — bez nazwy użytkownika Windows.
/// Nic nie jest wysyłane samo: otwiera się strona, a użytkownik decyduje, co wysłać.
/// </summary>
public sealed class ReportService(Func<string> eventRegion)
{
    private const int LogLines = 30;
    private const int LogChars = 3000;

    public static string LogsFolder => Path.GetDirectoryName(CrashLog.FilePath)!;

    public string BugUrl() => IssueReport.BuildUrl(AppInfo.RepoUrl, "bug.yml", "[Bug] ",
        new Dictionary<string, string> { ["version"] = AppInfo.Version, ["diagnostics"] = Diagnostics() });

    public string IdeaUrl() => IssueReport.BuildUrl(AppInfo.RepoUrl, "feature.yml", "[Idea] ",
        new Dictionary<string, string> { ["version"] = AppInfo.Version });

    public string TranslationUrl()
    {
        var lang = Languages.Of(Loc.Instance.Language);
        return IssueReport.BuildUrl(AppInfo.RepoUrl, "translation.yml", $"[Translation] {lang.NativeName}: ",
            new Dictionary<string, string> { ["language"] = $"{lang.NativeName} ({lang.Code})", ["version"] = AppInfo.Version });
    }

    /// <summary>Tekst do wklejenia (zgłoszenie, Discord): wersja, język, region, system i ostatnie błędy.</summary>
    public string Diagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ARC Speranza Intel {AppInfo.Version}");
        sb.AppendLine($"Language: {Languages.Of(Loc.Instance.Language).Code}");
        sb.AppendLine($"Event region: {eventRegion()}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine();
        sb.AppendLine("Last errors (crash.log):");
        sb.AppendLine("```");
        sb.AppendLine(LastErrors());
        sb.Append("```");
        return IssueReport.Sanitize(sb.ToString(), Environment.UserName);
    }

    private static string LastErrors()
    {
        try
        {
            if (!File.Exists(CrashLog.FilePath)) return "(none)";
            var tail = IssueReport.Tail(File.ReadAllText(CrashLog.FilePath), LogLines, LogChars);
            return tail.Length == 0 ? "(none)" : tail;
        }
        catch (Exception ex)
        {
            return "(could not read log: " + ex.GetType().Name + ")";
        }
    }
}
