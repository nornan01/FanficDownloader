using FanficDownloader.Core.Models;
using FanficDownloader.Core.Parsers;

namespace FanficDownloader.Core.Sources;


public class SnapetalesSource : IFanficSource
{
    private readonly HttpClient _http = new();
    private readonly SnapetalesParser _parser = new();

    public bool CanHandle(string url)
        => url.Contains("snapetales.com");

    public async Task<Fanfic> GetFanficAsync(string url, CancellationToken ct)
    {
        var html = await _http.GetStringAsync(url, ct);
        var fanfic = _parser.Parse(html);
        fanfic.SourceUrl = url;
        
        if (fanfic.Chapters.Count == 1 && string.IsNullOrEmpty(fanfic.Chapters[0].Url))
        {
            fanfic.Chapters[0].Url = fanfic.SourceUrl;
        }
        return fanfic;
    }
    public async Task<DownloadResult> PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
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

                var html = await _http.GetStringAsync(chapter.Url, ct);
                chapter.Text = _parser.ParseChapterText(html);
                result.LoadedChapters++;
                await Task.Delay(Random.Shared.Next(1200, 2500), ct);
            }
            catch
            {
                result.FailedChapters.Add(chapter.Number);
            }
        }
        return result;
    }

}
