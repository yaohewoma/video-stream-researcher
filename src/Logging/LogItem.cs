using Avalonia.Media;
using System;

namespace video_stream_researcher.Logs;

/// <summary>
/// 日志类型枚举
/// </summary>
public enum LogType
{
    /// <summary>
    /// 信息日志
    /// </summary>
    Info,
    /// <summary>
    /// 成功日志
    /// </summary>
    Success,
    /// <summary>
    /// 警告日志
    /// </summary>
    Warning,
    /// <summary>
    /// 错误日志
    /// </summary>
    Error
}

/// <summary>
/// 日志项类，包含日志内容、类型和时间戳
/// </summary>
public class LogItem
{
    /// <summary>
    /// 日志内容
    /// </summary>
    public string Content { get; set; }
    
    /// <summary>
    /// 日志类型
    /// </summary>
    public LogType Type { get; set; }
    
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// 格式化的时间字符串
    /// </summary>
    public string FormattedTime => Timestamp.ToString("HH:mm:ss");
    
    /// <summary>
    /// 日志项的字体颜色，根据日志类型自动设置
    /// </summary>
    public SolidColorBrush Foreground
    {
        get
        {
            switch (Type)
            {
                case LogType.Success:
                    return new SolidColorBrush(Colors.LightGreen);
                case LogType.Warning:
                    return new SolidColorBrush(Colors.Yellow);
                case LogType.Error:
                    return new SolidColorBrush(Colors.Red);
                default:
                    return new SolidColorBrush(Colors.LightBlue);
            }
        }
    }
    
    /// <summary>
    /// 初始化日志项
    /// </summary>
    /// <param name="content">日志内容</param>
    /// <param name="type">日志类型</param>
    /// <param name="timestamp">时间戳，默认使用当前时间</param>
    public LogItem(string content, LogType type, DateTime? timestamp = null)
    {
        Content = content;
        Type = type;
        Timestamp = timestamp ?? DateTime.Now;
    }
}