using Xunit;
using FanficDownloader.Core.Parsers;

namespace FanficDownloader.Tests;

public class FicbookParserTests
{
    [Fact]
    public void ParseChapterText_SplitsParagraphs()
    {
        // Arrange
        var html = """
        <div id="content">
        First paragraph

        Second paragraph
        </div>
        """;

        var parser = new FicbookParser();

        // Act
        var result = parser.ParseChapterText(html);

        // Assert
        Assert.Contains("<p>First paragraph</p>", result);
        Assert.Contains("<p>Second paragraph</p>", result);
    }
    
    [Fact]
    public void ParseChapterText_PreservesItalics()
    {
        // Arrange
        var html = """
        <div id="content">
        First paragraph with <i>italics</i>.
        </div>
        """;

        var parser = new FicbookParser();

        // Act
        var result = parser.ParseChapterText(html);

        // Assert
        Assert.Contains("<em>italics</em>", result);
    }

    [Fact]
    public void Parse_ExtractsChapters()
    {
        var html = """
        <ul class="list-of-fanfic-parts">
            <li class="part">
                <a class="part-link" href="/readfic/123#part1">
                    <h3>Chapter One</h3>
                </a>
            </li>
            <li class="part">
                <a class="part-link" href="/readfic/123#part2">
                    <h3>Chapter Two</h3>
                </a>
            </li>
        </ul>
        """;

        var parser = new FicbookParser();

        var fanfic = parser.Parse(html);

        Assert.Equal(2, fanfic.Chapters.Count);
        Assert.Equal("Chapter One", fanfic.Chapters[0].Title);
        Assert.Equal("Chapter Two", fanfic.Chapters[1].Title);
    }

}