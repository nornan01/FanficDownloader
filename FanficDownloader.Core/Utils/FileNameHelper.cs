using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FanficDownloader.Core.Utils;

public static class FileNameHelper
{
    
        public static string BuildSafeFileName(string title, string ext)
        {
            
            var invalidChars = Path.GetInvalidFileNameChars();

            var safeTitle = new string(
                title.Where(ch => !invalidChars.Contains(ch)).ToArray()
            );

            safeTitle = safeTitle.Replace(" ", "_");

            return $"{safeTitle}.{ext}";
        }

    public static string BuildHttpSafeFileName(string title, string ext)
    {
        var translit = Transliterate(title);
        var ascii = new string(translit
            .Where(c => c < 128 && (char.IsLetterOrDigit(c) || c == ' '))
            .ToArray());

        ascii = ascii.Trim('_');
        ascii = Regex.Replace(ascii, "_{2,}", "_");

        if (ascii.Length > 80)
            ascii = ascii.Substring(0, 80);

        if (string.IsNullOrWhiteSpace(ascii))
            ascii = "fanfic";
        return $"{ascii}.{ext}";
    }
    public static string Transliterate(string text)
    {
        var map = new Dictionary<char, string>
        {
            ['а'] = "a",
            ['б'] = "b",
            ['в'] = "v",
            ['г'] = "g",
            ['д'] = "d",
            ['е'] = "e",
            ['ё'] = "e",
            ['ж'] = "zh",
            ['з'] = "z",
            ['и'] = "i",
            ['й'] = "y",
            ['к'] = "k",
            ['л'] = "l",
            ['м'] = "m",
            ['н'] = "n",
            ['о'] = "o",
            ['п'] = "p",
            ['р'] = "r",
            ['с'] = "s",
            ['т'] = "t",
            ['у'] = "u",
            ['ф'] = "f",
            ['х'] = "h",
            ['ц'] = "ts",
            ['ч'] = "ch",
            ['ш'] = "sh",
            ['щ'] = "sch",
            ['ъ'] = "",
            ['ы'] = "y",
            ['ь'] = "",
            ['э'] = "e",
            ['ю'] = "yu",
            ['я'] = "ya"
        };

        var sb = new StringBuilder();

        foreach (var c in text.ToLower())
        {
            if (map.TryGetValue(c, out var latin))
                sb.Append(latin);
            else if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c == ' ')
                sb.Append('_');
        }

        return sb.ToString();
    }
}