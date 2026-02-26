using System.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using FanficDownloader.Bot.Services;
using FanficDownloader.Core.Models;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.DependencyInjection;
using FanficDownloader.Application.Services;
using System.Collections.Concurrent;





Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var token = Environment.GetEnvironmentVariable("TG_BOT_TOKEN");

if (string.IsNullOrEmpty(token))
{
    throw new Exception("TG_BOT_TOKEN is not set");
}

var bot = new TelegramBotClient(token);
//
var services = new ServiceCollection();

services.AddSingleton<SourceManager>();
services.AddScoped<FanficDownloadService>();
services.AddScoped<FanficService>();
services.AddHttpClient<ImageDownloadService>();
services.AddSingleton<DownloadQueueService>();


var provider = services.BuildServiceProvider();
var queue = provider.GetRequiredService<DownloadQueueService>();
await queue.StartWorkers(3);   // одновременно 3 загрузки



var pendingFanfics = new ConcurrentDictionary<long, Fanfic>();
var userLanguages = new ConcurrentDictionary<long, Language>();

string T(long chatId, string en, string ru)
{
    return GetUserLanguage(chatId) == Language.Russian ? ru : en;
}

Language GetUserLanguage(long chatId)
{
    if (userLanguages.TryGetValue(chatId, out var lang))
        return lang;

    return Language.English; // язык по умолчанию
}

void SetUserLanguage(long chatId, Language lang)
{
    userLanguages[chatId] = lang;
}

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

await Task.Delay(Timeout.Infinite);

cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    using var scope = provider.CreateScope();

    var downloadService = scope.ServiceProvider.GetRequiredService<FanficDownloadService>();
    var fanficService = scope.ServiceProvider.GetRequiredService<FanficService>();

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
        if (data!.StartsWith("lang:"))
        {
            var lang = data == "lang:ru"
                ? Language.Russian
                : Language.English;

            SetUserLanguage(chatId, lang);

            var text = lang == Language.Russian
                ? "🇷🇺 Язык установлен! Отправь ссылку на фанфик."
                : "🇬🇧 Language set! Send me a fanfic link.";

            await botClient.SendMessage(
                chatId: chatId,
                text: text,
                cancellationToken: cancellationToken
            );

            return;
        }
        if (!pendingFanfics.TryGetValue(chatId, out var pendingFanfic))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "Фанфик не найден 😢",
                cancellationToken: cancellationToken
            );
            return;
        }


        await botClient.SendMessage(
            chatId: chatId,
            text: T(chatId,
                        "⏳ Preparing the file, it might take a few minutes...",
                        "⏳ Готовлю файл, это может занять пару минут..."
                    ),
            cancellationToken: cancellationToken
        );

        var messageUrl = pendingFanfic.SourceUrl;

        var position = await queue.EnqueueWithPosition(async () =>
                        {
                            if (data == "format:txt")
                                await fanficService.SendFanficAsTxtAsync(botClient, chatId, messageUrl, cancellationToken);

                            if (data == "format:epub")
                                await fanficService.SendFanficAsEpubAsync(botClient, chatId, messageUrl, cancellationToken);
                        });

        await botClient.SendMessage(
            chatId,
            $"You are #{position} in queue. Preparing your file...",
            cancellationToken: cancellationToken
        );
        pendingFanfics.TryRemove(chatId, out _);
        return;
    }

    // ================= NORMAL MESSAGE =================
    if (update.Message is not { } message)
        return;

    if (message.Text is null)
        return;

    // ====== auto-detect language from Telegram ======
    if (!userLanguages.ContainsKey(message.Chat.Id))
    {
        var tgLang = message.From?.LanguageCode;

        if (tgLang == "ru" || tgLang == "uk" || tgLang == "be")
            SetUserLanguage(message.Chat.Id, Language.Russian);
        else
            SetUserLanguage(message.Chat.Id, Language.English);
    }


    // ================= /start =================
    if (message.Text == "/start")
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🇬🇧 English", "lang:en"),
            InlineKeyboardButton.WithCallbackData("🇷🇺 Русский", "lang:ru")
        }
    });

        var lang = GetUserLanguage(message.Chat.Id);

        var text = lang == Language.Russian
            ? "👋 Привет!\n\nЯ бот для скачивания фанфиков 📚\nВыбери язык:"
            : "👋 Hello!\n\nI'm a bot for downloading fanfics 📚\nChoose your language:";

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: text,
            replyMarkup: keyboard,
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
            text: T(message.Chat.Id, "I didn't find a link 😢", "Я не нашёл ссылку 😢"),
            cancellationToken: cancellationToken
        );
        return;
    }

    Fanfic fanfic;

    try
    {
        fanfic = await downloadService.GetInfoAsync(url, cancellationToken);
    }
    catch (NotSupportedException)
    {
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: T(message.Chat.Id,
                "This website is not supported yet. If you'd like to see it supported, check the bot description.",
                "Этот сайт пока не поддерживается. Если ты хочешь его добавить, посмотри описание бота."
            ),
            cancellationToken: cancellationToken
        );
        return;
    }


    // ================= DOWNLOAD =================
    try
    {
        var preparingMessage = await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: T(message.Chat.Id,
                        "⏳ Give me a moment...",
                        "⏳ Подожди секунду..."),
            cancellationToken: cancellationToken
        );

 

        await botClient.DeleteMessage(
                chatId: message.Chat.Id,
                messageId: preparingMessage.MessageId,
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
            text: T(message.Chat.Id, "✅ Done! Choose a format below 👇", "✅ Готово! Выбери формат ниже 👇"),
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
            text: $"❌ Download failed: couldn't download the page ({ex.StatusCode})",
            cancellationToken: cancellationToken
        );
    }
}


Task HandleErrorAsync(ITelegramBotClient botClient,Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine(exception.ToString());
    return Task.CompletedTask;
}