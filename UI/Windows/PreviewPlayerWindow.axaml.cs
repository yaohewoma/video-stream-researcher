using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using VideoPreviewer.MediaFoundation;
using VideoPreviewer.Preview;
using video_stream_researcher.Controls;
using video_stream_researcher.Services;
using VideoStreamFetcher.Remux;

namespace video_stream_researcher.UI;

public partial class PreviewPlayerWindow : Window
{
    private const int MaxFrameQueueSize = 32;  // 增加队列大小，提供更好的缓冲
    private const int PrebufferFrameCount = 16; // 增加预缓冲帧数
    private const double PreviewWindowSeconds = 40;

    private string _path;
    private string? _previewTsPath;
    private string? _previewMp4Path;
    private string? _previewTempTsPath;
    private int _previewTempStartIndex = -1;
    private PreviewMeta? _previewMeta;
    private long _previewBaseOffsetHns;
    private long _previewWindowEndHns;
    private bool _isReloadingWindow;
    private bool _keepOriginalFiles;

    private MfVideoReader? _reader;
    private WriteableBitmap? _bitmap;
    private long _currentHns;
    private DispatcherTimer? _playTimer;
    private MediaPlayer? _audioPlayer;
    private bool _audioEnabled;
    private Thread? _decodeThread;
    private CancellationTokenSource? _decodeCts;
    private SemaphoreSlim? _frameConsumedSignal;
    private readonly ConcurrentQueue<MfVideoReader.Frame> _frameQueue = new ConcurrentQueue<MfVideoReader.Frame>();
    private int _frameQueueSize;
    private long _pendingSeekHns = -1;
    private bool _singleStep;
    private int _videoWidth;
    private int _videoHeight;
    private int _videoOutputStride;
    private long _videoDurationHns;
    private Guid _videoSubtype;
    private string? _activeVideoPath;
    private readonly object _renderLock = new();
    private long _playStartHns;
    private long _playStartTimestamp;

    private CancellationTokenSource? _playCts;
    private bool _playing;

    private TimeSpan _duration;
    private bool _rangeConfirmed;
    private TimeSpan _confirmedStart;
    private TimeSpan _confirmedEnd;

    // 帧同步机制
    private long _lastRenderedFrameHns;      // 上一帧渲染时间戳
    private Stopwatch? _renderStopwatch;      // 渲染计时器
    private double _targetFrameIntervalMs;    // 目标帧间隔
    private long _frameIntervalHns;           // 帧间隔（100纳秒单位）
    private int _consecutiveEmptyQueueCount;  // 连续空队列计数

    // 性能监控
    private PerformanceMonitor? _performanceMonitor;
    private OptimizationManager? _optimizationManager;
    private Stopwatch? _decodeStopwatch;

    /// <summary>
    /// 初始化预览播放器窗口
    /// </summary>
    public PreviewPlayerWindow() : this(string.Empty)
    {
    }

    /// <summary>
    /// 初始化预览播放器窗口并指定媒体路径
    /// </summary>
    /// <param name="path">媒体文件路径</param>
    public PreviewPlayerWindow(string path)
    {
        _path = path;
        InitializeComponent();

        // 初始化性能监控
        InitializePerformanceMonitoring();

        SeekBar.SeekRequested += OnSeekRequested;

        BtnPlay.Click += (_, _) => Play();
        BtnPause.Click += (_, _) => Pause();
        BtnRestart.Click += (_, _) => Restart();
        BtnConfirmRange.Click += async (_, _) => await ConfirmAndExportAsync();

        Opened += async (_, _) => await StartPlaybackIfNeededAsync();
        Closed += async (_, _) => await OnWindowClosedAsync();
    }

    /// <summary>
    /// 初始化性能监控系统
    /// </summary>
    private void InitializePerformanceMonitoring()
    {
        _performanceMonitor = new PerformanceMonitor();
        _optimizationManager = new OptimizationManager();

        // 订阅性能事件
        _performanceMonitor.BottleneckDetected += OnPerformanceBottleneckDetected;
        _performanceMonitor.KpiAlertTriggered += OnKpiAlertTriggered;

        // 订阅优化事件
        _optimizationManager.IterationCompleted += OnOptimizationIterationCompleted;
        _optimizationManager.SuggestionsGenerated += OnOptimizationSuggestionsGenerated;

        _decodeStopwatch = new Stopwatch();
        _renderStopwatch = new Stopwatch();

        Debug.WriteLine("[性能监控] 系统已初始化");
    }

    /// <summary>
    /// 性能瓶颈检测处理
    /// </summary>
    private void OnPerformanceBottleneckDetected(object? sender, BottleneckDetectedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var bottlenecks = string.Join(", ", e.Bottlenecks.Select(b => b.ToString()));
            Debug.WriteLine($"[性能监控] 检测到瓶颈: {bottlenecks}");

            // 在调试文本中显示瓶颈信息
            if (DebugText != null)
            {
                DebugText.Text = $"{DebugText.Text}\n[瓶颈] {bottlenecks}";
            }
        });
    }

    /// <summary>
    /// KPI告警处理
    /// </summary>
    private void OnKpiAlertTriggered(object? sender, KpiAlertEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var alert in e.Alerts)
            {
                Debug.WriteLine($"[KPI告警] {alert.Type}: {alert.CurrentValue:F2} (阈值: {alert.Threshold:F2})");
            }
        });
    }

    /// <summary>
    /// 优化迭代完成处理
    /// </summary>
    private void OnOptimizationIterationCompleted(object? sender, OptimizationIterationEventArgs e)
    {
        var iteration = e.Iteration;
        Debug.WriteLine($"[优化] 迭代 #{iteration.Id} 完成: {(iteration.Success ? "成功" : "失败")}");

        if (iteration.Improvement != null)
        {
            Debug.WriteLine($"[优化] FPS改进: {iteration.Improvement.FpsImprovement:F1} ({iteration.Improvement.FpsImprovementPercent:F1}%)");
        }
    }

    /// <summary>
    /// 优化建议生成处理
    /// </summary>
    private void OnOptimizationSuggestionsGenerated(object? sender, OptimizationSuggestionEventArgs e)
    {
        foreach (var suggestion in e.Suggestions)
        {
            Debug.WriteLine($"[优化建议] [{suggestion.Priority}] {suggestion.Title}: {suggestion.Description}");
        }
    }

    /// <summary>
    /// 窗口关闭处理
    /// </summary>
    private async Task OnWindowClosedAsync()
    {
        if (_optimizationManager != null)
        {
            var summary = await _optimizationManager.StopContinuousOptimizationAsync();
            Debug.WriteLine($"[性能监控] 会话总结: {summary.TotalIterations} 次迭代, {summary.SuccessfulIterations} 次成功");
        }

        _performanceMonitor?.Dispose();
        _optimizationManager?.Dispose();
    }

    /// <summary>
    /// 加载媒体文件到预览播放器
    /// </summary>
    /// <param name="path">媒体文件路径</param>
    /// <param name="keepOriginalFiles">是否保留原始文件</param>
    public void LoadMedia(string path, bool keepOriginalFiles)
    {
        _path = path;
        _keepOriginalFiles = keepOriginalFiles;
        ChkDeleteSources.IsChecked = !keepOriginalFiles;
        _rangeConfirmed = false;
        _confirmedStart = TimeSpan.Zero;
        _confirmedEnd = TimeSpan.Zero;
        _currentHns = 0;
        _previewBaseOffsetHns = 0;
        _previewTempStartIndex = -1;
        _previewMeta = null;
        _previewWindowEndHns = 0;
        _isReloadingWindow = false;
        _playStartHns = 0;
        _playStartTimestamp = 0;
        SeekBar.RangeStart = TimeSpan.Zero;
        SeekBar.RangeEnd = TimeSpan.Zero;
        SeekBar.Position = TimeSpan.Zero;
        TimeText.Text = "00:00 / 00:00";
        DebugText.Text = "";

        StopPlayback();
        DisposeReader();
        DisableAudio();
        CleanupPreviewTemp();
        _videoWidth = 0;
        _videoHeight = 0;
        _videoOutputStride = 0;
        _videoDurationHns = 0;
        _videoSubtype = Guid.Empty;
        TrySetDurationFromMeta();
        VideoImage.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;

        _ = Dispatcher.UIThread.InvokeAsync(async () => await StartPlaybackIfNeededAsync());
    }

    /// <summary>
    /// 如果需要则启动播放
    /// </summary>
    private async Task StartPlaybackIfNeededAsync()
    {
        if (string.IsNullOrWhiteSpace(_path))
        {
            return;
        }

        var metaPath = GetMetaPath();
        if (!File.Exists(_path) && !File.Exists(metaPath))
        {
            return;
        }

        Title = $"预览 - {Path.GetFileName(_path)}";
        DebugText.Text = $"源文件: {_path}";

        await EnsurePreviewFileAsync();
        _previewMeta = null;
        _previewBaseOffsetHns = 0;
        _previewTempStartIndex = -1;
        _previewWindowEndHns = 0;
        _isReloadingWindow = false;
        _playStartHns = 0;
        _playStartHns = 0;
        _playStartTimestamp = 0;

        string? playPath = null;
        var enableAudio = false;

        if (!string.IsNullOrWhiteSpace(_previewMp4Path) && File.Exists(_previewMp4Path))
        {
            playPath = _previewMp4Path;
            enableAudio = true;
        }
        else if (!string.IsNullOrWhiteSpace(_previewTsPath) && File.Exists(_previewTsPath))
        {
            playPath = _previewTsPath;
            enableAudio = true;
        }
        else
        {
            _previewMeta = await TryLoadPreviewMetaAsync();
            if (_previewMeta != null)
            {
                var temp = await EnsurePreviewTempTsAsync(_previewMeta, 0);
                if (!string.IsNullOrWhiteSpace(temp) && File.Exists(temp))
                {
                    playPath = temp;
                    enableAudio = true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(playPath))
        {
            DebugText.Text = $"{DebugText.Text}\n无法加载任何预览文件";
            return;
        }

        try
        {
            DebugText.Text = $"{DebugText.Text}\n尝试加载: {playPath}";
            await StartDecoderAsync(playPath);

            if (_videoWidth > 0 && _videoHeight > 0)
            {
                EnsureBitmap(_videoWidth, _videoHeight);
            }

            if (_previewMeta != null)
            {
                TrySetDurationFromMeta();
            }
            else
            {
                UpdateDurationFromReader();
            }

            // 关键修复：等待第一帧可用并渲染
            var firstFrameRendered = await TryWaitAndRenderFirstFrameAsync();
            if (!firstFrameRendered)
            {
                DebugText.Text = $"{DebugText.Text}\n警告: 第一帧渲染超时";
            }

            if (enableAudio)
            {
                PrepareAudio(playPath);
                // 关键修复：等待音频准备就绪（最多等待 500ms）
                await Task.Delay(100);
            }
            else
            {
                DisableAudio();
            }

            var durationText = _duration > TimeSpan.Zero ? _duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture) : "00:00";
            DebugText.Text = $"{DebugText.Text}\n加载成功\n尺寸: {_videoWidth}x{_videoHeight} stride={_videoOutputStride} duration={durationText}\nsubtype={_videoSubtype}";
            Play();
        }
        catch (Exception ex)
        {
            DisposeReader();
            DebugText.Text = $"{DebugText.Text}\n解码初始化失败: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>
    /// 等待第一帧可用并渲染，用于解决初始黑屏问题
    /// </summary>
    /// <returns>是否成功渲染第一帧</returns>
    private async Task<bool> TryWaitAndRenderFirstFrameAsync()
    {
        const int maxWaitMs = 2000;
        const int checkIntervalMs = 50;
        var waitedMs = 0;

        while (waitedMs < maxWaitMs)
        {
            if (TryRenderLatestFrame())
            {
                return true;
            }

            await Task.Delay(checkIntervalMs);
            waitedMs += checkIntervalMs;
        }

        return false;
    }

    /// <summary>
    /// 启动视频解码器
    /// </summary>
    /// <param name="path">视频文件路径</param>
    private async Task StartDecoderAsync(string path)
    {
        DisposeReader();
        ClearQueuedFrames();

        _activeVideoPath = path;
        _pendingSeekHns = -1;

        _decodeCts = new CancellationTokenSource();
        _frameConsumedSignal = new SemaphoreSlim(0, MaxFrameQueueSize);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _decodeThread = new Thread(() => DecodeLoop(path, _decodeCts.Token, tcs))
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _decodeThread.Start();

        var ok = await tcs.Task;
        if (!ok)
        {
            throw new InvalidOperationException("解码器初始化失败");
        }
    }

    /// <summary>
    /// 视频解码循环
    /// </summary>
    /// <param name="path">视频文件路径</param>
    /// <param name="token">取消令牌</param>
    /// <param name="initTcs">初始化任务完成源</param>
    private void DecodeLoop(string path, CancellationToken token, TaskCompletionSource<bool> initTcs)
    {
        MfVideoReader? reader = null;
        try
        {
            reader = new MfVideoReader(path);
            _reader = reader;
            _videoWidth = reader.Width;
            _videoHeight = reader.Height;
            _videoOutputStride = reader.OutputStride;
            _videoDurationHns = reader.DurationHns;
            _videoSubtype = reader.Subtype;

            if (_currentHns > 0)
            {
                try
                {
                    reader.Seek(GetMediaHns(_currentHns));
                }
                catch
                {
                }
            }

            var first = reader.ReadNextFrame();
            if (first != null)
            {
                EnqueueFrame(first);
            }

            initTcs.TrySetResult(true);

            while (!token.IsCancellationRequested)
            {
                var seek = Interlocked.Exchange(ref _pendingSeekHns, -1);
                if (seek >= 0)
                {
                    try
                    {
                        reader.Seek(seek);
                        ClearQueuedFrames();
                    }
                    catch
                    {
                    }
                }

                if (!_playing && !_singleStep)
                {
                    // 暂停时短暂休眠
                    Thread.Sleep(10);
                    continue;
                }

                // 检查队列状态
                var currentQueueSize = Volatile.Read(ref _frameQueueSize);
                if (currentQueueSize >= MaxFrameQueueSize)
                {
                    // 队列已满，等待渲染线程消费
                    Thread.Sleep(5);
                    continue;
                }
                
                // 智能解码控制
                // 根据队列状态动态调整解码速度
                var frameIntervalMs = 1000.0 / reader.FrameRate;
                
                if (currentQueueSize >= PrebufferFrameCount)
                {
                    // 队列充足，按帧率解码
                    Thread.Sleep((int)(frameIntervalMs * 0.95));
                }
                else if (currentQueueSize >= PrebufferFrameCount / 2)
                {
                    // 队列中等，略快于帧率解码
                    Thread.Sleep((int)(frameIntervalMs * 0.5));
                }
                else if (currentQueueSize < 3)
                {
                    // 队列严重不足，不休眠，全速解码
                    // 不休眠，立即解码下一帧
                }
                else
                {
                    // 队列较低，快速解码补充
                    Thread.Sleep((int)(frameIntervalMs * 0.2));
                }

                // 开始解码计时
                _decodeStopwatch?.Restart();

                var frame = reader.ReadNextFrame();

                // 记录解码时间
                if (_decodeStopwatch != null && _performanceMonitor != null)
                {
                    _decodeStopwatch.Stop();
                    _performanceMonitor.RecordDecodeTime(_decodeStopwatch.Elapsed);
                }

                if (frame == null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        DebugText.Text = $"{DebugText.Text}\n播放结束";
                        StopPlayback();
                    });
                    continue;
                }

                if (token.IsCancellationRequested)
                {
                    frame.Dispose();
                    break;
                }

                // 记录解码帧
                _performanceMonitor?.RecordFrameDecoded();

                // 完全移除音频同步逻辑，避免跳帧
                // 视频和音频独立播放，依靠解码和渲染的稳定性保持同步

                EnqueueFrame(frame);

                if (!_playing && _singleStep)
                {
                    _singleStep = false;
                }
            }
        }
        catch
        {
            initTcs.TrySetResult(false);
        }
        finally
        {
            try
            {
                reader?.Dispose();
            }
            catch
            {
            }
            if (ReferenceEquals(_reader, reader))
            {
                _reader = null;
            }
        }
    }

    /// <summary>
    /// 将帧加入队列，确保线程安全和队列一致性
    /// </summary>
    /// <param name="frame">要加入队列的帧</param>
    private void EnqueueFrame(MfVideoReader.Frame frame)
    {
        _frameQueue.Enqueue(frame);
        var newSize = Interlocked.Increment(ref _frameQueueSize);

        // 如果队列超过最大大小，丢弃旧帧
        if (newSize > MaxFrameQueueSize)
        {
            // 丢弃最旧的一帧
            if (_frameQueue.TryDequeue(out var oldFrame))
            {
                Interlocked.Decrement(ref _frameQueueSize);
                oldFrame.Dispose();
                _performanceMonitor?.RecordFrameDropped("队列溢出");
            }
        }
    }

    /// <summary>
    /// 尝试从队列中取出下一帧
    /// </summary>
    /// <param name="frame">输出的帧</param>
    /// <returns>是否成功取出帧</returns>
    private bool TryDequeueNextFrame(out MfVideoReader.Frame frame)
    {
        frame = null!;
        if (_frameQueue.TryDequeue(out var f))
        {
            Interlocked.Decrement(ref _frameQueueSize);
            frame = f;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 尝试从队列中取出最新的一帧（丢弃之前的所有帧）
    /// 简化：只取队列中的第一帧，避免跳帧
    /// </summary>
    /// <param name="frame">输出的帧</param>
    /// <returns>是否成功取出帧</returns>
    private bool TryDequeueLatestFrame(out MfVideoReader.Frame frame)
    {
        // 简化为直接取下一帧，不再丢弃中间帧
        // 这样可以确保按顺序播放，避免跳帧
        return TryDequeueNextFrame(out frame);
    }

    /// <summary>
    /// 尝试从队列中取出最接近目标时间戳的帧
    /// 智能帧选择：根据目标时间戳选择最合适的帧，减少跳帧
    /// </summary>
    /// <param name="targetHns">目标时间戳（100纳秒单位）</param>
    /// <param name="frame">输出的帧</param>
    /// <returns>是否成功取出帧</returns>
    private bool TryDequeueFrameForTimestamp(long targetHns, out MfVideoReader.Frame frame)
    {
        frame = null!;
        
        // 首先尝试查看队列中的帧
        if (!_frameQueue.TryPeek(out var firstFrame))
        {
            return false;
        }
        
        // 计算时间差容忍度（半帧间隔）
        var toleranceHns = _frameIntervalHns / 2;
        
        // 如果第一帧的时间戳在容忍范围内，直接取出
        if (Math.Abs(firstFrame.TimestampHns - targetHns) <= toleranceHns)
        {
            if (_frameQueue.TryDequeue(out var f))
            {
                Interlocked.Decrement(ref _frameQueueSize);
                frame = f;
                return true;
            }
            return false;
        }
        
        // 如果第一帧的时间戳落后于目标时间，取出并继续检查
        if (firstFrame.TimestampHns < targetHns - toleranceHns)
        {
            // 取出落后的帧
            if (_frameQueue.TryDequeue(out var oldFrame))
            {
                Interlocked.Decrement(ref _frameQueueSize);
                
                // 如果队列中还有更多帧，检查下一帧
                if (_frameQueue.TryPeek(out var nextFrame))
                {
                    // 如果下一帧仍然落后，丢弃当前帧并递归检查
                    if (nextFrame.TimestampHns < targetHns - toleranceHns)
                    {
                        oldFrame.Dispose();
                        _performanceMonitor?.RecordFrameDropped("时间戳落后");
                        return TryDequeueFrameForTimestamp(targetHns, out frame);
                    }
                    
                    // 如果下一帧在范围内，使用当前帧
                    frame = oldFrame;
                    return true;
                }
                
                // 队列中只有这一帧，使用它
                frame = oldFrame;
                return true;
            }
        }
        
        // 如果第一帧的时间戳超前于目标时间，等待
        // 不取出帧，让解码线程继续填充队列
        return false;
    }

    /// <summary>
    /// 清空队列中的所有帧并释放资源
    /// </summary>
    private void ClearQueuedFrames()
    {
        int clearedCount = 0;
        while (_frameQueue.TryDequeue(out var f))
        {
            Interlocked.Decrement(ref _frameQueueSize);
            f.Dispose();
            clearedCount++;
        }
        
        // 释放相应数量的信号量，确保生产者可以继续工作
        if (_frameConsumedSignal != null && clearedCount > 0)
        {
            try
            {
                _frameConsumedSignal.Release(Math.Min(clearedCount, MaxFrameQueueSize));
            }
            catch (SemaphoreFullException)
            {
                // 信号量已满，忽略此异常
            }
        }
    }

    /// <summary>
    /// 开始播放视频
    /// </summary>
    private void Play()
    {
        if (_decodeThread == null || _decodeCts == null)
        {
            DebugText.Text = $"{DebugText.Text}\n播放失败: 解码器未就绪";
            return;
        }

        if (_playing)
        {
            DebugText.Text = $"{DebugText.Text}\n已经在播放中";
            return;
        }

        DebugText.Text = $"{DebugText.Text}\n开始播放...";
        _playing = true;
        _singleStep = false;
        _playStartHns = _currentHns;
        _playStartTimestamp = Stopwatch.GetTimestamp();
        _frameConsumedSignal?.Release();
        EnsurePlayTimer();
        _playTimer?.Start();

        // 启动性能监控
        _performanceMonitor?.StartMonitoring();
        _optimizationManager?.StartContinuousOptimization();

        if (_audioEnabled && _audioPlayer != null)
        {
            _audioPlayer.Position = TimeSpan.FromTicks(GetMediaHns(_currentHns));
            _audioPlayer.Play();
        }
    }

    /// <summary>
    /// 暂停播放
    /// </summary>
    private void Pause()
    {
        StopPlayback();
    }

    /// <summary>
    /// 重新开始播放
    /// </summary>
    private void Restart()
    {
        DebugText.Text = $"{DebugText.Text}\n重新开始播放";
        OnSeekRequested(TimeSpan.Zero);
        Play();
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    private void StopPlayback()
    {
        if (_playing)
        {
            DebugText.Text = $"{DebugText.Text}\n停止播放";
        }
        _playing = false;
        _playCts?.Cancel();
        _playCts = null;
        _playTimer?.Stop();

        // 停止性能监控
        _performanceMonitor?.StopMonitoring();

        if (_audioEnabled && _audioPlayer != null)
        {
            _audioPlayer.Pause();
        }
    }

    /// <summary>
    /// 处理定位请求
    /// </summary>
    /// <param name="t">目标时间位置</param>
    private void OnSeekRequested(TimeSpan t)
    {
        StopPlayback();

        var hns = (long)t.TotalMilliseconds * 10_000;
        if (hns < 0)
        {
            hns = 0;
        }

        if (_previewMeta != null)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () => await RestartPreviewFromAsync(t, false));
            return;
        }

        _currentHns = hns;
        Interlocked.Exchange(ref _pendingSeekHns, hns);
        ClearQueuedFrames();
        DebugText.Text = $"{DebugText.Text}\n定位到: {t.TotalSeconds:F2}s";
        if (_audioEnabled && _audioPlayer != null)
        {
            _audioPlayer.Position = TimeSpan.FromTicks(GetMediaHns(_currentHns));
        }

        _singleStep = true;
        _frameConsumedSignal?.Release();
        EnsurePlayTimer();
        _playTimer?.Start();
    }

    private void UpdateDurationFromReader()
    {
        if (_videoDurationHns <= 0)
        {
            TrySetDurationFromMeta();
            return;
        }

        _duration = TimeSpan.FromTicks(_videoDurationHns);
        SeekBar.Duration = _duration;
        if (SeekBar.RangeEnd <= TimeSpan.Zero || SeekBar.RangeEnd > _duration)
        {
            SeekBar.RangeStart = TimeSpan.Zero;
            SeekBar.RangeEnd = _duration;
        }
    }

    /// <summary>
    /// 尝试从队列中取出并渲染最新的一帧
    /// </summary>
    /// <returns>是否成功渲染</returns>
    private bool TryRenderLatestFrame()
    {
        if (_bitmap == null)
        {
            DebugText.Text = $"{DebugText.Text}\n渲染失败: 位图为空";
            return false;
        }

        if (!TryDequeueLatestFrame(out var frame))
        {
            return false;
        }

        try
        {
            RenderFrame(frame);
            return true;
        }
        finally
        {
            frame.Dispose();
        }
    }

    /// <summary>
    /// 渲染单帧视频到 WriteableBitmap，并强制刷新 UI 显示
    /// </summary>
    /// <param name="frame">要渲染的视频帧</param>
    private void RenderFrame(MfVideoReader.Frame frame)
    {
        if (_bitmap == null || _videoWidth <= 0 || _videoHeight <= 0)
        {
            System.Diagnostics.Debug.WriteLine($"[渲染错误] _bitmap={_bitmap}, _videoWidth={_videoWidth}, _videoHeight={_videoHeight}");
            return;
        }

        // 开始渲染计时
        _renderStopwatch?.Restart();

        System.Diagnostics.Debug.WriteLine($"[渲染] 开始渲染帧，时间戳: {frame.TimestampHns}, 队列: {Volatile.Read(ref _frameQueueSize)}");

        _currentHns = GetLogicalHns(frame.TimestampHns);
        var pos = TimeSpan.FromTicks(_currentHns);

        // 同步更新UI，确保立即生效
        SeekBar.Position = pos;
        TimeText.Text = $"{FormatTime(pos)} / {FormatTime(_duration)}";

        // 记录帧队列状态
        _performanceMonitor?.RecordFrameQueueStatus(
            Volatile.Read(ref _frameQueueSize),
            MaxFrameQueueSize);

        var bytes = frame.Buffer;
        var len = frame.Length;
        var expectedSize = _videoWidth * _videoHeight * 4;
        if (len < expectedSize)
        {
            System.Diagnostics.Debug.WriteLine($"[渲染错误] 帧数据大小不足: {len} < {expectedSize}");
            return;
        }

        var srcStride = _videoOutputStride;
        if (srcStride == 0)
        {
            srcStride = _videoWidth * 4;
        }

        // 使用局部变量避免在lock中访问对象属性
        var height = _videoHeight;
        var dstRowBytes = _videoWidth * 4;
        var rowBytes = Math.Min(dstRowBytes, Math.Abs(srcStride));

        try
        {
            lock (_renderLock)
            {
                using var fb = _bitmap.Lock();
                var dst = fb.Address;
                
                // 快速路径：直接内存复制
                if (srcStride == dstRowBytes && len >= dstRowBytes * height)
                {
                    Marshal.Copy(bytes, 0, dst, dstRowBytes * height);
                }
                else
                {
                    unsafe
                    {
                        fixed (byte* srcPtr = bytes)
                        {
                            for (var y = 0; y < height; y++)
                            {
                                var srcOffset = y * srcStride;
                                if (srcOffset + rowBytes > len)
                                {
                                    break;
                                }
                                Buffer.MemoryCopy(srcPtr + srcOffset, (void*)(dst + y * dstRowBytes), rowBytes, rowBytes);
                            }
                        }
                    }
                }
            }
            
            // 同步刷新UI，确保帧立即显示
            VideoImage.InvalidateVisual();
            VideoImage.InvalidateMeasure();

            // 记录渲染时间和渲染帧
            if (_renderStopwatch != null && _performanceMonitor != null)
            {
                _renderStopwatch.Stop();
                _performanceMonitor.RecordRenderTime(_renderStopwatch.Elapsed);
                _performanceMonitor.RecordFrameRendered();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[渲染错误] {ex.Message}");
        }
    }

    private void EnsurePlayTimer()
    {
        if (_playTimer != null)
        {
            return;
        }

        // 根据视频帧率调整定时器间隔
        // 使用精确的帧间隔，确保与视频帧率同步
        var frameRate = _reader?.FrameRate ?? 30.0;
        _targetFrameIntervalMs = 1000.0 / frameRate;
        _frameIntervalHns = (long)(10_000_000 / frameRate); // 转换为100纳秒单位
        
        // 使用更精确的定时器间隔
        var intervalMs = Math.Max(5, _targetFrameIntervalMs / 2); // 至少5ms，但频率是目标帧率的2倍
        
        _playTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };
        _playTimer.Tick += (_, _) => OnPlayTick();
        
        // 初始化渲染计时器
        _renderStopwatch = Stopwatch.StartNew();
        _consecutiveEmptyQueueCount = 0;
        
        System.Diagnostics.Debug.WriteLine($"[性能] 定时器间隔设置为 {intervalMs:F1}ms (目标帧率: {frameRate}fps, 目标间隔: {_targetFrameIntervalMs:F1}ms)");
    }

    private void OnPlayTick()
    {
        if (_bitmap == null || _renderStopwatch == null)
        {
            System.Diagnostics.Debug.WriteLine("[错误] _bitmap 或 _renderStopwatch 为 null，停止播放");
            _playTimer?.Stop();
            return;
        }

        // 计算目标渲染时间戳
        var elapsedHns = _renderStopwatch.ElapsedTicks * 10_000_000 / Stopwatch.Frequency;
        var targetHns = _playStartHns + elapsedHns;

        MfVideoReader.Frame? frame = null;
        try
        {
            // 基于时间戳获取帧
            if (!TryDequeueFrameForTimestamp(targetHns, out var f))
            {
                // 队列为空
                _consecutiveEmptyQueueCount++;
                
                // 如果连续多次队列为空，且不在播放状态，停止定时器
                if (!_playing && !_singleStep && _consecutiveEmptyQueueCount > 10)
                {
                    _playTimer?.Stop();
                }
                
                // 检查是否需要重新加载窗口
                if (_previewMeta != null && _playing && !_isReloadingWindow && _previewWindowEndHns > 0)
                {
                    if (targetHns >= _previewWindowEndHns - TimeSpan.FromSeconds(1).Ticks)
                    {
                        _isReloadingWindow = true;
                        _ = Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            try
                            {
                                await RestartPreviewFromAsync(TimeSpan.FromTicks(_previewWindowEndHns), true);
                            }
                            finally
                            {
                                _isReloadingWindow = false;
                            }
                        });
                    }
                }
                return;
            }
            
            // 成功获取帧，重置计数器
            _consecutiveEmptyQueueCount = 0;
            frame = f;

            // 检查范围结束
            if (_rangeConfirmed)
            {
                var endHns = (long)_confirmedEnd.TotalMilliseconds * 10_000;
                var logicalHns = GetLogicalHns(frame.TimestampHns);
                if (endHns > 0 && logicalHns >= endHns)
                {
                    StopPlayback();
                    OnSeekRequested(_confirmedStart);
                    return;
                }
            }

            // 同步渲染帧
            RenderFrame(frame);
            
            // 更新上一帧渲染时间戳
            _lastRenderedFrameHns = frame.TimestampHns;
            
            // 立即释放帧
            frame.Dispose();
            frame = null;
        }
        finally
        {
            // 确保帧被释放
            frame?.Dispose();
        }
    }

    private void PrepareAudio(string path)
    {
        try
        {
            _audioPlayer?.Stop();
        }
        catch
        {
        }

        try
        {
            _audioPlayer = new MediaPlayer();
            _audioPlayer.Open(new Uri(path));
            _audioPlayer.Position = TimeSpan.FromTicks(GetMediaHns(_currentHns));
            _audioEnabled = true;
        }
        catch
        {
            _audioEnabled = false;
        }
    }

    private void DisableAudio()
    {
        try
        {
            _audioPlayer?.Stop();
        }
        catch
        {
        }
        _audioEnabled = false;
    }

    private async Task ConfirmAndExportAsync()
    {
        if (_duration <= TimeSpan.Zero)
        {
            UpdateDurationFromReader();
        }

        var start = SeekBar.RangeStart;
        var end = SeekBar.RangeEnd;
        if (end <= start)
        {
            return;
        }

        _rangeConfirmed = true;
        _confirmedStart = start;
        _confirmedEnd = end;

        try
        {
            BtnConfirmRange.IsEnabled = false;
            await ExportSelectedRangeAsync(start, end);
        }
        finally
        {
            BtnConfirmRange.IsEnabled = true;
        }
    }

    private async Task ExportSelectedRangeAsync(TimeSpan start, TimeSpan end)
    {
        var metaPath = GetMetaPath();
        if (!File.Exists(metaPath))
        {
            return;
        }

        PreviewMeta? meta;
        try
        {
            var json = await File.ReadAllTextAsync(metaPath, Encoding.UTF8);
            meta = JsonSerializer.Deserialize<PreviewMeta>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch
        {
            return;
        }

        if (meta == null || string.IsNullOrWhiteSpace(meta.SegmentsDir) || meta.SegmentFiles.Count == 0)
        {
            return;
        }

        var startIndex = PreviewSegmentMerger.FindSegmentIndex(meta.Durations, start.TotalSeconds);
        var endIndex = PreviewSegmentMerger.FindSegmentEndIndex(meta.Durations, end.TotalSeconds);
        if (endIndex <= startIndex)
        {
            return;
        }

        var outputDir = Path.GetDirectoryName(_path) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(_path);
        var startMs = (long)start.TotalMilliseconds;
        var endMs = (long)end.TotalMilliseconds;

        var clipTs = MakeUniquePath(Path.Combine(outputDir, $"{baseName}_clip_{startMs}_{endMs}.ts"));
        await PreviewSegmentMerger.MergeSegmentsRangeAsync(meta.SegmentsDir, meta.SegmentFiles, startIndex, endIndex, clipTs);

        var clipMp4 = Path.ChangeExtension(clipTs, ".mp4");
        clipMp4 = MakeUniquePath(clipMp4);
        await TsToMp4Remuxer.RemuxAsync(clipTs, clipMp4, null, default);

        var deleteSources = ChkDeleteSources.IsChecked == true;
        if (deleteSources)
        {
            StopPlayback();
            DisposeReader();

            TryDeleteDirectory(meta.SegmentsDir);
            TryDeleteFile(metaPath);
            TryDeleteFile(_path);
            TryDeleteFile(_previewMp4Path);
            TryDeleteFile(_previewTsPath);
        }
    }

    private void TrySetDurationFromMeta()
    {
        try
        {
            var metaPath = GetMetaPath();
            if (!File.Exists(metaPath))
            {
                return;
            }

            var json = File.ReadAllText(metaPath, Encoding.UTF8);
            var meta = JsonSerializer.Deserialize<PreviewMeta>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            if (meta == null || meta.Durations.Count == 0)
            {
                return;
            }

            var sum = 0d;
            for (var i = 0; i < meta.Durations.Count; i++)
            {
                sum += Math.Max(0, meta.Durations[i]);
            }

            if (sum <= 0)
            {
                return;
            }

            _duration = TimeSpan.FromSeconds(sum);
            SeekBar.Duration = _duration;
            if (SeekBar.RangeEnd <= TimeSpan.Zero || SeekBar.RangeEnd > _duration)
            {
                SeekBar.RangeStart = TimeSpan.Zero;
                SeekBar.RangeEnd = _duration;
            }
        }
        catch
        {
        }
    }

    private async Task EnsurePreviewFileAsync()
    {
        _previewMp4Path = null;
        _previewTsPath = null;

        if (string.Equals(Path.GetExtension(_path), ".ts", StringComparison.OrdinalIgnoreCase))
        {
            _previewTsPath = _path;
            _previewMp4Path = null; // 不再尝试生成MP4
            return;
        }

        if (string.Equals(Path.GetExtension(_path), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            _previewMp4Path = _path;
            // 对于 MP4 文件，尝试查找对应的 TS 文件（用于回退）
            var tsPath = Path.ChangeExtension(_path, ".ts");
            if (File.Exists(tsPath))
            {
                _previewTsPath = tsPath;
            }
            return;
        }
    }

    private string GetMetaPath()
    {
        var direct = _path + ".meta.json";
        if (File.Exists(direct))
        {
            return direct;
        }

        if (string.Equals(Path.GetExtension(_path), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            var ts = Path.ChangeExtension(_path, ".ts");
            var alt = ts + ".meta.json";
            return alt;
        }

        return direct;
    }

    /// <summary>
    /// 确保 WriteableBitmap 存在且尺寸正确，线程安全
    /// </summary>
    /// <param name="width">位图宽度</param>
    /// <param name="height">位图高度</param>
    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap != null && _bitmap.PixelSize.Width == width && _bitmap.PixelSize.Height == height)
        {
            return;
        }

        // 使用与 RenderFrame 相同的锁，确保线程安全
        lock (_renderLock)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Opaque);
            VideoImage.Source = _bitmap;
        }
    }

    private void DisposeReader()
    {
        try
        {
            _decodeCts?.Cancel();
        }
        catch
        {
        }

        try
        {
            if (_frameConsumedSignal != null)
            {
                // 尝试释放足够的信号量以唤醒所有等待的线程
                // 使用循环确保不会一次性释放太多导致 SemaphoreFullException
                int released = 0;
                while (released < MaxFrameQueueSize)
                {
                    try
                    {
                        _frameConsumedSignal.Release();
                        released++;
                    }
                    catch (SemaphoreFullException)
                    {
                        // 信号量已满，停止释放
                        break;
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            if (_decodeThread != null && _decodeThread.IsAlive)
            {
                _decodeThread.Join(500);
            }
        }
        catch
        {
        }

        _decodeThread = null;

        try
        {
            _decodeCts?.Dispose();
        }
        catch
        {
        }
        _decodeCts = null;

        try
        {
            _frameConsumedSignal?.Dispose();
        }
        catch
        {
        }
        _frameConsumedSignal = null;

        try
        {
            _reader?.Dispose();
        }
        catch
        {
        }
        _reader = null;

        ClearQueuedFrames();
        _frameQueueSize = 0;
        _activeVideoPath = null;
    }

    private async Task<PreviewMeta?> TryLoadPreviewMetaAsync()
    {
        var metaPath = GetMetaPath();
        if (!File.Exists(metaPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(metaPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<PreviewMeta>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> EnsurePreviewTempTsAsync(PreviewMeta meta, int startIndex)
    {
        if (!string.IsNullOrWhiteSpace(_previewTempTsPath) && File.Exists(_previewTempTsPath) && _previewTempStartIndex == startIndex)
        {
            return _previewTempTsPath;
        }

        if (string.IsNullOrWhiteSpace(meta.SegmentsDir) || meta.SegmentFiles.Count == 0)
        {
            return null;
        }

        if (!Directory.Exists(meta.SegmentsDir))
        {
            return null;
        }

        var outputDir = Path.GetDirectoryName(GetMetaPath()) ?? Path.GetTempPath();
        var baseName = Path.GetFileNameWithoutExtension(_path);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "preview";
        }

        var tempPath = MakeUniquePath(Path.Combine(outputDir, $"{baseName}_preview_cache_{startIndex}.ts"));
        try
        {
            var startSeconds = GetOffsetHnsForIndex(meta, startIndex) / 10_000_000d;
            var endIndex = PreviewSegmentMerger.FindSegmentEndIndex(meta.Durations, startSeconds + PreviewWindowSeconds);
            if (endIndex <= startIndex)
            {
                endIndex = Math.Min(startIndex + 1, meta.SegmentFiles.Count);
            }
            await PreviewSegmentMerger.MergeSegmentsRangeAsync(meta.SegmentsDir, meta.SegmentFiles, startIndex, endIndex, tempPath);
            _previewTempTsPath = tempPath;
            _previewTempStartIndex = startIndex;
            _previewWindowEndHns = GetOffsetHnsForIndex(meta, endIndex);
            return tempPath;
        }
        catch
        {
            TryDeleteFile(tempPath);
            return null;
        }
    }

    private void CleanupPreviewTemp()
    {
        if (string.IsNullOrWhiteSpace(_previewTempTsPath))
        {
            return;
        }

        TryDeleteFile(_previewTempTsPath);
        _previewTempTsPath = null;
        _previewTempStartIndex = -1;
    }

    private long GetLogicalHns(long mediaHns)
    {
        return _previewMeta == null ? mediaHns : mediaHns + _previewBaseOffsetHns;
    }

    private long GetMediaHns(long logicalHns)
    {
        if (_previewMeta == null)
        {
            return logicalHns;
        }
        var v = logicalHns - _previewBaseOffsetHns;
        return v < 0 ? 0 : v;
    }

    private static long GetOffsetHnsForIndex(PreviewMeta meta, int index)
    {
        if (index <= 0)
        {
            return 0;
        }
        var sum = 0d;
        for (var i = 0; i < index && i < meta.Durations.Count; i++)
        {
            sum += Math.Max(0, meta.Durations[i]);
        }
        return (long)(sum * 10_000_000);
    }

    private async Task RestartPreviewFromAsync(TimeSpan t, bool resumePlay)
    {
        if (_previewMeta == null)
        {
            return;
        }

        var startIndex = PreviewSegmentMerger.FindSegmentIndex(_previewMeta.Durations, t.TotalSeconds);
        _previewBaseOffsetHns = GetOffsetHnsForIndex(_previewMeta, startIndex);
        _currentHns = _previewBaseOffsetHns;
        CleanupPreviewTemp();

        var temp = await EnsurePreviewTempTsAsync(_previewMeta, startIndex);
        if (string.IsNullOrWhiteSpace(temp) || !File.Exists(temp))
        {
            return;
        }

        try
        {
            await StartDecoderAsync(temp);

            if (_videoWidth > 0 && _videoHeight > 0)
            {
                EnsureBitmap(_videoWidth, _videoHeight);
            }

            TrySetDurationFromMeta();
            TryRenderLatestFrame();

            if (_audioEnabled)
            {
                PrepareAudio(temp);
            }
            else
            {
                DisableAudio();
            }

            DebugText.Text = $"{DebugText.Text}\n定位到: {t.TotalSeconds:F2}s";
        }
        catch (Exception ex)
        {
            DisposeReader();
            DebugText.Text = $"{DebugText.Text}\n解码初始化失败: {ex.GetType().Name}: {ex.Message}";
            return;
        }

        if (resumePlay)
        {
            _playing = true;
            _singleStep = false;
            _playStartHns = _currentHns;
            _playStartTimestamp = Stopwatch.GetTimestamp();
            _frameConsumedSignal?.Release();
            EnsurePlayTimer();
            _playTimer?.Start();
            if (_audioEnabled && _audioPlayer != null)
            {
                _audioPlayer.Position = TimeSpan.FromTicks(GetMediaHns(_currentHns));
                _audioPlayer.Play();
            }
        }
        else
        {
            _singleStep = true;
            _frameConsumedSignal?.Release();
            EnsurePlayTimer();
            _playTimer?.Start();
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    private static string MakeUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var dir = Path.GetDirectoryName(path) ?? ".";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    private static string FormatTime(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
        {
            t = TimeSpan.Zero;
        }

        if (t.TotalHours >= 1)
        {
            return t.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }

        return t.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        StopPlayback();
        DisposeReader();
        CleanupPreviewTemp();
        try
        {
            _audioPlayer?.Stop();
        }
        catch
        {
        }
        _audioPlayer = null;
        try
        {
            VideoImage.Source = null;
        }
        catch
        {
        }
        try
        {
            _bitmap?.Dispose();
        }
        catch
        {
        }
        _bitmap = null;
    }
}
