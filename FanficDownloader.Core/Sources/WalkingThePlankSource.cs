using FanficDownloader.Core.Models;
using FanficDownloader.Core.Parsers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace FanficDownloader.Core.Sources;

public class WalkingThePlankSource : IFanficSource
{
    private readonly HttpClient _http;
    private readonly WalkingThePlankParser _parser;
    private readonly ILogger<WalkingThePlankSource> _logger;

    public WalkingThePlankSource(
        HttpClient http,
        WalkingThePlankParser parser,
        ILogger<WalkingThePlankSource> logger)
    {
        _http = http;
        _parser = parser;
        _logger = logger;
    }
    public bool CanHandle(string url)
        => url.Contains("walkingtheplank.org");

    public async Task<Fanfic> GetFanficAsync(string url, CancellationToken ct)
    {
        var printable = MakePrintableAllUrl(url);

        _logger.LogInformation("Fetching fanfic info from walkingtheplank.org for {Url}. Printable: {PrintableUrl}",
            url, printable);
        var html = await _http.GetStringAsync(printable, ct);

        var fanfic = _parser.Parse(html);
        fanfic.SourceUrl = printable;

        _logger.LogInformation("Parsed fanfic info for {Url}. Chapters: {ChapterCount}", printable, fanfic.Chapters.Count);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load chapters for {Url}", fanfic.SourceUrl);
            result.FailedChapters.AddRange(
                fanfic.Chapters.Select(c => c.Number)
            );
        }

        _logger.LogInformation("Finished populating chapters for {Url}. Loaded: {Loaded}. Failed: {Failed}",
            fanfic.SourceUrl, result.LoadedChapters, result.FailedChapters.Count);
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
