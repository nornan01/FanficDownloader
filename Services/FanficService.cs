using FanficDownloader.Bot.Ficbook;
using FanficDownloader.Bot.Formatting;
using FanficDownloader.Bot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanficDownloader.Bot.Services;

public class FanficService
{
    private readonly FicbookClient _client;
    private readonly FicbookParser _parser;
    private readonly FanficTxtFormatter _formatterTxt;
    private readonly FanficEpubFormatter _formatterEpub;

    public FanficService()
    {
        _client = new FicbookClient();
        _parser = new FicbookParser();
        _formatterTxt = new FanficTxtFormatter();
        _formatterEpub = new FanficEpubFormatter();
    }

    public async Task SendFanficAsTxtAsync(
        ITelegramBotClient bot,
        long chatId,
        Fanfic fanfic,
        CancellationToken ct)
    {
        await LoadChaptersAsync(fanfic, ct);

        var filePath = await BuildTxtFileAsync(fanfic, ct);

        await SendFileAsync(bot, chatId, filePath, ct);

        File.Delete(filePath);
    }

    public async Task SendFanficAsEpubAsync(
    ITelegramBotClient bot,
    long chatId,
    Fanfic fanfic,
    CancellationToken ct)
    {
        await LoadChaptersAsync(fanfic, ct);

        var filePath = _formatterEpub.BuildEpubFile(fanfic);

        await SendFileAsync(bot, chatId, filePath, ct);

        File.Delete(filePath);
    }

    private async Task LoadChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        foreach (var chapter in fanfic.Chapters)
        {
            try
            {
                var html = await _client.LoadHtmlAsync(chapter.Url, ct);
                chapter.Text = _parser.ParseChapterText(html);

                var delay = Random.Shared.Next(1200, 2500);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                chapter.Text = $"[Ошибка загрузки главы: {ex.Message}]";
            }
        }
    }

    private async Task<string> BuildTxtFileAsync(Fanfic fanfic, CancellationToken ct)
    {
        var text = _formatterTxt.ToTxt(fanfic);

        var safeTitle = string.Concat(
            fanfic.Title.Where(c => !Path.GetInvalidFileNameChars().Contains(c))
        );

        var filePath = Path.Combine(Path.GetTempPath(), $"{safeTitle}.txt");

        await File.WriteAllTextAsync(filePath, text, ct);

        return filePath;
    }

   
    private async Task SendFileAsync(
        ITelegramBotClient bot,
        long chatId,
        string filePath,
        CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);

        await bot.SendDocument(
            chatId: chatId,
            document: new InputFileStream(stream, Path.GetFileName(filePath)),
            caption: "📘 Готово!",
            cancellationToken: ct
        );
    }
}
