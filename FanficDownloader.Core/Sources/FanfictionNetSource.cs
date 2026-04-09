using FanficDownloader.Core.Models;
using FanficDownloader.Core.Parsers;
using FanficDownloader.Core.Clients;
using Microsoft.Extensions.Logging;



namespace FanficDownloader.Core.Sources;


public class FanfictionNetSource : IFanficSource
{
    private readonly FanfictionNetParser _parser;
    private readonly FlareSolverrClient _flare;
    private readonly ILogger<FanfictionNetSource> _logger;

    public FanfictionNetSource(
    FanfictionNetParser parser,
    FlareSolverrClient flare,
    ILogger<FanfictionNetSource> logger)
    {
        _parser = parser;
        _flare = flare;
        _logger = logger;
    }

    public bool CanHandle(string url)
        => url.Contains("fanfiction.net");

    public async Task<Fanfic> GetFanficAsync(string url, string sessionId, CancellationToken ct)
    {

        _logger.LogInformation("Fetching fanfic info from fanfiction.net for {Url}", url);
        await _flare.EnsureSessionAsync(sessionId, ct);
        var html = await _flare.GetAsync(url, sessionId, ct);
        var fanfic = _parser.Parse(html, url);
        fanfic.SourceUrl = url;
        _logger.LogInformation("Parsed fanfic info for {Url}. Chapters: {ChapterCount}", url, fanfic.Chapters.Count);
        return fanfic;
    }
    public async Task PopulateChaptersAsync(Fanfic fanfic, string sessionId, DownloadProgress progress, CancellationToken ct)
    {
        progress.TotalChapters = fanfic.Chapters.Count;
        progress.CompletedChapters = 0;
        _logger.LogInformation("Populating chapters for {Url}. Total chapters: {TotalChapters}",
            fanfic.SourceUrl, fanfic.Chapters.Count);
        var loadedChapters = 0;
        var failedChapters = new List<int>();
            
        foreach (var chapter in fanfic.Chapters)
        {
            try
            {
                if (!string.IsNullOrEmpty(chapter.Text))
                    continue;

                _logger.LogDebug("Fetching chapter {ChapterNumber} from {ChapterUrl}",
                    chapter.Number, chapter.Url);
                var html = await _flare.GetAsync(chapter.Url, sessionId, ct);
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
        return _flare.DestroySessionAsync(sessionId, CancellationToken.None);
    }

}
