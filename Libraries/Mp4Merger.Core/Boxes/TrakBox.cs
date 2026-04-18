using System.Text;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;
using Mp4Merger.Core.Builders;

namespace Mp4Merger.Core.Boxes;

/// <summary>
/// trak盒子类，存储MP4文件的轨道信息
/// </summary>
public class TrakBox : BoxBase
{
    /// <summary>
    /// 轨道类型
    /// </summary>
    public enum TrackType
    {
        Video,
        Audio
    }

    /// <summary>
    /// 轨道类型
    /// </summary>
    private readonly TrackType _trackType;

    /// <summary>
    /// 文件信息
    /// </summary>
    private readonly MP4FileInfo _fileInfo;

    /// <summary>
    /// 媒体数据
    /// </summary>
    private readonly byte[]? _mediaData;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="trackType">轨道类型</param>
    /// <param name="fileInfo">文件信息</param>
    /// <param name="mediaData">媒体数据</param>
    public TrakBox(TrackType trackType, MP4FileInfo fileInfo, byte[]? mediaData = null) : base("trak") =>
        (_trackType, _fileInfo, _mediaData, Size) = (trackType, fileInfo, mediaData, CalculateSize());

    /// <summary>
    /// 计算盒子大小
    /// </summary>
    /// <returns>盒子大小</returns>
    protected override uint CalculateSize()
    {
        // 使用临时流计算准确的大小
        using MemoryStream tempStream = new MemoryStream();
        
        // 模拟写入tkhd和mdia盒子
        var writeTask = Task.Run(async () => {
            await WriteTkhdBoxAsync(tempStream);
            await WriteMdiaBoxAsync(tempStream);
        });
        writeTask.Wait();
        
        // 计算总大小：trak头部(8字节) + 内容大小
        return 8 + (uint)tempStream.Length;
    }

    /// <summary>
    /// 写入盒子数据到流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <returns>写入的字节数</returns>
    public override async Task<long> WriteToStreamAsync(Stream stream)
    {
        // 如果有原始trak盒子数据，直接使用原始数据
        byte[]? originalTrakBox = _trackType == TrackType.Video 
            ? _fileInfo?.OriginalVideoTrakBox 
            : _fileInfo?.OriginalAudioTrakBox;
            
        if (originalTrakBox != null)
        {
            // 修改原始trak盒子（如果需要）
            byte[] modifiedTrakBox = ModifyOriginalTrakBox(originalTrakBox);
            await stream.WriteAsync(modifiedTrakBox, 0, modifiedTrakBox.Length);
            return modifiedTrakBox.Length;
        }
        
        using MemoryStream tempStream = new MemoryStream();
        long written = 0;

        // 写入tkhd盒子到临时流
        written += await WriteTkhdBoxAsync(tempStream);

        // 写入mdia盒子到临时流
        written += await WriteMdiaBoxAsync(tempStream);

        // 计算实际的trak盒子大小
        uint actualSize = (uint)(8 + written);

        // 写入trak盒子头部
        byte[] header = new byte[8];
        MP4Utils.WriteBigEndianUInt32(header, 0, actualSize);
        Array.Copy(Encoding.ASCII.GetBytes(Type), 0, header, 4, 4);
        await stream.WriteAsync(header, 0, header.Length);

        // 写入临时流中的数据
        tempStream.Position = 0;
        await tempStream.CopyToAsync(stream);

        return 8 + written;
    }
    
    /// <summary>
    /// 修改原始trak盒子（例如更新avcC盒子）
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <returns>修改后的trak盒子数据</returns>
    private byte[] ModifyOriginalTrakBox(byte[] originalTrakBox)
    {
        // 返回原始trak盒子数据
        // 注意：trak盒子应该在MP4Merger中重建，这里直接返回重建后的数据
        return originalTrakBox;
    }

    /// <summary>
    /// 写入tkhd盒子
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <returns>写入的字节数</returns>
    private async Task<long> WriteTkhdBoxAsync(Stream stream)
    {
        // tkhd盒子结构 (版本0):
        // - 4字节: size
        // - 4字节: type ("tkhd")
        // - 1字节: version (0)
        // - 3字节: flags (0x000001 = track enabled)
        // - 4字节: creation_time
        // - 4字节: modification_time
        // - 4字节: track_ID
        // - 4字节: reserved
        // - 4字节: duration
        // - 8字节: reserved
        // - 2字节: layer
        // - 2字节: alternate_group
        // - 2字节: volume (仅音频)
        // - 2字节: reserved
        // - 36字节: matrix
        // - 4字节: width (16.16 fixed-point)
        // - 4字节: height (16.16 fixed-point)
        // 总共: 4 + 4 + 1 + 3 + 4 + 4 + 4 + 4 + 4 + 8 + 2 + 2 + 2 + 2 + 36 + 4 + 4 = 92字节
        
        byte[] buffer = new byte[92];

        // 写入头部
        MP4Utils.WriteBigEndianUInt32(buffer, 0, 92);
        Array.Copy(Encoding.ASCII.GetBytes("tkhd"), 0, buffer, 4, 4);

        // 版本 (0)
        buffer[8] = 0;
        // 标志 (0x000001 = track enabled, in movie, in preview)
        buffer[9] = 0x00;
        buffer[10] = 0x00;
        buffer[11] = 0x01;

        // 时间戳
        uint timestamp = MP4Utils.GetMp4Timestamp();
        MP4Utils.WriteBigEndianUInt32(buffer, 12, timestamp); // creation_time
        MP4Utils.WriteBigEndianUInt32(buffer, 16, timestamp); // modification_time

        // 轨道ID (1 = 视频, 2 = 音频)
        uint trackId = _trackType == TrackType.Video ? (uint)1 : (uint)2;
        MP4Utils.WriteBigEndianUInt32(buffer, 20, trackId);

        // 预留 (4字节)
        Array.Clear(buffer, 24, 4);

        // 持续时间
        uint duration = 0;
        if (_fileInfo != null)
        {
            if (_trackType == TrackType.Video && _fileInfo.VideoTimeScale > 0)
            {
                // 转换为mvhd的时间刻度 (1000)
                duration = (uint)((long)_fileInfo.VideoDuration * 1000 / _fileInfo.VideoTimeScale);
            }
            else if (_trackType == TrackType.Audio && _fileInfo.AudioTimeScale > 0)
            {
                // 转换为mvhd的时间刻度 (1000)
                duration = (uint)((long)_fileInfo.AudioDuration * 1000 / _fileInfo.AudioTimeScale);
            }
        }
        
        // 确保持续时间不为0
        if (duration == 0)
        {
            // 对于音频轨道，使用与视频轨道相同的持续时间或默认值
            if (_trackType == TrackType.Audio && _fileInfo != null && _fileInfo.VideoDuration > 0 && _fileInfo.VideoTimeScale > 0)
            {
                duration = (uint)((long)_fileInfo.VideoDuration * 1000 / _fileInfo.VideoTimeScale);
            }
            else
            {
                duration = 232000; // 默认3分52秒
            }
        }
        MP4Utils.WriteBigEndianUInt32(buffer, 28, duration);

        // 预留 (8字节)
        Array.Clear(buffer, 32, 8);

        // 层 (2字节)
        MP4Utils.WriteBigEndianUInt16(buffer, 40, 0);
        // 交替组 (2字节)
        MP4Utils.WriteBigEndianUInt16(buffer, 42, 0);
        // 音量 (2字节, 仅音频有效)
        if (_trackType == TrackType.Audio)
        {
            buffer[44] = 0x01; // 音量 1.0 (0x0100)
            buffer[45] = 0x00;
        }
        else
        {
            buffer[44] = 0x00;
            buffer[45] = 0x00;
        }
        // 预留 (2字节)
        Array.Clear(buffer, 46, 2);

        // 矩阵 (36字节) - 单位矩阵，使用正确的16.16固定点格式
        byte[] matrix = {
            0x00, 0x01, 0x00, 0x00, // a (1.0 in 16.16 fixed-point = 65536)
            0x00, 0x00, 0x00, 0x00, // b (0)
            0x00, 0x00, 0x00, 0x00, // u (0)
            0x00, 0x00, 0x00, 0x00, // c (0)
            0x00, 0x01, 0x00, 0x00, // d (1.0 in 16.16 fixed-point = 65536)
            0x00, 0x00, 0x00, 0x00, // v (0)
            0x00, 0x00, 0x00, 0x00, // x (0)
            0x00, 0x00, 0x00, 0x00, // y (0)
            0x00, 0x00, 0x40, 0x00  // w (1.0 in 2.30 fixed-point)
        };
        Array.Copy(matrix, 0, buffer, 48, matrix.Length);

        // 宽高 (16.16 fixed-point)
        if (_trackType == TrackType.Video)
        {
            uint width = 1920;
            uint height = 1080;
            if (_fileInfo != null && _fileInfo.VideoWidth > 0 && _fileInfo.VideoHeight > 0)
            {
                width = (uint)_fileInfo.VideoWidth;
                height = (uint)_fileInfo.VideoHeight;
            }
            // 16.16 fixed-point: 整数部分左移16位
            MP4Utils.WriteBigEndianUInt32(buffer, 84, width << 16);
            MP4Utils.WriteBigEndianUInt32(buffer, 88, height << 16);
        }
        else
        {
            // 音频轨道没有宽高，设置为0
            MP4Utils.WriteBigEndianUInt32(buffer, 84, 0);
            MP4Utils.WriteBigEndianUInt32(buffer, 88, 0);
        }

        await stream.WriteAsync(buffer, 0, buffer.Length);
        return buffer.Length;
    }

    /// <summary>
    /// 写入mdia盒子
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <returns>写入的字节数</returns>
    private async Task<long> WriteMdiaBoxAsync(Stream stream)
    {
        // 重新设计WriteMdiaBoxAsync方法，使用临时流计算准确的大小
        using MemoryStream tempStream = new MemoryStream();

        // 1. 写入mdhd盒子
        byte[] mdhdBox = new byte[32];
        MP4Utils.WriteBigEndianUInt32(mdhdBox, 0, 32);
        Array.Copy(Encoding.ASCII.GetBytes("mdhd"), 0, mdhdBox, 4, 4);
        mdhdBox[8] = 0;
        Array.Clear(mdhdBox, 9, 3);

        uint timestamp = MP4Utils.GetMp4Timestamp();
        MP4Utils.WriteBigEndianUInt32(mdhdBox, 12, timestamp);
        MP4Utils.WriteBigEndianUInt32(mdhdBox, 16, timestamp);

        uint timeScale = _trackType == TrackType.Video ? (uint)15360 : (uint)44100;
        if (_fileInfo != null)
        {
            timeScale = _trackType == TrackType.Video ? _fileInfo.VideoTimeScale : _fileInfo.AudioTimeScale;
        }
        MP4Utils.WriteBigEndianUInt32(mdhdBox, 20, timeScale);

        uint duration = 232000;
        if (_fileInfo != null)
        {
            duration = _trackType == TrackType.Video ? _fileInfo.VideoDuration : _fileInfo.AudioDuration;
        }
        // 确保持续时间不为0
        if (duration == 0)
        {
            // 对于音频轨道，使用与视频轨道相同的持续时间或默认值
            if (_trackType == TrackType.Audio && _fileInfo != null && _fileInfo.VideoDuration > 0)
            {
                duration = _fileInfo.VideoDuration;
            }
            else
            {
                duration = 232000; // 默认3分52秒
            }
        }
        MP4Utils.WriteBigEndianUInt32(mdhdBox, 24, duration);

        // 语言
        mdhdBox[28] = 0x55;
        mdhdBox[29] = 0xC4;

        // 预留
        Array.Clear(mdhdBox, 30, 2);

        tempStream.Write(mdhdBox, 0, mdhdBox.Length);

        // 2. 写入hdlr盒子
        byte[] hdlrBox = new byte[32];
        MP4Utils.WriteBigEndianUInt32(hdlrBox, 0, 32);
        Array.Copy(Encoding.ASCII.GetBytes("hdlr"), 0, hdlrBox, 4, 4);
        hdlrBox[8] = 0;
        Array.Clear(hdlrBox, 9, 3);
        Array.Copy(_trackType == TrackType.Video ? new byte[] { 0x76, 0x69, 0x64, 0x65 } : new byte[] { 0x61, 0x75, 0x64, 0x69 }, 0, hdlrBox, 12, 4);
        Array.Clear(hdlrBox, 16, 16);

        tempStream.Write(hdlrBox, 0, hdlrBox.Length);

        // 3. 写入minf盒子
        using MemoryStream minfTempStream = new MemoryStream();
        
        // 3.1 写入vmhd或smhd盒子
        if (_trackType == TrackType.Video)
        {
            // 视频轨道：vmhd盒子
            byte[] vmhdBox = new byte[20];
            MP4Utils.WriteBigEndianUInt32(vmhdBox, 0, 20);
            Array.Copy(Encoding.ASCII.GetBytes("vmhd"), 0, vmhdBox, 4, 4);
            vmhdBox[8] = 0;
            Array.Clear(vmhdBox, 9, 3);
            
            // graphicsmode和opcolor
            MP4Utils.WriteBigEndianUInt16(vmhdBox, 12, 0);
            MP4Utils.WriteBigEndianUInt16(vmhdBox, 14, 0);
            MP4Utils.WriteBigEndianUInt16(vmhdBox, 16, 0);
            MP4Utils.WriteBigEndianUInt16(vmhdBox, 18, 0);

            minfTempStream.Write(vmhdBox, 0, vmhdBox.Length);
        }
        else
        {
            // 音频轨道：smhd盒子
            byte[] smhdBox = new byte[16];
            MP4Utils.WriteBigEndianUInt32(smhdBox, 0, 16);
            Array.Copy(Encoding.ASCII.GetBytes("smhd"), 0, smhdBox, 4, 4);
            smhdBox[8] = 0;
            Array.Clear(smhdBox, 9, 3);
            // 音量 (0x0100 = 1.0)
            MP4Utils.WriteBigEndianUInt16(smhdBox, 12, 0x0100);
            // 保留
            Array.Clear(smhdBox, 14, 2);

            minfTempStream.Write(smhdBox, 0, smhdBox.Length);
        }

        // 3.2 写入dinf盒子
        byte[] dinfBox = new byte[36];
        MP4Utils.WriteBigEndianUInt32(dinfBox, 0, 36);
        Array.Copy(Encoding.ASCII.GetBytes("dinf"), 0, dinfBox, 4, 4);
        
        // 写入dref盒子
        MP4Utils.WriteBigEndianUInt32(dinfBox, 8, 28);
        Array.Copy(Encoding.ASCII.GetBytes("dref"), 0, dinfBox, 12, 4);
        dinfBox[16] = 0;
        Array.Clear(dinfBox, 17, 3);
        MP4Utils.WriteBigEndianUInt32(dinfBox, 20, 1);
        
        // 写入url盒子
        MP4Utils.WriteBigEndianUInt32(dinfBox, 24, 12);
        Array.Copy(Encoding.ASCII.GetBytes("url "), 0, dinfBox, 28, 4);
        Array.Clear(dinfBox, 32, 4);

        minfTempStream.Write(dinfBox, 0, dinfBox.Length);

        // 3.3 写入stbl盒子
        using MemoryStream stblTempStream = new MemoryStream();
        
        // 3.3.1 写入stsd盒子
        if (_trackType == TrackType.Video)
        {
            // 使用VideoTrackBuilder创建视频stsd盒子
            byte[] stsdBox = VideoTrackBuilder.CreateVideoStsdBox(_fileInfo);
            stblTempStream.Write(stsdBox, 0, stsdBox.Length);
        }
        else
        {
            // 使用AudioTrackBuilder创建音频stsd盒子
            byte[] stsdBox = AudioTrackBuilder.CreateAudioStsdBox(_fileInfo);
            stblTempStream.Write(stsdBox, 0, stsdBox.Length);
        }

        // 3.3.2 写入stts盒子
        byte[] sttsBox = new byte[24];
        MP4Utils.WriteBigEndianUInt32(sttsBox, 0, 24);
        Array.Copy(Encoding.ASCII.GetBytes("stts"), 0, sttsBox, 4, 4);
        sttsBox[8] = 0;
        Array.Clear(sttsBox, 9, 3);
        uint sttsSampleDelta = duration > 0 ? duration : 1;
        MP4Utils.WriteBigEndianUInt32(sttsBox, 12, 1);
        MP4Utils.WriteBigEndianUInt32(sttsBox, 16, 1);
        MP4Utils.WriteBigEndianUInt32(sttsBox, 20, sttsSampleDelta);

        stblTempStream.Write(sttsBox, 0, sttsBox.Length);

        // 3.3.3 写入stsc盒子
        byte[] stscBox = new byte[28];
        MP4Utils.WriteBigEndianUInt32(stscBox, 0, 28);
        Array.Copy(Encoding.ASCII.GetBytes("stsc"), 0, stscBox, 4, 4);
        stscBox[8] = 0;
        Array.Clear(stscBox, 9, 3);
        MP4Utils.WriteBigEndianUInt32(stscBox, 12, 1);
        MP4Utils.WriteBigEndianUInt32(stscBox, 16, 1);
        MP4Utils.WriteBigEndianUInt32(stscBox, 20, 1);
        MP4Utils.WriteBigEndianUInt32(stscBox, 24, 1);

        stblTempStream.Write(stscBox, 0, stscBox.Length);

        // 3.3.4 写入stsz盒子
        byte[] stszBox = new byte[20];
        MP4Utils.WriteBigEndianUInt32(stszBox, 0, 20);
        Array.Copy(Encoding.ASCII.GetBytes("stsz"), 0, stszBox, 4, 4);
        stszBox[8] = 0;
        Array.Clear(stszBox, 9, 3);
        
        uint sampleSize = _mediaData != null ? (uint)_mediaData.Length : 1024;
        MP4Utils.WriteBigEndianUInt32(stszBox, 12, sampleSize);
        
        uint sampleCount = _trackType == TrackType.Video ? (uint)1 : (uint)1;
        MP4Utils.WriteBigEndianUInt32(stszBox, 16, sampleCount);

        stblTempStream.Write(stszBox, 0, stszBox.Length);

        // 3.3.5 写入stco盒子
        byte[] stcoBox = new byte[20];
        MP4Utils.WriteBigEndianUInt32(stcoBox, 0, 20);
        Array.Copy(Encoding.ASCII.GetBytes("stco"), 0, stcoBox, 4, 4);
        stcoBox[8] = 0;
        Array.Clear(stcoBox, 9, 3);
        MP4Utils.WriteBigEndianUInt32(stcoBox, 12, 1);
        
        // 计算mdat盒子的起始位置
        uint estimatedMoovSize = 1200;
        uint mdatOffset = 32 + estimatedMoovSize;
        MP4Utils.WriteBigEndianUInt32(stcoBox, 16, mdatOffset);

        stblTempStream.Write(stcoBox, 0, stcoBox.Length);
        
        // 写入stbl盒子头部
        int stblContentSize = (int)stblTempStream.Length;
        int stblSize = 8 + stblContentSize;
        byte[] stblHeader = new byte[8];
        MP4Utils.WriteBigEndianUInt32(stblHeader, 0, (uint)stblSize);
        Array.Copy(Encoding.ASCII.GetBytes("stbl"), 0, stblHeader, 4, 4);
        minfTempStream.Write(stblHeader, 0, stblHeader.Length);
        
        // 写入stbl内容
        stblTempStream.Position = 0;
        await stblTempStream.CopyToAsync(minfTempStream);
        
        // 写入minf盒子头部
        int minfContentSize = (int)minfTempStream.Length;
        int minfSize = 8 + minfContentSize;
        byte[] minfHeader = new byte[8];
        MP4Utils.WriteBigEndianUInt32(minfHeader, 0, (uint)minfSize);
        Array.Copy(Encoding.ASCII.GetBytes("minf"), 0, minfHeader, 4, 4);
        tempStream.Write(minfHeader, 0, minfHeader.Length);
        
        // 写入minf内容
        minfTempStream.Position = 0;
        await minfTempStream.CopyToAsync(tempStream);
        
        // 写入mdia盒子头部
        int mdiaContentSize = (int)tempStream.Length;
        int mdiaSize = 8 + mdiaContentSize;
        byte[] mdiaHeader = new byte[8];
        MP4Utils.WriteBigEndianUInt32(mdiaHeader, 0, (uint)mdiaSize);
        Array.Copy(Encoding.ASCII.GetBytes("mdia"), 0, mdiaHeader, 4, 4);
        await stream.WriteAsync(mdiaHeader, 0, mdiaHeader.Length);
        
        // 写入mdia内容
        tempStream.Position = 0;
        await tempStream.CopyToAsync(stream);
        
        return mdiaSize;
    }


}
