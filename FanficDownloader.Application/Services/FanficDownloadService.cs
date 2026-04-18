using FanficDownloader.Core.Models;
using FanficDownloader.Core.Sources;
using FanficDownloader.Application.Models;
using FanficDownloader.Core.Formatting;
using System.Text;
using Microsoft.Extensions.Logging;
using FanficDownloader.Application.Security;
using FanficDownloader.Core.Utils;
using FanficDownloader.Core.Services;

namespace FanficDownloader.Application.Services;

public class FanficDownloadService
{
    private readonly SourceManager _sourceManager;
    private readonly FanficEpubFormatter _epubFormatter;

    private readonly HttpClient _http;

    private readonly ILogger<FanficDownloadService> _logger;
    private readonly FanficCacheService _cache;
    private readonly R2StorageService _storage;
    private readonly ProxyHttpClientFactory _proxyFactory;
    private static readonly SemaphoreSlim _infoSemaphore = new(3);

    public FanficDownloadService(
        SourceManager sourceManager,
        FanficEpubFormatter epubFormatter,
        HttpClient http,
        ILogger<FanficDownloadService> logger,
        FanficCacheService cache,
        R2StorageService storage, ProxyHttpClientFactory proxyFactory)
    {
        _sourceManager = sourceManager;
        _epubFormatter = epubFormatter;
        _http = http;
        _logger = logger;
        _cache = cache;
        _storage = storage;
        _proxyFactory = proxyFactory;
    }

    // 1. Получить только информацию (БЕЗ глав)
    public async Task<Fanfic> GetInfoAsync(string url, CancellationToken ct)
    {
        await _infoSemaphore.WaitAsync();
        try
        {
        _logger.LogInformation("Starting info fetch for {Url}", url);
        if (url.Contains("fanfiction.net"))
        {
            var ficHub = await TryGetFicHubEpubAsync(url, ct);

            if (ficHub != null)
            {
                _logger.LogInformation("FicHub used for info fetch {Url}", url);

                return new Fanfic
                {
                    Title = ficHub.Title,

                    Authors = string.IsNullOrEmpty(ficHub.Author)
                    ? new List<string>()
                    : new List<string> { ficHub.Author },

                    Description = ficHub.Description,

                    SourceUrl = url,

                    Chapters = new List<Chapter>()
                };
            }
        }
        UrlValidator.Validate(url);
        var source = _sourceManager.GetSource(url);
        var sessionId = Guid.NewGuid().ToString();
        try{
        var fanfic = await source.GetFanficAsync(url, sessionId, ct);
        _logger.LogInformation("Completed info fetch for {Url}, Title={Title}, Chapters={Chapters}", url, fanfic.Title, fanfic.Chapters?.Count ?? 0);
        return fanfic;
        }
        finally{
            await source.DestroySessionAsync(sessionId);
        }
        }
        finally
        {
            _infoSemaphore.Release();
        }
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


    public async Task<(Fanfic fanfic, List<string> tempFiles)> DownloadFullAsync(string url, DownloadProgress progress, CancellationToken ct)
    {
        var downloadId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Download started. DownloadId={DownloadId}, Url={Url}",
            downloadId, url);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting full download for {Url}", url);
        UrlValidator.Validate(url);

        var tempFiles = new List<string>();
        try
        {
            var source = _sourceManager.GetSource(url);
            var sessionId = Guid.NewGuid().ToString();
            try{
            var fanfic = await source.GetFanficAsync(url, sessionId, ct);
            await source.PopulateChaptersAsync(fanfic, sessionId, progress, ct);
            await DownloadImagesAsync(fanfic, tempFiles, ct);

            sw.Stop();
            _logger.LogInformation(
                "Full download completed in {ElapsedMs} ms. Chapters={Count}, ImagesTemp={Images}, DownloadId={DownloadId}, Url={Url}, Chapters={Count}",
                sw.ElapsedMilliseconds,
                fanfic.Chapters?.Count ?? 0,
                tempFiles.Count,
                downloadId,
                url,
                fanfic.Chapters?.Count ?? 0
            );

            return (fanfic, tempFiles);
            }
            finally{
                await source.DestroySessionAsync(sessionId);
            }
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

    public async Task<DownloadFileResult> BuildTxtAsync(string url, DownloadProgress progress, CancellationToken ct)
    {
        var cached = await _cache.GetAsync(url, FanficFormats.Txt);

        if (cached != null)
        {
            _logger.LogInformation("TXT cache hit for {Url}", url);

            using var stream = new MemoryStream();

            await _storage.DownloadFileAsync(cached.ObjectKey, stream);

            return new DownloadFileResult
            {
                Bytes = stream.ToArray(),
                ContentType = "text/plain",
                FileName = FileNameHelper.BuildSafeFileName(cached.Title, "txt")
            };
        }
        var downloadId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "TXT build started. DownloadId={DownloadId}, Url={Url}",
            downloadId,
            url
        );
        var (fanfic, tempFiles) = await DownloadFullAsync(url, progress, ct);

        try
        {
            var formatter = new FanficTxtFormatter();
            var text = formatter.ToTxt(fanfic);
            var bytes = Encoding.UTF8.GetBytes(text);
            _logger.LogInformation("TXT build completed for {Url}", url);
            var objectKey = _storage.BuildObjectKey(url, FanficFormats.Txt, fanfic.Title);

            using var uploadStream = new MemoryStream(bytes);
            if(fanfic.IsFinished == true){
                    await _storage.UploadFileAsync(
                        objectKey,
                        uploadStream,
                        "text/plain"
                    );

                    await _cache.SaveAsync(
                        url,
                        fanfic.Title,
                        objectKey,
                        FanficFormats.Txt
                    );
            }
            _logger.LogInformation(
                "TXT build completed. DownloadId={DownloadId}, Url={Url}, Size={Size}",
                downloadId,
                url,
                bytes.Length
            );
            return new DownloadFileResult
            {
                Bytes = bytes,
                ContentType = "text/plain",
                FileName = FileNameHelper.BuildSafeFileName(fanfic.Title, "txt")
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

    public async Task<DownloadFileResult> BuildEpubAsync(string url, DownloadProgress progress, CancellationToken ct)
    {
        var cached = await _cache.GetAsync(url, FanficFormats.Epub);

        if (cached != null)
        {
            _logger.LogInformation("EPUB cache hit for {Url}", url);

            using var stream = new MemoryStream();

            await _storage.DownloadFileAsync(cached.ObjectKey, stream);

            return new DownloadFileResult
            {
                Bytes = stream.ToArray(),
                ContentType = "application/epub+zip",
                FileName = FileNameHelper.BuildSafeFileName(cached.Title, "epub")
            };
        }
        _logger.LogInformation("Starting EPUB build for {Url}", url);

        if (url.Contains("fanfiction.net"))
        {
            var ficHub = await TryGetFicHubEpubAsync(url, ct);

            if (ficHub != null)
            {
                _logger.LogInformation("FicHub used for fanfiction.net {Url}", url);
                var objectKey = _storage.BuildObjectKey(url, FanficFormats.Epub, ficHub.Title);

                using var uploadStream = new MemoryStream(ficHub.Bytes);

                await _storage.UploadFileAsync(
                    objectKey,
                    uploadStream,
                    "application/epub+zip"
                );

                await _cache.SaveAsync(
                    url,
                    ficHub.Title,
                    objectKey,
                    FanficFormats.Epub
                );

                return new DownloadFileResult
                {
                    Bytes = ficHub.Bytes,
                    ContentType = "application/epub+zip",
                    FileName = FileNameHelper.BuildSafeFileName(ficHub.Title, "epub")
                };
            }
        }

        var (fanfic, tempFiles) = await DownloadFullAsync(url, progress, ct);

        string? path = null;

        try
        {
            path = await _epubFormatter.BuildEpubFileAsync(fanfic, ct);

            var bytes = await File.ReadAllBytesAsync(path, ct);
            _logger.LogInformation("EPUB build completed for {Url}", url);
            var objectKey = _storage.BuildObjectKey(url, FanficFormats.Epub, fanfic.Title);

            using var uploadStream = new MemoryStream(bytes);

            if(fanfic.IsFinished == true){
                await _storage.UploadFileAsync(
                    objectKey,
                    uploadStream,
                    "application/epub+zip"
                );

                await _cache.SaveAsync(
                    url,
                    fanfic.Title,
                    objectKey,
                    FanficFormats.Epub
                );
            }
            return new DownloadFileResult
            {
                Bytes = bytes,
                ContentType = "application/epub+zip",
                FileName = FileNameHelper.BuildSafeFileName(fanfic.Title, "epub")
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

    private async Task<FicHubResult?> TryGetFicHubEpubAsync(string url, CancellationToken ct)
    {
        for(int attempt = 1; attempt <= 3; attempt++)
        {
        try
        {
            var client = _proxyFactory.CreateClient();
            var encoded = Uri.EscapeDataString(url);
            var api = $"https://fichub.net/api/v0/epub?q={encoded}";

            var json = await client.GetStringAsync(api, ct);
            _logger.LogInformation("FicHub response: {Json}", json);

            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("err", out var err) || err.GetInt32() != 0)
                return null;

            if (!doc.RootElement.TryGetProperty("epub_url", out var epubProp))
                return null;

            var path = epubProp.GetString();
            if (string.IsNullOrEmpty(path))
                return null;

            var meta = doc.RootElement.GetProperty("meta");

            var title = meta.GetProperty("title").GetString();
            var author = meta.GetProperty("author").GetString();
            var chapters = meta.GetProperty("chapters").GetInt32();
            var descriptionRaw = meta.GetProperty("description").GetString();

            var description = System.Text.RegularExpressions.Regex
                .Replace(descriptionRaw ?? "", "<.*?>", "");

            if (string.IsNullOrEmpty(title))
                title = "fanfic";

            var fullUrl = "https://fichub.net" + path;

            var bytes = await client.GetByteArrayAsync(fullUrl, ct);

            return new FicHubResult
            {
                Bytes = bytes,
                Title = title,
                Author = author ?? "",
                Chapters = chapters,
                Description = description ?? ""
            };
        }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FicHub attempt {Attempt} failed for {Url}", attempt, url);
            }
        }
        return null;
    }
}
