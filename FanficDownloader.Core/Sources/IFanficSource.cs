using FanficDownloader.Core.Models;

public interface IFanficSource
{
    bool CanHandle(string url);
    Task<Fanfic> GetFanficAsync(string url, string sessionId, CancellationToken ct);
    Task<DownloadResult> PopulateChaptersAsync(Fanfic fanfic, string sessionId, CancellationToken ct);
    Task DestroySessionAsync(string sessionId);
}
