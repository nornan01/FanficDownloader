namespace FanficDownloader.Core.Models;

public class FanficCacheEntry
{
    public string Url { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string ObjectKey { get; set; } = default!;
    public DateTime CreatedAt { get; set; }

    public string Format {get; set; } = default!;
}