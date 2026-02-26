using System.Text;
using FanficDownloader.Core.Models;
using System.Text.RegularExpressions;
using System.Net;


namespace FanficDownloader.Core.Formatting;


public class FanficTxtFormatter
{
    public string ToTxt(Fanfic fanfic)
    {
        var sb = new StringBuilder();

        sb.AppendLine(fanfic.Title);
        sb.AppendLine(new string('=', fanfic.Title.Length));
        sb.AppendLine();

        if (fanfic.Authors.Any())
        {
            sb.AppendLine("Authors:");
            sb.AppendLine(string.Join(", ", fanfic.Authors));
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(fanfic.Description))
        {
            sb.AppendLine("Description:");
            sb.AppendLine(fanfic.Description);
            sb.AppendLine();
        }

        foreach (var chapter in fanfic.Chapters.OrderBy(c => c.Number))
        {
            sb.AppendLine(chapter.Title);
            sb.AppendLine(new string('-', chapter.Title.Length));
            sb.AppendLine();
            sb.AppendLine(HtmlToPlainText(chapter.Text));
            sb.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("––––––––––––––––––––");
        sb.AppendLine();
        sb.AppendLine("Thank you for using Fanfic Downloader 💜");
        sb.AppendLine();
        sb.AppendLine("Join our Telegram channel for updates, new supported websites, and improvements:");
        sb.AppendLine("https://t.me/fanficdownloaderhub");
        sb.AppendLine();
        sb.AppendLine("Have suggestions or want to see support for another site?");
        sb.AppendLine("Send us your ideas — we’re building this together.");
        sb.AppendLine();
        sb.AppendLine("Happy reading ✨");

        return sb.ToString();
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var text = html;

        // абзацы
        text = Regex.Replace(text, @"</p>\s*<p>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<p>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</p>", "", RegexOptions.IgnoreCase);

        // разделители сцен
        text = Regex.Replace(text, @"<hr\s*/?>", "\n\n*****\n\n", RegexOptions.IgnoreCase);

        // <br>
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

        // всё остальное HTML
        text = Regex.Replace(text, @"<[^>]+>", "");

        // HTML entities
        text = WebUtility.HtmlDecode(text);

        return text.Trim();
    }

}
