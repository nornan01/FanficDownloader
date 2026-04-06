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

    private readonly FlareSolverrClient _flareSolverr;

    public FicbookSource(HttpClient http, FicbookParser parser, ILogger<FicbookSource> logger, FlareSolverrClient flareSolverr)
    {
        _http = http;
        _parser = parser;
        _logger = logger;
        _flareSolverr = flareSolverr;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/120.0.0.0 Safari/537.36"
        );
    }

    public bool CanHandle(string url)
        => url.Contains("ficbook.net");

    public async Task<Fanfic> GetFanficAsync(string url, string sessionId, CancellationToken ct)
    {
        await _flareSolverr.EnsureSessionAsync(sessionId, ct); 
        _logger.LogInformation("SESSION START {SessionId} for {Url}", sessionId, url);
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

        request.Headers.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

        var response = await SendWithFallbackAsync(request, sessionId, ct);

        
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var fanfic = _parser.Parse(html);
        fanfic.SourceUrl = url;
        
        if (fanfic.Chapters.Count == 1 && string.IsNullOrEmpty(fanfic.Chapters[0].Url))
        {
            fanfic.Chapters[0].Url = url + "?adult=true";
            _logger.LogDebug("Single-chapter fanfic without chapter URL. Using adult URL for {Url}", url);
        }
        _logger.LogInformation(
            "SESSION {SessionId} parsed fanfic {Url} with {Chapters} chapters",
            sessionId,
            url,
            fanfic.Chapters.Count
        );
        return fanfic;
        
    }

    public async Task<DownloadResult> PopulateChaptersAsync(Fanfic fanfic, string sessionId, CancellationToken ct)
    {
        _logger.LogInformation(
                                "SESSION {SessionId} populating chapters for {Url}",
                                sessionId,
                                fanfic.SourceUrl
                                );
        var result = new DownloadResult
        {
            Fanfic = fanfic,
            TotalChapters = fanfic.Chapters.Count
        };
        
        foreach (var chapter in fanfic.Chapters)
        {
            if (!string.IsNullOrEmpty(chapter.Text))
                    continue;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
            {
                    _logger.LogInformation(
                    "SESSION {SessionId} → CHAPTER {ChapterNumber} → {ChapterUrl}",
                    sessionId,
                    chapter.Number,
                    chapter.Url
                );
                    var request = new HttpRequestMessage(HttpMethod.Get, chapter.Url);
                var response = await SendWithFallbackAsync(request, sessionId, ct);
                var html = await response.Content.ReadAsStringAsync(ct);
                chapter.Text = _parser.ParseChapterText(html);
                chapter.EndNotes = _parser.ParseChapterEndNotes(html);
                chapter.StartNotes = _parser.ParseChapterStartNotes(html);
                result.LoadedChapters++;
                var delay = Random.Shared.Next(1200, 2500);
                _logger.LogInformation("Sleeping {Delay} ms before next chapter", delay);
                await Task.Delay(delay, ct);
                break;
            }
            catch (Exception ex)
            {
                        _logger.LogWarning(ex, "Attempt {Attempt} failed for chapter {ChapterNumber}",
                        attempt, chapter.Number);

                        if (attempt == 2)
                        {
                            result.FailedChapters.Add(chapter.Number);
                        }
                        else
                        {
                            await Task.Delay(Random.Shared.Next(1500, 3000), ct); 
                        }
            }

                    
               
            }
            
            }
            _logger.LogInformation("Finished populating chapters for {Url}. Loaded: {Loaded}. Failed: {Failed}",
                        fanfic.SourceUrl, result.LoadedChapters, result.FailedChapters.Count);
            return result;
        
        
                
            
        
    }

    private async Task<HttpResponseMessage> SendWithFallbackAsync(HttpRequestMessage request, string sessionId, CancellationToken ct)
    {
        
        _logger.LogInformation("=== SendWithFallback START === Url={Url}", request.RequestUri);


        _logger.LogInformation("Using FlareSolverr. Session={SessionId}, Url={Url}", sessionId, request.RequestUri);

        var html = await _flareSolverr.GetAsync(request.RequestUri!.ToString(), sessionId, ct);

        if (string.IsNullOrWhiteSpace(html)){
            _logger.LogError("FlareSolverr returned EMPTY HTML for {Url}", request.RequestUri);
            throw new HttpRequestException($"FlareSolverr returned empty HTML for {request.RequestUri}");
        }
        _logger.LogInformation("FlareSolverr SUCCESS for {Url}", request.RequestUri);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        };
    }
    public Task DestroySessionAsync(string sessionId)
    {
        return _flareSolverr.DestroySessionAsync(sessionId, CancellationToken.None);
    }
}
