using System.Net.Http;

namespace FanficDownloader.Core.Ficbook;

public class FicbookClient
{
    private readonly HttpClient _http;

    public FicbookClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/120.0.0.0 Safari/537.36"
        );
    }

    public async Task<string> LoadHtmlAsync(string url, CancellationToken cancellationToken)
    {
        return await _http.GetStringAsync(url, cancellationToken);
    }
}
