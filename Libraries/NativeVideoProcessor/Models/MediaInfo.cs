namespace NativeVideoProcessor;

/// <summary>
/// 媒体信息
/// </summary>
public class MediaInfo
{
    /// <summary>
    /// 宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 帧率
    /// </summary>
    public double FrameRate { get; set; }

    /// <summary>
    /// 时长
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 视频编码
    /// </summary>
    public string VideoCodec { get; set; } = string.Empty;
}
