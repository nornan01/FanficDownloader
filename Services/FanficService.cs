using FanficDownloader.Bot.Ficbook;
using FanficDownloader.Bot.Formatting;
using FanficDownloader.Bot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanficDownloader.Bot.Services;

public class FanficService
{
    
    private readonly FanficTxtFormatter _formatterTxt;
    private readonly FanficEpubFormatter _formatterEpub;

    public FanficService()
    {
        _formatterTxt = new FanficTxtFormatter();
        _formatterEpub = new FanficEpubFormatter();
    }

    public async Task SendFanficAsTxtAsync(
        ITelegramBotClient bot,
        long chatId,
        Fanfic fanfic,
        CancellationToken ct)
    {
        
    

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

        var filePath = _formatterEpub.BuildEpubFile(fanfic);

        await SendFileAsync(bot, chatId, filePath, ct);

        File.Delete(filePath);
    }

    
    //мож вынесни тож в тхт форматтер??

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
