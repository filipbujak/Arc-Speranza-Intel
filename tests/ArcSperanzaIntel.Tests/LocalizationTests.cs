using System.Globalization;
using System.Text.RegularExpressions;
using ArcSperanzaIntel.Core.Localization;
using Xunit;

namespace ArcSperanzaIntel.Tests;

public class LocalizationTests
{
    private static readonly Regex Placeholder = new(@"\{\d+(?::[^}]*)?\}");

    /// <summary>Po podstawieniu argumentów w tekście nie może zostać żaden "{" ani "}" (np. podwójne {{0}}).</summary>
    [Fact]
    public void Placeholders_format_cleanly()
    {
        var args = new object[] { 1, 2, 3, 4 };
        foreach (var info in Languages.All)
            foreach (var key in Texts.All.Keys)
            {
                var text = Loc.Instance.Get(key, info.Language);
                if (!text.Contains('{')) continue;
                var formatted = string.Format(CultureInfo.InvariantCulture, text.Replace("{0:dd.MM HH:mm}", "{0}"), args);
                Assert.False(formatted.Contains('{') || formatted.Contains('}'), $"{info.Code} {key}: {formatted}");
            }
    }

    /// <summary>Każdy język ma każdy tekst i te same placeholdery co angielski.</summary>
    [Fact]
    public void Every_language_is_complete()
    {
        foreach (var info in Languages.All)
        {
            if (info.Language is AppLanguage.Polish or AppLanguage.English) continue;
            var extra = Languages.Extra(info.Language);
            Assert.NotNull(extra);
            foreach (var (key, (_, en)) in Texts.All)
            {
                Assert.True(extra!.ContainsKey(key), $"{info.Code}: brak {key}");
                var expected = string.Join(",", Placeholder.Matches(en).Select(m => m.Value).Order());
                var actual = string.Join(",", Placeholder.Matches(extra[key]).Select(m => m.Value).Order());
                Assert.True(expected == actual, $"{info.Code} {key}: {actual} zamiast {expected}");
            }
            Assert.True(extra!.Keys.All(Texts.All.ContainsKey), $"{info.Code}: nadmiarowe klucze");
        }
    }

    [Fact]
    public void Windows_language_maps_to_supported_language()
    {
        Assert.Equal(AppLanguage.German, Languages.FromCulture(CultureInfo.GetCultureInfo("de-AT")));
        Assert.Equal(AppLanguage.PortugueseBrazil, Languages.FromCulture(CultureInfo.GetCultureInfo("pt-PT")));
        Assert.Equal(AppLanguage.ChineseSimplified, Languages.FromCulture(CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Equal(AppLanguage.English, Languages.FromCulture(CultureInfo.GetCultureInfo("it-IT")));
    }

    [Fact]
    public void Language_codes_round_trip()
    {
        foreach (var info in Languages.All)
            Assert.Equal(info.Language, Languages.FromCode(info.Code)!.Language);
        Assert.Null(Languages.FromCode("xx"));
    }
}
