using FanficDownloader.Core.Models;

public class FanficTelegramFormatter
{
    public string FormatInfoMessage(Fanfic fanfic)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"📖 Title: {fanfic.Title}");
        sb.AppendLine();

        if (fanfic.Authors.Any())
        {
            sb.AppendLine($"✍️ Authors: {string.Join(", ", fanfic.Authors)}");
            sb.AppendLine();
        }

        if (fanfic.Fandoms.Any())
        {
            sb.AppendLine($"📚 Fandom: {string.Join(", ", fanfic.Fandoms)}");
            sb.AppendLine();
        }

        if (fanfic.Pairings.Any())
        {
            sb.AppendLine($"❤️ Pairings: {string.Join(", ", fanfic.Pairings)}");
            sb.AppendLine();
        }

        if (fanfic.Tags.Any())
        {
            sb.AppendLine($"🏷 Tags: {string.Join(", ", fanfic.Tags)}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(fanfic.Description))
        {
            sb.AppendLine("📝 Description:");
            sb.AppendLine(fanfic.Description);
        }

        return sb.ToString();
    }
}