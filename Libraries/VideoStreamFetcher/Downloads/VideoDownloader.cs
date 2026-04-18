using System.Net.Http;
using Mp4Merger.Core.Services;
using VideoStreamFetcher.Localization;
using VideoStreamFetcher.Parsers;

namespace VideoStreamFetcher.Downloads;

/// <summary>
/// 视频下载器，负责处理视频下载的完整流程
/// </summary>
public sealed class VideoDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private readonly HlsDownloader _hlsDownloader;
    private readonly StreamDownloader _streamDownloader;
    private readonly RemuxService _remuxService;

    public VideoDownloader()
    {
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseCookies = true,
            AllowAutoRedirect = true
        })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        _hlsDownloader = new HlsDownloader();
        _streamDownloader = new StreamDownloader(_httpClient, _hlsDownloader);
        _remuxService = new RemuxService();
    }

    /// <summary>
    /// 取消下载
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// 设置Cookie字符串
    /// </summary>
    public void SetCookieString(string cookieString)
    {
        _streamDownloader.SetCookieString(cookieString);
    }

    /// <summary>
    /// 下载视频
    /// </summary>
    public async Task<VideoDownloadResult> DownloadAsync(
        VideoInfo videoInfo,
        string targetDirectory,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        VideoDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new VideoDownloadOptions();

        ValidateInputs(videoInfo, targetDirectory);

        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _cts.Token;

        var outputDirectory = options.CreateSubfolderInTargetDirectory
            ? DownloadPathHelper.CreateOutputDirectory(targetDirectory, videoInfo.Title, options.OutputFolderName)
            : targetDirectory;

        Directory.CreateDirectory(outputDirectory);
        statusCallback?.Invoke($"📁 下载目录: {outputDirectory}");

        var safeTitle = DownloadPathHelper.SanitizeFileName(options.OutputFileBaseName ?? videoInfo.Title, "video");
        var finalVideoPath = Path.Combine(outputDirectory, $"{safeTitle}.mp4");

        // 验证输出路径安全
        PathSecurityValidator.ValidatePathOrThrow(finalVideoPath, outputDirectory);

        try
        {
            if (options.AudioOnly)
            {
                return await DownloadAudioOnlyAsync(videoInfo, outputDirectory, safeTitle, progressCallback, statusCallback, speedCallback, options, ct);
            }

            if (options.VideoOnly)
            {
                return await DownloadVideoOnlyAsync(videoInfo, outputDirectory, safeTitle, progressCallback, statusCallback, speedCallback, options, ct);
            }

            return await DownloadVideoWithAudioAsync(videoInfo, outputDirectory, safeTitle, finalVideoPath, progressCallback, statusCallback, speedCallback, options, ct);
        }
        catch (OperationCanceledException)
        {
            statusCallback?.Invoke("下载已取消");
            throw;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"下载失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 验证输入参数
    /// </summary>
    private static void ValidateInputs(VideoInfo videoInfo, string targetDirectory)
    {
        if (videoInfo == null)
        {
            throw new ArgumentNullException(nameof(videoInfo));
        }

        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new ArgumentNullException(nameof(targetDirectory));
        }
    }

    /// <summary>
    /// 仅下载音频
    /// </summary>
    private async Task<VideoDownloadResult> DownloadAudioOnlyAsync(
        VideoInfo videoInfo,
        string outputDirectory,
        string safeTitle,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        VideoDownloadOptions options,
        CancellationToken ct)
    {
        if (videoInfo.AudioStream != null)
        {
            var audioPath = Path.Combine(outputDirectory, $"{safeTitle}.mp3");
            PathSecurityValidator.ValidatePathOrThrow(audioPath, outputDirectory);

            var bytes = await _streamDownloader.DownloadStreamAsync(videoInfo.AudioStream, audioPath, progressCallback, statusCallback, speedCallback, options, "音频流", ct);
            progressCallback?.Invoke(100);
            return new VideoDownloadResult
            {
                Success = true,
                OutputDirectory = outputDirectory,
                AudioPath = audioPath,
                OutputPath = audioPath,
                BytesWritten = bytes
            };
        }

        if (videoInfo.CombinedStreams?.Count > 0)
        {
            var stream = videoInfo.CombinedStreams[0];
            var outputPath = GetOutputVideoPath(stream, outputDirectory, safeTitle, options);
            var actualOutputPath = outputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(outputPath, ".ts")
                : outputPath;

            PathSecurityValidator.ValidatePathOrThrow(actualOutputPath, outputDirectory);

            var bytes = await _streamDownloader.DownloadStreamAsync(stream, actualOutputPath, progressCallback, statusCallback, speedCallback, options, "视频流", ct);
            var (finalPath, finalBytes) = await _remuxService.RemuxTsIfNeededAsync(actualOutputPath, bytes, options.HlsRemuxToMp4, options.HlsKeepTsFile, statusCallback, ct);
            progressCallback?.Invoke(100);
            return new VideoDownloadResult
            {
                Success = true,
                OutputDirectory = outputDirectory,
                VideoPath = finalPath,
                OutputPath = finalPath,
                BytesWritten = finalBytes,
                Message = "未找到单独音频流，已保存合并流"
            };
        }

        throw new InvalidOperationException("未找到可用的音频流信息");
    }

    /// <summary>
    /// 仅下载视频
    /// </summary>
    private async Task<VideoDownloadResult> DownloadVideoOnlyAsync(
        VideoInfo videoInfo,
        string outputDirectory,
        string safeTitle,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        VideoDownloadOptions options,
        CancellationToken ct)
    {
        if (videoInfo.VideoStream != null)
        {
            var outputPath = GetOutputVideoPath(videoInfo.VideoStream, outputDirectory, safeTitle, options);
            var actualOutputPath = outputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(outputPath, ".ts")
                : outputPath;

            PathSecurityValidator.ValidatePathOrThrow(actualOutputPath, outputDirectory);

            var bytes = await _streamDownloader.DownloadStreamAsync(videoInfo.VideoStream, actualOutputPath, progressCallback, statusCallback, speedCallback, options, "视频流", ct);
            var (finalPath, finalBytes) = await _remuxService.RemuxTsIfNeededAsync(actualOutputPath, bytes, options.HlsRemuxToMp4, options.HlsKeepTsFile, statusCallback, ct);
            progressCallback?.Invoke(100);
            return new VideoDownloadResult
            {
                Success = true,
                OutputDirectory = outputDirectory,
                VideoPath = finalPath,
                OutputPath = finalPath,
                BytesWritten = finalBytes
            };
        }

        if (videoInfo.CombinedStreams?.Count > 0)
        {
            var stream = videoInfo.CombinedStreams[0];
            var outputPath = GetOutputVideoPath(stream, outputDirectory, safeTitle, options);
            var actualOutputPath = outputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(outputPath, ".ts")
                : outputPath;

            PathSecurityValidator.ValidatePathOrThrow(actualOutputPath, outputDirectory);

            var bytes = await _streamDownloader.DownloadStreamAsync(stream, actualOutputPath, progressCallback, statusCallback, speedCallback, options, "视频流", ct);
            var (finalPath, finalBytes) = await _remuxService.RemuxTsIfNeededAsync(actualOutputPath, bytes, options.HlsRemuxToMp4, options.HlsKeepTsFile, statusCallback, ct);
            progressCallback?.Invoke(100);
            return new VideoDownloadResult
            {
                Success = true,
                OutputDirectory = outputDirectory,
                VideoPath = finalPath,
                OutputPath = finalPath,
                BytesWritten = finalBytes
            };
        }

        throw new InvalidOperationException("未找到有效的视频流信息");
    }

    /// <summary>
    /// 下载视频和音频并合并
    /// </summary>
    private async Task<VideoDownloadResult> DownloadVideoWithAudioAsync(
        VideoInfo videoInfo,
        string outputDirectory,
        string safeTitle,
        string finalVideoPath,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        VideoDownloadOptions options,
        CancellationToken ct)
    {
        if (videoInfo.VideoStream != null && videoInfo.AudioStream != null)
        {
            var tempVideoPath = Path.Combine(outputDirectory, $"{safeTitle}_video_temp.mp4");
            var tempAudioPath = Path.Combine(outputDirectory, $"{safeTitle}_audio_temp.mp3");

            PathSecurityValidator.ValidatePathOrThrow(tempVideoPath, outputDirectory);
            PathSecurityValidator.ValidatePathOrThrow(tempAudioPath, outputDirectory);

            await _streamDownloader.DownloadStreamAsync(videoInfo.VideoStream, tempVideoPath, progressCallback, statusCallback, speedCallback, options, "视频流", ct);
            await _streamDownloader.DownloadStreamAsync(videoInfo.AudioStream, tempAudioPath, progressCallback, statusCallback, speedCallback, options, "音频流", ct);

            if (options.NoMerge)
            {
                progressCallback?.Invoke(100);
                return new VideoDownloadResult
                {
                    Success = true,
                    OutputDirectory = outputDirectory,
                    VideoPath = tempVideoPath,
                    AudioPath = tempAudioPath,
                    BytesWritten = new FileInfo(tempVideoPath).Length,
                    OutputPath = tempVideoPath,
                    Message = "不合并模式"
                };
            }

            statusCallback?.Invoke(FetcherLocalization.GetString("Download.Merging"));
            var mergeService = new Mp4MergeService();
            var mergeResult = await mergeService.MergeAsync(
                tempVideoPath,
                tempAudioPath,
                finalVideoPath,
                statusCallback,
                options.ConvertToNonFragmentedMp4);

            if (!mergeResult.Success)
            {
                throw new InvalidOperationException($"音视频合并失败: {mergeResult.Message}");
            }

            CleanupTempFiles(tempVideoPath, tempAudioPath, statusCallback);

            progressCallback?.Invoke(100);
            return new VideoDownloadResult
            {
                Success = true,
                OutputDirectory = outputDirectory,
                OutputPath = finalVideoPath,
                BytesWritten = new FileInfo(finalVideoPath).Length
            };
        }

        if (videoInfo.CombinedStreams?.Count > 0)
        {
            var stream = videoInfo.CombinedStreams[0];
            var outputPath = GetOutputVideoPath(stream, outputDirectory, safeTitle, options);
            var actualOutputPath = outputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(outputPath, ".ts")
                : outputPath;

            PathSecurityValidator.ValidatePathOrThrow(actualOutputPath, outputDirectory);

            var bytes = await _streamDownloader.DownloadStreamAsync(stream, actualOutputPath, progressCallback, statusCallback, speedCallback, options, "视频流", ct);
            var (finalPath, finalBytes) = await _remuxService.RemuxTsIfNeededAsync(actualOutputPath, bytes, options.HlsRemuxToMp4, options.HlsKeepTsFile, statusCallback, ct);
            progressCallback?.Invoke(100);
            return new VideoDownloadResult
            {
                Success = true,
                OutputDirectory = outputDirectory,
                OutputPath = finalPath,
                BytesWritten = finalBytes
            };
        }

        throw new InvalidOperationException("未找到有效的视频流信息");
    }

    /// <summary>
    /// 获取输出视频路径
    /// </summary>
    private static string GetOutputVideoPath(VideoStreamInfo stream, string outputDirectory, string safeTitle, VideoDownloadOptions options)
    {
        var url = stream.Url ?? string.Empty;
        if (stream.Format.Equals("m3u8", StringComparison.OrdinalIgnoreCase) ||
            url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            if (options.HlsRemuxToMp4)
            {
                return Path.Combine(outputDirectory, $"{safeTitle}.mp4");
            }
            return Path.Combine(outputDirectory, $"{safeTitle}.ts");
        }

        if (stream.Format.Equals("flv", StringComparison.OrdinalIgnoreCase) ||
            url.Contains(".flv", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(outputDirectory, $"{safeTitle}.flv");
        }

        return Path.Combine(outputDirectory, $"{safeTitle}.mp4");
    }

    /// <summary>
    /// 清理临时文件
    /// </summary>
    private static void CleanupTempFiles(string tempVideoPath, string tempAudioPath, Action<string>? statusCallback)
    {
        try
        {
            if (File.Exists(tempVideoPath))
            {
                File.Delete(tempVideoPath);
                statusCallback?.Invoke($"[清理] 已删除临时视频文件: {tempVideoPath}");
            }

            if (File.Exists(tempAudioPath))
            {
                File.Delete(tempAudioPath);
                statusCallback?.Invoke($"[清理] 已删除临时音频文件: {tempAudioPath}");
            }
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"[清理] 删除临时文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public bool CheckFileExists(VideoInfo videoInfo, string targetDirectory, VideoDownloadOptions? options = null)
    {
        options ??= new VideoDownloadOptions();
        if (videoInfo == null || string.IsNullOrWhiteSpace(targetDirectory))
        {
            return false;
        }

        var safeTitle = DownloadPathHelper.SanitizeFileName(options.OutputFileBaseName ?? videoInfo.Title, "video");

        if (options.CreateSubfolderInTargetDirectory)
        {
            return false;
        }

        if (options.AudioOnly)
        {
            return File.Exists(Path.Combine(targetDirectory, $"{safeTitle}.mp3"));
        }

        var stream = videoInfo.VideoStream ?? videoInfo.CombinedStreams?.FirstOrDefault();
        var ext = "mp4";
        if (stream != null)
        {
            var url = stream.Url ?? string.Empty;
            if (stream.Format.Equals("m3u8", StringComparison.OrdinalIgnoreCase) ||
                url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                ext = options.HlsRemuxToMp4 ? "mp4" : "ts";
            }
            else if (stream.Format.Equals("flv", StringComparison.OrdinalIgnoreCase) ||
                     url.Contains(".flv", StringComparison.OrdinalIgnoreCase))
            {
                ext = "flv";
            }
        }

        return File.Exists(Path.Combine(targetDirectory, $"{safeTitle}.{ext}"));
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _streamDownloader.Dispose();
        _httpClient.Dispose();
    }
}
