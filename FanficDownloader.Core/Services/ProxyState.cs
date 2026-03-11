namespace FanficDownloader.Core.Services;



public class ProxyState
{
    public string Url { get; set; } = "";
    public int FailCount { get; set; }
    public DateTime? CooldownUntil { get; set; }
}