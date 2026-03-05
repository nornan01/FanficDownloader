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

    public async Task<Fanfic> GetFanficAsync(string url, CancellationToken ct)
    {
        _logger.LogInformation("Fetching fanfic info from snapetales.com for {Url}", url);
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
                var html = await _http.GetStringAsync(chapter.Url, ct);
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
