using System.Globalization;
using System.Text;

namespace VideoStreamFetcher.Downloads;

internal static class DownloadPathHelper
{
    public static string SanitizeFileName(string name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (invalidChars.Contains(ch))
            {
                sb.Append('_');
                continue;
            }

            sb.Append(ch);
        }

        var sanitized = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return fallback;
        }

        return sanitized;
    }

    public static string CreateOutputDirectory(string targetDirectory, string? title, string? outputFolderName)
    {
        Directory.CreateDirectory(targetDirectory);

        if (!string.IsNullOrWhiteSpace(outputFolderName))
        {
            var specified = Path.Combine(targetDirectory, SanitizeFileName(outputFolderName, "download"));
            Directory.CreateDirectory(specified);
            return specified;
        }

        var safeTitle = SanitizeFileName(title ?? string.Empty, "download");
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var baseName = $"{timestamp}_{safeTitle}";

        for (var i = 0; i < 100; i++)
        {
            var folderName = i == 0 ? baseName : $"{baseName}_{i}";
            var path = Path.Combine(targetDirectory, folderName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return path;
            }
        }

        var fallback = Path.Combine(targetDirectory, $"{baseName}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}
