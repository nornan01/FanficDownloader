using FanficDownloader.Core.Models;
using HtmlAgilityPack;
using System.Text;

namespace FanficDownloader.Core.Parsers;

public class FanfictionNetParser
{
    public Fanfic Parse(string html, string sourceUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return new Fanfic
        {
            Title = ParseTitle(doc),
            Authors = ParseAuthors(doc),
            Fandoms = ParseFandoms(doc),
            Pairings = ParsePairings(doc),
            Tags = ParseTags(doc),
            Description = ParseDescription(doc),
            Chapters = ParseChapters(doc, sourceUrl),
            CoverUrl = ParseCoverUrl(doc),
            SourceUrl = sourceUrl
        };
    }

    // ================= CHAPTER LIST =================

    private List<Chapter> ParseChapters(HtmlDocument doc, string baseUrl)
    {
        var storyId = ExtractStoryId(baseUrl);

        var options = doc.DocumentNode
            .SelectNodes("//select[@id='chap_select']/option");

        var chapters = new List<Chapter>();

        if (options == null)
        {
            chapters.Add(new Chapter
            {
                Number = 1,
                Title = "Chapter 1",
                Url = baseUrl
            });
            return chapters;
        }

        foreach (var opt in options
            .GroupBy(o => o.GetAttributeValue("value", "1"))
            .Select(g => g.First()))
        {
            var number = opt.GetAttributeValue("value", "1");
            var title = HtmlEntity.DeEntitize(opt.InnerText.Trim());

            chapters.Add(new Chapter
            {
                Number = int.Parse(number),
                Title = title,
                Url = $"https://www.fanfiction.net/s/{storyId}/{number}"
            });
        }

        return chapters;
    }


    private string ExtractStoryId(string url)
    {
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var i = Array.IndexOf(parts, "s");
        return parts[i + 1];
    }

    // ================= CHAPTER TEXT =================

    public string ParseChapterText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var container = doc.DocumentNode
            .SelectSingleNode("//div[@id='storytext']");

        if (container == null)
            throw new Exception("Fanfiction.net: storytext not found");

        var sb = new StringBuilder();

        foreach (var node in container.ChildNodes)
        {
            // разделители сцен
            if (node.Name == "hr")
            {
                sb.Append("<hr/>\n");
                continue;
            }

            if (node.Name != "p")
                continue;

            var inner = new StringBuilder();
            foreach (var child in node.ChildNodes)
                inner.Append(CleanNode(child));

            var text = inner.ToString().Trim();
            if (text.Length > 0)
                sb.Append($"<p>{text}</p>\n");
        }

        return sb.ToString();
    }

    // ================= META =================

    private string ParseTitle(HtmlDocument doc)
        => doc.DocumentNode
            .SelectSingleNode("//b[@class='xcontrast_txt']")
            ?.InnerText.Trim()
            ?? "Unknown title";

    private List<string> ParseAuthors(HtmlDocument doc)
    {
        var node = doc.DocumentNode
            .SelectSingleNode("//div[@id='profile_top']//a[contains(@href,'/u/')]");

        return node == null
            ? new()
            : new List<string> { HtmlEntity.DeEntitize(node.InnerText.Trim()) };
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

        return parts[3]
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

        return parts[2]
            .Split('/')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private string? GetMetaLine(HtmlDocument doc)
        => doc.DocumentNode
            .SelectSingleNode("//div[@id='profile_top']//span[contains(@class,'xgray')]")
            ?.InnerText;

    private List<string> ParseFandoms(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode
            .SelectNodes("//div[@id='pre_story_links']//a");

        if (nodes == null || nodes.Count == 0)
            return new();

        return new List<string>
        {
            HtmlEntity.DeEntitize(nodes.Last().InnerText.Trim())
        };
    }

    // ================= INLINE HTML =================

    private string CleanNode(HtmlNode node)
    {
        if (node.Name == "#text")
        {
            return System.Net.WebUtility.HtmlEncode(
                HtmlEntity.DeEntitize(node.InnerText)
                    .Replace("\u00A0", " ")
            );
        }

        switch (node.Name.ToLower())
        {
            case "i":
            case "em":
                return $"<em>{CleanChildren(node)}</em>";

            case "b":
            case "strong":
                return $"<strong>{CleanChildren(node)}</strong>";

            case "br":
                return "<br/>";

            case "span":
                return CleanChildren(node);

            default:
                return "";
        }
    }

    private string CleanChildren(HtmlNode node)
    {
        var sb = new StringBuilder();
        foreach (var c in node.ChildNodes)
            sb.Append(CleanNode(c));
        return sb.ToString();
    }
}
