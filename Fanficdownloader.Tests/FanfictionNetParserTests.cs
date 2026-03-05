using Xunit;
using FanficDownloader.Core.Parsers;

namespace FanficDownloader.Tests;

public class FanfictionNetParserTests
{
    [Fact]
    public void ParseChapterText_ParsesParagraphs()
    {
        var html = """
        <div id="storytext">
            <p>Hello world</p>
            <p>Second paragraph</p>
        </div>
        """;

        var parser = new FanfictionNetParser();

        var result = parser.ParseChapterText(html);

        Assert.Contains("<p>Hello world</p>", result);
        Assert.Contains("<p>Second paragraph</p>", result);
    }

    [Fact]
    public void ParseChapterText_PreservesFormatting()
    {
        var html = """
        <div id="storytext">
            <p>Hello <i>world</i> and <b>bold</b></p>
        </div>
        """;

        var parser = new FanfictionNetParser();

        var result = parser.ParseChapterText(html);

        Assert.Contains("<em>world</em>", result);
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public void ParseChapterText_PreservesSceneBreak()
    {
        var html = """
        <div id="storytext">
            <p>First</p>
            <hr>
            <p>Second</p>
        </div>
        """;

        var parser = new FanfictionNetParser();

        var result = parser.ParseChapterText(html);

        Assert.Contains("<hr/>", result);
    }

    [Fact]
    public void Parse_ExtractsChapters()
    {
        var html = """
        <select id="chap_select">
            <option value="1">Chapter One</option>
            <option value="2">Chapter Two</option>
        </select>
        """;

        var parser = new FanfictionNetParser();

        var fanfic = parser.Parse(html, "https://www.fanfiction.net/s/12345/1/Test");

        Assert.Equal(2, fanfic.Chapters.Count);
        Assert.Equal("Chapter One", fanfic.Chapters[0].Title);
        Assert.Equal("https://www.fanfiction.net/s/12345/1", fanfic.Chapters[0].Url);
    }

    [Fact]
    public void Parse_ExtractsTitle()
    {
        var html = """
        <b class="xcontrast_txt">My Story</b>
        """;

        var parser = new FanfictionNetParser();

        var fanfic = parser.Parse(html, "https://www.fanfiction.net/s/1/1");

        Assert.Equal("My Story", fanfic.Title);
    }
}