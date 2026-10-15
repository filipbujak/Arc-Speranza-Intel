using Avalonia.Controls;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;

namespace ArcSperanzaIntel.App.Controls;

/// <summary>
/// Logo aplikacji — te same cztery skośne słupki co w ikonie (Assets/app.ico), bez czarnego tła.
/// Wysokość ustawia się zwykłym Height, szerokość dopasowuje się sama.
/// </summary>
public sealed class AppLogo : Viewbox
{
    // współrzędne zdjęte z ikony 256×256 (słupki od x=38, y=44)
    private static readonly (string Data, string Color)[] Bars =
    [
        ("M0,44 L37,30 L37,158 L0,158 Z", "#F0503F"),
        ("M48,14 L84,0 L84,158 L48,158 Z", "#FFC72C"),
        ("M95,34 L132,20 L132,158 L95,158 Z", "#3FD07A"),
        ("M143,24 L179,10 L179,158 L143,158 Z", "#36B5E6"),
    ];

    public AppLogo()
    {
        Stretch = Stretch.Uniform;
        var canvas = new Canvas { Width = 179, Height = 158 };
        foreach (var (data, color) in Bars)
            canvas.Children.Add(new Path { Data = Geometry.Parse(data), Fill = new SolidColorBrush(Color.Parse(color)) });
        Child = canvas;
    }
}
