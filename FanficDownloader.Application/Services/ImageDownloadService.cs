using FanficDownloader.Core.Models;

namespace FanficDownloader.Application.Services;

public class ImageDownloadService
{
    private readonly HttpClient _http;

    public ImageDownloadService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DownloadedImage?> DownloadAsync(string url, string folder, CancellationToken ct)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct);

            var ext = Path.GetExtension(url);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".jpg";

            var fileName = $"{Guid.NewGuid()}{ext}";
            var localPath = Path.Combine(folder, fileName);

            await File.WriteAllBytesAsync(localPath, bytes, ct);

            return new DownloadedImage
            {
                OriginalUrl = url,
                LocalPath = localPath,
                FileName = fileName
            };
        }
        catch
        {
            return null;
        }
    }
}
