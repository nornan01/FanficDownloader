using FanficDownloader.Application.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices.Marshalling;
using FanficDownloader.Core.Utils;
using Npgsql;
using FanficDownloader.Core.Models;

namespace FanficDownloader.Bot.Services;

public class FanficService
{
    private readonly FanficDownloadService _downloadService;

    private readonly ILogger<FanficService> _logger;
    private readonly string _connectionString;
    public FanficService(FanficDownloadService downloadService, ILogger<FanficService> logger, IConfiguration configuration)
    {
        _downloadService = downloadService;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("Postgres")!;
    }

    public async Task SendFanficAsTxtAsync(
        ITelegramBotClient bot,
        long chatId,
        string url,
        DownloadProgress progress,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "TXT request started. ChatId={ChatId}, Url={Url}",
            chatId, url);

        try{
        var file = await _downloadService.BuildTxtAsync(url, progress, ct);

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
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
                                                    INSERT INTO downloads (url, source, format)
                                                    VALUES (@url, 'bot', 'txt')
                                                ", connection);

            cmd.Parameters.AddWithValue("url", url);

            await cmd.ExecuteNonQueryAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to send TXT. ChatId={ChatId}, Url={Url}", chatId, url);
            throw;
        }
    }

    public async Task SendFanficAsEpubAsync(
        ITelegramBotClient bot,
        long chatId,
        string url,
        DownloadProgress progress,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "EPUB request started. ChatId={ChatId}, Url={Url}",
            chatId, url);
        try{
            var job = new DownloadJob
            {
                Url = url,
                Format = "epub",
                RequesterId = chatId.ToString()
            };

            _logger.LogInformation(
                "Job created. JobId={JobId}, ChatId={ChatId}, Url={Url}",
                job.Id,
                chatId,
                url
            );
            var file = await _downloadService.BuildEpubAsync(job.Url, progress, ct);

            job.Result = file.Bytes;
            if (job.RequesterId != chatId.ToString())
            {
                _logger.LogError(
                    "CRITICAL: Job mismatch! JobId={JobId}, Expected={Expected}, Actual={Actual}",
                    job.Id,
                    job.RequesterId,
                    chatId
                );

                return; 
            }
            if (job.Result == null)
            {
                _logger.LogError("Job result is null! JobId={JobId}", job.Id);
                return;
            }
            using var stream = new MemoryStream(job.Result!);
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
            await using var connection = new NpgsqlConnection(_connectionString);

            await connection.OpenAsync();

            await using var cmd = new NpgsqlCommand(@"
            INSERT INTO downloads (url, source, format)
            VALUES (@url, 'bot', 'epub')
             ", connection);

            cmd.Parameters.AddWithValue("url", url);

            await cmd.ExecuteNonQueryAsync();

            _logger.LogInformation(
                    "EPUB sent successfully. ChatId={ChatId}, File={File}, Size={Size}, Url={Url}",
                    chatId,
                    safeName,
                    file.Bytes.Length,
                    url);
        }catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to send EPUB. ChatId={ChatId}, Url={Url}", chatId, url);
            throw;
        }
    }
}
