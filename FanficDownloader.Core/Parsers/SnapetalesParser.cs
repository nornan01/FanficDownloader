using FanficDownloader.Core.Models;
using HtmlAgilityPack;
using Microsoft.VisualBasic;
using System.Text;
using System.Text.RegularExpressions;


namespace FanficDownloader.Core.Parsers;

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

    public Fanfic ParseFullText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables == null || tables.Count < 2)
            throw new Exception("Invalid Snapetales HTML structure");
        var metaTable = tables[0];
        var contentTable = tables[1];
        var contentTd = contentTable.SelectSingleNode(".//td");
        var normalized = NormalizeHtml(contentTd.InnerHtml);
        

        return new Fanfic
        {
            Title = metaTable.SelectSingleNode(".//h3")?.InnerText?.Trim() ?? "No Title",
            Authors = SplitToList(GetValue(metaTable, "Автор")),
            Pairings = SplitToList(GetValue(metaTable, "Пейринг")),
            Rating = GetValue(metaTable, "Рейтинг"),
            Fandoms = SplitToList(GetValue(metaTable, "Фандом")),
            Tags = SplitToList(GetValue(metaTable, "Жанр:")),
            OtherTags = SplitToList(GetValue(metaTable, "Предупреждения:")).Concat(SplitToList(GetValue(metaTable, "Каталог:"))).ToList(),
            Description = NormalizeHtml(GetHtmlValue(metaTable, "Аннотация")).Replace("<p>", "").Replace("</p>", "\n").Trim(),
            Notes = NormalizeHtml(GetHtmlValue(metaTable, "Комментарии")).Replace("<p>", "").Replace("</p>", "\n").Trim(),
            Chapters = SplitIntoChapters(normalized)

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

        var chapters = new List<Chapter>();
        
        if(nodes != null)
        {
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

        //fallback: если глав нет, то создаём одну с текстом всей книги
        var blockquote = doc.DocumentNode.SelectSingleNode("//blockquote");
        if (blockquote != null)
        {
            chapters.Add(new Chapter
            {
                Number = 1,
                Title = "Текст книги",
                Url = null,
            });
        }
        return chapters;

    }


    public string ParseChapterText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var blockquotes = doc.DocumentNode.SelectNodes("//blockquote");
        if (blockquotes == null || blockquotes.Count == 0)
            return string.Empty;

        var blockquote = blockquotes.Last();

        return NormalizeHtml(blockquote.InnerHtml);
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

            case "br":
                return "<br/>";

            case "img":
                var src = node.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src))
                    return $"<img src=\"{src}\" />";
                return "";
                
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
    

    private string GetValue(HtmlNode table, string label)
    {
        var rows = table.SelectNodes(".//tr");

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            if (cells == null || cells.Count < 2)
                continue;

            if (cells[0].InnerText.Contains(label))
                return HtmlEntity.DeEntitize(cells[1].InnerText.Trim());
        }

        return "";
    }
    private string GetHtmlValue(HtmlNode table, string label)
    {
        var rows = table.SelectNodes(".//tr");

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            if (cells == null || cells.Count < 2)
                continue;

            if (cells[0].InnerText.Contains(label))
                return cells[1].InnerHtml; // 🔥 ВАЖНО
        }

        return "";
    }
    private List<Chapter> SplitIntoChapters(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var chapters = new List<Chapter>();

        var nodes = doc.DocumentNode.ChildNodes;

        var currentContent = new StringBuilder();
        string currentTitle = "Текст";
        int number = 1;

        foreach (var node in nodes)
        {
            if (node.Name == "#text")
                continue;

            // глава
            if (node.Name == "p")
            {
                var strong = node.SelectSingleNode(".//strong");

                if (strong != null && strong.InnerText.Contains("Глава"))
                {
                    if (currentContent.Length > 0)
                    {
                        chapters.Add(new Chapter
                        {
                            Number = number++,
                            Title = currentTitle,
                            Text = currentContent.ToString()
                        });

                        currentContent.Clear();
                    }

                    currentTitle = HtmlEntity.DeEntitize(node.InnerText.Trim());
                    continue;
                }

                currentContent.Append(node.OuterHtml);
            }
            else if (node.Name == "img")
            {
                var src = node.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src))
                    currentContent.Append($"<img src=\"{src}\" />");
            }
        }

        if (currentContent.Length > 0)
        {
            chapters.Add(new Chapter
            {
                Number = number++,
                Title = currentTitle,
                Text = currentContent.ToString()
            });
        }

        return chapters;
    }

    private List<string> SplitToList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToList();
    }

    private string NormalizeHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var raw = doc.DocumentNode.InnerHtml
            .Replace("\r", "")
            .Replace("<br />", "<br>")
            .Replace("<br/>", "<br>")
            .Trim();

        // 🔥 нормализуем любые последовательности <br>
        raw = Regex.Replace(raw, @"(<br>\s*){2,}", "<br><br>");

        var parts = raw
            .Split(new[] { "<br><br>" }, StringSplitOptions.RemoveEmptyEntries);

        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            var cleaned = part.Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
                continue;

            var temp = new HtmlDocument();
            temp.LoadHtml(cleaned);

            var inner = new StringBuilder();

            bool hasImage = false;
            bool hasText = false;

            foreach (var node in temp.DocumentNode.ChildNodes)
            {
                if (node.Name == "img")
                    hasImage = true;

                if (node.Name == "#text" && !string.IsNullOrWhiteSpace(node.InnerText))
                    hasText = true;

                inner.Append(CleanNode(node));
            }

            var result = inner.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result))
                continue;

            // 💥 ЛОГИКА ОБЁРТКИ
            if (hasImage && !hasText)
            {
                // только картинка → НЕ оборачиваем
                sb.Append(result + "\n");
            }
            else
            {
                // текст или текст+картинка → оборачиваем
                sb.Append($"<p>{result}</p>\n");
            }
        }

        return sb.ToString();
    }


}