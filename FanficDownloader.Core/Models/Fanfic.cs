namespace FanficDownloader.Core.Models;

public class Fanfic
{
    public string Title { get; set; } = "";
    public List<string> Authors { get; set; } = [];
    public List<string> Fandoms { get; set; } = [];
    public List<string> Pairings { get; set; } = [];
    public string? Size { get; set; }
    public string? Rating { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> OtherTags { get; set; } = [];
    public string Description { get; set; } = "";

    public List<Chapter> Chapters { get; set; } = [];

    public string? CoverUrl { get; set; }

    public string SourceUrl { get; set; } = "";

    public string? Notes { get; set; }
    public string? Dedication { get; set; }
    public string? SessionId { get; set; }


}
