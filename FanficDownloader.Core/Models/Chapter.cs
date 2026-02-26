namespace FanficDownloader.Core.Models;

public class Chapter
{
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Text { get; set; } = "";

    public List<ImageInfo> Images { get; set; } = new();

}
