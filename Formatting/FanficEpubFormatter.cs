using FanficDownloader.Bot.Models;
using System.IO.Compression;
using System.Text;
using System.Linq;


namespace FanficDownloader.Bot.Formatting;

public class FanficEpubFormatter
{
    public string BuildEpubFile(Fanfic fanfic)
    {
        var safeTitle = string.Concat(
            fanfic.Title.Where(c => !Path.GetInvalidFileNameChars().Contains(c))
        );

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var metaInf = Path.Combine(tempRoot, "META-INF");
        var oebps = Path.Combine(tempRoot, "OEBPS");

        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(metaInf);
        Directory.CreateDirectory(oebps);

        // 1. mimetype
        File.WriteAllText(
            Path.Combine(tempRoot, "mimetype"),
            "application/epub+zip",
            Encoding.ASCII
        );

        // 2. container.xml
        var containerXml = """
        <?xml version="1.0"?>
        <container version="1.0"
          xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles>
            <rootfile full-path="OEBPS/content.opf"
              media-type="application/xhtml+xml"/>
          </rootfiles>
        </container>
        """;

        File.WriteAllText(
            Path.Combine(metaInf, "container.xml"),
            containerXml,
            Encoding.UTF8
        );

        // 3. content.html
        foreach (var chapter in fanfic.Chapters)
        {
            var html = BuildChapterHtml(chapter);
            File.WriteAllText(
                Path.Combine(oebps, $"chapter{chapter.Number}.html"),
                html,
                Encoding.UTF8
            );
        }

        // 4. content.opf
        var manifest = new StringBuilder();
        var spine = new StringBuilder();

        foreach (var chapter in fanfic.Chapters.OrderBy(c => c.Number))
        {
            var id = $"c{chapter.Number}";
            var fileName = $"chapter{chapter.Number}.html";

            manifest.AppendLine(
                $"<item id=\"{id}\" href=\"{fileName}\" media-type=\"application/xhtml+xml\"/>"
            );

            spine.AppendLine(
                $"<itemref idref=\"{id}\"/>"
            );
        }

        var opf = $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <package xmlns="http://www.idpf.org/2007/opf" unique-identifier="bookid" version="2.0">
                    <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                        <dc:title>{fanfic.Title}</dc:title>
                        <dc:creator>{string.Join(", ", fanfic.Authors)}</dc:creator>
                        <dc:language>ru</dc:language>
                        <dc:identifier id="bookid">fanfic-{Guid.NewGuid()}</dc:identifier>
                    </metadata>

                    <manifest>
                        {manifest}
                    </manifest>

                    <spine>
                        {spine}
                    </spine>
                    </package>
                    """;

        File.WriteAllText(
            Path.Combine(oebps, "content.opf"),
            opf,
            Encoding.UTF8
        );



        // 5. zip -> epub
        var epubPath = Path.Combine(Path.GetTempPath(), $"{safeTitle}.epub");
        ZipFile.CreateFromDirectory(tempRoot, epubPath);

        Directory.Delete(tempRoot, true);

        return epubPath;
    }

    private string BuildChapterHtml(Chapter chapter)
    {
        var text = chapter.Text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\n", "<br/>");

        return $"""
                <html>
                <head>
                <meta charset="utf-8">
                <title>{chapter.Title}</title>
                </head>
                <body>
                <h2>{chapter.Title}</h2>
                <p>{text}</p>
                </body>
                </html>
                """;
                    }

}
