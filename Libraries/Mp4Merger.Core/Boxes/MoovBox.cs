using System.Text;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Boxes;

/// <summary>
/// moov盒子类，存储MP4文件的元数据信息
/// </summary>
public class MoovBox : BoxBase
{
    /// <summary>
    /// 视频文件信息
    /// </summary>
    private readonly MP4FileInfo _videoFileInfo;
    
    /// <summary>
    /// 音频文件信息
    /// </summary>
    private readonly MP4FileInfo _audioFileInfo;

    /// <summary>
    /// 视频数据
    /// </summary>
    private readonly byte[]? _videoData;

    /// <summary>
    /// 音频数据
    /// </summary>
    private readonly byte[]? _audioData;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="videoFileInfo">视频文件信息</param>
    /// <param name="audioFileInfo">音频文件信息</param>
    /// <param name="videoData">视频数据</param>
    /// <param name="audioData">音频数据</param>
    public MoovBox(MP4FileInfo videoFileInfo, MP4FileInfo audioFileInfo, byte[]? videoData = null, byte[]? audioData = null) : base("moov") =>
        (_videoFileInfo, _audioFileInfo, _videoData, _audioData, Size) = (videoFileInfo, audioFileInfo, videoData, audioData, CalculateSize());
    
    /// <summary>
    /// 计算盒子大小
    /// </summary>
    /// <returns>盒子大小</returns>
    protected override uint CalculateSize()
    {
        // 计算视频trak盒子的大小
        // 视频avc1盒子大小：86字节 + avcC盒子大小(39字节)
        // 视频stsd盒子大小：16 + (86 + 39) = 141字节
        // 视频stbl盒子大小：8 + 141 + 24 + 28 + 20 + 20 = 241字节
        // 视频minf盒子大小：8 + 20 + 36 + 241 = 305字节
        // 视频mdia盒子大小：8 + 32 + 32 + 305 = 377字节
        // 视频trak盒子大小：8 + 92 + 377 = 477字节
        
        // 计算音频trak盒子的大小
        // 音频stsd盒子大小：40字节
        // 音频stbl盒子大小：8 + 40 + 24 + 28 + 20 + 20 = 140字节
        // 音频minf盒子大小：8 + 12 + 36 + 140 = 196字节
        // 音频mdia盒子大小：8 + 32 + 32 + 196 = 268字节
        // 音频trak盒子大小：8 + 92 + 268 = 368字节
        
        // 计算moov盒子的总大小
        // 1. moov盒子头部 (8字节)
        // 2. mvhd盒子 (108字节)
        // 3. 视频trak盒子 (477字节)
        // 4. 音频trak盒子 (368字节)
        return 8 + 108 + 477 + 368; // 961字节
    }
    
    /// <summary>
    /// 写入盒子数据到流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <returns>写入的字节数</returns>
    public override async Task<long> WriteToStreamAsync(Stream stream)
    {
        using MemoryStream tempStream = new MemoryStream();
        long written = 0;

        // 写入mvhd盒子到临时流
        written += await new MvhdBox(_videoFileInfo, _audioFileInfo).WriteToStreamAsync(tempStream);

        // 写入视频轨道到临时流
        written += await new TrakBox(TrakBox.TrackType.Video, _videoFileInfo, _videoData).WriteToStreamAsync(tempStream);

        // 写入音频轨道到临时流
        written += await new TrakBox(TrakBox.TrackType.Audio, _audioFileInfo, _audioData).WriteToStreamAsync(tempStream);

        // 计算实际的moov盒子大小
        uint actualSize = (uint)(8 + written);

        // 写入moov盒子头部
        byte[] header = new byte[8];
        MP4Utils.WriteBigEndianUInt32(header, 0, actualSize);
        Array.Copy(Encoding.ASCII.GetBytes(Type), 0, header, 4, 4);
        await stream.WriteAsync(header, 0, header.Length);

        // 写入临时流中的数据
        tempStream.Position = 0;
        await tempStream.CopyToAsync(stream);

        return 8 + written;
    }
}