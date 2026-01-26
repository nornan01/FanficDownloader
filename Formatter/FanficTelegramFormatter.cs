using FanficDownloader.Bot.Models;

class FanficTelegramFormatter
{
    public string FormatInfoMessage(Fanfic fanfic)
    {
        return 
                        $"""
                        📖 Название: {fanfic.Title}

                        ✍️ Автор: {string.Join(", ", fanfic.Authors)}

                        📚 Фандом: {string.Join(", ", fanfic.Fandoms)}

                        ❤️ Пейринг: {string.Join(", ", fanfic.Pairings)}
                        
                        🏷 Метки: {string.Join(", ", fanfic.Tags)}

                        📝 Описание:
                        {fanfic.Description}
                        """;
    }
    }