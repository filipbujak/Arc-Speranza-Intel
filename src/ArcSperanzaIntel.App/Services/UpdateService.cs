using Velopack;
using Velopack.Sources;

namespace ArcSperanzaIntel.App.Services;

/// <summary>
/// Aktualizacje przez Velopack z GitHub Releases. Działa tylko w wersji zainstalowanej Setup.exe
/// (w buildzie z folderu IsInstalled = false i nic nie robimy).
/// </summary>
public sealed class UpdateService
{
    private readonly UpdateManager? _manager;
    private UpdateInfo? _pending;

    // Velopack pozwala na jedno pobieranie naraz (plik blokady); cichy start i przycisk w ustawieniach nie mogą się zderzyć
    private readonly SemaphoreSlim _download = new(1, 1);

    public UpdateService()
    {
        try
        {
            _manager = new UpdateManager(new GithubSource(AppInfo.RepoUrl, null, false));
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "UpdateManager");
        }
    }

    public bool IsInstalled => _manager?.IsInstalled == true;

    /// <summary>Sprawdzona, jeszcze nie zainstalowana wersja.</summary>
    public string? PendingVersion => _pending?.TargetFullRelease.Version.ToString();

    /// <summary>Nowa wersja (np. "1.2.0") albo null, gdy aktualna / nie zainstalowano instalatorem.</summary>
    public async Task<string?> CheckAsync()
    {
        if (_manager is null || !_manager.IsInstalled) return null;
        _pending = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        return PendingVersion;
    }

    /// <summary>Czy nowa wersja jest już pobrana i czeka na zamknięcie aplikacji.</summary>
    public bool IsReadyOnExit { get; private set; }

    /// <summary>
    /// Pobiera w tle i ustawia instalację po zamknięciu aplikacji (cicho, bez okien).
    /// Następne uruchomienie to już nowa wersja.
    /// </summary>
    public async Task DownloadAndApplyOnExitAsync()
    {
        if (_pending is null || _manager is null || IsReadyOnExit) return;
        if (!await _download.WaitAsync(0).ConfigureAwait(false)) return; // już pobieramy — nie dublujemy
        try
        {
            await _manager.DownloadUpdatesAsync(_pending).ConfigureAwait(false);
            _manager.WaitExitThenApplyUpdates(_pending, silent: true, restart: false);
            IsReadyOnExit = true;
        }
        finally
        {
            _download.Release();
        }
    }

    /// <summary>Pobiera sprawdzoną wersję, instaluje i uruchamia aplikację ponownie.</summary>
    public async Task DownloadAndRestartAsync()
    {
        if (_pending is null || _manager is null) return;
        await _download.WaitAsync().ConfigureAwait(false); // poczekaj, aż ciche pobieranie się skończy
        try
        {
            await _manager.DownloadUpdatesAsync(_pending).ConfigureAwait(false);
            _manager.ApplyUpdatesAndRestart(_pending);
        }
        finally
        {
            _download.Release();
        }
    }
}
