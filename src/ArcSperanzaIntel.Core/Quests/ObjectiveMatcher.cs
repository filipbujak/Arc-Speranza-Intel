using System.Text;

namespace ArcSperanzaIntel.Core.Quests;

/// <summary>
/// Dopasowuje znacznik questa na mapie (jego nazwę z MetaForge, „instanceName”) do konkretnego podpunktu questa.
/// Ostrożnie: zwraca podpunkt tylko przy wyraźnym dopasowaniu słów i bez remisu — lepiej nic nie zaznaczyć niż zaznaczyć zły.
/// </summary>
public static class ObjectiveMatcher
{
    private const double MinScore = 0.5;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "from", "with", "into", "onto", "near", "inside", "outside", "at", "in", "of", "on", "to", "a", "an",
        "find", "get", "go", "reach", "visit", "locate", "search", "use", "your", "any", "all", "one", "two", "three",
    };

    /// <summary>Indeks podpunktu, którego dotyczy znacznik, albo null, gdy nie da się tego pewnie ustalić.</summary>
    public static int? BestMatch(IReadOnlyList<string> objectives, string? markerName)
    {
        if (string.IsNullOrWhiteSpace(markerName) || objectives.Count == 0) return null;
        var marker = Normalize(markerName);
        var markerWords = Words(marker);
        if (markerWords.Count == 0) return null;

        int? best = null;
        var bestScore = 0.0;
        var tie = false;
        for (var i = 0; i < objectives.Count; i++)
        {
            var objective = Normalize(objectives[i]);
            var score = objective.Contains(marker, StringComparison.Ordinal)
                ? 1.0
                : markerWords.Count(w => Words(objective).Contains(w)) / (double)markerWords.Count;
            if (score > bestScore)
            {
                best = i;
                bestScore = score;
                tie = false;
            }
            else if (score > 0 && Math.Abs(score - bestScore) < 1e-9)
            {
                tie = true;
            }
        }
        return bestScore >= MinScore && !tie ? best : null;
    }

    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static HashSet<string> Words(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !StopWords.Contains(w))
            .Select(Stem)
            .ToHashSet(StringComparer.Ordinal);

    // prosta liczba mnoga: "fuses" = "fuse", "batteries" = "battery"
    private static string Stem(string w) =>
        w.EndsWith("ies", StringComparison.Ordinal) && w.Length > 4 ? w[..^3] + "y"
        : w.EndsWith('s') && !w.EndsWith("ss", StringComparison.Ordinal) && w.Length > 3 ? w[..^1]
        : w;
}
