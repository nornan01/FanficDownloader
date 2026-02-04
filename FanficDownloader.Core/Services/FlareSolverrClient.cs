using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace FanficDownloader.Core.Services;

public class FlareSolverrClient
{
    private readonly HttpClient _http = new();

    public async Task<string> GetAsync(string url, CancellationToken ct)
    {
        var payload = new
        {
            cmd = "request.get",
            url = url,
            maxTimeout = 60000
        };

        var json = JsonSerializer.Serialize(payload);

        var resp = await _http.PostAsync(
            "http://localhost:8191/v1",
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct
        );

        var body = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(body);

        return doc.RootElement
            .GetProperty("solution")
            .GetProperty("response")
            .GetString()!;
    }
}
