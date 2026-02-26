using FanficDownloader.Core.Models;
using FanficDownloader.Core.Sources;
using FanficDownloader.Application.Models;
using FanficDownloader.Core.Formatting;
using System.Text;

namespace FanficDownloader.Application.Services;

public class FanficDownloadService
{
    private readonly SourceManager _sourceManager;
    
    public FanficDownloadService(SourceManager sourceManager)
    {
        _sourceManager = sourceManager;
    }

    // 1. Получить только информацию (БЕЗ глав)
    public async Task<Fanfic> GetInfoAsync(string url, CancellationToken ct)
    {
        var source = _sourceManager.GetSource(url);
        var fanfic = await source.GetFanficAsync(url, ct);
        return fanfic;
    }

    // 2. Догрузить главы
    public async Task<DownloadResult> PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        var source = _sourceManager.GetSource(fanfic.SourceUrl);
        var result = await source.PopulateChaptersAsync(fanfic, ct);
        return result;

    }

    private async Task DownloadImagesAsync(Fanfic fanfic, CancellationToken ct)
    {
        using var http = new HttpClient();

        foreach (var chapter in fanfic.Chapters)
        {
            foreach (var img in chapter.Images)
            {
                var bytes = await http.GetByteArrayAsync(img.Url, ct);

                var tempPath = Path.Combine(Path.GetTempPath(), img.LocalFileName);
                await File.WriteAllBytesAsync(tempPath, bytes, ct);

                img.LocalFileName = tempPath;
            }
        }
    }


    // 3. Полная загрузка (иногда тоже пригодится) - уже не нужна кажись поглядеть потом
    public async Task<Fanfic> DownloadFullAsync(string url, CancellationToken ct)
    {
        var source = _sourceManager.GetSource(url);

        var fanfic = await source.GetFanficAsync(url, ct);
        await source.PopulateChaptersAsync(fanfic, ct);
        await DownloadImagesAsync(fanfic, ct);

        return fanfic;

    }

    public async Task<DownloadFileResult> BuildTxtAsync(string url, CancellationToken ct)
    {
        
            var fanfic = await DownloadFullAsync(url, ct);

        var formatter = new FanficTxtFormatter();
        var text = formatter.ToTxt(fanfic);
        var bytes = Encoding.UTF8.GetBytes(text);

        var fileName = BuildSafeFileName(fanfic.Title, "txt");

        return new DownloadFileResult
        {
            Bytes = bytes,
            ContentType = "text/plain",
            FileName = fileName
        };
        
    }

    public async Task<DownloadFileResult> BuildEpubAsync(string url, CancellationToken ct)
    {
       
            var fanfic = await DownloadFullAsync(url, ct);

            var formatter = new FanficEpubFormatter();
            var path = formatter.BuildEpubFile(fanfic);

            var bytes = await File.ReadAllBytesAsync(path, ct);
            File.Delete(path);

            var fileName = BuildSafeFileName(fanfic.Title, "epub");

            return new DownloadFileResult
            {
                Bytes = bytes,
                ContentType = "application/epub+zip",
                FileName = fileName
            };
        
        
    }

    private static string BuildSafeFileName(string title, string ext)
    {
        var invalidChars = Path.GetInvalidFileNameChars();

        var safeTitle = new string(
            title.Where(ch => !invalidChars.Contains(ch)).ToArray()
        );

        safeTitle = safeTitle.Replace(" ", "_");

        return $"{safeTitle}.{ext}";
    }

    
}
