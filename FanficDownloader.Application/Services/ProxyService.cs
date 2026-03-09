using Microsoft.Extensions.Configuration;

namespace FanficDownloader.Application.Services;

public class ProxyService
{
    private readonly List<string> _proxies;
    private readonly Random _random = new();

    public ProxyService(IConfiguration config)
    {
        _proxies = config.GetSection("Proxies").Get<List<string>>() ?? new List<string>();
    }

    public string? GetRandomProxy()
    {
        if (_proxies.Count == 0)
            return null;

        return _proxies[_random.Next(_proxies.Count)];
    }
}го