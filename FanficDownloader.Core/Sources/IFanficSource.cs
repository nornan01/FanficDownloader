using FanficDownloader.Core.Models;

public interface IFanficSource
{
    bool CanHandle(string url);
    Task<Fanfic> GetFanficAsync(string url, string sessionId, CancellationToken ct);
    Task PopulateChaptersAsync(Fanfic fanfic, string sessionId, DownloadProgress progress, CancellationToken ct);
    Task DestroySessionAsync(string sessionId);
}
