using FanficDownloader.Core.Models;
using HtmlAgilityPack;
using System.Text;
using System.Net;

namespace FanficDownloader.Core.Parsers;

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
            Pairings = new List<string> { "Severus Snape/Harry Potter" },
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
                Url = null 
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

        return nodes.Select(CleanChapter).ToList();
    }

    private string CleanChapter(HtmlNode chapter)
    {
        var sb = new StringBuilder();

        foreach (var node in chapter.ChildNodes)
        {
            var block = CleanNode(node);
            if (!string.IsNullOrWhiteSpace(block))
                sb.Append(block);
        }

        return sb.ToString();
    }
    private string CleanNode(HtmlNode node)
    {
        if (node.Name == "#text")
        {
            var t = HtmlEntity.DeEntitize(node.InnerText);
            return WebUtility.HtmlEncode(t);
        }

        var name = node.Name.ToLower();

        switch (name)
        {
            case "p":
                return $"<p>{CleanChildren(node)}</p>\n";

            case "br":
                return "<br/>";

            case "i":
            case "em":
                return $"<em>{CleanChildren(node)}</em>";

            case "b":
            case "strong":
                return $"<strong>{CleanChildren(node)}</strong>";

            case "blockquote":
                return $"<blockquote>{CleanChildren(node)}</blockquote>\n";

            case "span":
                return CleanChildren(node);   // WalkingThePlank часто кладёт курсив в span

            case "div":
                return CleanChildren(node);

            default:
                return "";   // вырезаем мусор (script, style, etc)
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