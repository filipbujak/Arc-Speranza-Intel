using System.Text;
using System.Text.RegularExpressions;

namespace ArcSperanzaIntel.Core.Util;

/// <summary>
/// Gotowe zgłoszenie na GitHubie: link do formularza (issue form) z wypełnionymi polami.
/// Nic nie jest wysyłane automatycznie — użytkownik widzi zgłoszenie w przeglądarce i sam je wysyła.
/// </summary>
public static class IssueReport
{
    /// <summary>GitHub i przeglądarki obcinają bardzo długie adresy — trzymamy się bezpiecznej długości.</summary>
    public const int MaxUrlLength = 7000;

    private static readonly Regex UserFolder = new(@"(?i)([\\/]Users[\\/])[^\\/\r\n]+", RegexOptions.Compiled);

    /// <summary>Usuwa z tekstu nazwę użytkownika Windows (C:\Users\Jan\… → C:\Users\&lt;user&gt;\…), żeby nie trafiła do publicznego zgłoszenia.</summary>
    public static string Sanitize(string text, string? userName)
    {
        var result = UserFolder.Replace(text, "$1<user>");
        if (!string.IsNullOrWhiteSpace(userName) && userName.Length >= 3)
            result = Regex.Replace(result, Regex.Escape(userName), "<user>", RegexOptions.IgnoreCase);
        return result;
    }

    /// <summary>Ostatnie <paramref name="lines"/> niepustych linii, najwyżej <paramref name="maxChars"/> znaków (końcówka).</summary>
    public static string Tail(string text, int lines, int maxChars)
    {
        var all = text.Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        var tail = string.Join("\n", all.Skip(Math.Max(0, all.Count - lines)));
        return tail.Length <= maxChars ? tail : "…" + tail[^maxChars..];
    }

    /// <summary>
    /// Link do nowego zgłoszenia z szablonem i polami formularza (id pola → wartość).
    /// Za długi adres skraca najdłuższe pole od początku (zostaje najnowsza część logu).
    /// </summary>
    public static string BuildUrl(string repoUrl, string template, string title, IReadOnlyDictionary<string, string> fields)
    {
        var values = fields.ToDictionary(kv => kv.Key, kv => kv.Value);
        while (true)
        {
            var url = Compose(repoUrl, template, title, values);
            if (url.Length <= MaxUrlLength || values.Count == 0) return url;
            var longest = values.OrderByDescending(kv => kv.Value.Length).First();
            if (longest.Value.Length < 50) return url;
            var cut = Math.Max(50, longest.Value.Length - (url.Length - MaxUrlLength) / 2 - 20);
            values[longest.Key] = "…" + longest.Value[^cut..];
        }
    }

    private static string Compose(string repoUrl, string template, string title, Dictionary<string, string> fields)
    {
        var sb = new StringBuilder(repoUrl.TrimEnd('/'));
        sb.Append("/issues/new?template=").Append(Uri.EscapeDataString(template));
        if (title.Length > 0) sb.Append("&title=").Append(Uri.EscapeDataString(title));
        foreach (var (key, value) in fields)
            sb.Append('&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
        return sb.ToString();
    }
}
