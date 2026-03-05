using Xunit;
using FanficDownloader.Core.Parsers;

namespace FanficDownloader.Tests;

public class SnapetalesParserTests
{
    [Fact]
    public void Parse_ExtractsTitle()
    {
        var html = """
        <h3>My Snapetales Story</h3>
        """;

        var parser = new SnapetalesParser();

        var fanfic = parser.Parse(html);

        Assert.Equal("My Snapetales Story", fanfic.Title);
    }

    [Fact]
    public void Parse_ExtractsAuthors()
    {
        var html = """
        <table>
        <tr>
            <td>Автор</td>
            <td><a>Severus</a></td>
        </tr>
        </table>
        """;

        var parser = new SnapetalesParser();

        var fanfic = parser.Parse(html);

        Assert.Contains("Severus", fanfic.Authors);
    }

    [Fact]
    public void Parse_ExtractsChapters()
    {
        var html = """
        <a class="no_decoration12" href="index.php?ch_id=1">Chapter One</a>
        <a class="no_decoration12" href="index.php?ch_id=2">Chapter Two</a>
        """;

        var parser = new SnapetalesParser();

        var fanfic = parser.Parse(html);

        Assert.Equal(2, fanfic.Chapters.Count);
        Assert.Equal("Chapter One", fanfic.Chapters[0].Title);
        Assert.Equal("https://www.snapetales.com/index.php?ch_id=1", fanfic.Chapters[0].Url);
    }

    [Fact]
    public void ParseChapterText_SplitsParagraphs()
    {
        var html = """
        <blockquote>
        First paragraph<br><br>
        Second paragraph<br><br>
        Third paragraph
        </blockquote>
        """;

        var parser = new SnapetalesParser();

        var result = parser.ParseChapterText(html);

        Assert.Contains("<p>First paragraph</p>", result);
        Assert.Contains("<p>Second paragraph</p>", result);
        Assert.Contains("<p>Third paragraph</p>", result);
    }

    [Fact]
    public void ParseChapterText_PreservesFormatting()
    {
        var html = """
        <blockquote>
        Hello <i>world</i><br><br>
        <b>Bold text</b>
        </blockquote>
        """;

        var parser = new SnapetalesParser();

        var result = parser.ParseChapterText(html);

        Assert.Contains("<em>world</em>", result);
        Assert.Contains("<strong>Bold text</strong>", result);
    }
}