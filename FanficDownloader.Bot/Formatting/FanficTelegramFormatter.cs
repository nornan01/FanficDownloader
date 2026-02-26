using FanficDownloader.Core.Models;

class FanficTelegramFormatter
{
    public string FormatInfoMessage(Fanfic fanfic)
    {
        return 
                        $"""
                        📖 Title: {fanfic.Title}

                        ✍️ Authors: {string.Join(", ", fanfic.Authors)}

                        📚 Fandom: {string.Join(", ", fanfic.Fandoms)}
                        
                        ❤️ Pairings: {string.Join(", ", fanfic.Pairings)}
                        
                        🏷 Tags: {string.Join(", ", fanfic.Tags)}

                        📝 Description:
                        {fanfic.Description}
                        """;
    }
    }