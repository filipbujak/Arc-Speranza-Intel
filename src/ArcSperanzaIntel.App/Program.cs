using Avalonia;
using Velopack;

namespace ArcSperanzaIntel.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack musi ruszyć pierwszy: obsługuje instalację, odinstalowanie i restart po aktualizacji.
        VelopackApp.Build().Run();
        CrashLog.Install();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Używane też przez designer Avalonii.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

/// <summary>Zapisuje nieobsłużone wyjątki do crash.log w folderze danych — żeby dało się zdiagnozować błąd.</summary>
internal static class CrashLog
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcSperanzaIntel", "crash.log");

    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(e.ExceptionObject as Exception, "Unhandled");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write(e.Exception, "UnobservedTask");
            e.SetObserved();
        };
    }

    public static void Write(Exception? ex, string kind)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {ex}\n\n");
        }
        catch
        {
            // logowanie nie może wywrócić aplikacji
        }
    }
}
