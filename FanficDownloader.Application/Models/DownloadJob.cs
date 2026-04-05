public class DownloadJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Url { get; set; } = "";
    public string Format { get; set; } = "";

    public string RequesterId { get; set; } = "";

    public byte[]? Result { get; set; }
}