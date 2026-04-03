using FanficDownloader.Application.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices.Marshalling;
using FanficDownloader.Core.Utils;

namespace FanficDownloader.Bot.Services;

public class FanficService
{
    private readonly FanficDownloadService _downloadService;

    private readonly ILogger<FanficService> _logger;
    public FanficService(FanficDownloadService downloadService, ILogger<FanficService> logger)
    {
        _downloadService = downloadService;
        _logger = logger;
    }

    public async Task SendFanficAsTxtAsync(
        ITelegramBotClient bot,
        long chatId,
        string url,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "TXT request started. ChatId={ChatId}, Url={Url}",
            chatId, url);

        try{
        var file = await _downloadService.BuildTxtAsync(url, ct);

        using var stream = new MemoryStream(file.Bytes);

        var safeName = FileNameHelper.BuildHttpSafeFileName(
        Path.GetFileNameWithoutExtension(file.FileName),
        "txt"
        );
            await bot.SendDocument(
            chatId: chatId,
            document: new InputFileStream(stream, safeName),
            caption: "📘 Готово!",
            cancellationToken: ct
        );
            _logger.LogInformation(
                    "TXT sent successfully. ChatId={ChatId}, File={File}, Size={Size}",
                    chatId,
                    safeName,
                    file.Bytes.Length);
        }catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to send TXT. ChatId={ChatId}, Url={Url}", chatId, url);
            throw;
        }
    }

    public async Task SendFanficAsEpubAsync(
        ITelegramBotClient bot,
        long chatId,
        string url,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "EPUB request started. ChatId={ChatId}, Url={Url}",
            chatId, url);
        try{
        var file = await _downloadService.BuildEpubAsync(url, ct);

        using var stream = new MemoryStream(file.Bytes);
        var safeName = FileNameHelper.BuildHttpSafeFileName(
        Path.GetFileNameWithoutExtension(file.FileName),
        "epub"
        ); 
            await bot.SendDocument(
            chatId: chatId,
            document: new InputFileStream(stream, safeName),
            caption: "📘 Готово!",
            cancellationToken: ct
        );

        _logger.LogInformation(
                    "EPUB sent successfully. ChatId={ChatId}, File={File}, Size={Size}",
                    chatId,
                    safeName,
                    file.Bytes.Length);
        }catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to send EPUB. ChatId={ChatId}, Url={Url}", chatId, url);
            throw;
        }
    }
}
