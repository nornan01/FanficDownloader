namespace FanficDownloader.Core.Services;

public class ProxyService
{
    private readonly List<ProxyState> _proxies;
    private readonly Random _random = new();

    public ProxyService(List<string> proxies)
    {
        _proxies = proxies
            .Select(p => new ProxyState { Url = p })
            .ToList();
    }

    public string? GetRandomProxy()
    {
        var available = _proxies
            .Where(p => p.CooldownUntil == null || p.CooldownUntil < DateTime.UtcNow)
            .ToList();

        if (available.Count == 0)
            return null;

        return available[_random.Next(available.Count)].Url;
    }

    public void MarkFailed(string proxy)
    {
        var p = _proxies.FirstOrDefault(x => x.Url == proxy);
        if (p == null) return;

        p.FailCount++;

        if (p.FailCount >= 3)
        {
            p.CooldownUntil = DateTime.UtcNow.AddMinutes(5);
            p.FailCount = 0;
        }
    }

    public void MarkSuccess(string proxy)
    {
        var p = _proxies.FirstOrDefault(x => x.Url == proxy);
        if (p == null) return;

        p.FailCount = 0;
    }

}