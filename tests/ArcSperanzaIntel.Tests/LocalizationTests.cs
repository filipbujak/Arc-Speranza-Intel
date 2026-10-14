using ArcSperanzaIntel.Core.Localization;
using Xunit;

namespace ArcSperanzaIntel.Tests;

public class LocalizationTests
{
    /// <summary>Po podstawieniu argumentów w tekście nie może zostać żaden "{" ani "}" (np. podwójne {{0}}).</summary>
    [Fact]
    public void Placeholders_format_cleanly()
    {
        var args = new object[] { 1, 2, 3, 4 };
        foreach (var (key, (pl, en)) in Texts.All)
        {
            foreach (var text in new[] { pl, en })
            {
                if (!text.Contains('{')) continue;
                var formatted = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    text.Replace("{0:dd.MM HH:mm}", "{0}"), args);
                Assert.False(formatted.Contains('{') || formatted.Contains('}'), $"{key}: {formatted}");
            }
        }
    }
}
