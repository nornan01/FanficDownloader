using System.Net;
using Microsoft.Extensions.Logging;

namespace FanficDownloader.Core.Services;

public class ProxyHttpClientFactory
{
    private readonly ProxyService _proxyService;
    private readonly ILogger<ProxyHttpClientFactory> _logger;

    public ProxyHttpClientFactory(ProxyService proxyService, ILogger<ProxyHttpClientFactory> logger)
    {
        _proxyService = proxyService;
        _logger = logger;
    }

    public HttpClient CreateClient()
    {
        var proxyUrl = _proxyService.GetRandomProxy();
        _logger.LogInformation("Trying proxy: {Proxy}", proxyUrl);
        if (proxyUrl == null)
            return new HttpClient(); // fallback без прокси

        var uri = new Uri(proxyUrl);

        var proxy = new WebProxy($"{uri.Scheme}://{uri.Host}:{uri.Port}")
        {
            Credentials = new NetworkCredential(
                uri.UserInfo.Split(':')[0],
                uri.UserInfo.Split(':')[1]
            )
        };

        var handler = new HttpClientHandler
        {
            Proxy = proxy,
            UseProxy = true
        };

        var client = new HttpClient(handler);

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/120.0.0.0 Safari/537.36"
        );

        return client;
    }
}