using FanficDownloader.Core.Models;
using FanficDownloader.Core.Parsers;
using FanficDownloader.Core.Clients;
using Microsoft.Extensions.Logging;
using FanficDownloader.Application.Services;
using System.Net;



namespace FanficDownloader.Core.Sources;


public class FicbookSource : IFanficSource
{
    private readonly HttpClient _http;
    private readonly FicbookParser _parser;
    private readonly ILogger<FicbookSource> _logger;
    private readonly ProxyService _proxyService;
    private string? _workingProxy;

    public FicbookSource(HttpClient http, FicbookParser parser, ILogger<FicbookSource> logger, ProxyService proxyService)
    {
        _http = http;
        _parser = parser;
        _logger = logger;
        _proxyService = proxyService;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/120.0.0.0 Safari/537.36"
        );
    }

    public bool CanHandle(string url)
        => url.Contains("ficbook.net");

    public async Task<Fanfic> GetFanficAsync(string url, CancellationToken ct)
    {
        _logger.LogInformation("Fetching fanfic info from ficbook.net for {Url}", url);
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

        request.Headers.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

        var response = await SendWithFallbackAsync(request, ct);

        
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var fanfic = _parser.Parse(html);
        fanfic.SourceUrl = url;

        if (fanfic.Chapters.Count == 1 && string.IsNullOrEmpty(fanfic.Chapters[0].Url))
        {
            fanfic.Chapters[0].Url = url + "?adult=true";
            _logger.LogDebug("Single-chapter fanfic without chapter URL. Using adult URL for {Url}", url);
        }

        _logger.LogInformation("Parsed fanfic info for {Url}. Chapters: {ChapterCount}", url, fanfic.Chapters.Count);
        return fanfic;
        
    }

    public async Task<DownloadResult> PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        _logger.LogInformation("Populating chapters for {Url}. Total chapters: {TotalChapters}",
            fanfic.SourceUrl, fanfic.Chapters.Count);
        var result = new DownloadResult
        {
            Fanfic = fanfic,
            TotalChapters = fanfic.Chapters.Count
        };

        foreach (var chapter in fanfic.Chapters)
        {
            try
            {
                if (!string.IsNullOrEmpty(chapter.Text))
                    continue;

                _logger.LogDebug("Fetching chapter {ChapterNumber} from {ChapterUrl}",
                    chapter.Number, chapter.Url);
                var request = new HttpRequestMessage(HttpMethod.Get, chapter.Url);
                var response = await SendWithFallbackAsync(request, ct);
                var html = await response.Content.ReadAsStringAsync(ct);
                chapter.Text = _parser.ParseChapterText(html);
                result.LoadedChapters++;

                await Task.Delay(Random.Shared.Next(1200, 2500), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load chapter {ChapterNumber} from {ChapterUrl}",
                    chapter.Number, chapter.Url);
                result.FailedChapters.Add(chapter.Number);
            }
        }

        _logger.LogInformation("Finished populating chapters for {Url}. Loaded: {Loaded}. Failed: {Failed}",
            fanfic.SourceUrl, result.LoadedChapters, result.FailedChapters.Count);
        return result;
    }
    private async Task<HttpResponseMessage> SendWithFallbackAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_workingProxy != null)
        {
            try
            {
                var proxy = new WebProxy(_workingProxy);

                var handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true
                };

                using var proxyClient = new HttpClient(handler);

                proxyClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/120.0.0.0 Safari/537.36"
                );

                var proxyResponse = await proxyClient.SendAsync(request, ct);

                if (proxyResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Using cached working proxy");
                    return proxyResponse;
                }
            }
            catch
            {
                _workingProxy = null;
            }
        }

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var proxyUrl = _proxyService.GetRandomProxy();

                if (proxyUrl == null)
                    break;

                var proxy = new WebProxy(proxyUrl);

                var handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true
                };

                using var proxyClient = new HttpClient(handler);

                proxyClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/120.0.0.0 Safari/537.36"
                );

                var proxyResponse = await proxyClient.SendAsync(request, ct);

                if (proxyResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Proxy attempt {Attempt} succeeded", attempt);
                    _workingProxy = proxyUrl;
                    return proxyResponse;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Proxy attempt {Attempt} failed", attempt);
            }
        }

        _logger.LogInformation("All proxy attempts failed");
        throw new HttpRequestException("All proxy attempts failed");
    }
}
