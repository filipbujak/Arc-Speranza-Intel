namespace ArcSperanzaIntel.Core.Storage;

/// <summary>Foldery aplikacji: postęp w %APPDATA%, cache w %LOCALAPPDATA%.</summary>
public sealed class AppPaths
{
    public string DataDir { get; }
    public string CacheDir { get; }
    public string StateFile => Path.Combine(DataDir, "state.json");
    public string ApiCacheDir => Path.Combine(CacheDir, "api");
    public string ImageCacheDir => Path.Combine(CacheDir, "img");

    public AppPaths(string? dataDir = null, string? cacheDir = null)
    {
        DataDir = dataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcSperanzaIntel");
        CacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcSperanzaIntel", "cache");
    }

    public void EnsureCreated()
    {
        MigrateOldData();
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(ApiCacheDir);
        Directory.CreateDirectory(ImageCacheDir);
    }

    /// <summary>Postęp z czasów, gdy aplikacja nazywała się "ArcCompanion" — przenosimy raz, jeśli nowego jeszcze nie ma.</summary>
    private void MigrateOldData()
    {
        try
        {
            var oldState = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcCompanion", "state.json");
            if (File.Exists(StateFile) || !File.Exists(oldState)) return;
            Directory.CreateDirectory(DataDir);
            File.Copy(oldState, StateFile);
        }
        catch (IOException)
        {
            // brak migracji nie blokuje startu
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
