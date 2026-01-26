using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using FanficDownloader.Bot.Models;

namespace FanficDownloader.Bot.Formatting;

public class EpubGenerator
{
    /// <summary>
    /// Converts a text string to an EPUB file.
    /// </summary>
    /// <param name="txtContent">The text content to convert</param>
    /// <param name="title">The title of the book</param>
    /// <param name="author">The author of the book</param>
    /// <param name="outputPath">The path where the EPUB file will be saved</param>
    public void CreateEpubFromTxt(string txtContent, string title, string author, string outputPath)
    {
        // Create a temporary directory for EPUB contents
        string tempDir = Path.Combine(Path.GetTempPath(), $"epub_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create EPUB structure
            CreateEpubStructure(tempDir);

            // Generate content files
            GenerateContentFiles(tempDir, txtContent, title, author);

            // Generate metadata
            GenerateMetadata(tempDir, title, author);

            // Generate table of contents
            GenerateToc(tempDir, title);

            // Package as ZIP
            PackageAsEpub(tempDir, outputPath);
        }
        finally
        {
            // Clean up temporary directory
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Converts Fanfic object to EPUB file.
    /// </summary>
    public void CreateEpubFromFanfic(Fanfic fanfic, string outputPath)
    {
        var formatter = new FanficFormatter();
        string txtContent = formatter.ToTxt(fanfic);
        string authors = string.Join(", ", fanfic.Authors);
        
        CreateEpubFromTxt(txtContent, fanfic.Title, authors, outputPath);
    }

    private void CreateEpubStructure(string baseDir)
    {
        // Create directories
        Directory.CreateDirectory(Path.Combine(baseDir, "META-INF"));
        Directory.CreateDirectory(Path.Combine(baseDir, "OEBPS", "Text"));
        Directory.CreateDirectory(Path.Combine(baseDir, "OEBPS", "Styles"));

        // Create mimetype file (must be first and uncompressed)
        File.WriteAllText(Path.Combine(baseDir, "mimetype"), "application/epub+zip", Encoding.ASCII);
    }

    private void GenerateContentFiles(string baseDir, string txtContent, string title, string author)
    {
        // Split content into chapters (by double newlines or other delimiters)
        var chapters = SplitIntoChapters(txtContent);

        // Generate cover page
        string coverXhtml = GenerateCoverXhtml(title, author);
        File.WriteAllText(Path.Combine(baseDir, "OEBPS", "Text", "cover.xhtml"), coverXhtml, Encoding.UTF8);

        // Generate chapter files
        for (int i = 0; i < chapters.Count; i++)
        {
            string chapterXhtml = GenerateChapterXhtml(chapters[i]);
            string chapterFile = Path.Combine(baseDir, "OEBPS", "Text", $"chapter_{i + 1:D3}.xhtml");
            File.WriteAllText(chapterFile, chapterXhtml, Encoding.UTF8);
        }

        // Generate CSS
        string cssContent = GenerateCss();
        File.WriteAllText(Path.Combine(baseDir, "OEBPS", "Styles", "style.css"), cssContent, Encoding.UTF8);
    }

    private List<string> SplitIntoChapters(string content)
    {
        // Split by chapter headers (lines starting with specific patterns or double newlines)
        var chapters = new List<string>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        var currentChapter = new StringBuilder();
        
        foreach (var line in lines)
        {
            // Check if this line is a chapter header (has dashes underneath pattern)
            if (line.StartsWith("---") || line.All(c => c == '-'))
            {
                if (currentChapter.Length > 0)
                {
                    chapters.Add(currentChapter.ToString().Trim());
                    currentChapter.Clear();
                }
            }
            else
            {
                currentChapter.AppendLine(line);
            }
        }

        if (currentChapter.Length > 0)
            chapters.Add(currentChapter.ToString().Trim());

        return chapters.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    private string GenerateCoverXhtml(string title, string author)
    {
        return $@"<?xml version='1.0' encoding='utf-8'?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"" lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <title>Cover</title>
    <link rel=""stylesheet"" type=""text/css"" href=""../Styles/style.css"" />
</head>
<body class=""cover"">
    <h1>{HtmlEncode(title)}</h1>
    <p class=""author"">{HtmlEncode(author)}</p>
</body>
</html>";
    }

    private string GenerateChapterXhtml(string chapterContent)
    {
        var lines = chapterContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var sb = new StringBuilder();

        sb.AppendLine(@"<?xml version='1.0' encoding='utf-8'?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"" lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <link rel=""stylesheet"" type=""text/css"" href=""../Styles/style.css"" />
</head>
<body>");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine("    <p class=\"empty\"></p>");
            }
            else
            {
                sb.AppendLine($"    <p>{HtmlEncode(line)}</p>");
            }
        }

        sb.AppendLine(@"</body>
</html>");

        return sb.ToString();
    }

    private string GenerateCss()
    {
        return @"
body {
    font-family: serif;
    line-height: 1.5;
    margin: 1em;
}

h1, h2, h3 {
    font-weight: bold;
    margin-top: 1em;
    margin-bottom: 0.5em;
}

p {
    margin-bottom: 0.5em;
    text-align: justify;
}

p.empty {
    margin: 0.5em 0;
}

.cover {
    text-align: center;
}

.cover h1 {
    margin-top: 2em;
    font-size: 2em;
}

.author {
    margin-top: 1em;
    font-style: italic;
}
";
    }

    private void GenerateMetadata(string baseDir, string title, string author)
    {
        string uuid = $"urn:uuid:{Guid.NewGuid()}";
        string date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var containerXml = new XDocument(
            new XElement("container",
                new XAttribute("version", "1.0"),
                new XAttribute("xmlns", "urn:oasis:names:tc:opendocument:xmlns:container"),
                new XElement("rootfiles",
                    new XElement("rootfile",
                        new XAttribute("full-path", "OEBPS/content.opf"),
                        new XAttribute("media-type", "application/oebps-package+xml")
                    )
                )
            )
        );

        var metaDir = Path.Combine(baseDir, "META-INF");
        containerXml.Save(Path.Combine(metaDir, "container.xml"));

        // Generate content.opf
        var contentOpf = GenerateContentOpf(title, author, uuid, date);
        contentOpf.Save(Path.Combine(baseDir, "OEBPS", "content.opf"));
    }

    private XDocument GenerateContentOpf(string title, string author, string uuid, string date)
    {
        return new XDocument(
            new XElement("package",
                new XAttribute("version", "3.0"),
                new XAttribute("xmlns", "http://www.idpf.org/2007/opf"),
                new XAttribute("unique-identifier", "BookID"),
                new XElement("metadata",
                    new XAttribute("xmlns:dc", "http://purl.org/dc/elements/1.1/"),
                    new XAttribute("xmlns:opf", "http://www.idpf.org/2007/opf"),
                    new XElement(XName.Get("identifier", "http://purl.org/dc/elements/1.1/"),
                        new XAttribute("id", "BookID"),
                        uuid
                    ),
                    new XElement(XName.Get("title", "http://purl.org/dc/elements/1.1/"), title),
                    new XElement(XName.Get("creator", "http://purl.org/dc/elements/1.1/"), author),
                    new XElement(XName.Get("language", "http://purl.org/dc/elements/1.1/"), "en"),
                    new XElement(XName.Get("date", "http://purl.org/dc/elements/1.1/"), date)
                ),
                new XElement("manifest",
                    new XElement("item", new XAttribute("id", "ncx"), new XAttribute("href", "toc.ncx"), new XAttribute("media-type", "application/x-dtbncx+xml")),
                    new XElement("item", new XAttribute("id", "css"), new XAttribute("href", "Styles/style.css"), new XAttribute("media-type", "text/css")),
                    new XElement("item", new XAttribute("id", "cover"), new XAttribute("href", "Text/cover.xhtml"), new XAttribute("media-type", "application/xhtml+xml")),
                    new XElement("item", new XAttribute("id", "chapter_001"), new XAttribute("href", "Text/chapter_001.xhtml"), new XAttribute("media-type", "application/xhtml+xml"))
                ),
                new XElement("spine",
                    new XAttribute("toc", "ncx"),
                    new XElement("itemref", new XAttribute("idref", "cover")),
                    new XElement("itemref", new XAttribute("idref", "chapter_001"))
                )
            )
        );
    }

    private void GenerateToc(string baseDir, string title)
    {
        var tocNcx = new XDocument(
            new XElement("ncx",
                new XAttribute("version", "2005-1"),
                new XAttribute("xmlns", "http://www.daisy.org/z3986/2005/ncx/"),
                new XElement("head",
                    new XElement("meta", new XAttribute("name", "dtb:uid"), new XAttribute("content", Guid.NewGuid().ToString())),
                    new XElement("meta", new XAttribute("name", "dtb:depth"), new XAttribute("content", "1")),
                    new XElement("meta", new XAttribute("name", "dtb:totalPageCount"), new XAttribute("content", "0")),
                    new XElement("meta", new XAttribute("name", "dtb:maxPageNumber"), new XAttribute("content", "0"))
                ),
                new XElement("docTitle",
                    new XElement("text", title)
                ),
                new XElement("navMap",
                    new XElement("navPoint",
                        new XAttribute("id", "navpoint-1"),
                        new XAttribute("playOrder", "1"),
                        new XElement("navLabel",
                            new XElement("text", "Cover")
                        ),
                        new XElement("content", new XAttribute("src", "Text/cover.xhtml"))
                    )
                )
            )
        );

        tocNcx.Save(Path.Combine(baseDir, "OEBPS", "toc.ncx"));
    }

    private void PackageAsEpub(string sourceDir, string outputPath)
    {
        // Delete existing file if it exists
        if (File.Exists(outputPath))
            File.Delete(outputPath);

        using (var zipStream = File.Create(outputPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // Add mimetype first (uncompressed)
            var mimetypeFile = Path.Combine(sourceDir, "mimetype");
            if (File.Exists(mimetypeFile))
            {
                archive.CreateEntryFromFile(mimetypeFile, "mimetype", System.IO.Compression.CompressionLevel.NoCompression);
            }

            // Add all other files
            AddFilesToZip(archive, sourceDir, "");
        }
    }

    private void AddFilesToZip(ZipArchive archive, string directoryPath, string entryPrefix)
    {
        foreach (var file in Directory.GetFiles(directoryPath))
        {
            if (Path.GetFileName(file) == "mimetype") continue;

            string entryName = entryPrefix + Path.GetFileName(file);
            archive.CreateEntryFromFile(file, entryName);
        }

        foreach (var directory in Directory.GetDirectories(directoryPath))
        {
            string folderName = Path.GetFileName(directory);
            string newPrefix = entryPrefix + folderName + "/";
            AddFilesToZip(archive, directory, newPrefix);
        }
    }

    private string HtmlEncode(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return System.Net.WebUtility.HtmlEncode(text);
    }
}
