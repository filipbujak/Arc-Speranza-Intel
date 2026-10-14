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
    private const int MemoryLimit = 900;

    public ImageCache(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ArcSperanzaIntel/1.0 (+https://metaforge.app/arc-raiders)");
        Shared = this;
    }

    /// <summary>Obraz z pamięci, jeśli już jest (bez ładowania).</summary>
    public Bitmap? Peek(string url) => _memory.TryGetValue(url, out var b) ? b : null;

    public bool IsKnownMissing(string url) => _memory.TryGetValue(url, out var b) && b is null;

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
            var file = Path.Combine(_dir, Hash(url) + Path.GetExtension(new Uri(url).AbsolutePath));
            byte[]? bytes = null;
            if (File.Exists(file))
            {
                bytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
            }
            else
            {
                await _gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    using var resp = await _http.GetAsync(url).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        await File.WriteAllBytesAsync(file, bytes).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            Bitmap? bmp = null;
            if (bytes is { Length: > 0 })
            {
                using var ms = new MemoryStream(bytes);
                bmp = decodeWidth > 0 ? Bitmap.DecodeToWidth(ms, decodeWidth) : new Bitmap(ms);
            }
            Remember(url, bmp);
            return bmp;
        }
        catch (Exception)
        {
            Remember(url, null);
            return null;
        }
        finally
        {
            _pending.TryRemove(url, out _);
        }
    }

    private void Remember(string url, Bitmap? bmp)
    {
        _memory[url] = bmp;
        _order.Enqueue(url);
        // Prosty limit pamięci: wyrzucamy najstarsze wpisy (bez Dispose — mogą być jeszcze rysowane).
        while (_memory.Count > MemoryLimit && _order.TryDequeue(out var old))
            _memory.TryRemove(old, out _);
    }

    public void ClearDisk()
    {
        _memory.Clear();
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
