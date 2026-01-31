using FanficDownloader.Bot.Sources;

public class SourceManager
{
    private readonly List<IFanficSource> _sources = new()
    {
        new FicbookSource(),
        new SnapetalesSource(),
        new FanfictionNetSource(),
        new WalkingThePlankSource()
    };

    public IFanficSource GetSource(string url)
    {
        return _sources.FirstOrDefault(s => s.CanHandle(url))
            ?? throw new NotSupportedException();
    }
}
