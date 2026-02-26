using HtmlAgilityPack;
using FanficDownloader.Core.Models;
using System.Text.RegularExpressions;
using System.Text;

namespace FanficDownloader.Core.Parsers;

public class FicbookParser
{
    public Fanfic Parse(string html)
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
            Chapters = ParseChapters(doc),
            CoverUrl = ParseCover(doc)
        };
    }

    private string ParseTitle(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//h1");
        return HtmlEntity.DeEntitize(node?.InnerText ?? "").Trim();
    }

    private List<string> ParseAuthors(HtmlDocument doc)
    {
        return doc.DocumentNode
            .SelectNodes("//a[@itemprop='author']")?
            .Select(a => a.InnerText.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct()
            .ToList()
            ?? new List<string>();
    }

//the
    private List<string> ParseFandoms(HtmlDocument doc)
    {
        var scripts = doc.DocumentNode.SelectNodes("//script");
        if (scripts == null)
            return new List<string>();

        foreach (var script in scripts)
        {
            var text = script.InnerText;

            if (text.Contains("fanfic_fandoms"))
            {
                var match = Regex.Match(
                    text,
                    @"fanfic_fandoms:\s*\[(.*?)\]",
                    RegexOptions.Singleline
                );

                if (match.Success)
                {
                    var inside = match.Groups[1].Value;

                    return inside
                        .Split(',')
                        .Select(s => HtmlEntity.DeEntitize(
                            s.Trim().Trim('\'', '"')
                        ))
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                }
            }
        }

        return new List<string>();
    }



    private List<string> ParsePairings(HtmlDocument doc)
    {
        return doc.DocumentNode
            .SelectNodes("//div[contains(@class,'description')]//div[@class='mb-10'][.//strong[text()='Пэйринг и персонажи:']]//a")?
            .Select(x => x.InnerText.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList()
            ?? new List<string>();
    }

    private List<string> ParseTags(HtmlDocument doc)
    {
        return doc.DocumentNode
            .SelectNodes("//div[contains(@class,'description')]//div[@class='mb-10'][.//strong[text()='Метки:']]//a[contains(@class,'tag')]")?
            .Select(x => x.InnerText.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList()
            ?? new List<string>();
    }

    private string ParseDescription(HtmlDocument doc)
    {
        var node = doc.DocumentNode.SelectSingleNode("//div[@itemprop='description']");
        return HtmlEntity.DeEntitize(node?.InnerText ?? "Описание отсутствует").Trim();
    }

    private string? ParseCover(HtmlDocument doc)
    {
        var node = doc.DocumentNode
            .SelectSingleNode("//fanfic-cover");

        return node?.GetAttributeValue("src-original", null)
            ?? node?.GetAttributeValue("src-desktop", null)
            ?? node?.GetAttributeValue("src-mobile", null);
    }


    private List<Chapter> ParseChapters(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes(
            "//ul[contains(@class,'list-of-fanfic-parts')]//li[contains(@class,'part')]"
        );

        var chapters = new List<Chapter>();
        if (nodes != null)
        {
            int number = 1;

        foreach (var li in nodes)
        {
            var linkNode = li.SelectSingleNode(".//a[contains(@class,'part-link')]");
            var titleNode = li.SelectSingleNode(".//h3");

            if (linkNode == null)
                continue;

            var href = linkNode.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href))
                continue;

            var fullUrl = "https://ficbook.net" + href + "?adult=true";

            chapters.Add(new Chapter
            {
                Number = number++,
                Title = titleNode?.InnerText.Trim() ?? $"Chapter {number - 1}",
                Url = fullUrl
            });
        }

        return chapters;
        }
        chapters.Add(new Chapter
        {
            Number = 1,
            //here change if the web is eng - TODO
            Title = "Текст книги",
            Url = null,
        });
        return chapters;
    }



    public string ParseChapterText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var content = doc.DocumentNode.SelectSingleNode("//div[@id='content']");
        
        if (content == null)
            return "";

        content.SelectNodes(".//div[contains(@class,'fb-ads-block')]")
            ?.ToList()
            .ForEach(n => n.Remove());

        var raw = content.InnerHtml
            .Replace("\r", "");

        // разбиваем на абзацы по пустым строкам
        var blocks = Regex.Split(raw, @"\n\s*\n");

        var sb = new StringBuilder();

        foreach (var block in blocks)
        {
            var temp = new HtmlDocument();
            temp.LoadHtml(block);

            var inner = new StringBuilder();
            foreach (var n in temp.DocumentNode.ChildNodes)
                inner.Append(CleanNode(n));

            var text = inner.ToString().Trim();
            if (text.Length > 0)
                sb.Append($"<p>{text}</p>\n");
        }

        return sb.ToString();
    }

    private string CleanNode(HtmlNode node)
    {
        if (node.Name == "#text")
        {
            return System.Net.WebUtility.HtmlEncode(
                HtmlEntity.DeEntitize(node.InnerText)
            );
        }

        var name = node.Name.ToLower();

        switch (name)
        {
            case "i":
            case "em":
                return $"<em>{CleanChildren(node)}</em>";

            case "b":
            case "strong":
                return $"<strong>{CleanChildren(node)}</strong>";

            case "span":
                var cls = node.GetAttributeValue("class", "");
                if (cls.Contains("italic"))
                    return $"<em>{CleanChildren(node)}</em>";
                return CleanChildren(node);

            case "br":
                return "<br/>";

            case "img":
                var src = node.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src))
                    return $"<img src=\"{src}\" />";

                return "";

            case "div":
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
