using ArcSperanzaIntel.App.Services;
using ArcSperanzaIntel.App.ViewModels;
using ArcSperanzaIntel.App.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ArcSperanzaIntel.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new AppServices();
            var main = new MainViewModel(services);
            var window = new MainWindow { DataContext = main };
            services.AttachWindow(window);
            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => services.Dispose();
            _ = main.InitializeAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
