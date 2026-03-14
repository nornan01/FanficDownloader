using System.IO;
using System.Linq;

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
}