using Xunit;
using FanficDownloader.Core.Formatting;
using FanficDownloader.Core.Models;

namespace FanficDownloader.Tests;

public class TxtFormatterTests
{
    [Fact]
    public void ToTxt_RemovesHtmlTags()
    {
        var formatter = new FanficTxtFormatter();

        var fanfic = new Fanfic
        {
            Title = "Test",
            Authors = new() { "Author" },
            Chapters = new()
            {
                new Chapter
                {
                    Number = 1,
                    Title = "Chapter 1",
                    Text = "<p>Hello <em>world</em></p>"
                }
            }
        };

        var txt = formatter.ToTxt(fanfic);

        Assert.Contains("Hello world", txt);
        Assert.DoesNotContain("<em>", txt);
    }
    [Fact]
    public void ToTxt_ConvertsBrToNewLine()
    {
        var formatter = new FanficTxtFormatter();

        var fanfic = new Fanfic
        {
            Title = "Test",
            Chapters = new()
        {
            new Chapter
            {
                Number = 1,
                Title = "Chapter 1",
                Text = "Hello<br>World"
            }
        }
        };

        var txt = formatter.ToTxt(fanfic);

        Assert.Contains("Hello\nWorld", txt);
    }
    [Fact]
    public void ToTxt_ConvertsHrToSeparator()
    {
        var formatter = new FanficTxtFormatter();

        var fanfic = new Fanfic
        {
            Title = "Test",
            Chapters = new()
        {
            new Chapter
            {
                Number = 1,
                Title = "Chapter 1",
                Text = "<p>Scene</p><hr><p>Next</p>"
            }
        }
        };

        var txt = formatter.ToTxt(fanfic);

        Assert.Contains("*****", txt);
    }

}