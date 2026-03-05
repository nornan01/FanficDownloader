using Xunit;
using FanficDownloader.Core.Formatting;
using FanficDownloader.Core.Models;
using System.IO.Compression;

namespace FanficDownloader.Tests;

public class EpubFormatterTests
{
    [Fact]
    public async Task BuildEpub_CreatesValidFile()
    {
        var http = new HttpClient();

        var formatter = new FanficEpubFormatter(http);

        var fanfic = new Fanfic
        {
            Title = "Test Story",
            Authors = new() { "Author" },
            Fandoms = new() { "Harry Potter" },
            Pairings = new() { "HP/SS" },
            Tags = new() { "Angst" },
            Description = "Test description",
            SourceUrl = "https://example.com",
            Chapters = new()
            {
                new Chapter
                {
                    Number = 1,
                    Title = "Awesome Chapter",
                    Text = "<p>Damn good story</p>"
                }
            }
        };

        var path = await formatter.BuildEpubFileAsync(
            fanfic,
            CancellationToken.None
        );

        Assert.True(File.Exists(path));

        using var zip = ZipFile.OpenRead(path);

        Assert.Contains(zip.Entries, e => e.FullName == "mimetype");
        Assert.Contains(zip.Entries, e => e.FullName == "META-INF/container.xml");
        Assert.Contains(zip.Entries, e => e.FullName == "OEBPS/content.opf");
        Assert.Contains(zip.Entries, e => e.FullName == "OEBPS/toc.ncx");

        File.Delete(path);
    }
}