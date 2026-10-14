using ArcSperanzaIntel.Core.Localization;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ArcSperanzaIntel.App.Localization;

/// <summary>
/// Napis w bieżącym języku: Text="{l:T MapsTitle}". Wiązanie do indeksatora Loc, więc zmiana języka
/// w Ustawieniach od razu podmienia tekst.
/// </summary>
public sealed class T : MarkupExtension
{
    public T() { }

    public T(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
}
