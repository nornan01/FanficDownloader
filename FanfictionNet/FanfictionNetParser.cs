using FanficDownloader.Bot.Models;
using HtmlAgilityPack;
using System.Text;

namespace FanficDownloader.Bot.FanfictionNet;

public class FanfictionNetParser
{
    public Fanfic Parse(string html, string sourceUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return new Fanfic
        {
            Title = ParseTitle(doc),//
            Authors = ParseAuthors(doc),//
            Fandoms = ParseFandoms(doc),//
            Pairings = ParsePairings(doc),//
            Tags = ParseTags(doc),//
            Description = ParseDescription(doc),//
            Chapters = ParseChapters(doc, sourceUrl),//
            CoverUrl = ParseCoverUrl(doc),//
            SourceUrl = sourceUrl
        };
    }

    private List<Chapter> ParseChapters(HtmlDocument doc, string baseUrl)
    {
        var storyId = ExtractStoryId(baseUrl);

        var options = doc.DocumentNode
            .SelectNodes("//select[@id='chap_select']/option");

        var chapters = new List<Chapter>();

        if (options == null)
        {
            // фанфик с одной главой
            chapters.Add(new Chapter
            {
                Number = 1,
                Title = "Chapter 1",
                Url = baseUrl
            });

            return chapters;
        }

        foreach (var opt in options)
        {
            var number = opt.GetAttributeValue("value", "1");
            var title = HtmlEntity.DeEntitize(opt.InnerText.Trim());

            var url = $"https://www.fanfiction.net/s/{storyId}/{number}";

            chapters.Add(new Chapter
            {
                Number = int.Parse(number),
                Title = title,
                Url = url
            });
        }

        return chapters;
    }

    private string ExtractStoryId(string url)
    {
        // https://www.fanfiction.net/s/14536087/1/Operation-Cupid-Hogwarts-Edition
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var i = Array.IndexOf(parts, "s");
        return parts[i + 1];
    }

    public string ParseChapterText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nodes = doc.DocumentNode
            .SelectNodes("//div[@id='storytext']/p");

        if (nodes == null || nodes.Count == 0)
            throw new Exception("Fanfiction.net: storytext not found");

        var sb = new StringBuilder();

        foreach (var node in nodes)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText)
                .Replace("\u00A0", " ")
                .Trim();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    private string ParseTitle(HtmlDocument doc)
    {
        return doc.DocumentNode
            .SelectSingleNode("//b[@class='xcontrast_txt']")
            ?.InnerText
            ?.Trim() ?? "Unknown title";
    }
    private List<string> ParseAuthors(HtmlDocument doc)
    {
        var node = doc.DocumentNode
            .SelectSingleNode("//div[@id='profile_top']//a[contains(@href,'/u/')]");

        if (node == null)
            return new List<string>();

        return new List<string>
    {
        HtmlEntity.DeEntitize(node.InnerText.Trim())
    };
    }
    private string ParseDescription(HtmlDocument doc)
    {
        var node = doc.DocumentNode
            .SelectSingleNode("//div[@id='profile_top']/div[@class='xcontrast_txt']");

        return node != null
            ? HtmlEntity.DeEntitize(node.InnerText.Trim())
            : "";
    }
    private string? ParseCoverUrl(HtmlDocument doc)
    {
        var img = doc.DocumentNode
            .SelectSingleNode("//div[@id='profile_top']//img");

        if (img == null)
            return null;

        var src = img.GetAttributeValue("src", null);

        if (string.IsNullOrEmpty(src))
            return null;

        if (src.StartsWith("//"))
            return "https:" + src;

        if (src.StartsWith("/"))
            return "https://www.fanfiction.net" + src;

        return src;
    }
    private List<string> ParsePairings(HtmlDocument doc)
    {
        var meta = GetMetaLine(doc);
        if (meta == null) return new();

        var parts = meta.Split(" - ");
        if (parts.Length < 4) return new();

        return parts[3]                   // "Harry P., Severus S., OC"
            .Split(',')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }
    private List<string> ParseTags(HtmlDocument doc)
    {
        var meta = GetMetaLine(doc);
        if (meta == null) return new();

        var parts = meta.Split(" - ");
        if (parts.Length < 3) return new();

        return parts[2]                  // "Romance/Humor"
            .Split('/')                  // ["Romance", "Humor"]
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private string? GetMetaLine(HtmlDocument doc)
    {
        var node = doc.DocumentNode
            .SelectSingleNode("//div[@id='profile_top']//span[contains(@class,'xgray')]");

        return node?.InnerText;
    }

    private List<string> ParseFandoms(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode
            .SelectNodes("//div[@id='pre_story_links']//a");

        if (nodes == null || nodes.Count == 0)
            return new();

        // Последняя ссылка — это fandom
        var fandom = nodes.Last().InnerText.Trim();

        return new List<string>
    {
        HtmlEntity.DeEntitize(fandom)
    };
    }

}
