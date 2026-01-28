using FanficDownloader.Bot.Models;

public interface IFanficSource
{
    bool CanHandle(string url);
    Task<Fanfic> GetFanficAsync(string url, CancellationToken ct);
}