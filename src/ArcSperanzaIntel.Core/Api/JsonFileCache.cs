namespace ArcSperanzaIntel.Core.Api;

/// <summary>Prosty cache surowych odpowiedzi API na dysku (MetaForge prosi o cache po naszej stronie).</summary>
public sealed class JsonFileCache(string directory)
{
    public sealed record Entry(string Json, DateTimeOffset SavedAt);

    private string PathFor(string key)
    {
        var safe = string.Concat(key.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' ? ch : '_'));
        return Path.Combine(directory, safe + ".json");
    }

    public Entry? TryRead(string key)
    {
        var file = PathFor(key);
        if (!File.Exists(file)) return null;
        try
        {
            return new Entry(File.ReadAllText(file), File.GetLastWriteTimeUtc(file));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Write(string key, string json)
    {
        Directory.CreateDirectory(directory);
        var file = PathFor(key);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, file, overwrite: true);
    }

    public void Clear()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        Directory.CreateDirectory(directory);
    }
}
