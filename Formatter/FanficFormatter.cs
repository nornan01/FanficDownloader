using System.Text;
using FanficDownloader.Bot.Models;

namespace FanficDownloader.Bot.Formatting;


public class FanficFormatter
{
    public string ToTxt(Fanfic fanfic)
    {
        var sb = new StringBuilder();

        sb.AppendLine(fanfic.Title);
        sb.AppendLine(new string('=', fanfic.Title.Length));
        sb.AppendLine();

        if (fanfic.Authors.Any())
        {
            sb.AppendLine("Авторы:");
            sb.AppendLine(string.Join(", ", fanfic.Authors));
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(fanfic.Description))
        {
            sb.AppendLine("Описание:");
            sb.AppendLine(fanfic.Description);
            sb.AppendLine();
        }

        foreach (var chapter in fanfic.Chapters.OrderBy(c => c.Number))
        {
            sb.AppendLine(chapter.Title);
            sb.AppendLine(new string('-', chapter.Title.Length));
            sb.AppendLine();
            sb.AppendLine(chapter.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
