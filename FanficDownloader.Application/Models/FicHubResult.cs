namespace FanficDownloader.Application.Models;

public class FicHubResult
{
    public byte[] Bytes { get; set; } = default!;
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int Chapters { get; set; }
    public string Description { get; set; } = "";
}