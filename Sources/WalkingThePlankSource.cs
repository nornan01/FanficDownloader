using FanficDownloader.Bot.Models;
using FanficDownloader.Bot.WalkingThePlank;

namespace FanficDownloader.Bot.Sources;

public class WalkingThePlankSource : IFanficSource
{
    private readonly HttpClient _http = new();
    private readonly WalkingThePlankParser _parser = new();

    public bool CanHandle(string url)
        => url.Contains("walkingtheplank.org");

    public async Task<Fanfic> GetFanficAsync(string url, CancellationToken ct)
    {
        var printable = MakePrintableAllUrl(url);

        var html = await _http.GetStringAsync(printable, ct);

        var fanfic = _parser.Parse(html);
        fanfic.SourceUrl = printable;

        return fanfic;
    }
    //why the hell async??
    public async Task PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        var html = await _http.GetStringAsync(fanfic.SourceUrl, ct);

        var chapterTexts = _parser.ParseAllChapterTexts(html);

        for (int i = 0; i < fanfic.Chapters.Count && i < chapterTexts.Count; i++)
        {
            fanfic.Chapters[i].Text = chapterTexts[i];
        }
    }


    private string MakePrintableAllUrl(string url)
    {
        var uri = new Uri(url);
        var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);

        // Read existing values
        var sid = qs["sid"];

        // Rebuild query in the exact order you need
        var newQuery =
            "action=printable" +
            "&textsize=0" +
            "&sid=" + System.Web.HttpUtility.UrlEncode(sid) +
            "&chapter=all";

        return uri.GetLeftPart(UriPartial.Path) + "?" + newQuery;
    }

}
