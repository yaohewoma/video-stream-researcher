using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VideoStreamFetcher.Downloads;
using VideoStreamFetcher.Parsers;
using video_stream_researcher.Interfaces;

namespace video_stream_researcher.Services;

public sealed class DownloadManagerV2 : IDownloadManager
{
    private readonly IDownloadIndexService _downloadIndex;
    private readonly VideoDownloader _downloader = new VideoDownloader();

    public DownloadManagerV2(IDownloadIndexService downloadIndex)
    {
        _downloadIndex = downloadIndex;
    }

    public Task<long> DownloadVideo(
        object videoInfo,
        string savePath,
        Action<double> progressCallback,
        Action<string> statusCallback,
        Action<long> speedCallback,
        bool audioOnly = false,
        bool videoOnly = false,
        bool noMerge = false,
        bool isFFmpegEnabled = false,
        int mergeMode = 1,
        string? sourceUrl = null,
        string? downloadVariant = null,
        bool keepOriginalFiles = true)
    {
        if (videoInfo is not VideoInfo typedVideoInfo)
        {
            throw new ArgumentException("视频信息类型错误", nameof(videoInfo));
        }

        var plan = !string.IsNullOrWhiteSpace(sourceUrl)
            ? _downloadIndex.PrepareForDownload(sourceUrl, savePath, downloadVariant)
            : new DownloadIndexPlan();

        var previewSegments = TryParsePreviewSegments(downloadVariant);
        var isPreview = IsPreviewVariant(downloadVariant);

        var options = new VideoDownloadOptions
        {
            AudioOnly = audioOnly,
            VideoOnly = videoOnly,
            NoMerge = noMerge,
            ConvertToNonFragmentedMp4 = true,
            CreateSubfolderInTargetDirectory = true,
            OutputFolderName = plan.OutputFolderName,
            OutputFileBaseName = plan.OutputFileBaseName,
            HlsMaxSegments = previewSegments,
            HlsPreviewEdit = isPreview,
            HlsRemuxToMp4 = true,
            HlsKeepTsFile = keepOriginalFiles || isPreview
        };

        return DownloadAndReturnBytesAsync(typedVideoInfo, savePath, sourceUrl, downloadVariant, progressCallback, statusCallback, speedCallback, options);
    }

    private async Task<long> DownloadAndReturnBytesAsync(
        VideoInfo videoInfo,
        string savePath,
        string? sourceUrl,
        string? downloadVariant,
        Action<double> progressCallback,
        Action<string> statusCallback,
        Action<long> speedCallback,
        VideoDownloadOptions options)
    {
        var result = await _downloader.DownloadAsync(
            videoInfo,
            savePath,
            progressCallback,
            statusCallback,
            speedCallback,
            options);

        if (!string.IsNullOrWhiteSpace(sourceUrl) && result.Success && !string.IsNullOrWhiteSpace(result.OutputDirectory) && !string.IsNullOrWhiteSpace(result.OutputPath))
        {
            _downloadIndex.RecordCompleted(sourceUrl, savePath, result.OutputDirectory, result.OutputPath, result.BytesWritten, videoInfo.Title, downloadVariant);
        }

        return result.BytesWritten;
    }

    public bool CheckFileExists(
        object videoInfo,
        string savePath,
        Action<string> statusCallback,
        bool audioOnly = false,
        bool videoOnly = false,
        bool noMerge = false,
        string? sourceUrl = null,
        string? downloadVariant = null)
    {
        if (videoInfo is not VideoInfo typedVideoInfo)
        {
            statusCallback?.Invoke("视频信息类型错误");
            return false;
        }

        if (string.IsNullOrWhiteSpace(savePath) || !Directory.Exists(savePath))
        {
            return false;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(sourceUrl))
            {
                var hit = _downloadIndex.TryFindExisting(sourceUrl, savePath, downloadVariant);
                return hit != null;
            }

            var safeTitle = DownloadPathHelperForApp.SanitizeFileName(typedVideoInfo.Title, "video");
            var extension = audioOnly ? ".mp3" : GetVideoExtension(typedVideoInfo);
            var fileName = $"{safeTitle}{extension}";

            var existing = Directory.EnumerateDirectories(savePath)
                .Select(dir => Path.Combine(dir, fileName))
                .Any(File.Exists);

            return existing;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"检查文件失败: {ex.Message}");
            return false;
        }
    }

    private static int? TryParsePreviewSegments(string? downloadVariant)
    {
        if (string.IsNullOrWhiteSpace(downloadVariant))
        {
            return null;
        }

        var prefix = "preview_segments_";
        if (!downloadVariant.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var tail = downloadVariant.Substring(prefix.Length);
        var digits = new string(tail.TakeWhile(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var n) && n > 0)
        {
            return n;
        }

        return null;
    }

    private static bool IsPreviewVariant(string? downloadVariant)
    {
        if (string.IsNullOrWhiteSpace(downloadVariant))
        {
            return false;
        }

        return downloadVariant.StartsWith("preview_", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetVideoExtension(VideoInfo videoInfo)
    {
        var stream = videoInfo.VideoStream ?? videoInfo.CombinedStreams?.FirstOrDefault();
        if (stream == null)
        {
            return ".mp4";
        }

        var url = stream.Url ?? string.Empty;
        if (stream.Format.Equals("m3u8", StringComparison.OrdinalIgnoreCase) ||
            url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return ".mp4";
        }

        if (stream.Format.Equals("flv", StringComparison.OrdinalIgnoreCase) ||
            url.Contains(".flv", StringComparison.OrdinalIgnoreCase))
        {
            return ".flv";
        }

        return ".mp4";
    }

    public void CancelDownload()
    {
        _downloader.Cancel();
    }

    public void Dispose()
    {
        _downloader.Dispose();
    }

    private static class DownloadPathHelperForApp
    {
        public static string SanitizeFileName(string name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            var sanitized = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
        }
    }
}
