using System.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FanficDownloader.Bot.Services;
using FanficDownloader.Bot.Formatting;
using FanficDownloader.Bot.Models;
using Telegram.Bot.Types.ReplyMarkups;
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);


var token = "XXX";
var bot = new TelegramBotClient(token);
var pendingFanfics = new Dictionary<long, Fanfic>();
using var cts = new CancellationTokenSource();
var sourceManager = new SourceManager();
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
    // ================= CALLBACK BUTTONS =================
    if (update.CallbackQuery is { } callback)
    {
        await botClient.AnswerCallbackQuery(callback.Id);

        var chatId = callback.Message!.Chat.Id;

        await botClient.EditMessageReplyMarkup(
            chatId: chatId,
            messageId: callback.Message!.MessageId,
            replyMarkup: null,
            cancellationToken: cancellationToken
        );

        var data = callback.Data;

        if (!pendingFanfics.TryGetValue(chatId, out var fanfic))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "Фанфик не найден 😢",
                cancellationToken: cancellationToken
            );
            return;
        }

        var service = new FanficService();

        await botClient.SendMessage(
            chatId: chatId,
            text: "⏳ Готовлю файл, это может занять пару минут...",
            cancellationToken: cancellationToken
        );
        var fanficSource = sourceManager.GetSource(fanfic.SourceUrl);
        await fanficSource.PopulateChaptersAsync(fanfic, cancellationToken);

        if (data == "format:txt")
            await service.SendFanficAsTxtAsync(botClient, chatId, fanfic, cancellationToken);

        if (data == "format:epub")
            await service.SendFanficAsEpubAsync(botClient, chatId, fanfic, cancellationToken);

        pendingFanfics.Remove(chatId);
        return;
    }

    // ================= NORMAL MESSAGE =================
    if (update.Message is not { } message)
        return;

    if (message.Text is null)
        return;

    // ================= /start =================
    if (message.Text == "/start")
    {
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text:
                "👋 Привет!\n\n" +
                "Я бот для скачивания фанфиков 📚\n" +
                "Просто пришли мне ссылку с Ficbook или Snapetales, и я подготовлю файл для тебя.\n\n" +
                "Поддерживаемые форматы: TXT и EPUB.",
            cancellationToken: cancellationToken
        );
        return;
    }

    // ================= URL =================
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

    IFanficSource source;
    try
    {
        source = sourceManager.GetSource(url);
    }
    catch
    {
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Этот сайт пока не поддерживается 😢",
            cancellationToken: cancellationToken
        );
        return;
    }

    // ================= DOWNLOAD =================
    try
    {
        var preparingMessage = await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "⏳ Минуточку...",
            cancellationToken: cancellationToken
        );

        var fanfic = await source.GetFanficAsync(url, cancellationToken);

        await botClient.EditMessageText(
            chatId: message.Chat.Id,
            messageId: preparingMessage.MessageId,
            text: "✅ Готово! Выбирай формат ниже 👇",
            cancellationToken: cancellationToken
        );

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