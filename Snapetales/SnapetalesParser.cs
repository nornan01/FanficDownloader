using FanficDownloader.Bot.Models;
using HtmlAgilityPack;
using System.Text;

namespace FanficDownloader.Bot.Snapetales;

public class SnapetalesParser
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
            Chapters = ParseChapters(doc)
        };
    }


    private string? ParseTitle(HtmlDocument doc)
    {
        return doc.DocumentNode
            .SelectSingleNode("//h3")
            ?.InnerText.Trim();
    }
//
    private List<string> ParseAuthors(HtmlDocument doc)
    {
        return doc.DocumentNode
            .SelectNodes("//tr[td[1][contains(text(),'Автор')]]/td[2]//a")
            ?.Select(a => a.InnerText.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList()
        ?? new List<string>();
    }
  //
    private List<string> ParseFandoms(HtmlDocument doc)
    {
        return doc.DocumentNode
            .SelectNodes("//tr[td[1][contains(text(),'Фандом')]]/td[2]//a")
            ?.Select(a => a.InnerText.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList()
        ?? new List<string>();
    }
    //
    private List<string> ParsePairings(HtmlDocument doc)
    {
        var text = doc.DocumentNode
            .SelectSingleNode("//tr[td[1][contains(text(),'Пейринг')]]/td[2]")
            ?.InnerText;

        return text?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToList()
        ?? new List<string>();
    }
    //
    private List<string> ParseTags(HtmlDocument doc)
    {
        var tags = new List<string>();

        var genres = doc.DocumentNode
            .SelectNodes("//tr[td[1][contains(text(),'Жанр')]]/td[2]//a");

        if (genres != null)
            tags.AddRange(genres.Select(a => a.InnerText.Trim()));

        var warnings = doc.DocumentNode
            .SelectSingleNode("//tr[td[1][contains(text(),'Предупреждения')]]/td[2]")
            ?.InnerText;

        if (!string.IsNullOrWhiteSpace(warnings))
        {
            tags.AddRange(
                warnings.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(w => w.Trim())
            );
        }

        return tags;
    }


    private string? ParseDescription(HtmlDocument doc)
    {
        var node = doc.DocumentNode
            .SelectSingleNode("//tr[td[1][contains(text(),'Аннотация')]]/td[2]");

        return node != null
            ? HtmlEntity.DeEntitize(node.InnerText.Trim())
            : null;
    }
    //
    private List<Chapter> ParseChapters(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//a[contains(@class,'no_decoration12') and contains(@href,'ch_id=')]");


        if (nodes == null)
            return new List<Chapter>();

        var chapters = new List<Chapter>();
        int number = 1;

        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", "");
            if (!href.Contains("ch_id="))
                continue;

            chapters.Add(new Chapter
            {
                Number = number++,
                Title = HtmlEntity.DeEntitize(node.InnerText.Trim()),
                Url = "https://www.snapetales.com/" + href
            });
        }

        return chapters;
    }



    public string ParseChapterText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var blockquote = doc.DocumentNode.SelectSingleNode("//blockquote");
        if (blockquote == null)
            return string.Empty;

        // заменяем <br> на перенос строки
        foreach (var br in blockquote.SelectNodes(".//br") ?? Enumerable.Empty<HtmlNode>())
            br.ParentNode.ReplaceChild(doc.CreateTextNode("\n"), br);

        return HtmlEntity.DeEntitize(blockquote.InnerText).Trim();
    }


}