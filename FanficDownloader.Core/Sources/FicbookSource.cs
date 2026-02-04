using FanficDownloader.Core.Models;
using FanficDownloader.Core.Ficbook;


namespace FanficDownloader.Core.Sources;

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
        fanfic.SourceUrl = url;
        return fanfic;
    }
    public async Task PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        foreach (var chapter in fanfic.Chapters)
        {
            if (!string.IsNullOrEmpty(chapter.Text))
                continue;

            var html = await _client.LoadHtmlAsync(chapter.Url, ct);
            chapter.Text = _parser.ParseChapterText(html);

            await Task.Delay(Random.Shared.Next(1200, 2500), ct);
        }
    }

}
