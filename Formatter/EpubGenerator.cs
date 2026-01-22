using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EpubSharp;
using FanficDownloader.Bot.Models;

namespace FanficDownloader.Bot.Formatters
{
    public class EpubGenerator
    {
        public byte[] CreateEpub(Fanfic fanfic)
        {
            // Создаем книгу
            var book = new EpubBook
            {
                Title = fanfic.Title,
                Author = string.Join(", ", fanfic.Authors),
                Description = fanfic.Description,
                TableOfContents = new List<EpubChapter>()
            };

            // Добавляем метаданные
            AddMetadata(book, fanfic);

            // Добавляем обложку (если есть)
            if (!string.IsNullOrEmpty(fanfic.CoverUrl))
            {
                AddCover(book, fanfic.CoverUrl);
            }

            // Создаем титульную страницу
            AddTitlePage(book, fanfic);

            // Добавляем главы
            AddChapters(book, fanfic);

            // Добавляем информацию о книге
            AddInfoPage(book, fanfic);

            // Конвертируем в массив байтов
            using var memoryStream = new MemoryStream();
            EpubWriter.Write(memoryStream, book);
            return memoryStream.ToArray();
        }

        private void AddMetadata(EpubBook book, Fanfic fanfic)
        {
            // Добавляем дополнительные метаданные
            book.Metadata.Subjects.AddRange(fanfic.Fandoms);
            book.Metadata.Subjects.AddRange(fanfic.Tags);

            if (fanfic.Pairings.Any())
            {
                book.Metadata.Subjects.Add($"Пейринг: {string.Join(", ", fanfic.Pairings)}");
            }

            if (!string.IsNullOrEmpty(fanfic.Rating))
            {
                book.Metadata.Subjects.Add($"Рейтинг: {fanfic.Rating}");
            }

            if (!string.IsNullOrEmpty(fanfic.Status))
            {
                book.Metadata.Subjects.Add($"Статус: {fanfic.Status}");
            }

            book.Metadata.Language = "ru";
            book.Metadata.Date = DateTime.Now.ToString("yyyy-MM-dd");
        }

        private void AddCover(EpubBook book, string coverUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                var imageData = httpClient.GetByteArrayAsync(coverUrl).GetAwaiter().GetResult();
                var coverImage = new EpubByteFile
                {
                    FileName = "cover.jpg",
                    ContentType = "image/jpeg",
                    Content = imageData
                };

                book.AddFile(coverImage);
                book.CoverImage = coverImage;
            }
            catch
            {
                // Если не удалось загрузить обложку, создаем простую текстовую
                AddTextCover(book, fanfic);
            }
        }

        private void AddTextCover(EpubBook book, Fanfic fanfic)
        {
            var coverHtml = $@"
                <html>
                <head>
                    <title>{fanfic.Title}</title>
                    <style>
                        body {{ 
                            font-family: Arial, sans-serif; 
                            text-align: center; 
                            padding: 50px;
                        }}
                        h1 {{ font-size: 2em; margin-bottom: 20px; }}
                        h2 {{ font-size: 1.5em; color: #666; }}
                        .authors {{ font-size: 1.2em; margin: 20px 0; }}
                    </style>
                </head>
                <body>
                    <h1>{fanfic.Title}</h1>
                    <div class='authors'>{string.Join(", ", fanfic.Authors)}</div>
                    <h2>{string.Join(", ", fanfic.Fandoms)}</h2>
                </body>
                </html>";

            var coverPage = new EpubTextFile
            {
                FileName = "cover.xhtml",
                ContentType = "application/xhtml+xml",
                TextContent = coverHtml
            };

            book.AddFile(coverPage);
            book.CoverPage = coverPage;
        }

        private void AddTitlePage(EpubBook book, Fanfic fanfic)
        {
            var titleHtml = $@"
                <html>
                <head>
                    <title>Титульная страница</title>
                    <style>
                        body {{ 
                            font-family: Georgia, serif; 
                            text-align: center; 
                            padding: 100px 50px;
                            line-height: 1.6;
                        }}
                        h1 {{ 
                            font-size: 2.5em; 
                            margin-bottom: 30px;
                            color: #333;
                        }}
                        .authors {{
                            font-size: 1.5em;
                            margin: 20px 0;
                            color: #666;
                        }}
                        .fandoms {{
                            font-size: 1.2em;
                            margin: 20px 0;
                            font-style: italic;
                        }}
                        .description {{
                            text-align: left;
                            margin: 40px auto;
                            max-width: 600px;
                            font-size: 1.1em;
                            line-height: 1.8;
                        }}
                        hr {{
                            margin: 40px 100px;
                            border: none;
                            border-top: 1px solid #ccc;
                        }}
                    </style>
                </head>
                <body>
                    <h1>{EscapeHtml(fanfic.Title)}</h1>
                    <div class='authors'>{EscapeHtml(string.Join(", ", fanfic.Authors))}</div>
                    <div class='fandoms'>{EscapeHtml(string.Join(", ", fanfic.Fandoms))}</div>
                    <hr>
                    <div class='description'>{FormatDescription(fanfic.Description)}</div>
                </body>
                </html>";

            var titlePage = new EpubTextFile
            {
                FileName = "title.xhtml",
                ContentType = "application/xhtml+xml",
                TextContent = titleHtml
            };

            book.AddFile(titlePage);
            book.TableOfContents.Add(new EpubChapter("Титульная страница", titlePage));
        }

        private void AddChapters(EpubBook book, Fanfic fanfic)
        {
            for (int i = 0; i < fanfic.Chapters.Count; i++)
            {
                var chapter = fanfic.Chapters[i];
                var chapterNumber = i + 1;

                var chapterHtml = $@"
                    <html>
                    <head>
                        <title>Глава {chapterNumber}</title>
                        <style>
                            body {{
                                font-family: Georgia, serif;
                                padding: 50px;
                                line-height: 1.8;
                                font-size: 1.1em;
                            }}
                            h1 {{
                                text-align: center;
                                margin-bottom: 50px;
                                font-size: 1.8em;
                                color: #333;
                            }}
                            .chapter-title {{
                                text-align: center;
                                font-size: 1.4em;
                                margin-bottom: 40px;
                                color: #555;
                            }}
                            p {{
                                text-indent: 1.5em;
                                margin-bottom: 1em;
                            }}
                            .chapter-end {{
                                text-align: center;
                                margin-top: 50px;
                                font-style: italic;
                                color: #888;
                            }}
                        </style>
                    </head>
                    <body>
                        <h1>Глава {chapterNumber}</h1>
                        {(string.IsNullOrEmpty(chapter.Title) ? "" :
                          $"<div class='chapter-title'>{EscapeHtml(chapter.Title)}</div>")}
                        <div>{FormatChapterText(chapter.Text)}</div>
                        <div class='chapter-end'>• • •</div>
                    </body>
                    </html>";

                var chapterFile = new EpubTextFile
                {
                    FileName = $"chapter_{chapterNumber}.xhtml",
                    ContentType = "application/xhtml+xml",
                    TextContent = chapterHtml
                };

                book.AddFile(chapterFile);

                var chapterTitle = string.IsNullOrEmpty(chapter.Title)
                    ? $"Глава {chapterNumber}"
                    : $"Глава {chapterNumber}. {chapter.Title}";

                book.TableOfContents.Add(new EpubChapter(chapterTitle, chapterFile));
            }
        }

        private void AddInfoPage(EpubBook book, Fanfic fanfic)
        {
            var infoHtml = $@"
                <html>
                <head>
                    <title>Информация о произведении</title>
                    <style>
                        body {{
                            font-family: Arial, sans-serif;
                            padding: 50px;
                            line-height: 1.6;
                        }}
                        h1, h2 {{
                            color: #333;
                        }}
                        .info-table {{
                            width: 100%;
                            border-collapse: collapse;
                            margin: 20px 0;
                        }}
                        .info-table td {{
                            padding: 10px;
                            border-bottom: 1px solid #eee;
                            vertical-align: top;
                        }}
                        .label {{
                            font-weight: bold;
                            width: 150px;
                            color: #555;
                        }}
                        .tags {{
                            display: flex;
                            flex-wrap: wrap;
                            gap: 5px;
                            margin: 5px 0;
                        }}
                        .tag {{
                            background: #f0f0f0;
                            padding: 2px 8px;
                            border-radius: 10px;
                            font-size: 0.9em;
                        }}
                    </style>
                </head>
                <body>
                    <h1>Информация о произведении</h1>
                    
                    <table class='info-table'>
                        <tr><td class='label'>Название:</td><td>{EscapeHtml(fanfic.Title)}</td></tr>
                        <tr><td class='label'>Автор(ы):</td><td>{EscapeHtml(string.Join(", ", fanfic.Authors))}</td></tr>
                        <tr><td class='label'>Фандом(ы):</td><td>{EscapeHtml(string.Join(", ", fanfic.Fandoms))}</td></tr>
                        <tr><td class='label'>Пейринг(и):</td><td>{EscapeHtml(string.Join(", ", fanfic.Pairings))}</td></tr>
                        <tr><td class='label'>Рейтинг:</td><td>{EscapeHtml(fanfic.Rating)}</td></tr>
                        <tr><td class='label'>Статус:</td><td>{EscapeHtml(fanfic.Status)}</td></tr>
                        <tr><td class='label'>Всего глав:</td><td>{fanfic.Chapters.Count}</td></tr>
                    </table>
                    
                    <h2>Метки</h2>
                    <div class='tags'>
                        {string.Join("", fanfic.Tags.Select(t => $"<span class='tag'>{EscapeHtml(t)}</span>"))}
                    </div>
                    
                    <h2>Описание</h2>
                    <div>{FormatDescription(fanfic.Description)}</div>
                    
                    <div style='margin-top: 50px; color: #888; font-size: 0.9em;'>
                        <p>Сгенерировано: {DateTime.Now:dd.MM.yyyy HH:mm}</p>
                        <p>Источник: Ficbook.net</p>
                    </div>
                </body>
                </html>";

            var infoFile = new EpubTextFile
            {
                FileName = "info.xhtml",
                ContentType = "application/xhtml+xml",
                TextContent = infoHtml
            };

            book.AddFile(infoFile);
            book.TableOfContents.Add(new EpubChapter("Информация", infoFile));
        }

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return System.Net.WebUtility.HtmlEncode(text);
        }

        private string FormatDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return string.Empty;

            var formatted = EscapeHtml(description)
                .Replace("\n", "<br>")
                .Replace("\r", "");

            return $"<p style='white-space: pre-line;'>{formatted}</p>";
        }

        private string FormatChapterText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "<p>Текст главы отсутствует</p>";

            var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var formattedParagraphs = paragraphs.Select(p =>
                $"<p>{EscapeHtml(p.Trim()).Replace("\n", "<br>")}</p>");

            return string.Join("\n", formattedParagraphs);
        }
    }
}