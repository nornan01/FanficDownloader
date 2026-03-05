using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;


namespace FanficDownloader.Core.Clients;

public class FlareSolverrClient
{
    private readonly HttpClient _http;
    private readonly ILogger<FlareSolverrClient> _logger;

    public FlareSolverrClient(HttpClient http, ILogger<FlareSolverrClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> GetAsync(string url, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("FlareSolverr request started for {Url}", url);
        var payload = new
        {
            cmd = "request.get",
            url = url,
            maxTimeout = 60000
        };

        var json = JsonSerializer.Serialize(payload);

        try
        {
            var resp = await _http.PostAsync(
                "/v1", // БАЗОВЫЙ АДРЕС зададим в Program.cs
                new StringContent(json, Encoding.UTF8, "application/json"),
                ct
            );

            var body = await resp.Content.ReadAsStringAsync(ct);
            sw.Stop();
            _logger.LogInformation(
                                    "FlareSolverr responded. Url={Url}, Status={Status}, TimeMs={Time}",
                                    url,
                                    resp.StatusCode,
                                    sw.ElapsedMilliseconds);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("FlareSolverr returned non-success status {StatusCode} for {Url}",
                    resp.StatusCode, url);
            }

            using var doc = JsonDocument.Parse(body);
            
            if (!doc.RootElement.TryGetProperty("solution", out var solution) ||
                        !solution.TryGetProperty("response", out var responseElement))
            {
                _logger.LogError(
                    "FlareSolverr unexpected JSON structure for {Url}",
                    url);

                throw new InvalidOperationException("Invalid FlareSolverr response format");
            }

            var response = responseElement.GetString();

            if (string.IsNullOrEmpty(response))
            {
                _logger.LogWarning("FlareSolverr returned empty response for {Url}", url);
            }

            _logger.LogInformation("FlareSolverr request finished for {Url}", url);
            return response!;
        }
        catch (JsonException ex)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "FlareSolverr JSON parse error for {Url}",
                url);

            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "FlareSolverr request failed for {Url}",
                url);

            throw;
        }
    }
}
