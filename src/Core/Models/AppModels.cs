using System;
using System.Collections.Generic;

namespace video_stream_researcher.Models;

/// <summary>
/// 应用程序配置模型
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 默认保存路径
    /// </summary>
    public string DefaultSavePath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    /// <summary>
    /// 是否启用FFmpeg
    /// </summary>
    public bool IsFFmpegEnabled { get; set; } = false;

    /// <summary>
    /// 合并模式 (1: 非分片, 2: 分片, 3: 两者)
    /// </summary>
    public int MergeMode { get; set; } = 1;

    /// <summary>
    /// 是否使用深色主题
    /// </summary>
    public bool IsDarkTheme { get; set; } = true;

    /// <summary>
    /// 最大日志条目数量
    /// </summary>
    public int MaxLogItems { get; set; } = 100;

    /// <summary>
    /// 网络超时时间 (毫秒)
    /// </summary>
    public int NetworkTimeout { get; set; } = 30000;

    /// <summary>
    /// 下载重试次数
    /// </summary>
    public int DownloadRetryCount { get; set; } = 3;

    /// <summary>
    /// 下载缓冲区大小 (字节)
    /// </summary>
    public int DownloadBufferSize { get; set; } = 8192;
}



/// <summary>
/// 处理状态模型
/// </summary>
public class ProcessingStatus
{
    /// <summary>
    /// 当前状态
    /// </summary>
    public string Status { get; set; } = "就绪";

    /// <summary>
    /// 处理进度 (0-100)
    /// </summary>
    public double Progress { get; set; } = 0;

    /// <summary>
    /// 当前速度 (字节/秒)
    /// </summary>
    public long Speed { get; set; } = 0;

    /// <summary>
    /// 是否正在处理
    /// </summary>
    public bool IsProcessing { get; set; } = false;

    /// <summary>
    /// 是否处理完成
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// 是否处理失败
    /// </summary>
    public bool IsFailed { get; set; } = false;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// 日志条目模型
/// </summary>
public class LogEntry
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 日志级别
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.Info;

    /// <summary>
    /// 是否为可折叠条目
    /// </summary>
    public bool IsCollapsible { get; set; } = false;

    /// <summary>
    /// 是否为根条目
    /// </summary>
    public bool IsRootItem { get; set; } = true;

    /// <summary>
    /// 子日志条目
    /// </summary>
    public List<LogEntry> Children { get; set; } = new List<LogEntry>();
}

/// <summary>
/// 日志级别枚举
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 信息
    /// </summary>
    Info,

    /// <summary>
    /// 成功
    /// </summary>
    Success,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 调试
    /// </summary>
    Debug
}
