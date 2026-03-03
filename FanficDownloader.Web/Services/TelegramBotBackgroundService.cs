using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using FanficDownloader.Application.Services;
using FanficDownloader.Core.Models;
using FanficDownloader.Core.Formatting;
using System.Collections.Concurrent;
using FanficDownloader.Bot.Services;
using FanficDownloader.Web.Services;

public class TelegramBotBackgroundService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly DownloadQueueService _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramBotBackgroundService> _logger;
    private TelegramBotClient? _bot;

    private readonly ConcurrentDictionary<long, Fanfic> _pendingFanfics = new();
    private readonly ConcurrentDictionary<long, Language> _userLanguages = new();

    public TelegramBotBackgroundService(
        IConfiguration config,
        DownloadQueueService queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramBotBackgroundService> logger)
    {
        _config = config;
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _config["TG_BOT_TOKEN"];

        if (string.IsNullOrEmpty(token))
            throw new Exception("TG_BOT_TOKEN is not configured");

        _bot = new TelegramBotClient(token);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: stoppingToken
        );
        
        _logger.LogInformation("Telegram bot started");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        // ================= CALLBACK =================
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

            // === LANGUAGE SWITCH ===
            if (data!.StartsWith("lang:"))
            {
                var lang = data == "lang:ru"
                    ? Language.Russian
                    : Language.English;

                _userLanguages[chatId] = lang;

                var text = lang == Language.Russian
                    ? "🇷🇺 Язык установлен! Отправь ссылку на фанфик."
                    : "🇬🇧 Language set! Send me a fanfic link.";

                await botClient.SendMessage(
                    chatId,
                    text,
                    cancellationToken: cancellationToken
                );

                return;
            }

            // === FORMAT BUTTONS ===
            if (!_pendingFanfics.TryGetValue(chatId, out var pendingFanfic))
            {
                await botClient.SendMessage(
                    chatId,
                    "Фанфик не найден 😢",
                    cancellationToken: cancellationToken
                );
                return;
            }

            await botClient.SendMessage(
                chatId,
                T(chatId,
                    "⏳ Preparing the file, it might take a few minutes...",
                    "⏳ Готовлю файл, это может занять пару минут..."),
                cancellationToken: cancellationToken
            );

            var messageUrl = pendingFanfic.SourceUrl;

            var position = await _queue.EnqueueWithPosition(async (cancellationToken) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var fanficService = scope.ServiceProvider.GetRequiredService<FanficService>();

                if (data == "format:txt")
                    await fanficService.SendFanficAsTxtAsync(botClient, chatId, messageUrl, CancellationToken.None);

                if (data == "format:epub")
                    await fanficService.SendFanficAsEpubAsync(botClient, chatId, messageUrl, CancellationToken.None);
            });

            await botClient.SendMessage(
                chatId,
                $"You are #{position} in queue. Preparing your file...",
                cancellationToken: cancellationToken
            );

            _pendingFanfics.TryRemove(chatId, out _);
            return;
        }

        // ================= MESSAGE =================
        if (update.Message is not { } message)
            return;

        if (message.Text is null)
            return;

        var chatIdMessage = message.Chat.Id;

        // === AUTO LANGUAGE DETECT ===
        if (!_userLanguages.ContainsKey(chatIdMessage))
        {
            var tgLang = message.From?.LanguageCode;

            if (tgLang == "ru" || tgLang == "uk" || tgLang == "be")
                _userLanguages[chatIdMessage] = Language.Russian;
            else
                _userLanguages[chatIdMessage] = Language.English;
        }

        // === /start ===
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

            var lang = GetUserLanguage(chatIdMessage);

            var text = lang == Language.Russian
                ? "👋 Привет!\n\nЯ бот для скачивания фанфиков 📚\nВыбери язык:"
                : "👋 Hello!\n\nI'm a bot for downloading fanfics 📚\nChoose your language:";

            await botClient.SendMessage(
                chatIdMessage,
                text,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken
            );

            return;
        }

        // === URL ===
        var url = message.Text
            .Split(' ', '\n')
            .FirstOrDefault(x => x.StartsWith("http"));

        if (url is null)
        {
            await botClient.SendMessage(
                chatIdMessage,
                T(chatIdMessage, "I didn't find a link 😢", "Я не нашёл ссылку 😢"),
                cancellationToken: cancellationToken
            );
            return;
        }

        Fanfic fanfic;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var downloadService = scope.ServiceProvider.GetRequiredService<FanficDownloadService>();

            fanfic = await downloadService.GetInfoAsync(url, cancellationToken);
        }
        catch (NotSupportedException)
        {
            await botClient.SendMessage(
                chatIdMessage,
                T(chatIdMessage,
                    "This website is not supported yet.",
                    "Этот сайт пока не поддерживается."),
                cancellationToken: cancellationToken
            );
            return;
        }

        _pendingFanfics[chatIdMessage] = fanfic;

        var keyboardFormat = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📄 TXT", "format:txt"),
                InlineKeyboardButton.WithCallbackData("📚 EPUB", "format:epub")
            }
        });

        await botClient.SendMessage(
            chatIdMessage,
            T(chatIdMessage,
                "✅ Done! Choose a format below 👇",
                "✅ Готово! Выбери формат ниже 👇"),
            replyMarkup: keyboardFormat,
            cancellationToken: cancellationToken
        );

        var tgFormatter = new FanficTelegramFormatter();
        var infoText = tgFormatter.FormatInfoMessage(fanfic);

        await botClient.SendMessage(
            chatIdMessage,
            infoText,
            cancellationToken: cancellationToken
        );
    }

    private Language GetUserLanguage(long chatId)
    {
        if (_userLanguages.TryGetValue(chatId, out var lang))
            return lang;

        return Language.English;
    }

    private string T(long chatId, string en, string ru)
    {
        return GetUserLanguage(chatId) == Language.Russian ? ru : en;
    }

    private Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(exception);
        return Task.CompletedTask;
    }
}