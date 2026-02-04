using FanficDownloader.Core.Models;
using FanficDownloader.Core.Snapetales;

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
        return fanfic;
    }
    public async Task PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        foreach (var chapter in fanfic.Chapters)
        {
            if (!string.IsNullOrEmpty(chapter.Text))
                continue;

            var html = await _http.GetStringAsync(chapter.Url, ct);
            chapter.Text = _parser.ParseChapterText(html);

            await Task.Delay(Random.Shared.Next(1200, 2500), ct);
        }
    }

}
