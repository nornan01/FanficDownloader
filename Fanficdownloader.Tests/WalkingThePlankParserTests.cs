using Xunit;
using FanficDownloader.Core.Parsers;

namespace FanficDownloader.Tests;

public class WalkingThePlankParserTests
{
    [Fact]
    public void Parse_ExtractsTitle()
    {
        var html = """
        <div id="pagetitle">
            <a>My Story</a>
            <a>AuthorName</a>
        </div>
        """;

        var parser = new WalkingThePlankParser();

        var fanfic = parser.Parse(html);

        Assert.Equal("My Story", fanfic.Title);
    }

    [Fact]
    public void Parse_ExtractsAuthor()
    {
        var html = """
        <div id="pagetitle">
            <a>My Story</a>
            <a>AuthorName</a>
        </div>
        """;

        var parser = new WalkingThePlankParser();

        var fanfic = parser.Parse(html);

        Assert.Contains("AuthorName", fanfic.Authors);
    }

    [Fact]
    public void Parse_ExtractsTags()
    {
        var html = """
        <div class="infobox">
            <span class="label">Genres:</span> Angst, Drama
            <span class="label">Warnings:</span> Violence
        </div>
        """;

        var parser = new WalkingThePlankParser();

        var fanfic = parser.Parse(html);

        Assert.Contains("Angst", fanfic.Tags);
        Assert.Contains("Drama", fanfic.Tags);
        Assert.Contains("Violence", fanfic.Tags);
    }

    [Fact]
    public void Parse_ExtractsChapters()
    {
        var html = """
        <div class="chaptertitle">Chapter One</div>
        <div class="chapter">Text</div>

        <div class="chaptertitle">Chapter Two</div>
        <div class="chapter">Text</div>
        """;

        var parser = new WalkingThePlankParser();

        var fanfic = parser.Parse(html);

        Assert.Equal(2, fanfic.Chapters.Count);
        Assert.Equal("Chapter One", fanfic.Chapters[0].Title);
    }

    [Fact]
    public void ParseAllChapterTexts_CleansHtml()
    {
        var html = """
        <div class="chapter">
            <p>Hello <i>world</i></p>
            <p><b>Bold text</b></p>
        </div>
        """;

        var parser = new WalkingThePlankParser();

        var chapters = parser.ParseAllChapterTexts(html);

        Assert.Contains("<em>world</em>", chapters[0]);
        Assert.Contains("<strong>Bold text</strong>", chapters[0]);
    }
}