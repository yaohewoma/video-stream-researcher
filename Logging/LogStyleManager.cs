using Avalonia.Media;

namespace video_stream_researcher.Logs;

/// <summary>
/// 日志样式管理器，用于管理日志的样式和主题
/// </summary>
public class LogStyleManager
{
    /// <summary>
    /// 成功日志的前景色
    /// </summary>
    public static readonly SolidColorBrush SuccessColor = new SolidColorBrush(Color.FromRgb(100, 255, 100));
    
    /// <summary>
    /// 失败日志的前景色
    /// </summary>
    public static readonly SolidColorBrush FailureColor = new SolidColorBrush(Color.FromRgb(255, 100, 100));
    
    /// <summary>
    /// 信息日志的前景色
    /// </summary>
    public static readonly SolidColorBrush InfoColor = new SolidColorBrush(Color.FromRgb(150, 200, 150));
    
    /// <summary>
    /// 行动日志的前景色（开始、解析、下载等）
    /// </summary>
    public static readonly SolidColorBrush ActionColor = new SolidColorBrush(Color.FromRgb(100, 150, 255));
    
    /// <summary>
    /// 统计日志的前景色（处理时间、文件大小等）
    /// </summary>
    public static readonly SolidColorBrush StatsColor = new SolidColorBrush(Color.FromRgb(255, 200, 100));
    
    /// <summary>
    /// 默认字体家族
    /// </summary>
    public static readonly FontFamily DefaultFontFamily = new FontFamily("Consolas");
    
    /// <summary>
    /// 默认字体大小
    /// </summary>
    public static readonly double DefaultFontSize = 12;
    
    /// <summary>
    /// 子日志字体大小
    /// </summary>
    public static readonly double ChildLogFontSize = 11;
    
    /// <summary>
    /// 日志条目间距
    /// </summary>
    public static readonly Avalonia.Thickness DefaultMargin = new Avalonia.Thickness(0, 1, 0, 1);
    
    /// <summary>
    /// 日志条目内边距
    /// </summary>
    public static readonly Avalonia.Thickness DefaultPadding = new Avalonia.Thickness(2, 1, 2, 1);
    
    /// <summary>
    /// 获取日志消息对应的前景色
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <returns>对应的前景色</returns>
    public static SolidColorBrush GetForegroundColor(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return InfoColor;
        }
        
        if (message.Contains("失败") || message.Contains("取消"))
        {
            return FailureColor;
        }
        else if (message.Contains("完成") || message.Contains("成功"))
        {
            return SuccessColor;
        }
        else if (message.Contains("开始") || message.Contains("解析") || message.Contains("下载") || message.Contains("正在"))
        {
            return ActionColor;
        }
        else if (message.Contains("处理时间") || message.Contains("实际文件大小") || message.Contains("媒体信息") || message.Contains("统计"))
        {
            return StatsColor;
        }
        else
        {
            return InfoColor;
        }
    }
    
    /// <summary>
    /// 获取日志消息对应的前缀
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <returns>对应的前缀</returns>
    public static string GetMessagePrefix(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return ""; 
        }
        
        if (message.Contains("失败") || message.Contains("取消"))
        {
            return "❌ ";
        }
        else if (message.Contains("完成") || message.Contains("成功"))
        {
            return "✅ ";
        }
        else if (message.Contains("开始") || message.Contains("解析") || message.Contains("下载"))
        {
            return "▶️  ";
        }
        else if (message.Contains("处理时间") || message.Contains("实际文件大小") || message.Contains("媒体信息"))
        {
            return "📊 ";
        }
        else if (message.Contains("警告") || message.Contains("注意"))
        {
            return "⚠️  ";
        }
        else if (message.Contains("信息") || message.Contains("提示"))
        {
            return "ℹ️  ";
        }
        else
        {
            return ""; 
        }
    }
    
    /// <summary>
    /// 获取日志消息的显示文本（添加前缀和截断长消息）
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="message">日志消息</param>
    /// <param name="maxLength">最大显示长度</param>
    /// <returns>格式化后的日志文本</returns>
    public static string GetFormattedLogMessage(string timestamp, string message, int maxLength = 50)
    {
        string prefix = GetMessagePrefix(message);
        string displayMessage = message.Length > maxLength ? message.Substring(0, maxLength) + "..." : message;
        
        return $"[{timestamp}] {prefix}{displayMessage}";
    }
}
