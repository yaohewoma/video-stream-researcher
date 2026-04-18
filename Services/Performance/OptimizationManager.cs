using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace video_stream_researcher.Services;

/// <summary>
/// 优化管理器 - 持续优化流程的核心控制器
/// 负责协调性能监控、瓶颈分析、优化实施和效果验证
/// </summary>
public sealed class OptimizationManager : IDisposable
{
    private readonly PerformanceMonitor _monitor;
    private readonly OptimizationLogger _logger;
    private readonly List<OptimizationIteration> _iterations;
    private readonly object _lock = new();

    private PerformanceReport? _baselineReport;
    private bool _isOptimizing;
    private OptimizationStrategy _currentStrategy;

    /// <summary>
/// 优化迭代完成事件
/// </summary>
    public event EventHandler<OptimizationIterationEventArgs>? IterationCompleted;

    /// <summary>
/// 优化建议生成事件
/// </summary>
    public event EventHandler<OptimizationSuggestionEventArgs>? SuggestionsGenerated;

    public OptimizationManager()
    {
        _monitor = new PerformanceMonitor();
        _logger = new OptimizationLogger();
        _iterations = new List<OptimizationIteration>();
        _currentStrategy = OptimizationStrategy.Balanced;

        // 订阅性能监控事件
        _monitor.BottleneckDetected += OnBottleneckDetected;
        _monitor.KpiAlertTriggered += OnKpiAlertTriggered;
        _monitor.PerformanceReportGenerated += OnPerformanceReportGenerated;
    }

    /// <summary>
/// 启动持续优化流程
/// </summary>
    public void StartContinuousOptimization()
    {
        lock (_lock)
        {
            if (_isOptimizing) return;

            _isOptimizing = true;
            _monitor.StartMonitoring();

            Debug.WriteLine("[优化管理器] 持续优化流程已启动");
        }
    }

    /// <summary>
/// 停止持续优化流程
/// </summary>
    public async Task<OptimizationSummary> StopContinuousOptimizationAsync()
    {
        lock (_lock)
        {
            if (!_isOptimizing) return new OptimizationSummary();

            _isOptimizing = false;
        }

        _monitor.StopMonitoring();

        // 生成优化总结报告
        var summary = await GenerateOptimizationSummaryAsync();
        await _logger.SaveOptimizationLogAsync(_iterations, summary);

        Debug.WriteLine("[优化管理器] 持续优化流程已停止");

        return summary;
    }

    /// <summary>
/// 设置基线性能报告
/// </summary>
    public void SetBaseline(PerformanceReport baseline)
    {
        _baselineReport = baseline;
        Debug.WriteLine($"[优化管理器] 基线已设置: FPS={baseline.AverageFps:F1}");
    }

    /// <summary>
/// 设置优化策略
/// </summary>
    public void SetStrategy(OptimizationStrategy strategy)
    {
        _currentStrategy = strategy;
        Debug.WriteLine($"[优化管理器] 优化策略已切换: {strategy}");
    }

    /// <summary>
/// 记录优化迭代
/// </summary>
    public void RecordOptimizationIteration(
        string description,
        OptimizationType type,
        Dictionary<string, object> parameters)
    {
        lock (_lock)
        {
            var iteration = new OptimizationIteration
            {
                Id = _iterations.Count + 1,
                Timestamp = DateTime.UtcNow,
                Description = description,
                Type = type,
                Parameters = parameters,
                MetricsBefore = _monitor.GeneratePerformanceReport(),
                Strategy = _currentStrategy
            };

            _iterations.Add(iteration);

            Debug.WriteLine($"[优化管理器] 记录优化迭代 #{iteration.Id}: {description}");
        }
    }

    /// <summary>
/// 完成优化迭代并记录结果
/// </summary>
    public void CompleteOptimizationIteration(
        int iterationId,
        bool success,
        Dictionary<string, object> results)
    {
        lock (_lock)
        {
            var iteration = _iterations.FirstOrDefault(i => i.Id == iterationId);
            if (iteration == null) return;

            iteration.MetricsAfter = _monitor.GeneratePerformanceReport();
            iteration.Success = success;
            iteration.Results = results;
            iteration.CompletionTimestamp = DateTime.UtcNow;

            // 计算改进效果
            if (iteration.MetricsBefore != null && iteration.MetricsAfter != null)
            {
                iteration.Improvement = _monitor.CompareWithBaseline(iteration.MetricsBefore);
            }

            IterationCompleted?.Invoke(this, new OptimizationIterationEventArgs(iteration));

            Debug.WriteLine($"[优化管理器] 优化迭代 #{iterationId} 已完成: {(success ? "成功" : "失败")}");
        }
    }

    /// <summary>
/// 获取当前性能指标
/// </summary>
    public PerformanceSnapshot GetCurrentMetrics() => _monitor.CurrentMetrics;

    /// <summary>
/// 获取优化历史
/// </summary>
    public IReadOnlyList<OptimizationIteration> GetOptimizationHistory()
    {
        lock (_lock)
        {
            return _iterations.AsReadOnly();
        }
    }

    /// <summary>
/// 生成优化建议
/// </summary>
    public OptimizationSuggestion[] GenerateOptimizationSuggestions()
    {
        var suggestions = new List<OptimizationSuggestion>();
        var currentMetrics = _monitor.CurrentMetrics;
        var history = _monitor.GeneratePerformanceReport();

        // 基于当前指标生成建议
        if (currentMetrics.InstantFps < PerformanceMonitor.MinAcceptableFps)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Priority = OptimizationPriority.High,
                Category = OptimizationCategory.Performance,
                Title = "提升帧率",
                Description = $"当前FPS ({currentMetrics.InstantFps:F1}) 低于最低要求 ({PerformanceMonitor.MinAcceptableFps})",
                Actions = new[]
                {
                    "启用硬件加速解码",
                    "优化NV12转换算法",
                    "调整帧队列大小",
                    "减少渲染开销"
                },
                ExpectedImpact = "预计提升3-5 FPS"
            });
        }

        if (currentMetrics.FrameQueueUtilization > 0.9)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Priority = OptimizationPriority.High,
                Category = OptimizationCategory.ResourceManagement,
                Title = "优化帧队列",
                Description = "帧队列利用率过高，可能导致延迟增加",
                Actions = new[]
                {
                    "增加解码线程优先级",
                    "优化解码循环效率",
                    "调整预缓冲策略"
                },
                ExpectedImpact = "减少队列等待时间"
            });
        }

        if (currentMetrics.AvgRenderTimeMs > 20)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Priority = OptimizationPriority.Medium,
                Category = OptimizationCategory.Rendering,
                Title = "优化渲染性能",
                Description = $"平均渲染时间 ({currentMetrics.AvgRenderTimeMs:F1}ms) 过长",
                Actions = new[]
                {
                    "优化WriteableBitmap更新",
                    "减少UI刷新频率",
                    "使用批量渲染"
                },
                ExpectedImpact = "减少渲染延迟"
            });
        }

        if (currentMetrics.CpuUsagePercent > PerformanceMonitor.MaxCpuUsagePercent)
        {
            suggestions.Add(new OptimizationSuggestion
            {
                Priority = OptimizationPriority.Medium,
                Category = OptimizationCategory.ResourceManagement,
                Title = "降低CPU占用",
                Description = $"CPU使用率 ({currentMetrics.CpuUsagePercent:F1}%) 过高",
                Actions = new[]
                {
                    "优化算法复杂度",
                    "使用硬件解码",
                    "调整线程优先级"
                },
                ExpectedImpact = "降低CPU负载"
            });
        }

        // 基于历史趋势生成建议
        if (history.MetricsHistory.Length >= 3)
        {
            var fpsTrend = history.MetricsHistory.Select(m => m.InstantFps).ToArray();
            if (fpsTrend[0] > fpsTrend[^1] * 1.2)
            {
                suggestions.Add(new OptimizationSuggestion
                {
                    Priority = OptimizationPriority.High,
                    Category = OptimizationCategory.Stability,
                    Title = "解决性能下降",
                    Description = "检测到FPS持续下降趋势",
                    Actions = new[]
                    {
                        "检查内存泄漏",
                        "优化资源释放",
                        "实施定期垃圾回收"
                    },
                    ExpectedImpact = "恢复稳定性能"
                });
            }
        }

        var result = suggestions.OrderBy(s => s.Priority).ToArray();
        SuggestionsGenerated?.Invoke(this, new OptimizationSuggestionEventArgs(result));

        return result;
    }

    /// <summary>
/// 验证优化效果
/// </summary>
    public OptimizationValidationResult ValidateOptimization(PerformanceReport before, PerformanceReport after)
    {
        var comparison = _monitor.CompareWithBaseline(before);
        var improvements = new List<string>();
        var regressions = new List<string>();

        // 验证FPS改进
        if (after.AverageFps > before.AverageFps * 1.05)
        {
            improvements.Add($"FPS提升: {before.AverageFps:F1} -> {after.AverageFps:F1} (+{comparison.FpsImprovementPercent:F1}%)");
        }
        else if (after.AverageFps < before.AverageFps * 0.95)
        {
            regressions.Add($"FPS下降: {before.AverageFps:F1} -> {after.AverageFps:F1}");
        }

        // 验证跳帧率改进
        if (after.AverageFrameSkipRate < before.AverageFrameSkipRate * 0.8)
        {
            improvements.Add($"跳帧率改善: {before.AverageFrameSkipRate:P1} -> {after.AverageFrameSkipRate:P1}");
        }
        else if (after.AverageFrameSkipRate > before.AverageFrameSkipRate * 1.2)
        {
            regressions.Add($"跳帧率恶化: {before.AverageFrameSkipRate:P1} -> {after.AverageFrameSkipRate:P1}");
        }

        // 验证KPI合规率
        if (after.KpiCompliance > before.KpiCompliance + 0.1)
        {
            improvements.Add($"KPI合规率提升: {before.KpiCompliance:P1} -> {after.KpiCompliance:P1}");
        }

        var isSuccessful = after.AverageFps >= PerformanceMonitor.MinAcceptableFps
            && after.AverageFrameSkipRate <= PerformanceMonitor.MaxFrameSkipRate
            && improvements.Count > regressions.Count;

        return new OptimizationValidationResult
        {
            IsSuccessful = isSuccessful,
            Improvements = improvements.ToArray(),
            Regressions = regressions.ToArray(),
            Comparison = comparison
        };
    }

    /// <summary>
/// 瓶颈检测事件处理
/// </summary>
    private void OnBottleneckDetected(object? sender, BottleneckDetectedEventArgs e)
    {
        Debug.WriteLine($"[优化管理器] 检测到瓶颈: {string.Join(", ", e.Bottlenecks)}");

        foreach (var suggestion in e.Suggestions)
        {
            Debug.WriteLine($"[优化管理器] 建议: {suggestion}");
        }

        // 记录瓶颈信息
        _logger.LogBottleneckDetected(e.Bottlenecks, e.Metrics, e.Suggestions);
    }

    /// <summary>
/// KPI告警事件处理
/// </summary>
    private void OnKpiAlertTriggered(object? sender, KpiAlertEventArgs e)
    {
        foreach (var alert in e.Alerts)
        {
            Debug.WriteLine($"[优化管理器] KPI告警 [{alert.Severity}]: {alert.Type} = {alert.CurrentValue:F2} (阈值: {alert.Threshold:F2})");
        }

        _logger.LogKpiAlert(e.Alerts, e.Metrics);
    }

    /// <summary>
/// 性能报告生成事件处理
/// </summary>
    private void OnPerformanceReportGenerated(object? sender, PerformanceReportEventArgs e)
    {
        Debug.WriteLine($"[优化管理器] 性能报告生成: 平均FPS={e.Report.AverageFps:F1}, KPI合规率={e.Report.KpiCompliance:P1}");
    }

    /// <summary>
/// 生成优化总结
/// </summary>
    private async Task<OptimizationSummary> GenerateOptimizationSummaryAsync()
    {
        var finalReport = _monitor.GeneratePerformanceReport();

        var summary = new OptimizationSummary
        {
            TotalIterations = _iterations.Count,
            SuccessfulIterations = _iterations.Count(i => i.Success),
            FailedIterations = _iterations.Count(i => !i.Success),
            TotalDuration = _iterations.Count > 0
                ? (_iterations.Last().CompletionTimestamp - _iterations.First().Timestamp)
                : TimeSpan.Zero,
            BaselineMetrics = _baselineReport,
            FinalMetrics = finalReport,
            OverallImprovement = _baselineReport != null
                ? _monitor.CompareWithBaseline(_baselineReport)
                : null,
            Iterations = _iterations.ToArray()
        };

        return summary;
    }

    public void Dispose()
    {
        _monitor.Dispose();
        _logger.Dispose();
    }
}

/// <summary>
/// 优化迭代记录
/// </summary>
public class OptimizationIteration
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime CompletionTimestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public OptimizationType Type { get; set; }
    public OptimizationStrategy Strategy { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, object> Results { get; set; } = new();
    public PerformanceReport? MetricsBefore { get; set; }
    public PerformanceReport? MetricsAfter { get; set; }
    public OptimizationComparison? Improvement { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// 优化类型
/// </summary>
public enum OptimizationType
{
    Algorithm,
    Configuration,
    ResourceManagement,
    Rendering,
    Decoding,
    Synchronization,
    MemoryManagement
}

/// <summary>
/// 优化策略
/// </summary>
public enum OptimizationStrategy
{
    Conservative,  // 保守策略：小步快跑，低风险
    Balanced,      // 平衡策略：适度优化
    Aggressive     // 激进策略：大幅改动，追求极致性能
}

/// <summary>
/// 优化优先级
/// </summary>
public enum OptimizationPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// 优化类别
/// </summary>
public enum OptimizationCategory
{
    Performance,
    ResourceManagement,
    Rendering,
    Decoding,
    Stability,
    MemoryManagement
}

/// <summary>
/// 优化建议
/// </summary>
public class OptimizationSuggestion
{
    public OptimizationPriority Priority { get; set; }
    public OptimizationCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Actions { get; set; } = Array.Empty<string>();
    public string ExpectedImpact { get; set; } = string.Empty;
}

/// <summary>
/// 优化验证结果
/// </summary>
public class OptimizationValidationResult
{
    public bool IsSuccessful { get; set; }
    public string[] Improvements { get; set; } = Array.Empty<string>();
    public string[] Regressions { get; set; } = Array.Empty<string>();
    public OptimizationComparison Comparison { get; set; } = new();
}

/// <summary>
/// 优化总结
/// </summary>
public class OptimizationSummary
{
    public int TotalIterations { get; set; }
    public int SuccessfulIterations { get; set; }
    public int FailedIterations { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public PerformanceReport? BaselineMetrics { get; set; }
    public PerformanceReport? FinalMetrics { get; set; }
    public OptimizationComparison? OverallImprovement { get; set; }
    public OptimizationIteration[] Iterations { get; set; } = Array.Empty<OptimizationIteration>();
}

/// <summary>
/// 优化迭代事件参数
/// </summary>
public class OptimizationIterationEventArgs : EventArgs
{
    public OptimizationIteration Iteration { get; }

    public OptimizationIterationEventArgs(OptimizationIteration iteration)
    {
        Iteration = iteration;
    }
}

/// <summary>
/// 优化建议事件参数
/// </summary>
public class OptimizationSuggestionEventArgs : EventArgs
{
    public OptimizationSuggestion[] Suggestions { get; }

    public OptimizationSuggestionEventArgs(OptimizationSuggestion[] suggestions)
    {
        Suggestions = suggestions;
    }
}

/// <summary>
/// 优化日志记录器
/// </summary>
public class OptimizationLogger : IDisposable
{
    private readonly string _logDirectory;
    private readonly List<LogEntry> _entries;
    private readonly object _lock = new();

    public OptimizationLogger()
    {
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OptimizationLogs");
        Directory.CreateDirectory(_logDirectory);
        _entries = new List<LogEntry>();
    }

    public void LogBottleneckDetected(BottleneckType[] bottlenecks, PerformanceSnapshot metrics, string[] suggestions)
    {
        lock (_lock)
        {
            _entries.Add(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Type = LogEntryType.BottleneckDetected,
                Message = $"检测到瓶颈: {string.Join(", ", bottlenecks)}",
                Data = new Dictionary<string, object>
                {
                    ["Bottlenecks"] = bottlenecks,
                    ["Metrics"] = metrics,
                    ["Suggestions"] = suggestions
                }
            });
        }
    }

    public void LogKpiAlert(KpiAlert[] alerts, PerformanceSnapshot metrics)
    {
        lock (_lock)
        {
            foreach (var alert in alerts)
            {
                _entries.Add(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Type = LogEntryType.KpiAlert,
                    Message = $"KPI告警: {alert.Type} = {alert.CurrentValue:F2}",
                    Severity = alert.Severity switch
                    {
                        AlertSeverity.Critical => LogSeverity.Error,
                        AlertSeverity.Warning => LogSeverity.Warning,
                        _ => LogSeverity.Info
                    },
                    Data = new Dictionary<string, object>
                    {
                        ["Alert"] = alert,
                        ["Metrics"] = metrics
                    }
                });
            }
        }
    }

    public async Task SaveOptimizationLogAsync(List<OptimizationIteration> iterations, OptimizationSummary summary)
    {
        var logFile = Path.Combine(_logDirectory, $"optimization_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");

        var logData = new
        {
            Timestamp = DateTime.UtcNow,
            Summary = summary,
            Iterations = iterations,
            Events = _entries
        };

        var json = JsonSerializer.Serialize(logData, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(logFile, json);

        Debug.WriteLine($"[优化日志] 已保存到: {logFile}");
    }

    public void Dispose()
    {
        // 清理资源
    }
}

/// <summary>
/// 日志条目
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogEntryType Type { get; set; }
    public LogSeverity Severity { get; set; } = LogSeverity.Info;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// 日志条目类型
/// </summary>
public enum LogEntryType
{
    BottleneckDetected,
    KpiAlert,
    OptimizationApplied,
    ValidationResult
}

/// <summary>
/// 日志严重级别
/// </summary>
public enum LogSeverity
{
    Info,
    Warning,
    Error
}
