using FanficDownloader.Core.Models;
using FanficDownloader.Core.Parsers;
using FanficDownloader.Core.Clients;



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

                var html = await _flare.GetAsync(chapter.Url, ct);
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
