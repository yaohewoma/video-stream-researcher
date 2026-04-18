using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace video_stream_researcher.Services;

/// <summary>
/// 性能监控器 - 持续优化系统的核心组件
/// 负责收集、分析和报告视频播放性能指标
/// </summary>
public sealed class PerformanceMonitor : IDisposable
{
    // KPI阈值定义
    public const double TargetFps = 24.0;
    public const double MinAcceptableFps = 21.0;
    public const double CriticalFps = 15.0;
    public const double MaxFrameSkipRate = 0.05; // 5%
    public const double MaxMemoryUsageMB = 200.0;
    public const double MaxCpuUsagePercent = 80.0;
    public const int MaxFrameQueueSize = 32;
    public const int MinFrameQueueSize = 3;

    // 性能数据收集间隔
    private const int MetricsCollectionIntervalMs = 1000;
    private const int BottleneckDetectionWindowSize = 5;

    private readonly Stopwatch _sessionStopwatch;
    private readonly Queue<PerformanceSnapshot> _metricsHistory;
    private readonly object _lock = new();
    private Timer? _metricsTimer;
    private bool _isMonitoring;

    // 当前会话统计
    private long _totalFramesDecoded;
    private long _totalFramesRendered;
    private long _totalFramesDropped;
    private long _lastSecondFramesRendered;
    private long _lastSecondFramesDropped;
    private DateTime _lastMetricsUpdate;

    // 性能计数器
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _memoryCounter;

    /// <summary>
/// 当前性能指标快照
/// </summary>
    public PerformanceSnapshot CurrentMetrics { get; private set; }

    /// <summary>
/// 性能报告事件
/// </summary>
    public event EventHandler<PerformanceReportEventArgs>? PerformanceReportGenerated;

    /// <summary>
/// 瓶颈检测事件
/// </summary>
    public event EventHandler<BottleneckDetectedEventArgs>? BottleneckDetected;

    /// <summary>
/// KPI告警事件
/// </summary>
    public event EventHandler<KpiAlertEventArgs>? KpiAlertTriggered;

    public PerformanceMonitor()
    {
        _sessionStopwatch = new Stopwatch();
        _metricsHistory = new Queue<PerformanceSnapshot>();
        CurrentMetrics = new PerformanceSnapshot();
        _lastMetricsUpdate = DateTime.UtcNow;

        InitializePerformanceCounters();
    }

    /// <summary>
/// 初始化Windows性能计数器
/// </summary>
    private void InitializePerformanceCounters()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
            _cpuCounter.NextValue(); // 初始化
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[性能监控] 初始化性能计数器失败: {ex.Message}");
        }
    }

    /// <summary>
/// 开始性能监控会话
/// </summary>
    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _sessionStopwatch.Restart();
            _metricsHistory.Clear();
            _totalFramesDecoded = 0;
            _totalFramesRendered = 0;
            _totalFramesDropped = 0;
            _lastSecondFramesRendered = 0;
            _lastSecondFramesDropped = 0;

            // 启动定期指标收集
            _metricsTimer = new Timer(CollectMetrics, null, MetricsCollectionIntervalMs, MetricsCollectionIntervalMs);

            Debug.WriteLine("[性能监控] 监控会话已启动");
        }
    }

    /// <summary>
/// 停止性能监控会话
/// </summary>
    public void StopMonitoring()
    {
        lock (_lock)
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _sessionStopwatch.Stop();
            _metricsTimer?.Dispose();
            _metricsTimer = null;

            // 生成最终报告
            var finalReport = GeneratePerformanceReport();
            PerformanceReportGenerated?.Invoke(this, new PerformanceReportEventArgs(finalReport));

            Debug.WriteLine("[性能监控] 监控会话已停止");
        }
    }

    /// <summary>
/// 记录解码帧
/// </summary>
    public void RecordFrameDecoded()
    {
        Interlocked.Increment(ref _totalFramesDecoded);
    }

    /// <summary>
/// 记录渲染帧
/// </summary>
    public void RecordFrameRendered()
    {
        Interlocked.Increment(ref _totalFramesRendered);
        Interlocked.Increment(ref _lastSecondFramesRendered);
    }

    /// <summary>
/// 记录丢弃帧
/// </summary>
    public void RecordFrameDropped(string reason)
    {
        Interlocked.Increment(ref _totalFramesDropped);
        Interlocked.Increment(ref _lastSecondFramesDropped);

        Debug.WriteLine($"[性能监控] 帧丢弃: {reason}");
    }

    /// <summary>
/// 记录帧队列状态
/// </summary>
    public void RecordFrameQueueStatus(int currentSize, int maxSize)
    {
        CurrentMetrics = CurrentMetrics with
        {
            FrameQueueSize = currentSize,
            FrameQueueCapacity = maxSize,
            FrameQueueUtilization = maxSize > 0 ? (double)currentSize / maxSize : 0
        };
    }

    /// <summary>
/// 记录渲染耗时
/// </summary>
    public void RecordRenderTime(TimeSpan renderTime)
    {
        CurrentMetrics = CurrentMetrics with
        {
            LastRenderTimeMs = renderTime.TotalMilliseconds,
            AvgRenderTimeMs = CalculateMovingAverage(
                CurrentMetrics.AvgRenderTimeMs,
                renderTime.TotalMilliseconds,
                10)
        };
    }

    /// <summary>
/// 记录解码耗时
/// </summary>
    public void RecordDecodeTime(TimeSpan decodeTime)
    {
        CurrentMetrics = CurrentMetrics with
        {
            LastDecodeTimeMs = decodeTime.TotalMilliseconds,
            AvgDecodeTimeMs = CalculateMovingAverage(
                CurrentMetrics.AvgDecodeTimeMs,
                decodeTime.TotalMilliseconds,
                10)
        };
    }

    /// <summary>
/// 定期收集性能指标
/// </summary>
    private void CollectMetrics(object? state)
    {
        lock (_lock)
        {
            if (!_isMonitoring) return;

            var now = DateTime.UtcNow;
            var elapsed = now - _lastMetricsUpdate;
            _lastMetricsUpdate = now;

            // 计算瞬时FPS
            var framesRendered = Interlocked.Exchange(ref _lastSecondFramesRendered, 0);
            var framesDropped = Interlocked.Exchange(ref _lastSecondFramesDropped, 0);
            var instantFps = framesRendered / elapsed.TotalSeconds;

            // 获取系统资源使用情况
            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();

            // 创建性能快照
            var snapshot = new PerformanceSnapshot
            {
                Timestamp = now,
                SessionDuration = _sessionStopwatch.Elapsed,
                InstantFps = instantFps,
                AverageFps = CalculateAverageFps(),
                FramesRendered = _totalFramesRendered,
                FramesDropped = _totalFramesDropped,
                FrameSkipRate = CalculateFrameSkipRate(),
                CpuUsagePercent = cpuUsage,
                MemoryUsageMB = memoryUsage,
                FrameQueueSize = CurrentMetrics.FrameQueueSize,
                FrameQueueCapacity = CurrentMetrics.FrameQueueCapacity,
                FrameQueueUtilization = CurrentMetrics.FrameQueueUtilization,
                AvgRenderTimeMs = CurrentMetrics.AvgRenderTimeMs,
                AvgDecodeTimeMs = CurrentMetrics.AvgDecodeTimeMs,
   // 评估KPI状态
            KpiStatus = EvaluateKpiStatus(instantFps, (int)framesDropped, memoryUsage, cpuUsage)
            };

            // 保存到历史记录
            _metricsHistory.Enqueue(snapshot);
            while (_metricsHistory.Count > BottleneckDetectionWindowSize)
            {
                _metricsHistory.Dequeue();
            }

            CurrentMetrics = snapshot;

            // 检测瓶颈
            DetectBottlenecks(snapshot);

            // 检查KPI告警
            CheckKpiAlerts(snapshot);

            Debug.WriteLine($"[性能监控] FPS:{instantFps:F1} 队列:{snapshot.FrameQueueSize}/{snapshot.FrameQueueCapacity} CPU:{cpuUsage:F1}% 内存:{memoryUsage:F1}MB");
        }
    }

    /// <summary>
/// 获取CPU使用率
/// </summary>
    private double GetCpuUsage()
    {
        try
        {
            return _cpuCounter?.NextValue() ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
/// 获取内存使用量(MB)
/// </summary>
    private double GetMemoryUsage()
    {
        try
        {
            if (_memoryCounter != null)
            {
                return _memoryCounter.NextValue() / (1024 * 1024);
            }
            return Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
/// 计算平均FPS
/// </summary>
    private double CalculateAverageFps()
    {
        var elapsedSeconds = _sessionStopwatch.Elapsed.TotalSeconds;
        return elapsedSeconds > 0 ? _totalFramesRendered / elapsedSeconds : 0;
    }

    /// <summary>
/// 计算跳帧率
/// </summary>
    private double CalculateFrameSkipRate()
    {
        var total = _totalFramesRendered + _totalFramesDropped;
        return total > 0 ? (double)_totalFramesDropped / total : 0;
    }

    /// <summary>
/// 计算移动平均
/// </summary>
    private static double CalculateMovingAverage(double currentAvg, double newValue, int windowSize)
    {
        if (currentAvg <= 0) return newValue;
        return (currentAvg * (windowSize - 1) + newValue) / windowSize;
    }

    /// <summary>
/// 评估KPI状态
/// </summary>
    private static KpiStatus EvaluateKpiStatus(double fps, int framesDropped, double memoryMB, double cpuPercent)
    {
        if (fps < CriticalFps || memoryMB > MaxMemoryUsageMB * 1.5 || cpuPercent > 95)
            return KpiStatus.Critical;
        if (fps < MinAcceptableFps || memoryMB > MaxMemoryUsageMB || cpuPercent > MaxCpuUsagePercent)
            return KpiStatus.Warning;
        if (fps >= TargetFps && memoryMB < MaxMemoryUsageMB * 0.8 && cpuPercent < MaxCpuUsagePercent * 0.8)
            return KpiStatus.Excellent;
        return KpiStatus.Good;
    }

    /// <summary>
/// 检测性能瓶颈
/// </summary>
    private void DetectBottlenecks(PerformanceSnapshot current)
    {
        if (_metricsHistory.Count < BottleneckDetectionWindowSize) return;

        var recentMetrics = _metricsHistory.ToArray();
        var bottlenecks = new List<BottleneckType>();

        // 检测解码瓶颈
        var avgDecodeTime = recentMetrics.Average(m => m.AvgDecodeTimeMs);
        var targetDecodeTime = 1000.0 / TargetFps;
        if (avgDecodeTime > targetDecodeTime * 1.5)
        {
            bottlenecks.Add(BottleneckType.DecodePerformance);
        }

        // 检测渲染瓶颈
        var avgRenderTime = recentMetrics.Average(m => m.AvgRenderTimeMs);
        var targetRenderTime = 1000.0 / TargetFps;
        if (avgRenderTime > targetRenderTime * 1.5)
        {
            bottlenecks.Add(BottleneckType.RenderPerformance);
        }

        // 检测队列瓶颈
        var avgQueueUtilization = recentMetrics.Average(m => m.FrameQueueUtilization);
        if (avgQueueUtilization > 0.9)
        {
            bottlenecks.Add(BottleneckType.FrameQueueFull);
        }
        else if (avgQueueUtilization < 0.2 && current.InstantFps < MinAcceptableFps)
        {
            bottlenecks.Add(BottleneckType.FrameQueueStarvation);
        }

        // 检测系统资源瓶颈
        var avgCpu = recentMetrics.Average(m => m.CpuUsagePercent);
        var avgMemory = recentMetrics.Average(m => m.MemoryUsageMB);

        if (avgCpu > MaxCpuUsagePercent)
        {
            bottlenecks.Add(BottleneckType.CpuLimit);
        }
        if (avgMemory > MaxMemoryUsageMB)
        {
            bottlenecks.Add(BottleneckType.MemoryLimit);
        }

        // 检测FPS下降趋势
        var fpsTrend = recentMetrics.Select(m => m.InstantFps).ToArray();
        if (fpsTrend.Length >= 3 && fpsTrend[0] > fpsTrend[^1] * 1.3)
        {
            bottlenecks.Add(BottleneckType.FpsDegradation);
        }

        if (bottlenecks.Count > 0)
        {
            BottleneckDetected?.Invoke(this, new BottleneckDetectedEventArgs(
                bottlenecks.ToArray(),
                current,
                GenerateOptimizationSuggestions(bottlenecks)
            ));
        }
    }

    /// <summary>
/// 检查KPI告警
/// </summary>
    private void CheckKpiAlerts(PerformanceSnapshot current)
    {
        var alerts = new List<KpiAlert>();

        if (current.InstantFps < CriticalFps)
        {
            alerts.Add(new KpiAlert(KpiType.FrameRate, current.InstantFps, CriticalFps, AlertSeverity.Critical));
        }
        else if (current.InstantFps < MinAcceptableFps)
        {
            alerts.Add(new KpiAlert(KpiType.FrameRate, current.InstantFps, MinAcceptableFps, AlertSeverity.Warning));
        }

        if (current.FrameSkipRate > MaxFrameSkipRate * 2)
        {
            alerts.Add(new KpiAlert(KpiType.FrameSkipRate, current.FrameSkipRate, MaxFrameSkipRate, AlertSeverity.Critical));
        }
        else if (current.FrameSkipRate > MaxFrameSkipRate)
        {
            alerts.Add(new KpiAlert(KpiType.FrameSkipRate, current.FrameSkipRate, MaxFrameSkipRate, AlertSeverity.Warning));
        }

        if (current.MemoryUsageMB > MaxMemoryUsageMB * 1.2)
        {
            alerts.Add(new KpiAlert(KpiType.MemoryUsage, current.MemoryUsageMB, MaxMemoryUsageMB, AlertSeverity.Warning));
        }

        if (alerts.Count > 0)
        {
            KpiAlertTriggered?.Invoke(this, new KpiAlertEventArgs(alerts.ToArray(), current));
        }
    }

    /// <summary>
/// 生成优化建议
/// </summary>
    private static string[] GenerateOptimizationSuggestions(List<BottleneckType> bottlenecks)
    {
        var suggestions = new List<string>();

        foreach (var bottleneck in bottlenecks)
        {
            switch (bottleneck)
            {
                case BottleneckType.DecodePerformance:
                    suggestions.Add("建议: 启用硬件加速或降低视频分辨率");
                    suggestions.Add("建议: 优化NV12转换算法，使用SIMD指令");
                    break;
                case BottleneckType.RenderPerformance:
                    suggestions.Add("建议: 减少UI更新频率，使用批量渲染");
                    suggestions.Add("建议: 优化WriteableBitmap更新策略");
                    break;
                case BottleneckType.FrameQueueFull:
                    suggestions.Add("建议: 增加解码线程优先级或优化解码循环");
                    suggestions.Add("建议: 调整帧队列大小和预缓冲策略");
                    break;
                case BottleneckType.FrameQueueStarvation:
                    suggestions.Add("建议: 检查解码线程是否被阻塞");
                    suggestions.Add("建议: 优化解码和渲染的同步机制");
                    break;
                case BottleneckType.CpuLimit:
                    suggestions.Add("建议: 降低后台任务优先级");
                    suggestions.Add("建议: 考虑使用硬件解码减轻CPU负担");
                    break;
                case BottleneckType.MemoryLimit:
                    suggestions.Add("建议: 优化内存使用，及时释放帧缓冲区");
                    suggestions.Add("建议: 减小帧队列大小限制");
                    break;
                case BottleneckType.FpsDegradation:
                    suggestions.Add("建议: 检查长时间运行后的资源泄漏");
                    suggestions.Add("建议: 实施定期垃圾回收策略");
                    break;
            }
        }

        return suggestions.ToArray();
    }

    /// <summary>
/// 生成性能报告
/// </summary>
    public PerformanceReport GeneratePerformanceReport()
    {
        lock (_lock)
        {
            var history = _metricsHistory.ToArray();

            return new PerformanceReport
            {
                SessionDuration = _sessionStopwatch.Elapsed,
                TotalFramesDecoded = _totalFramesDecoded,
                TotalFramesRendered = _totalFramesRendered,
                TotalFramesDropped = _totalFramesDropped,
                AverageFps = CalculateAverageFps(),
                MinFps = history.Length > 0 ? history.Min(m => m.InstantFps) : 0,
                MaxFps = history.Length > 0 ? history.Max(m => m.InstantFps) : 0,
                AverageFrameSkipRate = CalculateFrameSkipRate(),
                FinalMetrics = CurrentMetrics,
                MetricsHistory = history,
                KpiCompliance = CalculateKpiCompliance(history),
                Recommendations = GenerateReportRecommendations(history)
            };
        }
    }

    /// <summary>
/// 计算KPI合规率
/// </summary>
    private static double CalculateKpiCompliance(PerformanceSnapshot[] history)
    {
        if (history.Length == 0) return 0;

        var compliantSnapshots = history.Count(m =>
            m.InstantFps >= MinAcceptableFps &&
            m.FrameSkipRate <= MaxFrameSkipRate &&
            m.MemoryUsageMB <= MaxMemoryUsageMB);

        return (double)compliantSnapshots / history.Length;
    }

    /// <summary>
/// 生成报告建议
/// </summary>
    private static string[] GenerateReportRecommendations(PerformanceSnapshot[] history)
    {
        var recommendations = new List<string>();

        if (history.Length == 0) return recommendations.ToArray();

        var avgFps = history.Average(m => m.InstantFps);
        var avgSkipRate = history.Average(m => m.FrameSkipRate);
        var avgMemory = history.Average(m => m.MemoryUsageMB);

        if (avgFps < TargetFps)
        {
            recommendations.Add($"平均FPS ({avgFps:F1}) 低于目标值 ({TargetFps})，建议进一步优化解码和渲染性能");
        }

        if (avgSkipRate > MaxFrameSkipRate)
        {
            recommendations.Add($"平均跳帧率 ({avgSkipRate:P1}) 超过阈值 ({MaxFrameSkipRate:P1})，建议优化帧同步机制");
        }

        if (avgMemory > MaxMemoryUsageMB * 0.8)
        {
            recommendations.Add($"平均内存使用 ({avgMemory:F1}MB) 接近上限，建议优化内存管理");
        }

        var fpsVariance = history.Select(m => m.InstantFps).ToArray().CalculateVariance();
        if (fpsVariance > 10)
        {
            recommendations.Add("FPS波动较大，建议检查系统资源竞争和线程调度");
        }

        return recommendations.ToArray();
    }

    /// <summary>
/// 获取优化效果对比
/// </summary>
    public OptimizationComparison CompareWithBaseline(PerformanceReport baseline)
    {
        var current = GeneratePerformanceReport();

        return new OptimizationComparison
        {
            BaselineFps = baseline.AverageFps,
            CurrentFps = current.AverageFps,
            FpsImprovement = current.AverageFps - baseline.AverageFps,
            FpsImprovementPercent = baseline.AverageFps > 0
                ? (current.AverageFps - baseline.AverageFps) / baseline.AverageFps * 100
                : 0,
            BaselineSkipRate = baseline.AverageFrameSkipRate,
            CurrentSkipRate = current.AverageFrameSkipRate,
            SkipRateImprovement = baseline.AverageFrameSkipRate - current.AverageFrameSkipRate,
            MemoryImprovementMB = 0 // 需要额外追踪
        };
    }

    public void Dispose()
    {
        StopMonitoring();
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
    }
}

/// <summary>
/// 性能快照
/// </summary>
public record PerformanceSnapshot
{
    public DateTime Timestamp { get; init; }
    public TimeSpan SessionDuration { get; init; }
    public double InstantFps { get; init; }
    public double AverageFps { get; init; }
    public long FramesRendered { get; init; }
    public long FramesDropped { get; init; }
    public double FrameSkipRate { get; init; }
    public double CpuUsagePercent { get; init; }
    public double MemoryUsageMB { get; init; }
    public int FrameQueueSize { get; init; }
    public int FrameQueueCapacity { get; init; }
    public double FrameQueueUtilization { get; init; }
    public double AvgRenderTimeMs { get; init; }
    public double AvgDecodeTimeMs { get; init; }
    public double LastRenderTimeMs { get; init; }
    public double LastDecodeTimeMs { get; init; }
    public KpiStatus KpiStatus { get; init; }
}

/// <summary>
/// KPI状态
/// </summary>
public enum KpiStatus
{
    Excellent,
    Good,
    Warning,
    Critical
}

/// <summary>
/// 瓶颈类型
/// </summary>
public enum BottleneckType
{
    DecodePerformance,
    RenderPerformance,
    FrameQueueFull,
    FrameQueueStarvation,
    CpuLimit,
    MemoryLimit,
    FpsDegradation
}

/// <summary>
/// KPI类型
/// </summary>
public enum KpiType
{
    FrameRate,
    FrameSkipRate,
    MemoryUsage,
    CpuUsage,
    DecodeLatency,
    RenderLatency
}

/// <summary>
/// 告警级别
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// 性能报告
/// </summary>
public record PerformanceReport
{
    public TimeSpan SessionDuration { get; init; }
    public long TotalFramesDecoded { get; init; }
    public long TotalFramesRendered { get; init; }
    public long TotalFramesDropped { get; init; }
    public double AverageFps { get; init; }
    public double MinFps { get; init; }
    public double MaxFps { get; init; }
    public double AverageFrameSkipRate { get; init; }
    public PerformanceSnapshot FinalMetrics { get; init; } = new();
    public PerformanceSnapshot[] MetricsHistory { get; init; } = Array.Empty<PerformanceSnapshot>();
    public double KpiCompliance { get; init; }
    public string[] Recommendations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// 优化效果对比
/// </summary>
public record OptimizationComparison
{
    public double BaselineFps { get; init; }
    public double CurrentFps { get; init; }
    public double FpsImprovement { get; init; }
    public double FpsImprovementPercent { get; init; }
    public double BaselineSkipRate { get; init; }
    public double CurrentSkipRate { get; init; }
    public double SkipRateImprovement { get; init; }
    public double MemoryImprovementMB { get; init; }

    public bool IsSignificantImprovement => FpsImprovementPercent > 10 || FpsImprovement > 3;
}

/// <summary>
/// KPI告警
/// </summary>
public record KpiAlert(KpiType Type, double CurrentValue, double Threshold, AlertSeverity Severity);

/// <summary>
/// 性能报告事件参数
/// </summary>
public class PerformanceReportEventArgs : EventArgs
{
    public PerformanceReport Report { get; }

    public PerformanceReportEventArgs(PerformanceReport report)
    {
        Report = report;
    }
}

/// <summary>
/// 瓶颈检测事件参数
/// </summary>
public class BottleneckDetectedEventArgs : EventArgs
{
    public BottleneckType[] Bottlenecks { get; }
    public PerformanceSnapshot Metrics { get; }
    public string[] Suggestions { get; }

    public BottleneckDetectedEventArgs(BottleneckType[] bottlenecks, PerformanceSnapshot metrics, string[] suggestions)
    {
        Bottlenecks = bottlenecks;
        Metrics = metrics;
        Suggestions = suggestions;
    }
}

/// <summary>
/// KPI告警事件参数
/// </summary>
public class KpiAlertEventArgs : EventArgs
{
    public KpiAlert[] Alerts { get; }
    public PerformanceSnapshot Metrics { get; }

    public KpiAlertEventArgs(KpiAlert[] alerts, PerformanceSnapshot metrics)
    {
        Alerts = alerts;
        Metrics = metrics;
    }
}

/// <summary>
/// 统计扩展方法
/// </summary>
public static class StatisticsExtensions
{
    public static double CalculateVariance(this double[] values)
    {
        if (values.Length < 2) return 0;
        var avg = values.Average();
        return values.Average(v => Math.Pow(v - avg, 2));
    }
}
