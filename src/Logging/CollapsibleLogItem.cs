using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Input;
using System;
using Avalonia.Layout;

namespace video_stream_researcher.Logs;

/// <summary>
/// 可折叠的日志条目控件
/// </summary>
public class CollapsibleLogItem : Expander
{
    private readonly TextBlock _logTextBlock;
    private readonly StackPanel _contentPanel;
    
    /// <summary>
    /// 初始化可折叠日志条目
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="prefix">前缀</param>
    /// <param name="message">日志消息</param>
    /// <param name="foregroundColor">前景色</param>
    public CollapsibleLogItem(string timestamp, string prefix, string message, SolidColorBrush foregroundColor)
    {
        // 设置Expander属性
        Header = CreateHeader(timestamp, prefix, message, foregroundColor);
        IsExpanded = false; // 默认折叠
        BorderBrush = new SolidColorBrush(Avalonia.Media.Colors.Transparent);
        Background = new SolidColorBrush(Avalonia.Media.Colors.Transparent);
        Margin = LogStyleManager.DefaultMargin;
        Padding = new Avalonia.Thickness(0);
        CornerRadius = new Avalonia.CornerRadius(0);
        
        // 创建内容面板，用于显示完整日志
        _contentPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Avalonia.Thickness(20, 0, 0, 0),
            Background = new SolidColorBrush(Avalonia.Media.Colors.Transparent)
        };
        
        // 创建完整日志文本块
        _logTextBlock = new TextBlock
        {
            Text = message,
            FontFamily = LogStyleManager.DefaultFontFamily,
            FontSize = LogStyleManager.DefaultFontSize,
            TextWrapping = TextWrapping.Wrap,
            Padding = LogStyleManager.DefaultPadding,
            Foreground = foregroundColor
        };
        
        _contentPanel.Children.Add(_logTextBlock);
        Content = _contentPanel;
        
        // 添加双击事件处理，实现双击展开/折叠
        AddHandler(PointerPressedEvent, OnDoubleClick, RoutingStrategies.Bubble);
    }
    
    /// <summary>
    /// 创建日志条目头部
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="prefix">前缀</param>
    /// <param name="message">日志消息</param>
    /// <param name="foregroundColor">前景色</param>
    /// <returns>日志条目头部</returns>
    private TextBlock CreateHeader(string timestamp, string prefix, string message, SolidColorBrush foregroundColor)
    {
        // 截断长消息，只显示前50个字符
        string displayMessage = message.Length > 50 ? message.Substring(0, 50) + "..." : message;
        
        return new TextBlock
        {
            Text = $"[{timestamp}] {prefix}{displayMessage}",
            FontFamily = LogStyleManager.DefaultFontFamily,
            FontSize = LogStyleManager.DefaultFontSize,
            TextWrapping = TextWrapping.NoWrap,
            Padding = LogStyleManager.DefaultPadding,
            Foreground = foregroundColor
        };
    }
    
    /// <summary>
    /// 添加子日志条目
    /// </summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="prefix">前缀</param>
    /// <param name="message">日志消息</param>
    /// <param name="foregroundColor">前景色</param>
    public void AddChildLog(string timestamp, string prefix, string message, SolidColorBrush foregroundColor)
    {
        var childLog = new TextBlock
        {
            Text = $"[{timestamp}] {prefix}{message}",
            FontFamily = LogStyleManager.DefaultFontFamily,
            FontSize = LogStyleManager.ChildLogFontSize,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Avalonia.Thickness(5, 1, 2, 1),
            Foreground = foregroundColor
        };
        
        _contentPanel.Children.Add(childLog);
    }
    
    /// <summary>
    /// 双击事件处理，实现双击展开/折叠
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="e">事件参数</param>
    private void OnDoubleClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            IsExpanded = !IsExpanded;
        }
    }
}