using FanficDownloader.Core.Models;

public class DownloadResult
{
    public Fanfic Fanfic { get; set; } = null!;
    public int TotalChapters { get; set; }
    public int LoadedChapters { get; set; }
    public List<int> FailedChapters { get; set; } = new();
}
