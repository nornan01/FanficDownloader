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

    public async Task<Fanfic> GetFanficAsync(string url, CancellationToken ct)
    {

        _logger.LogInformation("Fetching fanfic info from fanfiction.net for {Url}", url);
        var html = await _flare.GetAsync(url, ct);
        var fanfic = _parser.Parse(html, url);
        fanfic.SourceUrl = url;
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
                var html = await _flare.GetAsync(chapter.Url, ct);
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


}