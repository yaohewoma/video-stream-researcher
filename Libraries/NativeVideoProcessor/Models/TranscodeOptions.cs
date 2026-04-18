namespace NativeVideoProcessor;

/// <summary>
/// 转码选项
/// </summary>
public class TranscodeOptions
{
    /// <summary>
    /// 目标宽度
    /// </summary>
    public int TargetWidth { get; set; }

    /// <summary>
    /// 目标高度
    /// </summary>
    public int TargetHeight { get; set; }

    /// <summary>
    /// 视频比特率
    /// </summary>
    public int VideoBitrate { get; set; }
}
