using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FanficDownloader.Bot.Ficbook;
using FanficDownloader.Bot.Services;
using FanficDownloader.Bot.Formatting;
using FanficDownloader.Bot.Models;
using Telegram.Bot.Types.ReplyMarkups;



var token = "XXX";
var bot = new TelegramBotClient(token);
var pendingFanfics = new Dictionary<long, Fanfic>();

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
    

    if (update.CallbackQuery is { } callback)
    {
        await botClient.AnswerCallbackQuery(callback.Id);
        

        var chatId = callback.Message.Chat.Id;

        await botClient.EditMessageReplyMarkup(
            chatId: chatId,
            messageId: callback.Message.MessageId,
            replyMarkup: null,
            cancellationToken: cancellationToken
        );


        await botClient.SendMessage(
            chatId,
            "⏳ Готовлю файл, это может занять пару минут... Но не волнуйся, тебе придет уведомление, когда всё будет готово 😊",
            cancellationToken: cancellationToken
        );
        var data = callback.Data;

        if (!pendingFanfics.TryGetValue(chatId, out var fanfic))
        {
            await botClient.SendMessage(
                chatId,
                "Фанфик не найден 😢",
                cancellationToken: cancellationToken
            );

            return;
        }

        var service = new FanficService();

        if (data == "format:txt")
            await service.SendFanficAsTxtAsync(botClient, chatId, fanfic, cancellationToken);

        if (data == "format:epub")
            await service.SendFanficAsEpubAsync(botClient, chatId, fanfic, cancellationToken);

        pendingFanfics.Remove(chatId);
        return;
    }

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
        File.WriteAllText("debug.html", html);
        var fanfic = parser.Parse(html);




        pendingFanfics[message.Chat.Id] = fanfic;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
                {
                    InlineKeyboardButton.WithCallbackData("📄 TXT", "format:txt"),
                    InlineKeyboardButton.WithCallbackData("📚 EPUB", "format:epub")
                }
        });

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Выбери формат:",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken
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