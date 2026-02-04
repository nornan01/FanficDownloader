using FanficDownloader.Core.Models;
using HtmlAgilityPack;
using System.Text;

namespace FanficDownloader.Core.WalkingThePlank;

public class WalkingThePlankParser
{
    public Fanfic Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);


        return new Fanfic
        {
            Title = ParseTitle(doc),
            Authors = ParseAuthors(doc),
            Fandoms = new List<string> { "Harry Potter" },
            Pairings = ExtractMetaList(doc, "Pairings"),
            Tags = ExtractMetaList(doc, "Genres")
                    .Concat(ExtractMetaList(doc, "Warnings"))
                    .ToList(),
            Description = ExtractMetaBlock(doc),
            Chapters = ParseChapters(doc)
        };
    }

    // ================= META =================

    private string ParseTitle(HtmlDocument doc)
        => doc.DocumentNode.SelectSingleNode("//div[@id='pagetitle']/a")?.InnerText.Trim() ?? "Unknown";

    private List<string> ParseAuthors(HtmlDocument doc)
    {
        var a = doc.DocumentNode.SelectSingleNode("//div[@id='pagetitle']/a[2]");
        return a != null ? new List<string> { a.InnerText.Trim() } : new();
    }

    private string GetInfoBoxValue(HtmlDocument doc, string label)
    {
        var node = doc.DocumentNode
            .SelectSingleNode($"//div[@class='infobox']//span[@class='label' and normalize-space()='{label}:']");

        if (node == null)
            return "";

        var sb = new StringBuilder();
        var cur = node.NextSibling;

        while (cur != null && cur.Name != "span")
        {
            sb.Append(cur.InnerText);
            cur = cur.NextSibling;
        }

        return HtmlEntity.DeEntitize(sb.ToString().Trim());
    }

    private string ExtractMetaBlock(HtmlDocument doc)
    {
        return GetInfoBoxValue(doc, "Summary");
    }

    private List<string> ExtractMetaList(HtmlDocument doc, string label)
    {
        var raw = GetInfoBoxValue(doc, label);

        if (string.IsNullOrWhiteSpace(raw))
            return new();

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }


    // ================= CHAPTERS =================

    private List<Chapter> ParseChapters(HtmlDocument doc)
    {
        var titleNodes = doc.DocumentNode.SelectNodes("//div[@class='chaptertitle']");
        var contentNodes = doc.DocumentNode.SelectNodes("//div[@class='chapter']");

        if (titleNodes == null || contentNodes == null)
            return new();

        var chapters = new List<Chapter>();

        for (int i = 0; i < Math.Min(titleNodes.Count, contentNodes.Count); i++)
        {
            var title = HtmlEntity.DeEntitize(titleNodes[i].InnerText.Trim());

            chapters.Add(new Chapter
            {
                Number = i + 1,
                Title = title,
                Url = null // важное: тут нет отдельных URL
            });
        }

        return chapters;
    }



    public List<string> ParseAllChapterTexts(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nodes = doc.DocumentNode.SelectNodes("//div[@class='chapter']");

        if (nodes == null)
            return new();

        return nodes.Select(n => CleanChapter(n)).ToList();
    }

    private string CleanChapter(HtmlNode node)
    {
        var html = node.InnerHtml
            .Replace("<br><br>", "\n\n")
            .Replace("<br>", "\n");

        return HtmlEntity.DeEntitize(html).Trim();
    }

}
