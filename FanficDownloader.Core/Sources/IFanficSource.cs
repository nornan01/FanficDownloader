using FanficDownloader.Core.Models;

public interface IFanficSource
{
    bool CanHandle(string url);
    Task<Fanfic> GetFanficAsync(string url, CancellationToken ct);
    Task<DownloadResult> PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct);

}
