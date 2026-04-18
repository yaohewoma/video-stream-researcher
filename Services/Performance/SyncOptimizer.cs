using System;
using System.Diagnostics;
using System.Threading;

namespace video_stream_researcher.Services;

/// <summary>
/// 同步优化器 - 解决音视频同步调整累积问题
/// 采用音频主时钟策略，动态调整视频播放速度
/// </summary>
public sealed class SyncOptimizer : IDisposable
{
    // 同步参数
    private const double MaxSyncAdjustmentMs = 100;      // 最大同步调整阈值
    private const double SyncCorrectionFactor = 0.3;     // 同步修正系数（每次修正30%）
    private const double MinPlaybackSpeed = 0.95;        // 最小播放速度
    private const double MaxPlaybackSpeed = 1.05;        // 最大播放速度
    private const int SyncCheckIntervalMs = 500;         // 同步检查间隔
    private const int DriftHistorySize = 10;             // 漂移历史记录大小

    // 状态
    private readonly object _lock = new();
    private readonly Stopwatch _syncStopwatch;
    private readonly double[] _driftHistory;
    private int _driftIndex;
    private bool _isRunning;

    // 音频时钟（主时钟）
    private long _audioStartPositionHns;
    private long _audioStartTimeMs;

    // 视频时钟
    private long _videoStartPositionHns;
    private long _videoStartTimeMs;

    // 当前同步状态
    public SyncState CurrentState { get; private set; }
    public double CurrentDriftMs { get; private set; }
    public double AverageDriftMs { get; private set; }
    public double PlaybackSpeed { get; private set; } = 1.0;

    // 事件
    public event EventHandler<SyncAdjustmentEventArgs>? SyncAdjustmentApplied;
    public event EventHandler<SyncDriftWarningEventArgs>? SyncDriftWarning;

    public SyncOptimizer()
    {
        _syncStopwatch = new Stopwatch();
        _driftHistory = new double[DriftHistorySize];
        CurrentState = SyncState.Initializing;
    }

    /// <summary>
    /// 启动同步优化
    /// </summary>
    public void StartSync()
    {
        lock (_lock)
        {
            if (_isRunning) return;

            _isRunning = true;
            _syncStopwatch.Restart();
            _driftIndex = 0;
            Array.Clear(_driftHistory, 0, _driftHistory.Length);
            CurrentDriftMs = 0;
            AverageDriftMs = 0;
            PlaybackSpeed = 1.0;
            CurrentState = SyncState.Synchronizing;

            Debug.WriteLine("[同步优化] 同步优化已启动");
        }
    }

    /// <summary>
    /// 停止同步优化
    /// </summary>
    public void StopSync()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            _isRunning = false;
            _syncStopwatch.Stop();
            CurrentState = SyncState.Stopped;
            PlaybackSpeed = 1.0;

            Debug.WriteLine("[同步优化] 同步优化已停止");
        }
    }

    /// <summary>
    /// 初始化音频时钟（主时钟）
    /// </summary>
    public void InitializeAudioClock(long positionHns)
    {
        lock (_lock)
        {
            _audioStartPositionHns = positionHns;
            _audioStartTimeMs = _syncStopwatch.ElapsedMilliseconds;

            Debug.WriteLine($"[同步优化] 音频时钟初始化: 位置={positionHns / 10000}ms");
        }
    }

    /// <summary>
    /// 初始化视频时钟
    /// </summary>
    public void InitializeVideoClock(long positionHns)
    {
        lock (_lock)
        {
            _videoStartPositionHns = positionHns;
            _videoStartTimeMs = _syncStopwatch.ElapsedMilliseconds;

            Debug.WriteLine($"[同步优化] 视频时钟初始化: 位置={positionHns / 10000}ms");
        }
    }

    /// <summary>
    /// 获取当前音频位置（基于音频时钟）
    /// </summary>
    public long GetAudioPositionHns()
    {
        lock (_lock)
        {
            if (!_isRunning) return _audioStartPositionHns;

            var elapsedMs = _syncStopwatch.ElapsedMilliseconds - _audioStartTimeMs;
            return _audioStartPositionHns + (long)(elapsedMs * 10000 * PlaybackSpeed);
        }
    }

    /// <summary>
    /// 获取当前视频目标位置
    /// </summary>
    public long GetVideoTargetPositionHns(long currentVideoPositionHns)
    {
        lock (_lock)
        {
            if (!_isRunning) return currentVideoPositionHns;

            // 计算当前音频位置
            var audioPositionHns = GetAudioPositionHns();

            // 计算漂移
            var driftHns = currentVideoPositionHns - audioPositionHns;
            var driftMs = driftHns / 10000.0;

            // 更新漂移历史
            UpdateDriftHistory(driftMs);

            // 检查是否需要调整
            if (Math.Abs(driftMs) > MaxSyncAdjustmentMs)
            {
                // 漂移过大，需要警告
                SyncDriftWarning?.Invoke(this, new SyncDriftWarningEventArgs
                {
                    DriftMs = driftMs,
                    ThresholdMs = MaxSyncAdjustmentMs,
                    Message = $"同步漂移过大: {driftMs:F1}ms"
                });
            }

            // 应用动态播放速度调整
            var adjustedSpeed = CalculatePlaybackSpeed(driftMs);

            if (Math.Abs(adjustedSpeed - PlaybackSpeed) > 0.001)
            {
                PlaybackSpeed = adjustedSpeed;

                SyncAdjustmentApplied?.Invoke(this, new SyncAdjustmentEventArgs
                {
                    PreviousSpeed = PlaybackSpeed,
                    NewSpeed = adjustedSpeed,
                    DriftMs = driftMs,
                    Reason = $"同步调整: 漂移={driftMs:F1}ms"
                });

                Debug.WriteLine($"[同步优化] 播放速度调整: {PlaybackSpeed:F3} (漂移={driftMs:F1}ms)");
            }

            // 返回目标位置（基于音频时钟）
            return audioPositionHns;
        }
    }

    /// <summary>
    /// 计算播放速度
    /// </summary>
    private double CalculatePlaybackSpeed(double driftMs)
    {
        // 如果漂移在可接受范围内，保持正常速度
        if (Math.Abs(driftMs) < 20)
        {
            return 1.0;
        }

        // 视频落后于音频，加快播放
        if (driftMs < 0)
        {
            // 根据漂移量计算加速比例
            var speedUp = Math.Min(MaxPlaybackSpeed - 1.0, Math.Abs(driftMs) / 1000.0 * SyncCorrectionFactor);
            return 1.0 + speedUp;
        }
        // 视频超前于音频，减慢播放
        else
        {
            // 根据漂移量计算减速比例
            var slowDown = Math.Min(1.0 - MinPlaybackSpeed, driftMs / 1000.0 * SyncCorrectionFactor);
            return 1.0 - slowDown;
        }
    }

    /// <summary>
    /// 更新漂移历史
    /// </summary>
    private void UpdateDriftHistory(double driftMs)
    {
        _driftHistory[_driftIndex] = driftMs;
        _driftIndex = (_driftIndex + 1) % DriftHistorySize;

        // 计算平均漂移
        var sum = 0.0;
        var count = 0;
        for (int i = 0; i < DriftHistorySize; i++)
        {
            if (_driftHistory[i] != 0)
            {
                sum += _driftHistory[i];
                count++;
            }
        }
        AverageDriftMs = count > 0 ? sum / count : 0;
        CurrentDriftMs = driftMs;

        // 更新同步状态
        UpdateSyncState(driftMs);
    }

    /// <summary>
    /// 更新同步状态
    /// </summary>
    private void UpdateSyncState(double driftMs)
    {
        var absDrift = Math.Abs(driftMs);

        if (absDrift < 20)
        {
            CurrentState = SyncState.Synchronized;
        }
        else if (absDrift < 50)
        {
            CurrentState = SyncState.Adjusting;
        }
        else if (absDrift < 100)
        {
            CurrentState = SyncState.DriftWarning;
        }
        else
        {
            CurrentState = SyncState.CriticalDrift;
        }
    }

    /// <summary>
    /// 强制同步（用于seek操作后）
    /// </summary>
    public void ForceSync(long audioPositionHns, long videoPositionHns)
    {
        lock (_lock)
        {
            _audioStartPositionHns = audioPositionHns;
            _audioStartTimeMs = _syncStopwatch.ElapsedMilliseconds;
            _videoStartPositionHns = videoPositionHns;
            _videoStartTimeMs = _syncStopwatch.ElapsedMilliseconds;

            // 重置漂移历史
            Array.Clear(_driftHistory, 0, _driftHistory.Length);
            _driftIndex = 0;
            CurrentDriftMs = 0;
            AverageDriftMs = 0;
            PlaybackSpeed = 1.0;
            CurrentState = SyncState.Synchronizing;

            Debug.WriteLine($"[同步优化] 强制同步: 音频={audioPositionHns / 10000}ms, 视频={videoPositionHns / 10000}ms");
        }
    }

    /// <summary>
    /// 获取同步报告
    /// </summary>
    public SyncReport GetSyncReport()
    {
        lock (_lock)
        {
            return new SyncReport
            {
                CurrentState = CurrentState,
                CurrentDriftMs = CurrentDriftMs,
                AverageDriftMs = AverageDriftMs,
                PlaybackSpeed = PlaybackSpeed,
                IsRunning = _isRunning,
                SessionDuration = _syncStopwatch.Elapsed,
                DriftHistory = (double[])_driftHistory.Clone()
            };
        }
    }

    public void Dispose()
    {
        StopSync();
    }
}

/// <summary>
/// 同步状态
/// </summary>
public enum SyncState
{
    Initializing,    // 初始化中
    Synchronizing,   // 同步中
    Synchronized,    // 已同步
    Adjusting,       // 调整中
    DriftWarning,    // 漂移警告
    CriticalDrift,   // 严重漂移
    Stopped          // 已停止
}

/// <summary>
/// 同步调整事件参数
/// </summary>
public class SyncAdjustmentEventArgs : EventArgs
{
    public double PreviousSpeed { get; set; }
    public double NewSpeed { get; set; }
    public double DriftMs { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 同步漂移警告事件参数
/// </summary>
public class SyncDriftWarningEventArgs : EventArgs
{
    public double DriftMs { get; set; }
    public double ThresholdMs { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 同步报告
/// </summary>
public class SyncReport
{
    public SyncState CurrentState { get; set; }
    public double CurrentDriftMs { get; set; }
    public double AverageDriftMs { get; set; }
    public double PlaybackSpeed { get; set; }
    public bool IsRunning { get; set; }
    public TimeSpan SessionDuration { get; set; }
    public double[] DriftHistory { get; set; } = Array.Empty<double>();
}
