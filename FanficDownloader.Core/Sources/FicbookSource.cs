using FanficDownloader.Core.Models;
using FanficDownloader.Core.Parsers;
using FanficDownloader.Core.Clients;
using Microsoft.Extensions.Logging;
using FanficDownloader.Core.Services;
using System.Net;



namespace FanficDownloader.Core.Sources;


public class FicbookSource : IFanficSource
{
    private readonly HttpClient _http;
    private readonly FicbookParser _parser;
    private readonly ILogger<FicbookSource> _logger;
    private readonly ProxyService _proxyService;
    private string? _workingProxy;

    private readonly FlareSolverrClient _flareSolverr;
    private bool _useFlareSolverr = false;

    public FicbookSource(HttpClient http, FicbookParser parser, ILogger<FicbookSource> logger, ProxyService proxyService, FlareSolverrClient flareSolverr)
    {
        _http = http;
        _parser = parser;
        _logger = logger;
        _proxyService = proxyService;
        _flareSolverr = flareSolverr;
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
        var sessionId = Guid.NewGuid().ToString();
        _logger.LogInformation("Fetching fanfic info from ficbook.net for {Url}", url);
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

        request.Headers.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

        var response = await SendWithFallbackAsync(request, sessionId, ct);

        
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var fanfic = _parser.Parse(html);
        fanfic.SourceUrl = url;
        fanfic.SessionId = sessionId;

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
                var response = await SendWithFallbackAsync(request, fanfic.SessionId, ct);
                var html = await response.Content.ReadAsStringAsync(ct);
                chapter.Text = _parser.ParseChapterText(html);
                chapter.EndNotes = _parser.ParseChapterEndNotes(html);
                chapter.StartNotes = _parser.ParseChapterStartNotes(html);
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
    private async Task<HttpResponseMessage> SendWithFallbackAsync(HttpRequestMessage request, string? sessionId, CancellationToken ct)
    {
        // CHANGE: убрали _useFlareSolverr, теперь локальный флаг
        bool proxyFailed = false;
        string? workingProxy = _workingProxy;

        // CHANGE: оставили локальную функцию (как ты хочешь)
        async Task<HttpResponseMessage?> TryProxy(string proxyUrl)
        {
            try
            {
                var uri = new Uri(proxyUrl);

                var proxy = new WebProxy($"{uri.Scheme}://{uri.Host}:{uri.Port}")
                {
                    Credentials = new NetworkCredential(
                        uri.UserInfo.Split(':')[0],
                        uri.UserInfo.Split(':')[1]
                    )
                };

                var handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true
                };

                using var proxyClient = new HttpClient(handler);

                proxyClient.DefaultRequestHeaders.Accept.ParseAdd(
                    "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

                proxyClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(
                    "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

                proxyClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/120.0.0.0 Safari/537.36"
                );

                var proxyRequest = new HttpRequestMessage(HttpMethod.Get, request.RequestUri);

                foreach (var header in request.Headers)
                    proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

                var response = await proxyClient.SendAsync(proxyRequest, ct);

                if (response.IsSuccessStatusCode)
                    return response;

                return null;
            }
            catch
            {
                return null;
            }
        }

        // CHANGE: пробуем cached proxy один раз
        if (!proxyFailed && workingProxy != null)
        {
            var response = await TryProxy(workingProxy);

            if (response != null)
            {
                _proxyService.MarkSuccess(workingProxy);
                return response;
            }

            // CHANGE: если умер — больше не пробуем прокси в этом запросе
            _proxyService.MarkFailed(workingProxy);
            _workingProxy = null;
            workingProxy = null;
            proxyFailed = true; // 🔥 ВАЖНО
        }

        // CHANGE: новые прокси только если ещё не было fail
        if (!proxyFailed)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                string? proxyUrl = _proxyService.GetRandomProxy();

                if (proxyUrl == null)
                    break;

                var response = await TryProxy(proxyUrl);

                if (response != null)
                {
                    _workingProxy = proxyUrl;
                    _proxyService.MarkSuccess(proxyUrl);
                    return response;
                }

                _proxyService.MarkFailed(proxyUrl);
            }

            // CHANGE: после 3 фейлов — отключаем прокси для этого запроса
            proxyFailed = true; // 🔥 ВАЖНО
            _workingProxy = null;
        }

        // CHANGE: всегда идём в FlareSolverr без _useFlareSolverr
        var actualSessionId = sessionId ?? Guid.NewGuid().ToString();

        var html = await _flareSolverr.GetAsync(request.RequestUri!.ToString(), actualSessionId, ct);

        if (string.IsNullOrWhiteSpace(html))
            throw new HttpRequestException($"FlareSolverr returned empty HTML for {request.RequestUri}");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        };
    }
}
