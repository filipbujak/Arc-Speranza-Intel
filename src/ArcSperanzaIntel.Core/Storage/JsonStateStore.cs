using System.Text.Json;
using ArcSperanzaIntel.Core.Models;

namespace ArcSperanzaIntel.Core.Storage;

public interface IStateStore
{
    UserState Load();
    void Save(UserState state);
    void Delete();
}

/// <summary>Zapis postępu do JSON. Zapis atomowy (plik tymczasowy + podmiana), kopia uszkodzonego pliku.</summary>
public sealed class JsonStateStore(string file) : IStateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly object _lock = new();

    public UserState Load()
    {
        lock (_lock)
        {
            if (!File.Exists(file)) return new UserState();
            try
            {
                return JsonSerializer.Deserialize<UserState>(File.ReadAllText(file), Options) ?? new UserState();
            }
            catch (JsonException)
            {
                File.Copy(file, file + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), overwrite: true);
                return new UserState();
            }
        }
    }

    public void Save(UserState state)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state, Options));
            File.Move(tmp, file, overwrite: true);
        }
    }

    public void Delete()
    {
        lock (_lock)
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }
}
