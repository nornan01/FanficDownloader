using FanficDownloader.Core.Models;

namespace FanficDownloader.Core.Sources;

public interface IFanficSource
{
    bool CanHandle(string url);
    Task<Fanfic> GetFanficAsync(string url, CancellationToken ct);
    Task PopulateChaptersAsync(Fanfic fanfic, CancellationToken ct);
}
