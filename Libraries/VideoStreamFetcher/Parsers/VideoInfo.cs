using System;
using System.Collections.Generic;

namespace VideoStreamFetcher.Parsers;

/// <summary>
/// 视频信息类，包含视频和音频流信息
/// </summary>
public class VideoInfo
{
    public string Title { get; set; } = "";
    public VideoStreamInfo? VideoStream { get; set; }
    public VideoStreamInfo? AudioStream { get; set; }
    public List<VideoStreamInfo>? CombinedStreams { get; set; }
    public double Duration { get; set; } // 视频时长（秒）
}

/// <summary>
/// 视频流信息类
/// </summary>
public class VideoStreamInfo
{
    public string Url { get; set; } = "";
    public long Size { get; set; }
    public string Quality { get; set; } = "";
    public string Format { get; set; } = "";
}