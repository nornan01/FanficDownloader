using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FanficDownloader.Bot.Ficbook;
using FanficDownloader.Bot.Services;
using FanficDownloader.Bot.Formatting;
using FanficDownloader.Bot.Models;


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

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Message is not { } message) return;
    if (message.Text is null) return;

    if (!message.Text.Contains("ficbook.net"))
    {
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Жду ссылочку на фанфик 📚",
            cancellationToken: cancellationToken
        );
        return;
    }

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

    try
    {
        var ficbookClient = new FicbookClient();
        var parser = new FicbookParser();

        var html = await ficbookClient.LoadHtmlAsync(url, cancellationToken);
        var fanfic = parser.Parse(html);

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: $"📚 Найдено глав: {fanfic.Chapters.Count}",
            cancellationToken: cancellationToken
        );

        var service = new FanficService();
        await service.SendFanficAsTxtAsync(
            botClient,
            message.Chat.Id,
            fanfic,
            cancellationToken
        );

        var tgFormatter = new FanficTelegramFormatter();
        var infoText = tgFormatter.FormatInfoMessage(fanfic);
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: infoText,
            cancellationToken: cancellationToken
        );
    }
    catch (HttpRequestException ex)
    {
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: $"❌ Не удалось скачать страницу ({ex.StatusCode})",
            cancellationToken: cancellationToken
        );
    }
}

Task HandleErrorAsync(ITelegramBotClient botClient,Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine(exception.ToString());
    return Task.CompletedTask;
}
