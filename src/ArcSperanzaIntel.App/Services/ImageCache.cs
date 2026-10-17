using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Media.Imaging;

namespace ArcSperanzaIntel.App.Services;

/// <summary>
/// Pobiera obrazki (ikony itemów, kafelki map, portrety) z cache w pamięci, z dysku albo z sieci.
/// Kafelki i ikony zostają na dysku, więc po pierwszym obejrzeniu działają bez internetu.
/// </summary>
public sealed class ImageCache : IDisposable
{
    public static ImageCache Shared { get; private set; } = null!;

    private readonly string _dir;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _gate = new(8);
    private readonly ConcurrentDictionary<string, Bitmap?> _memory = new();
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _pending = new();
    private readonly ConcurrentQueue<string> _order = new();
    private readonly ConcurrentDictionary<string, DateTime> _failedAt = new();
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(2);
    private const int MemoryLimit = 1500;
    // budżet pamięci na zdekodowane obrazy: kafelek mapy 512×512 to ~1 MB, ikona ~50 KB
    private const long MemoryBudgetBytes = 320L * 1024 * 1024;
    private long _memoryBytes;

    public ImageCache(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ArcSperanzaIntel/1.0 (+https://metaforge.app/arc-raiders)");
        // bez AVIF — Skia (Avalonia) go nie dekoduje; prosimy o WebP/PNG/JPEG
        _http.DefaultRequestHeaders.Accept.ParseAdd("image/webp,image/png,image/jpeg;q=0.9,image/*;q=0.5");
        // bez podszywania się pod stronę MetaForge (Referer) — przedstawiamy się uczciwie User-Agentem
        Shared = this;
    }

    /// <summary>Obraz z pamięci, jeśli już jest (bez ładowania).</summary>
    public Bitmap? Peek(string url) => _memory.TryGetValue(url, out var b) ? b : null;

    /// <summary>Plik z listą ikon, których nie udało się pobrać (diagnostyka).</summary>
    public string LogPath => Path.Combine(Path.GetDirectoryName(_dir) ?? _dir, "images.log");

    public bool IsKnownMissing(string url) => _failedAt.ContainsKey(url);

    /// <summary>Ładuje obraz. decodeWidth &gt; 0 zmniejsza przy dekodowaniu (ikony), 0 = pełny rozmiar (kafelki).</summary>
    public Task<Bitmap?> GetAsync(string url, int decodeWidth = 0)
    {
        if (_memory.TryGetValue(url, out var hit)) return Task.FromResult(hit);
        return _pending.GetOrAdd(url, u => LoadAsync(u, decodeWidth));
    }

    private async Task<Bitmap?> LoadAsync(string url, int decodeWidth)
    {
        try
        {
            if (_failedAt.TryGetValue(url, out var when) && DateTime.UtcNow - when < RetryAfter) return null;
            var file = Path.Combine(_dir, Hash(url) + Path.GetExtension(new Uri(url).AbsolutePath));

            // 1) z dysku — uszkodzony plik (np. zapisana strona błędu) kasujemy i pobieramy od nowa
            if (File.Exists(file))
            {
                var cached = Decode(await File.ReadAllBytesAsync(file).ConfigureAwait(false), decodeWidth);
                if (cached is not null)
                {
                    Remember(url, cached);
                    return cached;
                }
                TryDelete(file);
            }

            // 2) z sieci — na dysk trafia tylko obraz, który dał się zdekodować
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                var bytes = resp.IsSuccessStatusCode ? await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false) : null;
                var bmp = bytes is null ? null : Decode(bytes, decodeWidth);
                if (bmp is null)
                {
                    _failedAt[url] = DateTime.UtcNow;
                    // brakujące kafelki na brzegach mapy (404) to normalka — nie logujemy
                    if (!(resp.StatusCode == System.Net.HttpStatusCode.NotFound && url.Contains("/maps/", StringComparison.Ordinal)))
                        Fail(url, $"HTTP {(int)resp.StatusCode}, {resp.Content.Headers.ContentType?.MediaType ?? "?"}, {bytes?.Length ?? 0} B, {Signature(bytes)}");
                    return null;
                }
                await File.WriteAllBytesAsync(file, bytes!).ConfigureAwait(false);
                _failedAt.TryRemove(url, out _);
                Remember(url, bmp);
                return bmp;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            Fail(url, ex.GetType().Name + ": " + ex.Message);
            return null;
        }
        finally
        {
            _pending.TryRemove(url, out _);
        }
    }

    private static Bitmap? Decode(byte[] bytes, int decodeWidth)
    {
        if (bytes.Length == 0) return null;
        try
        {
            using var ms = new MemoryStream(bytes);
            return decodeWidth > 0 ? Bitmap.DecodeToWidth(ms, decodeWidth) : new Bitmap(ms);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Nieudane pobranie: ponowimy za chwilę (nie na zawsze) i zapisujemy powód do images.log.</summary>
    private void Fail(string url, string reason)
    {
        _failedAt[url] = DateTime.UtcNow;
        try
        {
            var log = Path.Combine(Path.GetDirectoryName(_dir) ?? _dir, "images.log");
            if (File.Exists(log) && new FileInfo(log).Length > 512 * 1024) File.Delete(log);
            File.AppendAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {reason}  {url}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Pierwsze bajty pliku — po nich widać, czy to PNG/WebP/AVIF czy HTML.</summary>
    private static string Signature(byte[]? b) =>
        b is null || b.Length < 12 ? "-" : Encoding.ASCII.GetString(b, 0, 12).Replace('\0', '.').Replace('\n', ' ').Replace('\r', ' ');

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
    }

    private void Remember(string url, Bitmap? bmp)
    {
        if (bmp is null) return;
        if (_memory.TryAdd(url, bmp))
        {
            _order.Enqueue(url);
            Interlocked.Add(ref _memoryBytes, SizeOf(bmp));
        }
        // limit pamięci: wyrzucamy najstarsze wpisy (bez Dispose — mogą być jeszcze rysowane, zwolni je GC)
        while ((_memory.Count > MemoryLimit || Interlocked.Read(ref _memoryBytes) > MemoryBudgetBytes) && _order.TryDequeue(out var old))
            if (_memory.TryRemove(old, out var gone) && gone is not null)
                Interlocked.Add(ref _memoryBytes, -SizeOf(gone));
    }

    private static long SizeOf(Bitmap b) => (long)b.PixelSize.Width * b.PixelSize.Height * 4;

    public void ClearDisk()
    {
        _memory.Clear();
        Interlocked.Exchange(ref _memoryBytes, 0);
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
            Directory.CreateDirectory(_dir);
        }
        catch (IOException)
        {
        }
    }

    private static string Hash(string s) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)))[..20];

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}
