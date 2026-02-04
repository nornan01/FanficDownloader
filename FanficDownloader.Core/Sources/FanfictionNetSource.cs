using FanficDownloader.Core.Models;
using FanficDownloader.Core.FanfictionNet;
using FanficDownloader.Core.Services;



namespace FanficDownloader.Core.Sources;

public class FanfictionNetSource : IFanficSource
{
    private readonly FanfictionNetParser _parser = new();
    private readonly FlareSolverrClient _flare = new();

    

    public bool CanHandle(string url)
        => url.Contains("fanfiction.net");
    
    public async Task<Fanfic> GetFanficAsync(string url, CancellationToken ct)
    {

        var html = await _flare.GetAsync(url, ct);
        var fanfic = _parser.Parse(html, url);
        fanfic.SourceUrl = url;
        return fanfic; 
    }
    public async Task PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        foreach (var chapter in fanfic.Chapters)
        {
            if (!string.IsNullOrEmpty(chapter.Text))
                continue;

            var html = await _flare.GetAsync(chapter.Url, ct);
            chapter.Text = _parser.ParseChapterText(html);

            await Task.Delay(Random.Shared.Next(1200, 2500), ct);
        }
    }

}
