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
        File.WriteAllText(
            Path.Combine(metaInf, "container.xml"),
            """
<?xml version="1.0"?>
<container version="1.0"
 xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
 <rootfiles>
  <rootfile full-path="OEBPS/content.opf"
   media-type="application/oebps-package+xml"/>
 </rootfiles>
</container>
""",
            Encoding.UTF8
        );

        // 3. cover
        if (!string.IsNullOrEmpty(fanfic.CoverUrl))
        {
            using var http = new HttpClient();
            var bytes = http.GetByteArrayAsync(fanfic.CoverUrl).Result;
            File.WriteAllBytes(Path.Combine(oebps, "cover.jpg"), bytes);

            File.WriteAllText(Path.Combine(oebps, "cover.html"), """
<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN"
 "http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<body>
<img src="cover.jpg" alt="cover"/>
</body>
</html>
""", Encoding.UTF8);
        }

        // 4. title page
        File.WriteAllText(Path.Combine(oebps, "title.html"), $"""
<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN"
 "http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<body>
<h1>{fanfic.Title}</h1>
<p><b>Авторы:</b> {string.Join(", ", fanfic.Authors)}</p>
<p><b>Фэндомы:</b> {string.Join(", ", fanfic.Fandoms)}</p>
<p><b>Пейринги:</b> {string.Join(", ", fanfic.Pairings)}</p>
<p><b>Теги:</b> {string.Join(", ", fanfic.Tags)}</p>
<p><b>Описание:</b> {fanfic.Description}</p>
</body>
</html>
""", Encoding.UTF8);

        // 5. chapters
        foreach (var ch in fanfic.Chapters)
        {
            File.WriteAllText(
                Path.Combine(oebps, $"chapter{ch.Number}.html"),
                BuildChapterHtml(ch),
                Encoding.UTF8
            );
        }

        // 6. toc.ncx
        var toc = new StringBuilder();
        toc.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        toc.AppendLine(@"<ncx xmlns=""http://www.daisy.org/z3986/2005/ncx/"" version=""2005-1"">");
        toc.AppendLine(@"<navMap>");

        toc.AppendLine("""
<navPoint id="titlepage" playOrder="1">
 <navLabel><text>О книге</text></navLabel>
 <content src="title.html"/>
</navPoint>
""");

        int index = 2;
        foreach (var ch in fanfic.Chapters.OrderBy(c => c.Number))
        {
            toc.AppendLine($"""
<navPoint id="c{ch.Number}" playOrder="{index}">
 <navLabel><text>{ch.Title}</text></navLabel>
 <content src="chapter{ch.Number}.html"/>
</navPoint>
""");
            index++;
        }

        toc.AppendLine("</navMap></ncx>");

        File.WriteAllText(
            Path.Combine(oebps, "toc.ncx"),
            toc.ToString(),
            Encoding.UTF8
        );

        // 7. content.opf
        var manifest = new StringBuilder();
        var spine = new StringBuilder();

        manifest.AppendLine("<item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>");

        if (!string.IsNullOrEmpty(fanfic.CoverUrl))
        {
            manifest.AppendLine("<item id=\"cover\" href=\"cover.jpg\" media-type=\"image/jpeg\"/>");
            manifest.AppendLine("<item id=\"coverpage\" href=\"cover.html\" media-type=\"application/xhtml+xml\"/>");
            spine.AppendLine("<itemref idref=\"coverpage\"/>");
        }

        manifest.AppendLine("<item id=\"titlepage\" href=\"title.html\" media-type=\"application/xhtml+xml\"/>");
        spine.AppendLine("<itemref idref=\"titlepage\"/>");

        foreach (var ch in fanfic.Chapters.OrderBy(c => c.Number))
        {
            manifest.AppendLine($"<item id=\"c{ch.Number}\" href=\"chapter{ch.Number}.html\" media-type=\"application/xhtml+xml\"/>");
            spine.AppendLine($"<itemref idref=\"c{ch.Number}\"/>");
        }

        File.WriteAllText(
            Path.Combine(oebps, "content.opf"),
            $"""
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
<spine toc="ncx">
{spine}
</spine>
</package>
""",
            Encoding.UTF8
        );

        // 8. zip
        var epubPath = Path.Combine(Path.GetTempPath(), $"{safeTitle}.epub");

        using (var fs = new FileStream(epubPath, FileMode.Create))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var mimeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimeEntry.Open(), Encoding.ASCII))
                writer.Write("application/epub+zip");

            foreach (var file in Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(tempRoot, file)
                    .Replace("\\", "/");

                if (relative == "mimetype") continue;

                zip.CreateEntryFromFile(file, relative, CompressionLevel.Optimal);
            }
        }

        Directory.Delete(tempRoot, true);
        return epubPath;
    }

    private string BuildChapterHtml(Chapter chapter)
    {
        return $"""
<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN"
 "http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<body>
<h2>{chapter.Title}</h2>
{chapter.Text}
</body>
</html>
""";
    }

}
