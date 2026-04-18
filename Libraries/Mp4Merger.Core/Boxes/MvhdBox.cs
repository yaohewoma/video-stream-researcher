using System.Text;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Boxes;

/// <summary>
/// mvhd盒子类，存储MP4文件的全局信息
/// </summary>
public class MvhdBox : BoxBase
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
    /// 构造函数
    /// </summary>
    /// <param name="videoFileInfo">视频文件信息</param>
    /// <param name="audioFileInfo">音频文件信息</param>
    public MvhdBox(MP4FileInfo videoFileInfo, MP4FileInfo audioFileInfo) : base("mvhd") =>
        (_videoFileInfo, _audioFileInfo, Size) = (videoFileInfo, audioFileInfo, CalculateSize());
    
    /// <summary>
    /// 计算盒子大小
    /// </summary>
    /// <returns>盒子大小</returns>
    protected override uint CalculateSize() => 108; // 固定大小108字节
    
    /// <summary>
    /// 写入盒子数据到流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <returns>写入的字节数</returns>
    public override async Task<long> WriteToStreamAsync(Stream stream)
    {
        byte[] buffer = new byte[Size];
        
        // 写入头部
        MP4Utils.WriteBigEndianUInt32(buffer, 0, Size);
        Array.Copy(Encoding.ASCII.GetBytes(Type), 0, buffer, 4, 4);
        
        // 版本和标志
        buffer[8] = 0;
        Array.Clear(buffer, 9, 3);
        
        // 时间戳
        uint timestamp = MP4Utils.GetMp4Timestamp();
        MP4Utils.WriteBigEndianUInt32(buffer, 12, timestamp);
        MP4Utils.WriteBigEndianUInt32(buffer, 16, timestamp);
        
        // 时间刻度
        MP4Utils.WriteBigEndianUInt32(buffer, 20, 1000);
        
        // 时长 - 如果原始duration为0，则根据样本数量计算
        ulong videoDuration = _videoFileInfo.VideoDuration;
        ulong audioDuration = _audioFileInfo.AudioDuration;
        
        // 转换视频duration到mvhd的时间刻度（1000）
        if (_videoFileInfo.VideoTimeScale > 0 && videoDuration > 0)
        {
            // 视频duration转换: videoDuration * (1000 / videoTimeScale)
            videoDuration = (ulong)((double)videoDuration * 1000 / _videoFileInfo.VideoTimeScale);
        }
        else
        {
             videoDuration = 0;
        }
        
        // 转换音频duration到mvhd的时间刻度（1000）
        if (_audioFileInfo.AudioTimeScale > 0 && audioDuration > 0)
        {
            // 音频duration转换: audioDuration * (1000 / audioTimeScale)
            audioDuration = (ulong)((double)audioDuration * 1000 / _audioFileInfo.AudioTimeScale);
        }
        else
        {
             audioDuration = 0;
        }
        
        ulong duration = Math.Max(videoDuration, audioDuration);
        
        // 确保duration不为0，如果为0，则设置一个最小安全值（如1秒）
        // 以避免播放器认为是无效文件
        if (duration == 0) duration = 1000;

        MP4Utils.WriteBigEndianUInt32(buffer, 24, (uint)duration);
        
        // 速率和音量
        MP4Utils.WriteBigEndianUInt32(buffer, 28, 0x00010000);
        MP4Utils.WriteBigEndianUInt16(buffer, 32, 0x0100);
        
        // 预留
        Array.Clear(buffer, 34, 10);
        
        // 矩阵
        byte[] matrix = {
            0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00
        };
        Array.Copy(matrix, 0, buffer, 44, matrix.Length);
        
        // 预定义
        for (int i = 0; i < 5; i++)
            MP4Utils.WriteBigEndianUInt32(buffer, 80 + i * 4, 0);
        MP4Utils.WriteBigEndianUInt32(buffer, 100, 1);
        
        await stream.WriteAsync(buffer, 0, buffer.Length);
        return buffer.Length;
    }
}
