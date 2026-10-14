using System.Text.Json.Nodes;

namespace ArcSperanzaIntel.Core.Api;

/// <summary>Niskopoziomowe zapytania HTTP do API MetaForge.</summary>
public sealed class MetaForgeHttp : IDisposable
{
    public const string BaseUrl = "https://metaforge.app/api/";
    private readonly HttpClient _http;

    public MetaForgeHttp(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("ArcSperanzaIntel/1.0 (+https://metaforge.app/arc-raiders)");
    }

    public Task<string> GetStringAsync(string relativeUrl, CancellationToken ct) =>
        _http.GetStringAsync(BaseUrl + relativeUrl, ct);

    /// <summary>Pobiera wszystkie strony endpointu z paginacją i skleja pole "data" w jedną tablicę JSON.</summary>
    public async Task<string> GetAllPagesAsync(string relativeUrl, CancellationToken ct, int pageSize = 100)
    {
        var all = new JsonArray();
        var sep = relativeUrl.Contains('?') ? '&' : '?';
        for (var page = 1; page <= 50; page++)
        {
            var json = await GetStringAsync($"{relativeUrl}{sep}limit={pageSize}&page={page}", ct);
            var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Pusta odpowiedź API");
            if (node["data"] is JsonArray data)
            {
                foreach (var item in data.ToList())
                {
                    data.Remove(item);
                    all.Add(item);
                }
            }
            var hasNext = node["pagination"]?["hasNextPage"]?.GetValue<bool>() ?? false;
            if (!hasNext) break;
            await Task.Delay(200, ct); // nie młotkujemy API
        }
        return all.ToJsonString();
    }

    public void Dispose() => _http.Dispose();
}
