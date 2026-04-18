using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Models;

/// <summary>
/// MP4文件信息类，用于存储视频和音频文件的基本信息
/// </summary>
public class MP4FileInfo
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; set; }
    
    /// <summary>
    /// 视频宽度
    /// </summary>
    public int VideoWidth { get; set; }
    
    /// <summary>
    /// 视频高度
    /// </summary>
    public int VideoHeight { get; set; }
    
    /// <summary>
    /// 视频时间刻度
    /// </summary>
    public uint VideoTimeScale { get; set; }
    
    /// <summary>
    /// 视频时长
    /// </summary>
    public uint VideoDuration { get; set; }
    
    /// <summary>
    /// 视频样本间隔
    /// </summary>
    public uint SampleDelta { get; set; }
    
    /// <summary>
    /// 原始视频avc1盒子数据
    /// </summary>
    public byte[]? OriginalVideoAvc1Box { get; set; }
    
    /// <summary>
    /// 从视频样本中生成的avcC盒子数据
    /// </summary>
    public byte[]? GeneratedAvcCBox { get; set; }
    
    /// <summary>
    /// 原始视频trak盒子数据（包含完整的样本表）
    /// </summary>
    public byte[]? OriginalVideoTrakBox { get; set; }
    
    /// <summary>
    /// 原始音频trak盒子数据（包含完整的样本表）
    /// </summary>
    public byte[]? OriginalAudioTrakBox { get; set; }
    
    /// <summary>
    /// 视频sidx分段信息
    /// </summary>
    public SidxParser.SidxInfo? SidxInfo { get; set; }
    
    /// <summary>
    /// 音频sidx分段信息
    /// </summary>
    public SidxParser.SidxInfo? AudioSidxInfo { get; set; }
    
    /// <summary>
    /// 音频时间刻度
    /// </summary>
    public uint AudioTimeScale { get; set; }
    
    /// <summary>
    /// 音频时长
    /// </summary>
    public uint AudioDuration { get; set; }
    
    /// <summary>
    /// 音频通道数
    /// </summary>
    public int AudioChannels { get; set; }
    
    /// <summary>
    /// 音频采样率
    /// </summary>
    public uint AudioSampleRate { get; set; }
}
