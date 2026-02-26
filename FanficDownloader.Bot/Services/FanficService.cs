using FanficDownloader.Application.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanficDownloader.Bot.Services;

public class FanficService
{
    private readonly FanficDownloadService _downloadService;

    public FanficService(FanficDownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    public async Task SendFanficAsTxtAsync(
        ITelegramBotClient bot,
        long chatId,
        string url,
        CancellationToken ct)
    {
        var file = await _downloadService.BuildTxtAsync(url, ct);

        using var stream = new MemoryStream(file.Bytes);

        await bot.SendDocument(
            chatId: chatId,
            document: new InputFileStream(stream, file.FileName),
            caption: "📘 Готово!",
            cancellationToken: ct
        );
    }

    public async Task SendFanficAsEpubAsync(
        ITelegramBotClient bot,
        long chatId,
        string url,
        CancellationToken ct)
    {
        var file = await _downloadService.BuildEpubAsync(url, ct);

        using var stream = new MemoryStream(file.Bytes);

        await bot.SendDocument(
            chatId: chatId,
            document: new InputFileStream(stream, file.FileName),
            caption: "📘 Готово!",
            cancellationToken: ct
        );
    }
}
