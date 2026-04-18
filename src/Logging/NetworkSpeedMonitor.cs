using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using video_stream_researcher.Interfaces;

namespace video_stream_researcher.Logs
{
    public class NetworkSpeedMonitor : INetworkSpeedMonitor
    {
        // 处理进度相关变量
    private DispatcherTimer? _chartUpdateTimer;
    private bool _isDrawing = false;
    private double _processingProgress = 0; // 当前显示进度（0-100）
    private double _targetProgress = 0; // 目标进度（0-100）
    private bool _isProcessing = false; // 是否正在处理文件
    private bool _processingCompleted = false; // 处理是否完成
    private bool _processingCanceled = false; // 处理是否被取消
    
    // 动画相关变量
    private double _lastProgress = 0; // 上一帧的进度
    private long _lastUpdateTime = Environment.TickCount64; // 上一次更新的时间戳
    
    // 速度历史记录，用于平滑过渡和进度预测
    private Queue<double> _deltaHistory = new Queue<double>(); // 最近10次的进度差值
    private const int HistorySize = 10; // 历史记录大小
    private double _smoothedDelta = 0; // 平滑后的进度差值
    
    // 进度历史记录，用于预测完成时间
    private Queue<(long timestamp, double progress)> _progressHistory = new Queue<(long, double)>(); // 最近的进度记录
    private const int ProgressHistorySize = 20; // 进度历史记录大小
    private const int MinHistoryForPrediction = 5; // 进行预测所需的最小历史记录数
    private double _estimatedTimeRemaining = 0; // 估计剩余时间（秒）
    
    // 跳跃缓动相关变量
    private const double JumpThreshold = 10.0; // 进度跳跃阈值（超过此值使用特殊缓动）
    private const double NormalEasing = 0.3; // 正常情况下的缓动因子
    private const double JumpEasingStart = 0.15; // 跳跃时的起始缓动因子（更小，更平滑）
    private const double JumpEasingEnd = 0.4; // 跳跃时的结束缓动因子（逐渐增大）
    private double _jumpEasingFactor = NormalEasing; // 当前跳跃缓动因子
    
    // 图表绘制相关变量
    private readonly Canvas? _chartCanvas;
    private readonly TextBlock? _speedTextBlock;
    private readonly TextBlock? _statusTextBlock;
    
    // 缓存画笔对象，减少内存分配，使用懒加载
    private IBrush? _canceledLineBrush;
    private IBrush? _canceledTextBrush;
    private IBrush? _completedLineBrush;
    private IBrush? _completedTextBrush;
    private IBrush? _processingLineBrush;
    private IBrush? _processingTextBrush;
    
    // 缓存UI控件，减少对象创建
    private Path? _progressLine;
    private Border? _progressTextContainer;
    private TextBlock? _progressTextBlock;
    
    // 构造函数
    public NetworkSpeedMonitor(Canvas chartCanvas, TextBlock speedTextBlock, TextBlock? statusTextBlock = null)
    {
        _chartCanvas = chartCanvas;
        _speedTextBlock = speedTextBlock;
        _statusTextBlock = statusTextBlock;

        // 初始化定时器，提高刷新频率以获得更流畅的动画效果
        _chartUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // 约60fps，获得流畅动画
        };
        _chartUpdateTimer.Tick += ChartUpdateTimer_Tick;
        _chartUpdateTimer.Start();
    
        // 初始化Canvas绘制事件
        _chartCanvas.AttachedToVisualTree += OnChartCanvasAttachedToVisualTree;
        
        // 延迟初始化画笔缓存，直到实际需要时才创建
    }
    
    // 更新当前处理状态文本
    public void UpdateCurrentStatus(string status)
    {
        if (_statusTextBlock != null)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 直接设置状态文本，不添加前缀
                _statusTextBlock.Text = status;
                // 设置更淡的颜色
                _statusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100)); // 淡绿色
            });
        }
    }
    
    // 图表更新定时器事件
        private void ChartUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isDrawing && _chartCanvas != null)
            {
                // 只有在处理中或处理完成/取消时才更新图表
                if (_isProcessing || _processingCompleted || _processingCanceled)
                {
                    // 计算当前时间
                    long currentTime = Environment.TickCount64;
                    
                    // 计算进度差值
                    double delta = _targetProgress - _processingProgress;
                    
                    // 更新进度历史记录
                    UpdateProgressHistory(currentTime, _processingProgress);
                    
                    // 更新进度差值历史记录
                    UpdateDeltaHistory(delta);
                    
                    // 计算平滑后的差值
                    CalculateSmoothedDelta();
                    
                    // 预测完成时间
                    PredictCompletionTime(currentTime);
                    
                    // 简单直接的动画算法，参考旧版本动画实现，添加动态缓动
                    if (Math.Abs(delta) > 0.01)
                    {
                        double easingFactor;
                        
                        // 检测进度跳跃（差值大于阈值）
                        if (Math.Abs(delta) > JumpThreshold)
                        {
                            // 进度跳跃，使用动态缓动因子
                            // 计算缓动因子：根据当前进度与目标进度的接近程度，从JumpEasingStart过渡到JumpEasingEnd
                            double progressRatio = Math.Abs(_processingProgress - _targetProgress) / Math.Abs(delta);
                            _jumpEasingFactor = JumpEasingStart + (JumpEasingEnd - JumpEasingStart) * (1 - progressRatio);
                            easingFactor = _jumpEasingFactor;
                        }
                        else
                        {
                            // 正常进度变化，使用固定缓动因子
                            easingFactor = NormalEasing;
                        }
                        
                        // 应用缓动
                        _processingProgress += delta * easingFactor;
                        
                        // 确保进度不会超过目标值
                        if (Math.Abs(_processingProgress - _targetProgress) < 0.01)
                        {
                            _processingProgress = _targetProgress;
                        }
                    }
                    else
                    {
                        // 当接近目标值时，直接设置为目标值
                        _processingProgress = _targetProgress;
                    }
                    
                    _isDrawing = true;
                    DrawSpeedChart();
                    _isDrawing = false;
                    
                    // 更新上一帧进度
                    _lastProgress = _processingProgress;
                    _lastUpdateTime = currentTime;
                }
                else
                {
                    // 没有处理任务时，停止定时器以节省资源
                    if (_chartUpdateTimer != null && _chartUpdateTimer.IsEnabled)
                    {
                        _chartUpdateTimer.Stop();
                    }
                }
            }
        }
    
    // 设置视频时长（保留方法但不使用）
    public void SetVideoDuration(long durationSeconds)
    {
        // 不使用视频时长调整数据点数量，保持固定数据点数量
    }
    
    // 更新处理进度
    public void UpdateProcessingProgress(double progress)
    {
        _isProcessing = true;
        _targetProgress = Math.Max(0, Math.Min(100, progress));
        
        // 有处理任务时，确保定时器正在运行
        if (_chartUpdateTimer != null && !_chartUpdateTimer.IsEnabled)
        {
            _chartUpdateTimer.Start();
        }
    }
    
    // 更新进度历史记录
    private void UpdateProgressHistory(long timestamp, double progress)
    {
        _progressHistory.Enqueue((timestamp, progress));
        
        // 保持历史记录大小
        if (_progressHistory.Count > ProgressHistorySize)
        {
            _progressHistory.Dequeue();
        }
    }
    
    // 更新进度差值历史记录
    private void UpdateDeltaHistory(double delta)
    {
        _deltaHistory.Enqueue(Math.Abs(delta));
        
        // 保持历史记录大小
        if (_deltaHistory.Count > HistorySize)
        {
            _deltaHistory.Dequeue();
        }
    }
    
    // 计算平滑后的进度差值
    private void CalculateSmoothedDelta()
    {
        if (_deltaHistory.Count == 0)
        {
            _smoothedDelta = 0;
            return;
        }
        
        // 计算平均值
        double sum = 0;
        foreach (double delta in _deltaHistory)
        {
            sum += delta;
        }
        _smoothedDelta = sum / _deltaHistory.Count;
    }
    
    // 预测完成时间
    private void PredictCompletionTime(long currentTime)
    {
        // 如果历史记录不足，无法预测
        if (_progressHistory.Count < MinHistoryForPrediction)
        {
            _estimatedTimeRemaining = 0;
            return;
        }
        
        // 获取最近的进度记录
        var recentRecords = _progressHistory.ToArray();
        if (recentRecords.Length < 2)
        {
            _estimatedTimeRemaining = 0;
            return;
        }
        
        // 计算平均进度增长速度（进度/毫秒）
        double totalProgressChange = recentRecords[^1].progress - recentRecords[0].progress;
        long totalTimeChange = recentRecords[^1].timestamp - recentRecords[0].timestamp;
        
        // 避免除以零
        if (totalTimeChange == 0 || totalProgressChange <= 0)
        {
            _estimatedTimeRemaining = 0;
            return;
        }
        
        // 计算进度增长速度（进度/毫秒）
        double progressSpeed = totalProgressChange / totalTimeChange;
        
        // 计算剩余进度
        double remainingProgress = 100 - _processingProgress;
        
        // 避免预测完成时间为负数
        if (remainingProgress <= 0)
        {
            _estimatedTimeRemaining = 0;
            return;
        }
        
        // 计算剩余时间（毫秒）
        long remainingTimeMs = (long)(remainingProgress / progressSpeed);
        
        // 转换为秒
        _estimatedTimeRemaining = remainingTimeMs / 1000.0;
        
        // 限制最大预测时间为1小时（避免不合理的预测）
        _estimatedTimeRemaining = Math.Min(_estimatedTimeRemaining, 3600);
    }
    
    // 格式化剩余时间显示
    private string FormatTimeRemaining(double seconds)
    {
        if (seconds <= 0)
        {
            return "--:--";
        }
        
        int minutes = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        
        return $"{minutes:D2}:{secs:D2}";
    }
    
    // 标记下载完成
    public void MarkDownloadCompleted()
    {
        _processingCompleted = true;
        _isProcessing = false;
        _processingCanceled = false;

        // 立即更新UI显示"完成喵"
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_speedTextBlock != null)
            {
                _speedTextBlock.Text = "完成喵";
                // 设置更淡的颜色
                _speedTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100)); // 淡绿色
            }
        });

        // 处理完成后，确保定时器正在运行以更新最终状态
        if (_chartUpdateTimer != null && !_chartUpdateTimer.IsEnabled)
        {
            _chartUpdateTimer.Start();
        }
    }
    
    // 标记下载取消
    public void MarkDownloadCanceled()
    {
        _processingCanceled = true;
        _isProcessing = false;
        _processingCompleted = false;
        
        // 处理取消后，确保定时器正在运行以更新最终状态
        if (_chartUpdateTimer != null && !_chartUpdateTimer.IsEnabled)
        {
            _chartUpdateTimer.Start();
        }
    }
    
    // 处理实时网速数据
    public void OnSpeedUpdate(long speedBytesPerSecond, bool isInitial = false)
    {
        // 如果处理已完成，显示"完成喵"
        if (_processingCompleted)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_speedTextBlock != null)
                {
                    _speedTextBlock.Text = "完成喵";
                    // 设置更淡的颜色
                    _speedTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100)); // 淡绿色
                }
            });
            return;
        }

        // 更新当前速度显示，添加剩余时间预测
        string speedTextStr = FormatSpeed(speedBytesPerSecond);
        string speedType = isInitial ? "初始速度" : "当前速度";

        // 构建速度文本，添加剩余时间预测
        string timeRemainingText = "";
        if (_isProcessing && _estimatedTimeRemaining > 0)
        {
            string timeRemaining = FormatTimeRemaining(_estimatedTimeRemaining);
            timeRemainingText = $" (剩余时间: {timeRemaining})";
        }

        string finalText = $"{speedType}: {speedTextStr}{timeRemainingText}";

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_speedTextBlock != null)
            {
                _speedTextBlock.Text = finalText;
                // 设置更淡的颜色
                _speedTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 100)); // 淡绿色
            }
        });
    }
    
    // 创建渐变画笔的辅助方法
    private LinearGradientBrush CreateGradientBrush(Color[] colors)
    {
        var gradientStops = new GradientStops();
        for (int i = 0; i < colors.Length; i++)
        {
            double offset = (double)i / (colors.Length - 1);
            gradientStops.Add(new GradientStop(colors[i], offset));
        }
        
        return new LinearGradientBrush
        {
            GradientStops = gradientStops,
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) // 水平方向渐变
        };
    }
    
    // 复用PathGeometry和PathFigure对象，减少内存分配
    private PathGeometry? _cachedPathGeometry;
    private PathFigure? _cachedPathFigure;
    private LineSegment? _cachedLineSegment;
    
    // 绘制网速图表
    private void DrawSpeedChart()
    {
        if (_chartCanvas == null)
            return;
        
        try
        {
            _isDrawing = true;
            
            // 获取画布尺寸
            double width = _chartCanvas.Bounds.Width;
            double height = _chartCanvas.Bounds.Height;
            
            if (width <= 0 || height <= 0)
                return;
            
            // 绘制处理进度（绿色，平行x轴，根据进度在y轴移动）
            // 处理中、处理完成或处理取消都需要绘制
            if (_isProcessing || _processingCompleted || _processingCanceled)
            {
                // 计算y轴位置，根据进度从底部到顶部移动
                // 进度0%时在底部，100%时在顶部
                // 提高绿线在y轴的最高高度（从0.8改为0.9）
                double lineY = height - (height * 0.9 * (_processingProgress / 100.0));
                
                // 根据处理状态选择颜色，实现懒加载
                IBrush lineBrush;
                IBrush textBackgroundBrush;
                
                if (_processingCanceled)
                {
                    // 处理取消，使用红色，懒加载
                    _canceledLineBrush ??= CreateGradientBrush(new Color[] { Color.FromRgb(255, 100, 100), Color.FromRgb(255, 150, 150), Color.FromRgb(255, 100, 100) });
                    _canceledTextBrush ??= CreateGradientBrush(new Color[] { Color.FromRgb(255, 100, 100), Color.FromRgb(255, 150, 150), Color.FromRgb(255, 100, 100) });
                    
                    lineBrush = _canceledLineBrush!;
                    textBackgroundBrush = _canceledTextBrush!;
                }
                else if (_processingCompleted)
                {
                    // 处理完成，使用灰绿色，懒加载
                    _completedLineBrush ??= CreateGradientBrush(new Color[] { Color.FromRgb(100, 150, 100), Color.FromRgb(150, 200, 150), Color.FromRgb(100, 150, 100) });
                    _completedTextBrush ??= CreateGradientBrush(new Color[] { Color.FromRgb(100, 150, 100), Color.FromRgb(150, 200, 150), Color.FromRgb(100, 150, 100) });
                    
                    lineBrush = _completedLineBrush!;
                    textBackgroundBrush = _completedTextBrush!;
                }
                else
                {
                    // 处理中，使用正常绿色，懒加载
                    _processingLineBrush ??= CreateGradientBrush(new Color[] { Color.FromRgb(0, 200, 0), Color.FromRgb(0, 255, 100), Color.FromRgb(0, 200, 0) });
                    _processingTextBrush ??= CreateGradientBrush(new Color[] { Color.FromRgb(0, 200, 0), Color.FromRgb(0, 255, 100), Color.FromRgb(0, 200, 0) });
                    
                    lineBrush = _processingLineBrush!;
                    textBackgroundBrush = _processingTextBrush!;
                }
                
                // 绘制水平直线，根据状态使用不同颜色
                if (_progressLine == null)
                {
                    // 首次创建路径对象
                    _progressLine = new Path
                    {
                        Stroke = lineBrush,
                        StrokeThickness = 2
                    };
                    _chartCanvas.Children.Add(_progressLine);
                }
                else
                {
                    // 复用现有路径对象
                    _progressLine.Stroke = lineBrush;
                }
                
                // 更新路径数据，复用对象减少内存分配
                if (_cachedPathGeometry == null || _cachedPathFigure == null || _cachedLineSegment == null)
                {
                    // 首次创建路径几何对象
                    _cachedPathGeometry = new PathGeometry();
                    _cachedPathFigure = new PathFigure { StartPoint = new Point(0, lineY), IsClosed = false };
                    _cachedLineSegment = new LineSegment { Point = new Point(width, lineY) };
                    _cachedPathFigure.Segments!.Add(_cachedLineSegment);
                    _cachedPathGeometry.Figures!.Add(_cachedPathFigure);
                }
                else
                {
                    // 复用现有对象，只更新位置
                    _cachedPathFigure.StartPoint = new Point(0, lineY);
                    _cachedLineSegment.Point = new Point(width, lineY);
                }
                
                _progressLine.Data = _cachedPathGeometry;
                
                // 在直线右侧添加当前进度百分比文本，跟随直线移动，使用渐变效果
                if (_progressTextContainer == null)
                {
                    // 首次创建文本容器
                    _progressTextContainer = new Border
                    {
                        Background = textBackgroundBrush,
                        Padding = new Thickness(5, 2, 5, 2),
                        CornerRadius = new CornerRadius(3)
                    };
                    
                    // 创建文本块
                    _progressTextBlock = new TextBlock
                    {
                        FontSize = 12,
                        Foreground = Brushes.White, // 白色文字，与渐变背景形成对比
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    // 将文本块添加到容器中
                    _progressTextContainer.Child = _progressTextBlock;
                    
                    // 添加到画布
                    _chartCanvas.Children.Add(_progressTextContainer);
                }
                else
                {
                    // 复用现有容器
                    _progressTextContainer.Background = textBackgroundBrush;
                }
                
                // 更新文本内容
                if (_progressTextBlock != null)
                {
                    _progressTextBlock.Text = $"{_processingProgress:F1}%";
                }
                
                // 容器位置跟随直线，显示在直线右侧，靠近图表中心，确保能被看到
                Canvas.SetLeft(_progressTextContainer, width - 60); // 调整到图表右侧内部，距离右侧60像素
                Canvas.SetTop(_progressTextContainer, lineY - 18); // 调整位置，确保垂直居中
            }
            else
            {
                // 未处理时，清空画布
                _chartCanvas.Children.Clear();
                // 重置缓存控件引用
                _progressLine = null;
                _progressTextContainer = null;
                _progressTextBlock = null;
                // 重置路径几何对象引用
                _cachedPathGeometry = null;
                _cachedPathFigure = null;
                _cachedLineSegment = null;
            }
        }
        finally
        {
            _isDrawing = false;
        }
    }
    
    // 格式化网速显示
    private string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond} B/s";
        else if (bytesPerSecond < 1024 * 1024)
            return $"{(bytesPerSecond / 1024.0):F1} KB/s";
        else if (bytesPerSecond < 1024 * 1024 * 1024)
            return $"{(bytesPerSecond / (1024 * 1024.0)):F1} MB/s";
        else
            return $"{(bytesPerSecond / (1024 * 1024 * 1024.0)):F1} GB/s";
    }
    
    // 重置网络速度监控
    public void Reset()
    {
        // 立即重置所有状态
        _processingProgress = 0; // 重置处理进度到0%
        _targetProgress = 0; // 重置目标进度到0%
        _isProcessing = false; // 重置处理状态
        _processingCompleted = false; // 重置处理完成状态
        _processingCanceled = false; // 重置处理取消状态
        
        // 重置进度预测相关变量
        _progressHistory.Clear();
        _deltaHistory.Clear();
        _smoothedDelta = 0;
        _estimatedTimeRemaining = 0;
        _jumpEasingFactor = NormalEasing;
        
        // 重置动画相关变量
        
        if (_speedTextBlock != null)
        {
            _speedTextBlock.Text = "当前速度: 0 B/s";
        }
        
        if (_statusTextBlock != null)
        {
            _statusTextBlock.Text = "就绪";
        }
        
        // 清理UI资源，避免内存泄漏
        if (_chartCanvas != null)
        {
            // 清空画布上的所有子元素
            _chartCanvas.Children.Clear();
            
            // 重置缓存的UI控件引用，帮助GC回收内存
            _progressLine = null;
            _progressTextContainer = null;
            _progressTextBlock = null;
            
            // 重置路径几何对象引用
            _cachedPathGeometry = null;
            _cachedPathFigure = null;
            _cachedLineSegment = null;
        }
        
        // 立即重绘，确保进度线返回0%
        DrawSpeedChart();
        
        // 重置状态后，由于没有处理任务，定时器会在下次Tick时自动停止
        // 移除强制GC调用，避免干扰.NET运行时的垃圾回收策略
    }
    
    // 重置进度线到0%的动画
    public async Task ResetProgressAnimation()
    {
        // 设置目标进度为0%
        _targetProgress = 0;
        _processingCanceled = false;
        _processingCompleted = false;

        // 重置速度文本为初始状态
        if (_speedTextBlock != null)
        {
            _speedTextBlock.Text = "当前速度: 0 B/s";
        }

        // 动画时长300ms
        int duration = 300;
        int steps = 15;
        double stepDuration = duration / steps;
        
        // 使用平滑动画将进度线过渡到0%
        for (int i = 0; i <= steps; i++)
        {
            double progress = (double)i / steps;
            
            // 缓动函数 - 三次方缓动
            double easedProgress = Math.Pow(progress, 3);
            
            // 更新当前进度
            _processingProgress = _processingProgress - (_processingProgress * easedProgress);
            
            // 重绘图表
            DrawSpeedChart();
            
            // 等待下一步
            await Task.Delay((int)stepDuration);
        }
        
        // 确保最终进度为0%
        _processingProgress = 0;
        DrawSpeedChart();
    }
    
    // 停止监控
    public void Stop()
    {
        if (_chartUpdateTimer != null)
        {
            _chartUpdateTimer.Stop();
            _chartUpdateTimer.Tick -= ChartUpdateTimer_Tick;
        }
        
        // 取消事件订阅
        if (_chartCanvas != null)
        {
            _chartCanvas.AttachedToVisualTree -= OnChartCanvasAttachedToVisualTree;
        }
    }
    
    // 画布附加到视觉树时的事件处理函数
    private void OnChartCanvasAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DrawSpeedChart();
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 释放托管资源
            Stop();
            
            // 清空画布并重置UI资源
            if (_chartCanvas != null)
            {
                _chartCanvas.Children.Clear();
            }
            
            // 重置所有缓存的UI资源
            _progressLine = null;
            _progressTextContainer = null;
            _progressTextBlock = null;
            
            // 重置所有画笔资源
            _canceledLineBrush = null;
            _canceledTextBrush = null;
            _completedLineBrush = null;
            _completedTextBrush = null;
            _processingLineBrush = null;
            _processingTextBrush = null;
            
            // 重置路径几何对象
            _cachedPathGeometry = null;
            _cachedPathFigure = null;
            _cachedLineSegment = null;
            
            // 停止并释放定时器
            if (_chartUpdateTimer != null)
            {
                _chartUpdateTimer.Stop();
                _chartUpdateTimer.Tick -= ChartUpdateTimer_Tick;
                _chartUpdateTimer = null;
            }
        }
    }
}
}