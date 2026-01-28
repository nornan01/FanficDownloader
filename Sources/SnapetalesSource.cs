using FanficDownloader.Bot.Models;
using FanficDownloader.Bot.Snapetales;

namespace FanficDownloader.Bot.Sources;

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

        foreach (var chapter in fanfic.Chapters)
        {
            var chapterHtml = await _http.GetStringAsync(chapter.Url, ct);
            chapter.Text = _parser.ParseChapterText(chapterHtml);
//бля дело в парсере ебаный рот сука 6 часов
            File.WriteAllText("debugchaptertext.html", _parser.ParseChapterText(chapterHtml));
            File.WriteAllText("debug.html", chapterHtml);
            await Task.Delay(Random.Shared.Next(1200, 2500), ct);
        }

        return fanfic;
    }
}
