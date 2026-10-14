using System.Globalization;
using System.Text;

namespace ArcSperanzaIntel.Core.Util;

public static class TextFormat
{
    /// <summary>"weapon_case" / "fuel-cell" / "damagePerSecond" → "Weapon Case" / "Fuel Cell" / "Damage Per Second".</summary>
    public static string Pretty(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var sb = new StringBuilder(raw.Length + 8);
        var prev = ' ';
        foreach (var ch in raw.Trim())
        {
            if (ch is '_' or '-')
            {
                sb.Append(' ');
                prev = ' ';
                continue;
            }
            if (char.IsUpper(ch) && char.IsLower(prev)) sb.Append(' ');
            sb.Append(prev == ' ' ? char.ToUpperInvariant(ch) : ch);
            prev = ch;
        }
        return sb.ToString();
    }

    public static string Number(double value) =>
        value.ToString(value % 1 == 0 ? "#,0" : "#,0.##", ArcSperanzaIntel.Core.Localization.Loc.Culture);

    /// <summary>Porównanie bez wielkości liter i polskich znaków — do wyszukiwarek.</summary>
    public static bool ContainsLoose(string? haystack, string needle) =>
        haystack is not null &&
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
