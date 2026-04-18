using FanficDownloader.Core.Models;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection.PortableExecutable;

namespace FanficDownloader.Core.Formatting;


public class FanficEpubFormatter
{
    private readonly HttpClient _http;

    public FanficEpubFormatter(
        HttpClient http)
    {
        _http = http;
    }

    public async Task<string> BuildEpubFileAsync(Fanfic fanfic, CancellationToken cancellationToken)
    {
        var safeTitle = string.Concat(
            fanfic.Title.Where(c => !Path.GetInvalidFileNameChars().Contains(c))
        );

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string? epubPath = null;
        var metaInf = Path.Combine(tempRoot, "META-INF");
        var oebps = Path.Combine(tempRoot, "OEBPS");
        


try{
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(metaInf);
        Directory.CreateDirectory(oebps);
        var imagesDir = Path.Combine(oebps, "Images");
        Directory.CreateDirectory(imagesDir);
        // mimetype
        File.WriteAllText(
            Path.Combine(tempRoot, "mimetype"),
            "application/epub+zip",
            Encoding.ASCII
        );

        // container.xml
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

        // cover
        if (!string.IsNullOrEmpty(fanfic.CoverUrl))
        {
            var bytes = await _http.GetByteArrayAsync(fanfic.CoverUrl, cancellationToken);
            File.WriteAllBytes(Path.Combine(oebps, "cover.jpg"), bytes);

            File.WriteAllText(Path.Combine(oebps, "cover.xhtml"), """
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

        // title page
        File.WriteAllText(Path.Combine(oebps, "title.xhtml"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN"
            "http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">
            <html xmlns="http://www.w3.org/1999/xhtml">
            <body>
            <h1>{XmlEscape(fanfic.Title)}</h1>
            {LineList("Авторы", fanfic.Authors)}
            {LineList("Фэндомы", fanfic.Fandoms)}
            {LineList("Пейринги", fanfic.Pairings)}

            {Line("Рейтинг", fanfic.Rating)}
            {Line("Размер", fanfic.Size)}
            {Line("Статус", fanfic.IsFinished == true ? "Завершён" : "В процессе")}

            {LineList("Жанры", fanfic.Tags)}
            {LineList("Другие метки", fanfic.OtherTags)}

            {Line("Описание", fanfic.Description)}

            {LineRaw("Примечания", fanfic.Notes)}
            {LineRaw("Посвящение", fanfic.Dedication)}

            {Line("Ссылка", fanfic.SourceUrl)}
            </body>
            </html>
            """, Encoding.UTF8);
        // about page
        File.WriteAllText(Path.Combine(oebps, "about.xhtml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN"
            "http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">
            <html xmlns="http://www.w3.org/1999/xhtml">
            <body>
            <h2>About Fanfic Downloader</h2>

            <p>––––––––––––––––––––</p>

            <p>Thank you for using Fanfic Downloader 💜</p>

            <p>
            Join the Telegram channel for updates, new supported websites, and improvements:<br/>
            https://t.me/fanficdownloaderhub
            </p>
            
            <p>
            Telegram bot for fast downloads:<br/>
            https://t.me/fanfic_downloader_bot
            </p>

            <p>
            Web version:<br/>
            https://fanficdownloader.com/
            </p>

            <p>
            Found a bug, have suggestions or want to see support for another site?<br/>
            Send us your ideas — we’re building this together.
            </p>

            <p>Happy reading ✨</p>

            </body>
            </html>
            """, Encoding.UTF8);

        // chapters
        // download images + chapters
        var downloaded = new HashSet<string>();

        foreach (var ch in fanfic.Chapters)
        {
            var matches = Regex.Matches(ch.Text, "<img src=\"(.*?)\"", RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                var url = m.Groups[1].Value;

                if (downloaded.Contains(url))
                    continue;

                downloaded.Add(url);

                try
                {
                    var bytes = await _http.GetByteArrayAsync(url, cancellationToken);
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        continue;

                    var name = Path.GetFileName(uri.LocalPath);


                    File.WriteAllBytes(Path.Combine(imagesDir, name), bytes);
                }
                    catch (Exception ex)
                    {
                    }
                }

            // заменить ссылки в html
            ch.Text = Regex.Replace(
                ch.Text,
                "<img src=\"(.*?)\"",
                m =>
                {
                    var url = m.Groups[1].Value;
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        return m.Value;

                    var name = Path.GetFileName(uri.LocalPath);

                    return $"<img src=\"Images/{name}\"";
                }
            );

            File.WriteAllText(
                Path.Combine(oebps, $"chapter{ch.Number}.xhtml"),
                BuildChapterHtml(ch),
                Encoding.UTF8
            );
        }


        // toc.ncx
        var toc = new StringBuilder();
        toc.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        toc.AppendLine(@"<ncx xmlns=""http://www.daisy.org/z3986/2005/ncx/"" version=""2005-1"">");
        toc.AppendLine(@"<navMap>");

        toc.AppendLine("""
            <navPoint id="titlepage" playOrder="1">
            <navLabel><text>О книге</text></navLabel>
            <content src="title.xhtml"/>
            </navPoint>
            """);

        int index = 2;
        foreach (var ch in fanfic.Chapters.OrderBy(c => c.Number))
        {
            var chapterTitle = string.IsNullOrWhiteSpace(ch.Title)
                ? fanfic.Title
                : ch.Title;

            toc.AppendLine($"""
                <navPoint id="c{ch.Number}" playOrder="{index}">
                <navLabel><text>{XmlEscape(chapterTitle)}</text></navLabel>
                <content src="chapter{ch.Number}.xhtml"/>
                </navPoint>
                """);
            index++;
        }
        toc.AppendLine($"""
            <navPoint id="about" playOrder="{index}">
            <navLabel><text>About Fanfic Downloader</text></navLabel>
            <content src="about.xhtml"/>
            </navPoint>
            """);

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
            manifest.AppendLine("<item id=\"coverpage\" href=\"cover.xhtml\" media-type=\"application/xhtml+xml\"/>");
            spine.AppendLine("<itemref idref=\"coverpage\"/>");
        }

        manifest.AppendLine("<item id=\"titlepage\" href=\"title.xhtml\" media-type=\"application/xhtml+xml\"/>");
        spine.AppendLine("<itemref idref=\"titlepage\"/>");

        if (Directory.Exists(imagesDir))
        {
            foreach (var img in Directory.GetFiles(imagesDir))
            {
                var name = Path.GetFileName(img);
                manifest.AppendLine($"<item id=\"img_{name}\" href=\"Images/{name}\" media-type=\"image/jpeg\"/>");
            }
        }


        foreach (var ch in fanfic.Chapters.OrderBy(c => c.Number))
        {
            manifest.AppendLine($"<item id=\"c{ch.Number}\" href=\"chapter{ch.Number}.xhtml\" media-type=\"application/xhtml+xml\"/>");
            spine.AppendLine($"<itemref idref=\"c{ch.Number}\"/>");
        }
        manifest.AppendLine("<item id=\"about\" href=\"about.xhtml\" media-type=\"application/xhtml+xml\"/>");
        spine.AppendLine("<itemref idref=\"about\"/>");


        File.WriteAllText(
            Path.Combine(oebps, "content.opf"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" unique-identifier="bookid" version="2.0">
            <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
            <dc:title>{XmlEscape(fanfic.Title)}</dc:title>
            <dc:creator>{XmlEscape(string.Join(", ", fanfic.Authors))}</dc:creator>
            <dc:language>ru</dc:language>
            <dc:identifier id="bookid">fanfic-{Guid.NewGuid()}</dc:identifier>
            <dc:source>{XmlEscape(fanfic.SourceUrl)}</dc:source>
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

            // zip
            epubPath = Path.Combine(
                Path.GetTempPath(),
                $"{safeTitle}_{Guid.NewGuid()}.epub"
            );

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

        return epubPath;
        }
        finally
        {
            try
            {
                if(Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch (Exception ex)
            {
            }
        }
    }

    private string BuildChapterHtml(Chapter chapter)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.1//EN"
            "http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd">
            <html xmlns="http://www.w3.org/1999/xhtml">
            <body>
            <h2>{XmlEscape(chapter.Title)}</h2>
            {(string.IsNullOrWhiteSpace(chapter.StartNotes) ? "" : $@"
            <p><b>Примечания автора:</b></p>
            {chapter.StartNotes}
            <p>***</p>
            ")}
            {MakeValidXhtml(chapter.Text)}
            {(string.IsNullOrWhiteSpace(chapter.EndNotes) ? "" : $@"
            <p><b>Примечания автора:</b></p>
            {chapter.EndNotes}
            ")}
            </body>
            </html>
            """;
    }

    private static string XmlEscape(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return "";

        return System.Security.SecurityElement.Escape(s);
    }

    private static string MakeValidXhtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return "";

        // исправить & 
        html = Regex.Replace(
            html,
            "&(?!amp;|lt;|gt;|quot;|apos;|#\\d+;)",
            "&amp;"
        );

        // <br> → <br />
        html = Regex.Replace(html, "<br>", "<br />", RegexOptions.IgnoreCase);

        return html;
    }

    private string Line(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return $"<p><b>{label}:</b> {XmlEscape(value)}</p>";
    }

    private string LineRaw(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return $"<p><b>{label}:</b> {value}</p>";
    }

    private string LineList(string label, List<string> values)
    {
        if (values == null || values.Count == 0)
            return "";

        return $"<p><b>{label}:</b> {XmlEscape(string.Join(", ", values))}</p>";
    }

}
