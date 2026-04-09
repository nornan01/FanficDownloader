using FanficDownloader.Application.Models;
using FanficDownloader.Core.Models;

namespace FanficDownloader.Web.Models;

public class WebDownloadJob
{
    public Guid Id { get; init; }
    public string Url { get; init; } = "";
    public string Format { get; init; } = "";
    public string RequesterId { get; init; } = "";
    public int QueuePosition { get; set; }
    public DownloadProgress Progress { get; init; } = new();
    public WebDownloadJobStatus Status { get; set; } = WebDownloadJobStatus.Queued;
    public DownloadFileResult? Result { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
