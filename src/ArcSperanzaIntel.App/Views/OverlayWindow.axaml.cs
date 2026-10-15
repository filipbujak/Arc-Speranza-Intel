using Avalonia.Controls;
using Avalonia.Input;

namespace ArcSperanzaIntel.App.Views;

/// <summary>Okienko overlaya: bez ramki, zawsze na wierzchu, nie kradnie fokusu grze. Przesuwane za pasek, rozmiar za róg.</summary>
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        DragBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        ResizeGrip.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginResizeDrag(WindowEdge.SouthEast, e);
                e.Handled = true;
            }
        };
    }
}
