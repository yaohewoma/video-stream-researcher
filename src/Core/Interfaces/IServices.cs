using System;
using System.Threading.Tasks;

namespace video_stream_researcher.Interfaces;

/// <summary>
/// 下载管理器接口
/// </summary>
public interface IDownloadManager : IDisposable
{
    /// <summary>
    /// 下载视频
    /// </summary>
    /// <param name="videoInfo">视频信息</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="speedCallback">速度回调</param>
    /// <param name="audioOnly">仅音频</param>
    /// <param name="videoOnly">仅视频</param>
    /// <param name="noMerge">不合并</param>
    /// <param name="isFFmpegEnabled">是否启用FFmpeg</param>
    /// <param name="mergeMode">合并模式</param>
    /// <returns>实际文件大小</returns>
    Task<long> DownloadVideo(
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
        bool keepOriginalFiles = true);

    /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="videoInfo">视频信息</param>
        /// <param name="savePath">保存路径</param>
        /// <param name="statusCallback">状态回调</param>
        /// <param name="audioOnly">仅音频</param>
        /// <param name="videoOnly">仅视频</param>
        /// <param name="noMerge">不合并</param>
        /// <returns>文件是否存在</returns>
        bool CheckFileExists(
            object videoInfo,
            string savePath,
            Action<string> statusCallback,
            bool audioOnly = false,
            bool videoOnly = false,
            bool noMerge = false,
            string? sourceUrl = null,
            string? downloadVariant = null);

    /// <summary>
    /// 取消下载
    /// </summary>
    void CancelDownload();
}

public interface IDownloadIndexService
{
    DownloadIndexHit? TryFindExisting(string url, string savePath, string? variant = null);
    DownloadIndexPlan PrepareForDownload(string url, string savePath, string? variant = null);
    void RecordCompleted(string url, string savePath, string outputDirectory, string outputPath, long bytesWritten, string? title, string? variant = null);
}

public sealed class DownloadIndexHit
{
    public required string OutputDirectory { get; init; }
    public required string OutputPath { get; init; }
    public required long Size { get; init; }
}

public sealed class DownloadIndexPlan
{
    public string? OutputFolderName { get; init; }
    public string? OutputFileBaseName { get; init; }
}

/// <summary>
/// 配置管理器接口
/// </summary>
public interface IConfigManager
{
    /// <summary>
    /// 读取配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>配置值</returns>
    T ReadConfig<T>(string key, T defaultValue = default!);

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="key">配置键</param>
    /// <param name="value">配置值</param>
    void SaveConfig<T>(string key, T value);

    /// <summary>
    /// 重置配置
    /// </summary>
    void ResetConfig();
}

/// <summary>
/// 日志管理器接口
/// </summary>
public interface ILogManager
{
    /// <summary>
    /// 更新日志
    /// </summary>
    /// <param name="message">日志消息</param>
    void UpdateLog(string message);

    /// <summary>
    /// 更新可折叠日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="isRootItem">是否为根项</param>
    /// <param name="autoCollapse">是否自动折叠</param>
    void UpdateCollapsibleLog(string message, bool isRootItem = true, bool autoCollapse = true);

    /// <summary>
    /// 重置可折叠日志状态
    /// </summary>
    void ResetCollapsibleLog();
}

/// <summary>
/// 网络速度监控器接口
/// </summary>
public interface INetworkSpeedMonitor : IDisposable
{
    /// <summary>
    /// 更新处理进度
    /// </summary>
    /// <param name="progress">进度值 (0-100)</param>
    void UpdateProcessingProgress(double progress);

    /// <summary>
    /// 更新当前状态
    /// </summary>
    /// <param name="status">状态消息</param>
    void UpdateCurrentStatus(string status);

    /// <summary>
    /// 更新速度
    /// </summary>
    /// <param name="speed">速度 (字节/秒)</param>
    /// <param name="isInitial">是否为初始状态</param>
    void OnSpeedUpdate(long speed, bool isInitial = false);

    /// <summary>
    /// 标记下载完成
    /// </summary>
    void MarkDownloadCompleted();

    /// <summary>
    /// 标记下载取消
    /// </summary>
    void MarkDownloadCanceled();

    /// <summary>
    /// 重置进度动画
    /// </summary>
    /// <returns>任务</returns>
    Task ResetProgressAnimation();
}

/// <summary>
/// 视频解析器接口
/// </summary>
public interface IVideoParser : IDisposable
{
    /// <summary>
    /// 解析视频信息
    /// </summary>
    /// <param name="url">视频URL</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>视频信息</returns>
    Task<object?> ParseVideoInfo(string url, Action<string> statusCallback);

    /// <summary>
    /// 设置Cookie字符串（用于B站登录）
    /// </summary>
    /// <param name="cookieString">Cookie字符串</param>
    void SetCookieString(string cookieString);
}
