using System;

namespace video_stream_researcher.Models;

/// <summary>
/// 下载选项
/// </summary>
public class DownloadOptions
{
    /// <summary>
    /// 是否仅下载音频
    /// </summary>
    public bool AudioOnly { get; set; }

    /// <summary>
    /// 是否仅下载视频
    /// </summary>
    public bool VideoOnly { get; set; }

    /// <summary>
    /// 是否不合并音视频流
    /// </summary>
    public bool NoMerge { get; set; }

    /// <summary>
    /// 是否启用FFmpeg
    /// </summary>
    public bool IsFFmpegEnabled { get; set; }

    /// <summary>
    /// 合并模式
    /// </summary>
    public int MergeMode { get; set; } = 1;

    public bool PreviewEnabled { get; set; }
    public int PreviewSegments { get; set; } = 60;

    public bool KeepOriginalFiles { get; set; } = true;

    /// <summary>
    /// 默认选项
    /// </summary>
    public static DownloadOptions Default => new DownloadOptions();
}
