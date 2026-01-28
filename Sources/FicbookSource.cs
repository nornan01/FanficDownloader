using FanficDownloader.Bot.Models;
using FanficDownloader.Bot.Ficbook;


namespace FanficDownloader.Bot.Sources;

public class FicbookSource : IFanficSource
{
    private readonly FicbookClient _client = new();
    private readonly FicbookParser _parser = new();

    public bool CanHandle(string url)
        => url.Contains("ficbook.net");

    public async Task<Fanfic> GetFanficAsync(string url, CancellationToken ct)
    {
        var html = await _client.LoadHtmlAsync(url, ct);
        var fanfic = _parser.Parse(html);

        foreach (var chapter in fanfic.Chapters)
        {
            var chapterHtml = await _client.LoadHtmlAsync(chapter.Url, ct);
            chapter.Text = _parser.ParseChapterText(chapterHtml);
            await Task.Delay(Random.Shared.Next(1200, 2500), ct);
        }

        return fanfic;
    }
}
