namespace FanficDownloader.Bot.Models;

public class Fanfic
{
    public string Title { get; set; } = "";
    public List<string> Authors { get; set; } = [];
    public List<string> Fandoms { get; set; } = [];
    public List<string> Pairings { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string Description { get; set; } = "";

    public List<Chapter> Chapters { get; set; } = [];

}
