using HtmlAgilityPack;
using FanficDownloader.Bot.Models;
using System.Text.RegularExpressions;

namespace FanficDownloader.Bot.Ficbook;

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
        return node?.InnerText.Trim() ?? "No title found";
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
                        .Select(s => s.Trim().Trim('\'', '"'))
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
        return node?.InnerText.Trim() ?? "Описание отсутствует";
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

        if (nodes == null)
            return new List<Chapter>();

        var chapters = new List<Chapter>();
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



    public string ParseChapterText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var contentNode = doc.DocumentNode.SelectSingleNode("//div[@id='content']");

        if (contentNode == null)
            return "";

        // убираем скрипты и мусор
        contentNode.SelectNodes(".//script|.//style")
            ?.ToList()
            .ForEach(n => n.Remove());

        var text = HtmlEntity.DeEntitize(contentNode.InnerText);

        // чистим пробелы
        text = text
            .Replace("\r", "")
            .Trim();

        return text;
    }



}
