using System;
using System.IO;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using video_stream_researcher.Interfaces;
using video_stream_researcher.Models;
using video_stream_researcher.Services;
using VideoStreamFetcher.Parsers;

namespace video_stream_researcher.Services;

/// <summary>
/// 下载流程管理器实现
/// 负责协调视频下载的完整业务流程，包括解析、检查、下载和后处理
/// </summary>
public class DownloadFlowManager : IDownloadFlowManager
{
    private static readonly ConcurrentDictionary<string, byte> InProgress = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
    private readonly IDownloadManager _downloadManager;
    private readonly IVideoParser _videoParser;
    private readonly IDownloadIndexService _downloadIndex;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="downloadManager">下载管理器</param>
    /// <param name="videoParser">视频解析器</param>
    /// <param name="downloadIndex">下载索引服务</param>
    public DownloadFlowManager(IDownloadManager downloadManager, IVideoParser videoParser, IDownloadIndexService downloadIndex)
    {
        _downloadManager = downloadManager;
        _videoParser = videoParser;
        _downloadIndex = downloadIndex;
    }

    /// <summary>
    /// 执行下载流程
    /// </summary>
    /// <param name="url">视频URL</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="options">下载选项</param>
    /// <param name="progressReporter">进度报告器 (0-100)</param>
    /// <param name="speedReporter">速度报告器 (bytes/s)</param>
    /// <param name="statusReporter">状态报告器 (文本)</param>
    /// <param name="logReporter">日志报告器 (文本)</param>
    /// <param name="isConfirmed">是否已确认下载（用于跳过文件存在检查）</param>
    /// <returns>下载结果（是否成功，视频信息，是否需要确认）</returns>
    public async Task<DownloadFlowResult> ExecuteDownloadAsync(
        string url, 
        string savePath, 
        DownloadOptions options,
        IProgress<double> progressReporter,
        IProgress<long> speedReporter,
        IProgress<string> statusReporter,
        Action<string, bool, bool> logReporter,
        bool isConfirmed = false)
    {
        var result = new DownloadFlowResult();
        string? inProgressKey = null;

        try
        {
            // 构建variant标识
            var variant = BuildVariant(options, isConfirmed);
            inProgressKey = BuildInProgressKey(url, variant);

            // 检查是否已有相同任务在进行中
            if (!InProgress.TryAdd(inProgressKey, 0))
            {
                return CreateDuplicateResult(result, statusReporter, logReporter);
            }

            // 检查是否已存在相同的下载
            var existingResult = await CheckExistingDownloadAsync(url, savePath, variant, isConfirmed, statusReporter, logReporter);
            if (existingResult != null)
            {
                return existingResult;
            }

            // 执行下载流程
            return await ExecuteDownloadFlowAsync(url, savePath, options, variant, progressReporter, speedReporter, statusReporter, logReporter, result);
        }
        catch (OperationCanceledException)
        {
            return HandleCancellation(result, logReporter, statusReporter);
        }
        catch (Exception ex)
        {
            return HandleError(result, ex, logReporter, statusReporter);
        }
        finally
        {
            CleanupInProgress(inProgressKey);
        }
    }

    /// <summary>
    /// 构建variant标识
    /// </summary>
    private string? BuildVariant(DownloadOptions options, bool isConfirmed)
    {
        var previewSegments = options.PreviewEnabled && options.PreviewSegments > 0 ? options.PreviewSegments : 0;
        var variant = options.PreviewEnabled
            ? (previewSegments > 0 ? $"preview_segments_{previewSegments}" : "preview_full")
            : null;

        if (isConfirmed)
        {
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            variant = string.IsNullOrWhiteSpace(variant) ? $"redo_{stamp}" : $"{variant}_redo_{stamp}";
        }

        return variant;
    }

    /// <summary>
    /// 构建进行中任务的键
    /// </summary>
    private string BuildInProgressKey(string url, string? variant)
    {
        return string.IsNullOrWhiteSpace(variant) ? url : $"{url}#{variant}";
    }

    /// <summary>
    /// 创建重复任务的结果
    /// </summary>
    private DownloadFlowResult CreateDuplicateResult(DownloadFlowResult result, IProgress<string> statusReporter, Action<string, bool, bool> logReporter)
    {
        result.Success = true;
        statusReporter.Report(LocalizationService.Instance["StatusDuplicate"]);
        logReporter(LocalizationService.Instance["LogDownloadDuplicate"], true, true);
        return result;
    }

    /// <summary>
    /// 检查是否已存在相同的下载
    /// </summary>
    private async Task<DownloadFlowResult?> CheckExistingDownloadAsync(
        string url, 
        string savePath, 
        string? variant, 
        bool isConfirmed,
        IProgress<string> statusReporter, 
        Action<string, bool, bool> logReporter)
    {
        var existing = _downloadIndex.TryFindExisting(url, savePath, variant);
        if (existing != null && !isConfirmed)
        {
            var result = new DownloadFlowResult
            {
                Success = true,
                RequiresConfirmation = true,
                ActualFileSize = existing.Size,
                OutputDirectory = existing.OutputDirectory,
                OutputPath = existing.OutputPath
            };

            statusReporter.Report(LocalizationService.Instance["StatusExisting"]);
            logReporter(LocalizationService.Instance["LogDownloadExisting"], true, true);
            logReporter(LocalizationService.Instance.GetString("LogDownloadOutputDir", existing.OutputDirectory), false, false);
            logReporter(LocalizationService.Instance.GetString("LogDownloadOutputFile", existing.OutputPath), false, false);

            return result;
        }

        return null;
    }

    /// <summary>
    /// 执行下载流程的核心逻辑
    /// </summary>
    private async Task<DownloadFlowResult> ExecuteDownloadFlowAsync(
        string url, 
        string savePath, 
        DownloadOptions options,
        string? variant,
        IProgress<double> progressReporter,
        IProgress<long> speedReporter,
        IProgress<string> statusReporter,
        Action<string, bool, bool> logReporter,
        DownloadFlowResult result)
    {
        // 1. 记录下载请求信息
        LogDownloadRequest(url, savePath, options, logReporter, statusReporter);

        // 2. 解析视频信息
        var videoInfo = await ParseVideoInfoAsync(url, logReporter, statusReporter);
        if (videoInfo == null)
        {
            result.Success = false;
            result.ErrorMessage = LocalizationService.Instance["LogDownloadParseFailed"];
            return result;
        }

        result.VideoInfo = videoInfo;

        // 3. 检查文件是否存在
        if (await CheckFileExistsAsync(videoInfo, savePath, options, url, variant, logReporter, statusReporter, result))
        {
            return result;
        }

        // 4. 执行下载
        var actualSize = await DownloadVideoAsync(videoInfo, savePath, options, url, variant, progressReporter, speedReporter, statusReporter, logReporter);

        // 5. 设置结果
        result.Success = true;
        result.ActualFileSize = actualSize;

        // 6. 解析输出路径
        ResolveOutputPath(result, url, savePath, variant, logReporter);

        // 7. 记录完成信息
        LogCompletion(actualSize, logReporter, statusReporter);

        return result;
    }

    /// <summary>
    /// 记录下载请求信息
    /// </summary>
    private void LogDownloadRequest(string url, string savePath, DownloadOptions options, Action<string, bool, bool> logReporter, IProgress<string> statusReporter)
    {
        statusReporter.Report(LocalizationService.Instance["StatusParsingVideo"]);
        logReporter(LocalizationService.Instance["LogDownloadStartRequest"], true, true);
        logReporter(LocalizationService.Instance.GetString("LogDownloadUrl", url), false, false);
        logReporter(LocalizationService.Instance.GetString("LogDownloadSavePath", savePath), false, false);
        logReporter(LocalizationService.Instance.GetString("LogDownloadAudioOnly", options.AudioOnly.ToString()), false, false);
        logReporter(LocalizationService.Instance.GetString("LogDownloadVideoOnly", options.VideoOnly.ToString()), false, false);
        logReporter(LocalizationService.Instance.GetString("LogDownloadNoMerge", options.NoMerge.ToString()), false, false);
    }

    /// <summary>
    /// 解析视频信息
    /// </summary>
    private async Task<VideoInfo?> ParseVideoInfoAsync(string url, Action<string, bool, bool> logReporter, IProgress<string> statusReporter)
    {
        logReporter(LocalizationService.Instance["LogDownloadParseStart"], false, false);
        statusReporter.Report(LocalizationService.Instance["StatusParsingVideo"]);

        Action<string> parseStatusCallback = message =>
        {
            if (message.Contains('|'))
            {
                var parts = message.Split('|');
                if (parts.Length == 2 && int.TryParse(parts[1], out int progress))
                {
                    logReporter(parts[0], false, false);
                }
                else
                {
                    logReporter(message, false, false);
                }
            }
            else
            {
                logReporter(message, false, false);
            }
        };

        var parsedInfo = await _videoParser.ParseVideoInfo(url, parseStatusCallback);
        var videoInfo = parsedInfo as VideoInfo;

        if (videoInfo != null)
        {
            logReporter(LocalizationService.Instance.GetString("LogDownloadParseSuccess", videoInfo.Title), false, false);
        }

        return videoInfo;
    }

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    private async Task<bool> CheckFileExistsAsync(
        VideoInfo videoInfo, 
        string savePath, 
        DownloadOptions options,
        string url,
        string? variant,
        Action<string, bool, bool> logReporter,
        IProgress<string> statusReporter,
        DownloadFlowResult result)
    {
        logReporter(LocalizationService.Instance["LogDownloadCheckFile"], false, false);

        bool fileExists = _downloadManager.CheckFileExists(
            videoInfo, 
            savePath, 
            message => logReporter(message, false, false), 
            options.AudioOnly, 
            options.VideoOnly, 
            options.NoMerge,
            url,
            variant);

        if (fileExists)
        {
            result.Success = true;
            result.RequiresConfirmation = false;

            var existingHit = _downloadIndex.TryFindExisting(url, savePath, variant);
            if (existingHit != null)
            {
                result.OutputDirectory = existingHit.OutputDirectory;
                result.OutputPath = existingHit.OutputPath;
            }

            statusReporter.Report(LocalizationService.Instance["StatusFileExists"]);
            logReporter(LocalizationService.Instance["LogDownloadFileExists"], false, false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 下载视频
    /// </summary>
    private async Task<long> DownloadVideoAsync(
        VideoInfo videoInfo, 
        string savePath, 
        DownloadOptions options,
        string url,
        string? variant,
        IProgress<double> progressReporter,
        IProgress<long> speedReporter,
        IProgress<string> statusReporter,
        Action<string, bool, bool> logReporter)
    {
        statusReporter.Report(LocalizationService.Instance["StatusDownloading"]);
        logReporter(LocalizationService.Instance.GetString("LogDownloadStart", videoInfo.Title), true, true);

        Action<double> progressCallback = p => progressReporter.Report(p);
        Action<long> speedCallback = s => speedReporter.Report(s);

        Action<string> statusCallback = message =>
        {
            logReporter(message, false, false);
            UpdateStatusFromMessage(message, statusReporter);
        };

        return await _downloadManager.DownloadVideo(
            videoInfo,
            savePath,
            progressCallback,
            statusCallback,
            speedCallback,
            options.AudioOnly,
            options.VideoOnly,
            options.NoMerge,
            options.IsFFmpegEnabled,
            options.MergeMode,
            url,
            variant,
            options.KeepOriginalFiles);
    }

    /// <summary>
    /// 根据消息更新状态
    /// </summary>
    private void UpdateStatusFromMessage(string message, IProgress<string> statusReporter)
    {
        if (message.Contains("开始下载视频流") || message.Contains("开始接收视频流数据"))
        {
            statusReporter.Report(LocalizationService.Instance["StatusVideoStream"]);
        }
        else if (message.Contains("开始下载音频流") || message.Contains("开始接收音频流数据"))
        {
            statusReporter.Report(LocalizationService.Instance["StatusAudioStream"]);
        }
        else if (message.Contains("开始合并音视频流"))
        {
            statusReporter.Report(LocalizationService.Instance["StatusMerging"]);
        }
        else if (message.Contains("下载完成") || message.Contains("合并完成"))
        {
            statusReporter.Report(LocalizationService.Instance["StatusCompleted"]);
        }
    }

    /// <summary>
    /// 解析输出路径
    /// </summary>
    private void ResolveOutputPath(DownloadFlowResult result, string url, string savePath, string? variant, Action<string, bool, bool> logReporter)
    {
        var completed = _downloadIndex.TryFindExisting(url, savePath, variant);
        if (completed != null)
        {
            result.OutputDirectory = completed.OutputDirectory;
            result.OutputPath = completed.OutputPath;
            logReporter(LocalizationService.Instance.GetString("LogDownloadOutputDirFinal", result.OutputDirectory), false, false);
            logReporter(LocalizationService.Instance.GetString("LogDownloadOutputFileFinal", result.OutputPath), false, false);
        }
        else
        {
            ResolveOutputPathFromPlan(result, url, savePath, variant, logReporter);
        }
    }

    /// <summary>
    /// 从下载计划解析输出路径
    /// </summary>
    private void ResolveOutputPathFromPlan(DownloadFlowResult result, string url, string savePath, string? variant, Action<string, bool, bool> logReporter)
    {
        var plan = _downloadIndex.PrepareForDownload(url, savePath, variant);
        if (string.IsNullOrWhiteSpace(plan.OutputFolderName) || string.IsNullOrWhiteSpace(plan.OutputFileBaseName))
        {
            logReporter(LocalizationService.Instance["LogDownloadWarningOutputPath"], false, false);
            return;
        }

        var folder = Path.Combine(savePath, plan.OutputFolderName);
        var baseName = plan.OutputFileBaseName;

        var outputFile = FindOutputFile(folder, baseName);
        if (outputFile != null)
        {
            result.OutputDirectory = folder;
            result.OutputPath = outputFile.Path;
            logReporter(LocalizationService.Instance.GetString("LogDownloadOutputDirFinal", folder), false, false);
            logReporter(LocalizationService.Instance.GetString("LogDownloadOutputFileFinal", outputFile.Path), false, false);
        }
        else
        {
            logReporter(LocalizationService.Instance.GetString("LogDownloadWarningOutputFile", folder), false, false);
        }
    }

    /// <summary>
    /// 输出文件信息
    /// </summary>
    private class OutputFileInfo
    {
        public string Path { get; set; } = string.Empty;
        public bool Exists { get; set; }
    }

    /// <summary>
    /// 查找输出文件
    /// </summary>
    private OutputFileInfo? FindOutputFile(string folder, string baseName)
    {
        // 定义可能的文件路径
        var candidates = new[]
        {
            new OutputFileInfo { Path = Path.Combine(folder, $"{baseName}.ts") },
            new OutputFileInfo { Path = Path.Combine(folder, $"{baseName}.mp4") },
            new OutputFileInfo { Path = Path.Combine(folder, $"{baseName}.ts.meta.json") },
            new OutputFileInfo { Path = Path.Combine(folder, $"{baseName}.mp4.meta.json") }
        };

        // 检查文件是否存在
        foreach (var candidate in candidates)
        {
            candidate.Exists = File.Exists(candidate.Path);
        }

        // 优先返回实际存在的媒体文件
        if (candidates[0].Exists) // .ts file
        {
            return new OutputFileInfo { Path = candidates[0].Path, Exists = true };
        }

        if (candidates[1].Exists) // .mp4 file
        {
            return new OutputFileInfo { Path = candidates[1].Path, Exists = true };
        }

        // 如果没有媒体文件，但有元数据文件，返回对应的路径
        if (candidates[2].Exists) // .ts.meta.json
        {
            return new OutputFileInfo { Path = candidates[0].Path, Exists = false };
        }

        if (candidates[3].Exists) // .mp4.meta.json
        {
            return new OutputFileInfo { Path = candidates[1].Path, Exists = false };
        }

        return null;
    }

    /// <summary>
    /// 记录完成信息
    /// </summary>
    private void LogCompletion(long actualSize, Action<string, bool, bool> logReporter, IProgress<string> statusReporter)
    {
        string sizeStr = FormatFileSize(actualSize);
        logReporter(LocalizationService.Instance.GetString("LogDownloadComplete", sizeStr), false, false);
        statusReporter.Report(LocalizationService.Instance["StatusCompleted"]);
    }

    /// <summary>
    /// 处理取消异常
    /// </summary>
    private DownloadFlowResult HandleCancellation(DownloadFlowResult result, Action<string, bool, bool> logReporter, IProgress<string> statusReporter)
    {
        result.Success = false;
        result.ErrorMessage = LocalizationService.Instance["LogDownloadCancelled"];
        logReporter(LocalizationService.Instance["LogDownloadCancelled"], false, false);
        statusReporter.Report(LocalizationService.Instance["StatusCancelled"]);
        return result;
    }

    /// <summary>
    /// 处理错误异常
    /// </summary>
    private DownloadFlowResult HandleError(DownloadFlowResult result, Exception ex, Action<string, bool, bool> logReporter, IProgress<string> statusReporter)
    {
        result.Success = false;
        result.ErrorMessage = ex.Message;
        logReporter(LocalizationService.Instance.GetString("LogDownloadFailed", ex.Message), false, false);
        statusReporter.Report(LocalizationService.Instance["StatusFailed"]);
        return result;
    }

    /// <summary>
    /// 清理进行中任务
    /// </summary>
    private void CleanupInProgress(string? inProgressKey)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(inProgressKey))
            {
                InProgress.TryRemove(inProgressKey, out _);
            }
        }
        catch
        {
            // 忽略清理异常
        }
    }

    /// <summary>
    /// 取消下载
    /// </summary>
    public void CancelDownload()
    {
        _downloadManager.CancelDownload();
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    /// <param name="bytes">字节数</param>
    /// <returns>格式化后的字符串</returns>
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F2} KB";
        else if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024 * 1024.0):F2} MB";
        else
            return $"{bytes / (1024 * 1024 * 1024.0):F2} GB";
    }
}
