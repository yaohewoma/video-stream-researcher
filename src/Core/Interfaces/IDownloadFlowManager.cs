using System;
using System.Threading.Tasks;
using video_stream_researcher.Models;

namespace video_stream_researcher.Interfaces;

/// <summary>
/// 下载流程管理器接口
/// 负责协调视频下载的完整业务流程，包括解析、检查、下载和后处理
/// </summary>
public interface IDownloadFlowManager
{
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
    Task<DownloadFlowResult> ExecuteDownloadAsync(
        string url, 
        string savePath, 
        DownloadOptions options,
        IProgress<double> progressReporter,
        IProgress<long> speedReporter,
        IProgress<string> statusReporter,
        Action<string, bool, bool> logReporter,
        bool isConfirmed = false);

    /// <summary>
    /// 取消下载
    /// </summary>
    void CancelDownload();
}

/// <summary>
/// 下载流程结果
/// </summary>
public class DownloadFlowResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 是否需要用户确认（例如文件已存在）
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// 视频信息（如果解析成功）
    /// </summary>
    public object? VideoInfo { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 实际文件大小
    /// </summary>
    public long ActualFileSize { get; set; }

    public string? OutputDirectory { get; set; }

    public string? OutputPath { get; set; }
}
