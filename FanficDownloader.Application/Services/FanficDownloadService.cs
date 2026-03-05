using FanficDownloader.Core.Models;
using FanficDownloader.Core.Sources;
using FanficDownloader.Application.Models;
using FanficDownloader.Core.Formatting;
using System.Text;
using Microsoft.Extensions.Logging;
using FanficDownloader.Application.Security;

namespace FanficDownloader.Application.Services;

public class FanficDownloadService
{
    private readonly SourceManager _sourceManager;
    private readonly FanficEpubFormatter _epubFormatter;

    private readonly HttpClient _http;

    private readonly ILogger<FanficDownloadService> _logger;

    public FanficDownloadService(
        SourceManager sourceManager,
        FanficEpubFormatter epubFormatter,
        HttpClient http,
        ILogger<FanficDownloadService> logger)
    {
        _sourceManager = sourceManager;
        _epubFormatter = epubFormatter;
        _http = http;
        _logger = logger;
    }

    // 1. Получить только информацию (БЕЗ глав)
    public async Task<Fanfic> GetInfoAsync(string url, CancellationToken ct)
    {
        _logger.LogInformation("Starting info fetch for {Url}", url);
        UrlValidator.Validate(url);
        var source = _sourceManager.GetSource(url);
        var fanfic = await source.GetFanficAsync(url, ct);
        _logger.LogInformation("Completed info fetch for {Url}, Title={Title}, Chapters={Chapters}", url, fanfic.Title, fanfic.Chapters?.Count ?? 0);
        return fanfic;
    }

    // 2. Догрузить главы
    public async Task<DownloadResult> PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct)
    {
        _logger.LogInformation("Starting chapter population for {Url}", fanfic.SourceUrl);
        var source = _sourceManager.GetSource(fanfic.SourceUrl);
        var result = await source.PopulateChaptersAsync(fanfic, ct);
        _logger.LogInformation("Chapter population completed. ChaptersLoaded={Count}", fanfic.Chapters?.Count ?? 0);
        return result;

    }

    private async Task DownloadImagesAsync(
    Fanfic fanfic,
    List<string> tempFiles,
    CancellationToken ct)
    {
        _logger.LogInformation("Starting image download for {Url}", fanfic.SourceUrl);
        foreach (var chapter in fanfic.Chapters)
        {
            foreach (var img in chapter.Images)
            {
                try{
                var bytes = await _http.GetByteArrayAsync(img.Url, ct);

                var tempPath = Path.Combine(
                    Path.GetTempPath(),
                    Guid.NewGuid() + "_" + img.LocalFileName);

                await File.WriteAllBytesAsync(tempPath, bytes, ct);

                img.LocalFileName = tempPath;
                tempFiles.Add(tempPath);
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Failed to download image {ImageUrl} for {FanficUrl}", img.Url, fanfic.SourceUrl);
                }
            }
        }
    }


    public async Task<(Fanfic fanfic, List<string> tempFiles)> DownloadFullAsync(string url, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting full download for {Url}", url);
        UrlValidator.Validate(url);

        var tempFiles = new List<string>();
        try
        {
            var source = _sourceManager.GetSource(url);

            var fanfic = await source.GetFanficAsync(url, ct);
            await source.PopulateChaptersAsync(fanfic, ct);
            await DownloadImagesAsync(fanfic, tempFiles, ct);

            sw.Stop();
            _logger.LogInformation(
                "Full download completed in {ElapsedMs} ms. Chapters={Count}, ImagesTemp={Images}",
                sw.ElapsedMilliseconds,
                fanfic.Chapters?.Count ?? 0,
                tempFiles.Count
            );

            return (fanfic, tempFiles);
        }
        catch
        {
            foreach (var file in tempFiles)
            {
                try { if (File.Exists(file)) File.Delete(file); }
                catch { }
            }
            throw;
        }
    }

    public async Task<DownloadFileResult> BuildTxtAsync(string url, CancellationToken ct)
    {
        _logger.LogInformation("Starting TXT build for {Url}", url);
        var (fanfic, tempFiles) = await DownloadFullAsync(url, ct);

        try
        {
            var formatter = new FanficTxtFormatter();
            var text = formatter.ToTxt(fanfic);
            var bytes = Encoding.UTF8.GetBytes(text);
            _logger.LogInformation("TXT build completed for {Url}", url);
            return new DownloadFileResult
            {
                Bytes = bytes,
                ContentType = "text/plain",
                FileName = BuildSafeFileName(fanfic.Title, "txt")
            };
        }
        finally
        {
            foreach (var file in tempFiles)
            {
                try { if (File.Exists(file)) File.Delete(file); }
                catch { }
            }
        }
    }

    public async Task<DownloadFileResult> BuildEpubAsync(string url, CancellationToken ct)
    {
        _logger.LogInformation("Starting EPUB build for {Url}", url);
        var (fanfic, tempFiles) = await DownloadFullAsync(url, ct);

        string? path = null;

        try
        {
            path = await _epubFormatter.BuildEpubFileAsync(fanfic, ct);

            var bytes = await File.ReadAllBytesAsync(path, ct);
            _logger.LogInformation("EPUB build completed for {Url}", url);
            return new DownloadFileResult
            {
                Bytes = bytes,
                ContentType = "application/epub+zip",
                FileName = BuildSafeFileName(fanfic.Title, "epub")
            };
        }
        finally
        {
            if (path != null && File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }

            foreach (var file in tempFiles)
            {
                try { if (File.Exists(file)) File.Delete(file); }
                catch { }
            }
        }
    }

    private string BuildSafeFileName(string title, string ext)
    {   
        _logger.LogInformation("Building safe filename for {Title}", title);
        var invalidChars = Path.GetInvalidFileNameChars();

        var safeTitle = new string(
            title.Where(ch => !invalidChars.Contains(ch)).ToArray()
        );

        safeTitle = safeTitle.Replace(" ", "_");

        return $"{safeTitle}.{ext}";
    }

    
}
