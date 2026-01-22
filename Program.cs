using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using HtmlAgilityPack;
using System.Net.Http;
using FanficDownloader.Bot.Models;
using FanficDownloader.Bot.Ficbook;
using FanficDownloader.Bot.Formatting;




var token = "XXX";
var bot = new TelegramBotClient(token);

using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

bot.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandleErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);



Console.WriteLine("Bot started...");
Console.ReadLine();
cts.Cancel();

async Task HandleUpdateAsync(
    ITelegramBotClient botClient,
    Update update,
    CancellationToken cancellationToken)
{
    if (update.Message is not { } message) return;
    if (message.Text is null) return;




    if (message.Text.Contains("ficbook.net"))
    {
        var url = message.Text.Split(' ', '\n').FirstOrDefault(x => x.StartsWith("http"));

        if (url is null)
        {
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Я не нашёл ссылку 😢",
                cancellationToken: cancellationToken
            );
            return;
        }


        try{

            var ficbookClient = new FicbookClient();
            var html = await ficbookClient.LoadHtmlAsync(url, cancellationToken);

            var parser = new FicbookParser();
            var fanfic = parser.Parse(html);
            await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: $"📚 Найдено глав: {fanfic.Chapters.Count}",
                    cancellationToken: cancellationToken
             );

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"📥 Начинаю загрузку глав ({fanfic.Chapters.Count})...",
                cancellationToken: cancellationToken
            );

            foreach (var chapter in fanfic.Chapters)
            {
                try{
                var chapterHtml = await ficbookClient.LoadHtmlAsync(chapter.Url, cancellationToken);
                chapter.Text = parser.ParseChapterText(chapterHtml);

                

                var delay = Random.Shared.Next(1200, 2500);
                await Task.Delay(delay, cancellationToken);
                }
                catch(Exception ex)
                {
                    chapter.Text = $"[Ошибка загрузки главы: {ex.Message}]";
                }

            }

            var formatter = new FanficFormatter();
            var fanficText = formatter.ToTxt(fanfic);

            var safeTitle = string.Concat(
                fanfic.Title.Where(c => !Path.GetInvalidFileNameChars().Contains(c))
            );

            var filePath = Path.Combine(
                Path.GetTempPath(),
                $"{safeTitle}.txt"
            );

            await File.WriteAllTextAsync(filePath, fanficText, cancellationToken);

            await using var stream = File.OpenRead(filePath);

            await botClient.SendDocument(
                chatId: message.Chat.Id,
                document: new InputFileStream(stream, Path.GetFileName(filePath)),
                caption: "📘 Готово!",
                cancellationToken: cancellationToken
            );

            // После отправки TXT файла, добавьте:
            var epubGenerator = new EpubGenerator();
            var epubBytes = epubGenerator.CreateEpub(fanfic);

            // Сохраняем временный файл
            var epubFilePath = Path.Combine(
                Path.GetTempPath(),
                $"{safeTitle}.epub"
            );
            await File.WriteAllBytesAsync(epubFilePath, epubBytes, cancellationToken);

            await using var epubStream = File.OpenRead(epubFilePath);
            await botClient.SendDocument(
                chatId: message.Chat.Id,
                document: new InputFileStream(epubStream, $"{safeTitle}.epub"),
                caption: "📗 EPUB версия",
                cancellationToken: cancellationToken
            );

            File.Delete(epubFilePath);






            File.Delete(filePath);



            var text =
                        $"""
                        📖 Название: {fanfic.Title}

                        ✍️ Автор: {string.Join(", ", fanfic.Authors)}

                        📚 Фандом: {string.Join(", ", fanfic.Fandoms)}

                        ❤️ Пейринг: {string.Join(", ", fanfic.Pairings)}
                        
                        🏷 Метки: {string.Join(", ", fanfic.Tags)}

                        📝 Описание:
                        {fanfic.Description}
                        """;
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: text,
                cancellationToken: cancellationToken
            );
        }
        catch (HttpRequestException ex)
        {
            await botClient.SendMessage(
        chatId: message.Chat.Id,
        text: $"❌ Не удалось скачать страницу ({ex.StatusCode}). Проверь ссылку.",
        cancellationToken: cancellationToken
    );
        }
    }
    else
    {
        await botClient.SendMessage(
        chatId: message.Chat.Id,
        text:$"Что-то не то",
    cancellationToken: cancellationToken
        );
    }
}





Task HandleErrorAsync(
    ITelegramBotClient botClient,
    Exception exception,
    CancellationToken cancellationToken)
{
    Console.WriteLine(exception.ToString());
    return Task.CompletedTask;
}

