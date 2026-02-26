namespace FanficDownloader.Application.Models;

public class DownloadFileResult
{
    public byte[] Bytes { get; set; } = [];
    public string ContentType { get; set; } = "";
    public string FileName { get; set; } = "";
}
