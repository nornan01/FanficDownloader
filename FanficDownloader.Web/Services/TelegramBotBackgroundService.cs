using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Exceptions;
using FanficDownloader.Application.Services;
using FanficDownloader.Core.Models;
using FanficDownloader.Core.Formatting;
using System.Collections.Concurrent;
using FanficDownloader.Bot.Services;
using FanficDownloader.Web.Services;
using Npgsql;

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

            var messageUrl = pendingFanfic.SourceUrl;
            var progress = new DownloadProgress();
            var statusMessage = await botClient.SendMessage(
                chatId,
                T(chatId,
                    "⏳ Preparing the file, it might take a few minutes...",
                    "⏳ Готовлю файл, это может занять пару минут..."),
                cancellationToken: cancellationToken
            );

            var position = await _queue.EnqueueWithPosition(async (ct) =>
            {
                using var scope = _scopeFactory.CreateScope();
                var fanficService = scope.ServiceProvider.GetRequiredService<FanficService>();
                using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var progressTask = ReportProgressAsync(
                    botClient,
                    chatId,
                    statusMessage.MessageId,
                    progress,
                    progressCts.Token);

                try
                {
                    if (data == "format:txt")
                        await fanficService.SendFanficAsTxtAsync(botClient, chatId, messageUrl, progress, ct);

                    if (data == "format:epub")
                        await fanficService.SendFanficAsEpubAsync(botClient, chatId, messageUrl, progress, ct);

                    await UpdateProgressMessageAsync(
                        botClient,
                        chatId,
                        statusMessage.MessageId,
                        T(chatId,
                            "⏳ Preparing the file... 100% downloaded. Sending file...",
                            "⏳ Готовлю файл... 100% загружено. Отправляю файл..."),
                        ct);
                }
                catch (Exception)
                {
                    await UpdateProgressMessageAsync(
                        botClient,
                        chatId,
                        statusMessage.MessageId,
                        T(chatId,
                            "❌ Download failed. Please try again later.",
                            "❌ Не удалось скачать файл. Попробуй позже."),
                        ct);
                    throw;
                }
                finally
                {
                    await progressCts.CancelAsync();

                    try
                    {
                        await progressTask;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            });

            await UpdateProgressMessageAsync(
                botClient,
                chatId,
                statusMessage.MessageId,
                T(chatId,
                    $"⏳ You're #{position} in queue. Preparing the file...",
                    $"⏳ Ты #{position} в очереди. Готовлю файл..."),
                cancellationToken);

            _pendingFanfics.TryRemove(chatId, out _);
            return;
        }

        // ================= MESSAGE =================
        if (update.Message is not { } message)
            return;

        if (message.Text is null)
            return;

        var connectionString = _config.GetConnectionString("Postgres");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO bot_users (user_id, first_seen, last_seen)
            VALUES (@userId, NOW(), NOW())
            ON CONFLICT (user_id)
            DO UPDATE SET last_seen = NOW();
        ", connection);

        cmd.Parameters.AddWithValue("userId", message.From!.Id);

        await cmd.ExecuteNonQueryAsync();

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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Telegram bot");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("Telegram bot stopped");
    }

    private async Task ReportProgressAsync(
        ITelegramBotClient botClient,
        long chatId,
        int messageId,
        DownloadProgress progress,
        CancellationToken cancellationToken)
    {
        string? lastText = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var text = BuildProgressText(chatId, progress);

            if (!string.Equals(text, lastText, StringComparison.Ordinal))
            {
                await UpdateProgressMessageAsync(
                    botClient,
                    chatId,
                    messageId,
                    text,
                    cancellationToken);

                lastText = text;
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    private string BuildProgressText(long chatId, DownloadProgress progress)
    {
        var total = progress.TotalChapters;
        var completed = progress.CompletedChapters;

        if (total <= 0)
        {
            return T(chatId,
                "⏳ Preparing the file, it might take a few minutes...",
                "⏳ Готовлю файл, это может занять пару минут...");
        }

        var percent = (int)Math.Clamp(Math.Floor((double)completed / total * 100), 0, 100);

        return T(chatId,
            $"⏳ Preparing the file... {percent}% downloaded ({completed}/{total} chapters)",
            $"⏳ Готовлю файл... {percent}% загружено ({completed}/{total} глав)");
    }

    private async Task UpdateProgressMessageAsync(
        ITelegramBotClient botClient,
        long chatId,
        int messageId,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            await botClient.EditMessageText(
                chatId,
                messageId,
                text,
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
        }
    }
}
