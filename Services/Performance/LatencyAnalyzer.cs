using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace video_stream_researcher.Services;

/// <summary>
/// 延迟分析器 - 测量和分析系统各模块延迟
/// 用于识别同步调整累积的根本原因
/// </summary>
public sealed class LatencyAnalyzer : IDisposable
{
    // 延迟阈值定义（毫秒）
    public const double MaxAcceptableSyncDriftMs = 50;    // 最大可接受同步漂移
    public const double MaxAcceptableDecodeLatencyMs = 30; // 最大可接受解码延迟
    public const double MaxAcceptableRenderLatencyMs = 20; // 最大可接受渲染延迟
    public const double MaxAcceptableAudioLatencyMs = 40;  // 最大可接受音频延迟

    private readonly Dictionary<LatencyType, LatencyMetrics> _metrics;
    private readonly Queue<SyncDriftRecord> _syncDriftHistory;
    private readonly object _lock = new();
    private Stopwatch? _sessionStopwatch;

    // 当前延迟状态
    public LatencyReport CurrentReport { get; private set; }

    // 事件
    public event EventHandler<LatencyAlertEventArgs>? LatencyAlert;
    public event EventHandler<SyncDriftEventArgs>? SyncDriftDetected;

    public LatencyAnalyzer()
    {
        _metrics = new Dictionary<LatencyType, LatencyMetrics>();
        _syncDriftHistory = new Queue<SyncDriftRecord>();
        CurrentReport = new LatencyReport();

        // 初始化各类型指标
        foreach (LatencyType type in Enum.GetValues<LatencyType>())
        {
            _metrics[type] = new LatencyMetrics(type);
        }
    }

    /// <summary>
    /// 开始延迟分析会话
    /// </summary>
    public void StartAnalysis()
    {
        lock (_lock)
        {
            _sessionStopwatch = Stopwatch.StartNew();
            foreach (var metric in _metrics.Values)
            {
                metric.Reset();
            }
            _syncDriftHistory.Clear();

            Debug.WriteLine("[延迟分析] 分析会话已启动");
        }
    }

    /// <summary>
    /// 记录解码延迟
    /// </summary>
    public void RecordDecodeLatency(TimeSpan latency, long frameTimestampHns)
    {
        RecordLatency(LatencyType.Decode, latency, frameTimestampHns);
    }

    /// <summary>
    /// 记录渲染延迟
    /// </summary>
    public void RecordRenderLatency(TimeSpan latency, long frameTimestampHns)
    {
        RecordLatency(LatencyType.Render, latency, frameTimestampHns);
    }

    /// <summary>
    /// 记录音频延迟
    /// </summary>
    public void RecordAudioLatency(TimeSpan latency, long timestampHns)
    {
        RecordLatency(LatencyType.Audio, latency, timestampHns);
    }

    /// <summary>
    /// 记录帧队列延迟
    /// </summary>
    public void RecordQueueLatency(int queueSize, TimeSpan waitTime)
    {
        var latency = TimeSpan.FromMilliseconds(queueSize * 41.67); // 假设24fps，每帧约41.67ms
        RecordLatency(LatencyType.FrameQueue, latency, 0);
    }

    /// <summary>
    /// 记录同步漂移
    /// </summary>
    public void RecordSyncDrift(double driftMs, long videoTimestampHns, long audioTimestampHns)
    {
        lock (_lock)
        {
            var record = new SyncDriftRecord
            {
                Timestamp = DateTime.UtcNow,
                DriftMs = driftMs,
                VideoTimestampHns = videoTimestampHns,
                AudioTimestampHns = audioTimestampHns,
                SessionElapsed = _sessionStopwatch?.Elapsed ?? TimeSpan.Zero
            };

            _syncDriftHistory.Enqueue(record);

            // 保持最近100条记录
            while (_syncDriftHistory.Count > 100)
            {
                _syncDriftHistory.Dequeue();
            }

            // 检测同步漂移累积趋势
            AnalyzeSyncDriftTrend(record);

            // 检查是否超过阈值
            if (Math.Abs(driftMs) > MaxAcceptableSyncDriftMs)
            {
                LatencyAlert?.Invoke(this, new LatencyAlertEventArgs(
                    LatencyType.SyncDrift,
                    driftMs,
                    MaxAcceptableSyncDriftMs,
                    $"同步漂移超过阈值: {driftMs:F1}ms"
                ));
            }
        }
    }

    /// <summary>
    /// 记录通用延迟
    /// </summary>
    private void RecordLatency(LatencyType type, TimeSpan latency, long timestampHns)
    {
        lock (_lock)
        {
            if (_metrics.TryGetValue(type, out var metric))
            {
                metric.AddSample(latency, timestampHns);

                // 检查是否超过阈值
                var threshold = GetThresholdForType(type);
                if (latency.TotalMilliseconds > threshold)
                {
                    LatencyAlert?.Invoke(this, new LatencyAlertEventArgs(
                        type,
                        latency.TotalMilliseconds,
                        threshold,
                        $"{type}延迟超过阈值: {latency.TotalMilliseconds:F1}ms"
                    ));
                }
            }
        }
    }

    /// <summary>
    /// 分析同步漂移趋势
    /// </summary>
    private void AnalyzeSyncDriftTrend(SyncDriftRecord current)
    {
        if (_syncDriftHistory.Count < 5) return;

        var recentRecords = _syncDriftHistory.TakeLast(5).ToArray();
        var avgDrift = recentRecords.Average(r => r.DriftMs);
        var driftVariance = recentRecords.Average(r => Math.Pow(r.DriftMs - avgDrift, 2));

        // 检测持续累积趋势
        var first = recentRecords.First();
        var last = recentRecords.Last();
        var driftChange = last.DriftMs - first.DriftMs;
        var timeSpan = last.SessionElapsed - first.SessionElapsed;

        // 如果漂移持续增加或减少，说明存在系统性问题
        if (Math.Abs(driftChange) > 20 && timeSpan.TotalSeconds > 2)
        {
            var trend = driftChange > 0 ? "增加" : "减少";
            var rate = Math.Abs(driftChange) / timeSpan.TotalSeconds;

            SyncDriftDetected?.Invoke(this, new SyncDriftEventArgs
            {
                Trend = trend,
                RateMsPerSecond = rate,
                CurrentDriftMs = current.DriftMs,
                AverageDriftMs = avgDrift,
                SuggestedCorrection = -driftChange * 0.5, // 建议修正量为漂移变化的50%
                PossibleCauses = AnalyzeDriftCauses(recentRecords)
            });
        }
    }

    /// <summary>
    /// 分析漂移原因
    /// </summary>
    private string[] AnalyzeDriftCauses(SyncDriftRecord[] records)
    {
        var causes = new List<string>();

        // 检查解码延迟
        if (_metrics.TryGetValue(LatencyType.Decode, out var decodeMetric) &&
            decodeMetric.AverageLatencyMs > MaxAcceptableDecodeLatencyMs)
        {
            causes.Add("解码延迟过高，导致视频落后于音频");
        }

        // 检查渲染延迟
        if (_metrics.TryGetValue(LatencyType.Render, out var renderMetric) &&
            renderMetric.AverageLatencyMs > MaxAcceptableRenderLatencyMs)
        {
            causes.Add("渲染延迟过高，影响视频显示时机");
        }

        // 检查帧队列
        if (_metrics.TryGetValue(LatencyType.FrameQueue, out var queueMetric) &&
            queueMetric.AverageLatencyMs > 100)
        {
            causes.Add("帧队列等待时间过长，造成延迟累积");
        }

        // 检查漂移方向
        var avgDrift = records.Average(r => r.DriftMs);
        if (avgDrift > 50)
        {
            causes.Add("视频明显落后于音频，需要加快视频播放或减慢音频");
        }
        else if (avgDrift < -50)
        {
            causes.Add("视频明显超前于音频，需要减慢视频播放或加快音频");
        }

        if (causes.Count == 0)
        {
            causes.Add("可能是时钟源不同步或系统资源竞争导致");
        }

        return causes.ToArray();
    }

    /// <summary>
    /// 获取延迟类型的阈值
    /// </summary>
    private static double GetThresholdForType(LatencyType type)
    {
        return type switch
        {
            LatencyType.Decode => MaxAcceptableDecodeLatencyMs,
            LatencyType.Render => MaxAcceptableRenderLatencyMs,
            LatencyType.Audio => MaxAcceptableAudioLatencyMs,
            LatencyType.FrameQueue => 100,
            LatencyType.SyncDrift => MaxAcceptableSyncDriftMs,
            _ => 50
        };
    }

    /// <summary>
    /// 生成延迟分析报告
    /// </summary>
    public LatencyReport GenerateReport()
    {
        lock (_lock)
        {
            var report = new LatencyReport
            {
                SessionDuration = _sessionStopwatch?.Elapsed ?? TimeSpan.Zero,
                Metrics = _metrics.Values.Select(m => m.GetSnapshot()).ToArray(),
                SyncDriftHistory = _syncDriftHistory.ToArray(),
                Bottlenecks = IdentifyBottlenecks(),
                Recommendations = GenerateRecommendations()
            };

            CurrentReport = report;
            return report;
        }
    }

    /// <summary>
    /// 识别性能瓶颈
    /// </summary>
    private LatencyBottleneck[] IdentifyBottlenecks()
    {
        var bottlenecks = new List<LatencyBottleneck>();

        foreach (var metric in _metrics.Values)
        {
            var snapshot = metric.GetSnapshot();
            var threshold = GetThresholdForType(snapshot.Type);

            if (snapshot.AverageLatencyMs > threshold * 1.5)
            {
                bottlenecks.Add(new LatencyBottleneck
                {
                    Type = snapshot.Type,
                    Severity = snapshot.AverageLatencyMs > threshold * 2 ? BottleneckSeverity.Critical : BottleneckSeverity.Warning,
                    AverageLatencyMs = snapshot.AverageLatencyMs,
                    MaxLatencyMs = snapshot.MaxLatencyMs,
                    ThresholdMs = threshold,
                    Impact = CalculateImpact(snapshot.Type, snapshot.AverageLatencyMs)
                });
            }
        }

        // 检查同步漂移
        if (_syncDriftHistory.Count > 0)
        {
            var avgDrift = _syncDriftHistory.Average(r => Math.Abs(r.DriftMs));
            if (avgDrift > MaxAcceptableSyncDriftMs)
            {
                bottlenecks.Add(new LatencyBottleneck
                {
                    Type = LatencyType.SyncDrift,
                    Severity = avgDrift > MaxAcceptableSyncDriftMs * 2 ? BottleneckSeverity.Critical : BottleneckSeverity.Warning,
                    AverageLatencyMs = avgDrift,
                    MaxLatencyMs = _syncDriftHistory.Max(r => Math.Abs(r.DriftMs)),
                    ThresholdMs = MaxAcceptableSyncDriftMs,
                    Impact = "音视频不同步，影响观看体验"
                });
            }
        }

        return bottlenecks.OrderByDescending(b => b.Severity).ToArray();
    }

    /// <summary>
    /// 计算影响描述
    /// </summary>
    private static string CalculateImpact(LatencyType type, double avgLatencyMs)
    {
        return type switch
        {
            LatencyType.Decode => $"解码延迟导致视频播放不流畅，当前延迟{avgLatencyMs:F1}ms",
            LatencyType.Render => $"渲染延迟影响画面更新速度，当前延迟{avgLatencyMs:F1}ms",
            LatencyType.Audio => $"音频延迟影响音画同步，当前延迟{avgLatencyMs:F1}ms",
            LatencyType.FrameQueue => $"帧队列延迟造成画面卡顿，当前延迟{avgLatencyMs:F1}ms",
            _ => $"系统延迟过高，当前延迟{avgLatencyMs:F1}ms"
        };
    }

    /// <summary>
    /// 生成优化建议
    /// </summary>
    private string[] GenerateRecommendations()
    {
        var recommendations = new List<string>();
        var bottlenecks = IdentifyBottlenecks();

        foreach (var bottleneck in bottlenecks)
        {
            switch (bottleneck.Type)
            {
                case LatencyType.Decode:
                    recommendations.Add("[解码优化] 启用硬件加速解码，优化NV12转换算法，减少解码线程阻塞");
                    break;
                case LatencyType.Render:
                    recommendations.Add("[渲染优化] 减少UI更新频率，优化WriteableBitmap更新策略，使用双缓冲");
                    break;
                case LatencyType.FrameQueue:
                    recommendations.Add("[队列优化] 调整帧队列大小，优化解码和渲染的同步机制");
                    break;
                case LatencyType.SyncDrift:
                    recommendations.Add("[同步优化] 实施动态同步调整，使用音频时钟作为主时钟，定期校准视频位置");
                    break;
            }
        }

        // 通用建议
        if (bottlenecks.Length == 0)
        {
            recommendations.Add("系统延迟表现良好，继续保持当前配置");
        }
        else
        {
            recommendations.Add("[通用优化] 监控系统资源使用情况，避免后台任务干扰，确保足够的CPU/内存资源");
        }

        return recommendations.ToArray();
    }

    public void Dispose()
    {
        _sessionStopwatch?.Stop();
    }
}

/// <summary>
/// 延迟类型
/// </summary>
public enum LatencyType
{
    Decode,      // 解码延迟
    Render,      // 渲染延迟
    Audio,       // 音频延迟
    FrameQueue,  // 帧队列延迟
    SyncDrift    // 同步漂移
}

/// <summary>
/// 延迟指标
/// </summary>
public class LatencyMetrics
{
    private readonly List<LatencySample> _samples;
    private readonly object _lock = new();

    public LatencyType Type { get; }
    public double AverageLatencyMs { get; private set; }
    public double MaxLatencyMs { get; private set; }
    public double MinLatencyMs { get; private set; }

    public LatencyMetrics(LatencyType type)
    {
        Type = type;
        _samples = new List<LatencySample>();
        Reset();
    }

    public void AddSample(TimeSpan latency, long timestampHns)
    {
        lock (_lock)
        {
            _samples.Add(new LatencySample
            {
                LatencyMs = latency.TotalMilliseconds,
                TimestampHns = timestampHns,
                RecordedAt = DateTime.UtcNow
            });

            // 只保留最近100个样本
            while (_samples.Count > 100)
            {
                _samples.RemoveAt(0);
            }

            // 重新计算统计值
            if (_samples.Count > 0)
            {
                AverageLatencyMs = _samples.Average(s => s.LatencyMs);
                MaxLatencyMs = _samples.Max(s => s.LatencyMs);
                MinLatencyMs = _samples.Min(s => s.LatencyMs);
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _samples.Clear();
            AverageLatencyMs = 0;
            MaxLatencyMs = 0;
            MinLatencyMs = double.MaxValue;
        }
    }

    public LatencyMetricsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new LatencyMetricsSnapshot
            {
                Type = Type,
                AverageLatencyMs = AverageLatencyMs,
                MaxLatencyMs = MaxLatencyMs,
                MinLatencyMs = MinLatencyMs == double.MaxValue ? 0 : MinLatencyMs,
                SampleCount = _samples.Count,
                RecentSamples = _samples.TakeLast(10).Select(s => s.LatencyMs).ToArray()
            };
        }
    }
}

/// <summary>
/// 延迟样本
/// </summary>
public class LatencySample
{
    public double LatencyMs { get; set; }
    public long TimestampHns { get; set; }
    public DateTime RecordedAt { get; set; }
}

/// <summary>
/// 同步漂移记录
/// </summary>
public class SyncDriftRecord
{
    public DateTime Timestamp { get; set; }
    public double DriftMs { get; set; }
    public long VideoTimestampHns { get; set; }
    public long AudioTimestampHns { get; set; }
    public TimeSpan SessionElapsed { get; set; }
}

/// <summary>
/// 延迟指标快照
/// </summary>
public class LatencyMetricsSnapshot
{
    public LatencyType Type { get; set; }
    public double AverageLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public int SampleCount { get; set; }
    public double[] RecentSamples { get; set; } = Array.Empty<double>();
}

/// <summary>
/// 延迟瓶颈
/// </summary>
public class LatencyBottleneck
{
    public LatencyType Type { get; set; }
    public BottleneckSeverity Severity { get; set; }
    public double AverageLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double ThresholdMs { get; set; }
    public string Impact { get; set; } = string.Empty;
}

/// <summary>
/// 瓶颈严重程度
/// </summary>
public enum BottleneckSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// 延迟分析报告
/// </summary>
public class LatencyReport
{
    public TimeSpan SessionDuration { get; set; }
    public LatencyMetricsSnapshot[] Metrics { get; set; } = Array.Empty<LatencyMetricsSnapshot>();
    public SyncDriftRecord[] SyncDriftHistory { get; set; } = Array.Empty<SyncDriftRecord>();
    public LatencyBottleneck[] Bottlenecks { get; set; } = Array.Empty<LatencyBottleneck>();
    public string[] Recommendations { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 延迟告警事件参数
/// </summary>
public class LatencyAlertEventArgs : EventArgs
{
    public LatencyType Type { get; }
    public double CurrentValue { get; }
    public double Threshold { get; }
    public string Message { get; }

    public LatencyAlertEventArgs(LatencyType type, double currentValue, double threshold, string message)
    {
        Type = type;
        CurrentValue = currentValue;
        Threshold = threshold;
        Message = message;
    }
}

/// <summary>
/// 同步漂移事件参数
/// </summary>
public class SyncDriftEventArgs : EventArgs
{
    public string Trend { get; set; } = string.Empty;
    public double RateMsPerSecond { get; set; }
    public double CurrentDriftMs { get; set; }
    public double AverageDriftMs { get; set; }
    public double SuggestedCorrection { get; set; }
    public string[] PossibleCauses { get; set; } = Array.Empty<string>();
}
