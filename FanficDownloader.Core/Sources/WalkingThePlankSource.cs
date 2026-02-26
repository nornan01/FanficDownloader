using FanficDownloader.Core.Models;
using FanficDownloader.Core.Parsers;
using System.Text.RegularExpressions;

namespace FanficDownloader.Core.Sources;

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

    public async Task<DownloadResult> PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        var result = new DownloadResult
        {
            Fanfic = fanfic,
            TotalChapters = fanfic.Chapters.Count
        };

        try
        {
            var html = await _http.GetStringAsync(fanfic.SourceUrl, ct);

            var chapterTexts = _parser.ParseAllChapterTexts(html);

            for (int i = 0; i < fanfic.Chapters.Count && i < chapterTexts.Count; i++)
            {
                fanfic.Chapters[i].Text = SanitizeForXhtml(chapterTexts[i]);
                result.LoadedChapters++;
            }
            
        }
        catch
        {
            result.FailedChapters.AddRange(
                fanfic.Chapters.Select(c => c.Number)
            );
        }

        return result;
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


    private static string SanitizeForXhtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        html = html.Replace("&nbsp;", "&#160;");
        html = Regex.Replace(html, "&(?!#?\\w+;)", "&amp;");
        html = Regex.Replace(html, "<br>", "<br />", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<img([^>]*?)>", "<img$1 />", RegexOptions.IgnoreCase);

        return html;
    }


}
