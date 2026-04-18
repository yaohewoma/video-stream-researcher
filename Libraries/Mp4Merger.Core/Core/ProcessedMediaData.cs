using System.Collections.Generic;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Core;

/// <summary>
/// 处理后的媒体数据
/// </summary>
public class ProcessedMediaData
{
    public MP4FileInfo VideoFileInfo { get; set; } = new();
    public MP4FileInfo AudioFileInfo { get; set; } = new();
    public byte[] ExtractedVideoData { get; set; } = Array.Empty<byte>();
    public byte[] ExtractedAudioData { get; set; } = Array.Empty<byte>();
    public List<uint> VideoMdatSizes { get; set; } = new();
    public List<uint> AudioMdatSizes { get; set; } = new();
    public List<byte[]> VideoMdatContents { get; set; } = new();
    public List<FMP4Parser.SampleInfo>? Fmp4VideoSamples { get; set; }
    public List<FMP4Parser.SampleInfo>? Fmp4AudioSamples { get; set; }
}
