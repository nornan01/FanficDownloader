using System.Text;
using FanficDownloader.Core.Models;
using FanficDownloader.Core.Parsers;
using Microsoft.Extensions.Logging;


namespace FanficDownloader.Core.Sources;


public class SnapetalesSource : IFanficSource
{
    private readonly SnapetalesParser _parser;
    private readonly HttpClient _http;
    private readonly ILogger<SnapetalesSource> _logger;

    public SnapetalesSource(SnapetalesParser parser, HttpClient http, ILogger<SnapetalesSource> logger)
    {
        _parser = parser;
        _http = http;
        _logger = logger;
    }

    public bool CanHandle(string url)
        => url.Contains("snapetales.com");

    public async Task<Fanfic> GetFanficAsync(string url, string sessionId, CancellationToken ct)
    {
        if(url.Contains("index.php")){
            _logger.LogInformation("Fetching fanfic info from snapetales.com for {Url}, session {SessionId}", url, sessionId);
            var html = await _http.GetStringAsync(url, ct);
            var fanfic = _parser.Parse(html);
            fanfic.SourceUrl = url;

            if (fanfic.Chapters.Count == 1 && string.IsNullOrEmpty(fanfic.Chapters[0].Url))
                {
                    fanfic.Chapters[0].Url = fanfic.SourceUrl;
                    _logger.LogDebug("Single-chapter fanfic without chapter URL. Using source URL for {Url}", url);
                }
                _logger.LogInformation("Parsed fanfic info for {Url}. Chapters: {ChapterCount}", url, fanfic.Chapters.Count);
                return fanfic;
            }else if (url.Contains("all.php"))
            {
                _logger.LogInformation("Fetching fanfic info from snapetales.com for {Url}, session {SessionId}", url, sessionId);
            var bytes = await _http.GetByteArrayAsync(url, ct);
            var html = Encoding.GetEncoding("windows-1251").GetString(bytes);
            try
                {
                    var fanfic = _parser.ParseFullText(html);
                fanfic.SourceUrl = url;
                _logger.LogInformation("Parsed full-text fanfic. Session: {SessionId}, Chapters: {Count}", sessionId, fanfic.Chapters.Count);
                return fanfic;
                }
                 catch (Exception ex)
                 {
                       _logger.LogError(ex, "Failed to parse full-text fanfic from {Url}. Session: {SessionId}", url, sessionId);
                        throw;
                   }
            }
            else{
                _logger.LogWarning("Unsupported Snapetales URL format: {Url}", url);
                 throw new NotSupportedException("Unsupported Snapetales URL format");
           }
    }
    public async Task PopulateChaptersAsync(Fanfic fanfic, string sessionId, DownloadProgress progress, CancellationToken ct)
    {
        _logger.LogInformation("Populating chapters for {Url}. Total chapters: {TotalChapters}",
            fanfic.SourceUrl, fanfic.Chapters.Count);
        progress.TotalChapters = fanfic.Chapters.Count;
        progress.CompletedChapters = 0;
        var loadedChapters = 0;
        var failedChapters = new List<int>();
        foreach (var chapter in fanfic.Chapters)
        {
            try
            {
                if (chapter.Text == null)
                    continue;

                _logger.LogDebug("Fetching chapter {ChapterNumber} from {ChapterUrl}",
                    chapter.Number, chapter.Url);
                var html = await _http.GetStringAsync(chapter.Url, ct);
                chapter.Text = _parser.ParseChapterText(html);
                loadedChapters++;
                progress.CompletedChapters = loadedChapters;
                await Task.Delay(Random.Shared.Next(1200, 2500), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load chapter {ChapterNumber} from {ChapterUrl}",
                    chapter.Number, chapter.Url);
                failedChapters.Add(chapter.Number);
            }
        }
        _logger.LogInformation("Finished populating chapters for {Url}. Loaded: {Loaded}. Failed: {Failed}",
            fanfic.SourceUrl, loadedChapters, failedChapters.Count);
    }
    public Task DestroySessionAsync(string sessionId)
    {
        // No session management needed for Snapetales
        return Task.CompletedTask;
    }
}
