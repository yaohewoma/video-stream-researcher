using System;
using System.IO;
using NAudio.Wave;

namespace VideoPreviewer.MediaFoundation;

/// <summary>
/// 音频解码器，使用 NAudio 的 MediaFoundationReader 解码和播放音频
/// </summary>
public sealed class MfAudioReader : IDisposable
{
    private readonly string _filePath;
    private MediaFoundationReader? _mediaReader;
    private WaveOutEvent? _audioOutput;
    private volatile bool _isPlaying;
    private volatile bool _isPaused;

    public int SampleRate => _mediaReader?.WaveFormat.SampleRate ?? 44100;
    public int Channels => _mediaReader?.WaveFormat.Channels ?? 2;
    public int BitsPerSample => _mediaReader?.WaveFormat.BitsPerSample ?? 16;
    public long DurationHns => _mediaReader != null ? (long)(_mediaReader.TotalTime.TotalMilliseconds * 10000) : 0;
    public bool IsPlaying => _isPlaying;
    
    /// <summary>
    /// 获取当前播放位置（100纳秒单位）
    /// </summary>
    public long CurrentPositionHns => _mediaReader != null ? (long)(_mediaReader.CurrentTime.TotalMilliseconds * 10000) : 0;

    public MfAudioReader(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("文件不存在", filePath);

        _filePath = Path.GetFullPath(filePath);
        _isPaused = false;

        // 只验证文件存在，不创建 reader，避免延迟
    }

    /// <summary>
    /// 开始播放音频
    /// </summary>
    public void Play()
    {
        if (_isPlaying && !_isPaused) return;

        // 如果是暂停状态，直接继续播放
        if (_isPaused && _audioOutput != null && _mediaReader != null)
        {
            _audioOutput.Play();
            _isPlaying = true;
            _isPaused = false;
            return;
        }

        // 完全重新创建，确保稳定
        StopInternal();

        _mediaReader = new MediaFoundationReader(_filePath);
        
        _audioOutput = new WaveOutEvent
        {
            DesiredLatency = 150,
            NumberOfBuffers = 3
        };
        _audioOutput.Init(_mediaReader);
        
        _audioOutput.Play();
        _isPlaying = true;
        _isPaused = false;
    }

    /// <summary>
    /// 暂停播放
    /// </summary>
    public void Pause()
    {
        if (!_isPlaying || _isPaused) return;

        try
        {
            _audioOutput?.Pause();
            _isPlaying = false;
            _isPaused = true;
        }
        catch { }
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        _isPaused = false;
        StopInternal();
    }

    private void StopInternal()
    {
        _isPlaying = false;
        _isPaused = false;
        
        try
        {
            if (_audioOutput != null)
            {
                _audioOutput.Stop();
                _audioOutput.Dispose();
                _audioOutput = null;
            }
        }
        catch { }

        try
        {
            if (_mediaReader != null)
            {
                _mediaReader.Dispose();
                _mediaReader = null;
            }
        }
        catch { }
    }

    public void Dispose()
    {
        StopInternal();
    }
}
