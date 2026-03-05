using System.ComponentModel.DataAnnotations;
using FanficDownloader.Core.Models;
using FanficDownloader.Core.Sources;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
public class SourceManager
{
    private readonly IEnumerable<IFanficSource> _sources;
    private readonly ILogger<SourceManager> _logger;
    
    public SourceManager(IEnumerable<IFanficSource> sources, ILogger<SourceManager> logger)
    {
        _logger = logger;   
        _sources = sources;
    }

    public IFanficSource GetSource(string url)
    {
        _logger.LogInformation("Getting source for URL: {Url}", url);
        
        var source = _sources.FirstOrDefault(s => s.CanHandle(url));
        if (source == null)
        {
            _logger.LogWarning("No source found for URL: {Url}", url);
            throw new NotSupportedException($"No source found for URL: {url}");
        }
        _logger.LogInformation("Source {SourceType} will handle URL: {Url}", source.GetType().Name, url);
        return source;  
    }
}
