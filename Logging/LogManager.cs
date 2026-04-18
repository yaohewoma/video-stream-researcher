using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using video_stream_researcher.Interfaces;

namespace video_stream_researcher.Logs;

// 日志管理器类，用于处理日志的显示
public class LogManager(StackPanel statusContainer, UI.MainWindow? mainWindow = null) : ILogManager
{
    private readonly StackPanel _statusContainer = statusContainer;
    private readonly UI.MainWindow? _mainWindow = mainWindow;

    // 最大日志条目数量，超过则移除最旧的日志
    private const int MaxLogItems = 100;
    
    // 最后一个可折叠日志条目，用于添加子日志
    private CollapsibleLogItem? _lastCollapsibleItem;
    
    // 更新日志（普通日志）
    public void UpdateLog(string message)
    {
        // 在调用时生成时间戳，确保顺序正确
        string timestamp = DateTime.Now.ToString("HH:mm:fff");
        
        // 使用Dispatcher确保UI更新在主线程上执行
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // 根据消息内容添加不同的前缀标识
            string prefix = "";
            if (!string.IsNullOrEmpty(message))
            {
                if (message.Contains("失败"))
                {
                    prefix = "❌ ";
                }
                else if (message.Contains("完成"))
                {
                    prefix = "✅ ";
                }
                else if (message.Contains("开始") || message.Contains("解析") || message.Contains("下载"))
                {
                    prefix = "▶️  ";
                }
                else if (message.Contains("处理时间") || message.Contains("实际文件大小") || message.Contains("媒体信息"))
                {
                    prefix = "📊 ";
                }
            }

            // 创建日志文本块
            var logTextBlock = new TextBlock
            {
                Text = $"[{timestamp}] {prefix}{message}",
                FontFamily = "Consolas",
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Padding = new Avalonia.Thickness(2, 1, 2, 1),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                ClipToBounds = false
            };

            // 根据日志内容设置不同的颜色
            SolidColorBrush foregroundColor = GetForegroundColor(message);
            logTextBlock.Foreground = foregroundColor;

            // 限制日志条目数量，防止内存占用过高
            if (_statusContainer.Children.Count >= MaxLogItems)
            {
                // 移除最旧的日志条目
                _statusContainer.Children.RemoveAt(0);
            }
            
            // 直接添加到主容器
            _statusContainer.Children.Add(logTextBlock);

            // 调用主窗口的滚动方法，实现自动滚动
            _mainWindow?.ScrollToBottomIfNeeded();
        });
    }
    
    /// <summary>
    /// 更新可折叠日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="isRootItem">是否为根日志条目</param>
    /// <param name="autoCollapse">是否自动折叠</param>
    public void UpdateCollapsibleLog(string message, bool isRootItem = true, bool autoCollapse = true)
    {
        // 在调用时生成时间戳，确保顺序正确
        string timestamp = DateTime.Now.ToString("HH:mm:fff");
        
        // 使用Dispatcher确保UI更新在主线程上执行
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // 根据消息内容添加不同的前缀标识
            string prefix = "";
            if (!string.IsNullOrEmpty(message))
            {
                if (message.Contains("失败"))
                {
                    prefix = "❌ ";
                }
                else if (message.Contains("完成"))
                {
                    prefix = "✅ ";
                }
                else if (message.Contains("开始") || message.Contains("解析") || message.Contains("下载"))
                {
                    prefix = "▶️  ";
                }
                else if (message.Contains("处理时间") || message.Contains("实际文件大小") || message.Contains("媒体信息"))
                {
                    prefix = "📊 ";
                }
            }

            // 根据日志内容设置不同的颜色
            SolidColorBrush foregroundColor = GetForegroundColor(message);
            
            // 限制日志条目数量，防止内存占用过高
            if (_statusContainer.Children.Count >= MaxLogItems)
            {
                // 移除最旧的日志条目
                _statusContainer.Children.RemoveAt(0);
            }
            
            if (isRootItem)
            {
                // 创建新的可折叠日志条目
                var logItem = new CollapsibleLogItem(timestamp, prefix, message, foregroundColor)
                {
                    IsExpanded = !autoCollapse // 根据参数决定是否折叠
                };
                
                // 添加到主容器
                _statusContainer.Children.Add(logItem);
                
                // 保存为最后一个可折叠条目
                _lastCollapsibleItem = logItem;
            }
            else if (_lastCollapsibleItem != null)
            {
                // 向最后一个可折叠条目添加子日志
                _lastCollapsibleItem.AddChildLog(timestamp, prefix, message, foregroundColor);
            }
            else
            {
                // 如果没有根日志条目，创建普通日志
                UpdateLog(message);
            }

            // 调用主窗口的滚动方法，实现自动滚动
            _mainWindow?.ScrollToBottomIfNeeded();
        });
    }
    
    /// <summary>
    /// 根据日志消息获取前景色
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <returns>前景色</returns>
    private SolidColorBrush GetForegroundColor(string message)
    {
        // 使用LogStyleManager获取统一的日志颜色
        return LogStyleManager.GetForegroundColor(message);
    }
    
    /// <summary>
    /// 重置可折叠日志状态
    /// </summary>
    public void ResetCollapsibleLog()
    {
        _lastCollapsibleItem = null;
    }

    /// <summary>
    /// 清除所有日志
    /// </summary>
    public void ClearLogs()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _statusContainer.Children.Clear();
            _lastCollapsibleItem = null;
        });
    }

    /// <summary>
    /// 清除帮助信息日志（带有 HelpInfo 标记的日志）
    /// </summary>
    public void ClearHelpInfoLogs()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // 从后向前遍历，移除带有 HelpInfo 标记的日志
            for (int i = _statusContainer.Children.Count - 1; i >= 0; i--)
            {
                var child = _statusContainer.Children[i];
                if (child is TextBlock textBlock && textBlock.Tag?.ToString() == "HelpInfo")
                {
                    _statusContainer.Children.RemoveAt(i);
                }
                else if (child is CollapsibleLogItem collapsibleItem && collapsibleItem.Tag?.ToString() == "HelpInfo")
                {
                    _statusContainer.Children.RemoveAt(i);
                    // 如果被移除的是最后一个可折叠项，重置引用
                    if (_lastCollapsibleItem == collapsibleItem)
                    {
                        _lastCollapsibleItem = null;
                    }
                }
            }
        });
    }

    /// <summary>
    /// 更新帮助信息日志（带有 HelpInfo 标记）
    /// </summary>
    /// <param name="message">日志消息</param>
    public void UpdateHelpInfoLog(string message)
    {
        // 在调用时生成时间戳，确保顺序正确
        string timestamp = DateTime.Now.ToString("HH:mm:fff");
        
        // 使用Dispatcher确保UI更新在主线程上执行
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // 创建日志文本块，并标记为帮助信息
            var logTextBlock = new TextBlock
            {
                Text = $"[{timestamp}] {message}",
                FontFamily = "Consolas",
                FontSize = 12,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Padding = new Avalonia.Thickness(2, 1, 2, 1),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                ClipToBounds = false,
                Tag = "HelpInfo"  // 标记为帮助信息
            };

            // 根据日志内容设置不同的颜色
            logTextBlock.Foreground = GetForegroundColor(message);

            // 限制日志条目数量，防止内存占用过高
            if (_statusContainer.Children.Count >= MaxLogItems)
            {
                // 移除最旧的日志条目
                _statusContainer.Children.RemoveAt(0);
            }
            
            // 直接添加到主容器
            _statusContainer.Children.Add(logTextBlock);

            // 调用主窗口的滚动方法，实现自动滚动
            _mainWindow?.ScrollToBottomIfNeeded();
        });
    }
}