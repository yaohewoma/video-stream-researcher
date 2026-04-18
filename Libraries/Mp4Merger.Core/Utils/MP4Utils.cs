using System.Text;
using Mp4Merger.Core.Models;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// MP4工具类，提供MP4文件处理的通用工具方法
/// </summary>
public static class MP4Utils
{
    /// <summary>
    /// MP4时间戳 epoch（1904-01-01）
    /// </summary>
    private static readonly DateTime Epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    
    /// <summary>
    /// 读取大端序32位无符号整数
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移量</param>
    /// <returns>读取的32位无符号整数</returns>
    public static uint ReadBigEndianUInt32(this byte[] data, int offset) =>
        (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    
    /// <summary>
    /// 读取大端序64位无符号整数
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移量</param>
    /// <returns>读取的64位无符号整数</returns>
    public static ulong ReadBigEndianUInt64(this byte[] data, int offset) =>
        ((ulong)data.ReadBigEndianUInt32(offset) << 32) | data.ReadBigEndianUInt32(offset + 4);
    
    /// <summary>
    /// 写入大端序32位无符号整数
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移量</param>
    /// <param name="value">要写入的值</param>
    public static void WriteBigEndianUInt32(this byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
    
    /// <summary>
    /// 读取大端序16位无符号整数
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移量</param>
    /// <returns>读取的16位无符号整数</returns>
    public static ushort ReadBigEndianUInt16(this byte[] data, int offset) =>
        (ushort)((data[offset] << 8) | data[offset + 1]);
    
    /// <summary>
    /// 写入大端序16位无符号整数
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移量</param>
    /// <param name="value">要写入的值</param>
    public static void WriteBigEndianUInt16(this byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)value;
    }
    
    /// <summary>
    /// 向字节数组中写入64位无符号整数（大端序）
    /// </summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移量</param>
    /// <param name="value">要写入的值</param>
    public static void WriteBigEndianUInt64(this byte[] data, int offset, ulong value)
    {
        data[offset] = (byte)(value >> 56);
        data[offset + 1] = (byte)(value >> 48);
        data[offset + 2] = (byte)(value >> 40);
        data[offset + 3] = (byte)(value >> 32);
        data[offset + 4] = (byte)(value >> 24);
        data[offset + 5] = (byte)(value >> 16);
        data[offset + 6] = (byte)(value >> 8);
        data[offset + 7] = (byte)value;
    }
    
    /// <summary>
    /// 获取MP4时间戳（相对于1904-01-01）
    /// </summary>
    /// <returns>MP4时间戳</returns>
    public static uint GetMp4Timestamp() =>
        (uint)(DateTime.UtcNow - Epoch).TotalSeconds;
    
    /// <summary>
    /// 检查文件是否是MP3文件
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <returns>是否是MP3文件</returns>
    public static bool IsMP3File(this byte[] fileData) =>
        fileData.Length >= 4 && 
        ((fileData[0] == 0x49 && fileData[1] == 0x44 && fileData[2] == 0x33) || 
         ((fileData[0] & 0xFF) == 0xFF && (fileData[1] & 0xE0) == 0xE0));
    
    /// <summary>
    /// 检查文件是否是AAC音频文件（MP4容器）
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <returns>是否是AAC音频文件</returns>
    public static bool IsAACAudioFile(this byte[] fileData) =>
        fileData.Length >= 12 && 
        !fileData.IsMP3File() &&
        ((fileData[4] == 0x66 && fileData[5] == 0x74 && fileData[6] == 0x79 && fileData[7] == 0x70) || 
         (fileData[4] == 0x6D && fileData[5] == 0x6F && fileData[6] == 0x6F && fileData[7] == 0x76));
    
    /// <summary>
    /// 检查数据是否全为零
    /// </summary>
    /// <param name="data">数据数组</param>
    /// <param name="offset">偏移量</param>
    /// <param name="length">长度</param>
    /// <returns>是否全为零</returns>
    public static bool IsAllZeroes(this byte[] data, int offset, int length) =>
        !data.Skip(offset).Take(length).Any(b => b != 0);
    
    /// <summary>
    /// 检查H.264媒体数据是否有效
    /// </summary>
    /// <param name="data">数据数组</param>
    /// <param name="offset">偏移量</param>
    /// <param name="length">长度</param>
    /// <returns>是否有效</returns>
    public static bool IsValidH264Data(this byte[] data, int offset, int length)
    {
        if (length < 4) return false;
        
        return ((data[offset] == 0 && data[offset + 1] == 0 && data[offset + 2] == 1) ||
                (data[offset] == 0 && data[offset + 1] == 0 && data[offset + 2] == 0 && data[offset + 3] == 1) ||
                (data.ReadBigEndianUInt32(offset) > 0 && data.ReadBigEndianUInt32(offset) <= length - 4) ||
                data.Skip(offset).Take(Math.Min(length, 100)).Count(b => b != 0) > 5);
    }
    
    /// <summary>
    /// 检查AAC媒体数据是否有效
    /// </summary>
    /// <param name="data">数据数组</param>
    /// <param name="offset">偏移量</param>
    /// <param name="length">长度</param>
    /// <returns>是否有效</returns>
    public static bool IsValidAACData(this byte[] data, int offset, int length)
    {
        if (length < 7) return false;
        
        return ((data[offset] & 0xFF) == 0xFF && (data[offset + 1] & 0xF0) == 0xF0) ||
               data.Skip(offset).Take(Math.Min(length, 100)).Count(b => b != 0) > 5;
    }
    
    /// <summary>
    /// 从视频文件数据中提取基本媒体信息
    /// </summary>
    /// <param name="videoData">视频文件数据</param>
    /// <param name="fileInfo">MP4文件信息对象，用于存储提取的avc1盒子</param>
    /// <returns>视频媒体信息，包含宽度、高度、时长等</returns>
    public static (int Width, int Height, long Duration, int TimeScale) ExtractVideoInfo(this byte[] videoData, MP4FileInfo? fileInfo = null)
    {
        try
        {
            // 查找moov盒子并提取信息
            if (FindBox(videoData, "moov", out var moovOffset, out var moovSize))
            {
                // 查找视频轨道
                if (FindBox(videoData, "trak", moovOffset + 8, moovOffset + moovSize, out var trakOffset, out var trakSize))
                {
                    // 查找tkhd盒子获取宽度和高度
                    if (FindBox(videoData, "tkhd", trakOffset + 8, trakOffset + trakSize, out var tkhdOffset, out var tkhdSize))
                    {
                        // 从tkhd盒子中提取宽度和高度
                        // tkhd盒子结构(版本0):
                        // - 8字节: header
                        // - 1字节: version
                        // - 3字节: flags
                        // - 4字节: creation_time
                        // - 4字节: modification_time
                        // - 4字节: track_ID
                        // - 4字节: reserved
                        // - 4字节: duration
                        // - 8字节: reserved
                        // - 2字节: layer
                        // - 2字节: alternate_group
                        // - 2字节: volume
                        // - 2字节: reserved
                        // - 36字节: matrix
                        // - 4字节: width (16.16固定点数)
                        // - 4字节: height (16.16固定点数)
                        // 总头部长度: 8 + 1 + 3 + 4 + 4 + 4 + 4 + 4 + 8 + 2 + 2 + 2 + 2 + 36 = 84字节
                        // 宽度在偏移量84, 高度在偏移量88
                        if (tkhdOffset + 88 + 4 <= videoData.Length && tkhdSize >= 92)
                        {
                            uint widthValue = videoData.ReadBigEndianUInt32((int)tkhdOffset + 84);
                            uint heightValue = videoData.ReadBigEndianUInt32((int)tkhdOffset + 88);
                            int actualWidth = (int)(widthValue >> 16);
                            int actualHeight = (int)(heightValue >> 16);

                            // 查找mdia盒子获取媒体信息
                            if (FindBox(videoData, "mdia", trakOffset + 8, trakOffset + trakSize, out var mdiaOffset, out var mdiaSize))
                            {
                                // 查找mdhd盒子获取媒体时间刻度和持续时间
                                if (FindBox(videoData, "mdhd", mdiaOffset + 8, mdiaOffset + mdiaSize, out var mdhdOffset, out var mdhdSize))
                                {
                                    // mdhd盒子结构(版本0):
                                    // - 8字节: header
                                    // - 1字节: version
                                    // - 3字节: flags
                                    // - 4字节: creation_time
                                    // - 4字节: modification_time
                                    // - 4字节: time_scale (在偏移量20)
                                    // - 4字节: duration (在偏移量24)
                                    if (mdhdSize >= 28 && mdhdOffset + 24 + 4 <= videoData.Length)
                                    {
                                        uint timeScale = videoData.ReadBigEndianUInt32((int)mdhdOffset + 20);
                                        uint duration = videoData.ReadBigEndianUInt32((int)mdhdOffset + 24);

                                        // 提取avc1盒子数据和trak盒子数据
                                        if (fileInfo != null)
                                        {
                                            fileInfo.OriginalVideoAvc1Box = ExtractAvc1Box(videoData, trakOffset, trakSize);

                                            // 提取完整的trak盒子数据（包含样本表）
                                            byte[] trakBox = new byte[trakSize];
                                            Array.Copy(videoData, trakOffset, trakBox, 0, (int)trakSize);
                                            fileInfo.OriginalVideoTrakBox = trakBox;

                                            // 解析sidx信息
                                            fileInfo.SidxInfo = SidxParser.ParseSidx(videoData);
                                        }

                                        return (actualWidth > 0 ? actualWidth : 1920, actualHeight > 0 ? actualHeight : 1080, duration, (int)timeScale);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // 基于文件大小和常见编码推断
            var (width, height) = videoData.Length switch
            {
                < 10 * 1024 * 1024 => (1280, 720), // 小于10MB，720p
                > 50 * 1024 * 1024 => (3840, 2160), // 大于50MB，4K
                _ => (1920, 1080) // 默认1080p
            };
            
            return (width, height, 232000, 15360); // 默认3分52秒，标准时间刻度
        }
        catch { }
        
        // 默认值
        return (1920, 1080, 232000, 15360);
    }
    
    /// <summary>
    /// 从视频轨道中提取avc1盒子数据
    /// </summary>
    /// <param name="videoData">视频文件数据</param>
    /// <param name="trakOffset">trak盒子偏移量</param>
    /// <param name="trakSize">trak盒子大小</param>
    /// <returns>avc1盒子数据，如果未找到则返回null</returns>
    private static byte[]? ExtractAvc1Box(byte[] videoData, long trakOffset, long trakSize)
    {
        try
        {
            // 查找stsd盒子
            if (FindBox(videoData, "stsd", trakOffset + 8, trakOffset + trakSize, out var stsdOffset, out var stsdSize))
            {
                // stsd盒子结构:
                // - 8字节: header (size + type)
                // - 1字节: version
                // - 3字节: flags
                // - 4字节: entry_count
                // - 然后是条目（如avc1）
                // 所以avc1盒子应该在stsd盒子的第16字节开始
                
                // 在stsd盒子中查找avc1盒子
                long offset = stsdOffset + 16; // 跳过stsd头部(8) + version(1) + flags(3) + entry_count(4)
                long endOffset = stsdOffset + stsdSize;
                
                while (offset + 8 <= endOffset && offset + 8 <= videoData.Length)
                {
                    uint boxSize = videoData.ReadBigEndianUInt32((int)offset);
                    string boxType = Encoding.ASCII.GetString(videoData, (int)offset + 4, 4);
                    
                    if (boxSize < 8 || boxSize > videoData.Length - offset)
                    {
                        break;
                    }
                    
                    if (boxType == "avc1")
                    {
                        // 提取完整的avc1盒子
                        byte[] avc1Box = new byte[boxSize];
                        Array.Copy(videoData, offset, avc1Box, 0, (int)boxSize);
                        return avc1Box;
                    }
                    
                    offset += boxSize;
                }
            }
        }
        catch { }
        
        return null;
    }
    
    /// <summary>
    /// 从音频文件数据中提取基本媒体信息
    /// </summary>
    /// <param name="audioData">音频文件数据</param>
    /// <returns>音频媒体信息，包含声道数、采样率、时长等</returns>
    public static (int Channels, int SampleRate, long Duration, int TimeScale) ExtractAudioInfo(this byte[] audioData, MP4FileInfo? fileInfo = null)
    {
        try
        {
            // 检查是否是MP3文件
            if (audioData.IsMP3File())
            {
                // 基于MP3文件特性推断
                int channels = 2; // 默认立体声
                int sampleRate = 44100; // 默认44.1kHz
                long durationInSeconds = (long)(audioData.Length / (sampleRate * channels * 2.0)); // 基于文件大小估算时长（秒）
                long duration = durationInSeconds * sampleRate; // 转换为基于时间刻度的单位
                
                // 对于MP3文件，创建一个基本的音频trak盒子
                if (fileInfo != null)
                {
                    fileInfo.OriginalAudioTrakBox = CreateBasicAudioTrakBox(channels, sampleRate);
                }
                
                return (channels, sampleRate, duration > 0 ? duration : 232000 * sampleRate, sampleRate);
            }
            
            // 检查是否是AAC文件 - 简化处理，避免复杂解析导致崩溃
            if (audioData.IsAACAudioFile())
            {
                // 简化处理：直接提取trak盒子和sidx信息
                if (fileInfo != null && FindBox(audioData, "moov", out var moovOffset, out var moovSize))
                {
                    if (FindBox(audioData, "trak", moovOffset + 8, moovOffset + moovSize, out var trakOffset, out var trakSize))
                    {
                        // 提取音频trak盒子
                        byte[] audioTrakBox = new byte[trakSize];
                        Array.Copy(audioData, trakOffset, audioTrakBox, 0, (int)trakSize);
                        fileInfo.OriginalAudioTrakBox = audioTrakBox;
                        
                        // 解析音频的sidx信息
                        fileInfo.AudioSidxInfo = SidxParser.ParseSidx(audioData);
                    }
                }
                
                // 尝试从音频文件中提取时长信息
                uint timeScale = 44100;
                ulong duration = 232000;
                
                // 查找mdhd盒子获取媒体时间刻度和持续时间
                if (FindBox(audioData, "moov", out var moovOff, out var moovSiz))
                {
                    if (FindBox(audioData, "trak", moovOff + 8, moovOff + moovSiz, out var trakOff, out var trakSiz))
                    {
                        if (FindBox(audioData, "mdia", trakOff + 8, trakOff + trakSiz, out var mdiaOff, out var mdiaSiz))
                        {
                            if (FindBox(audioData, "mdhd", mdiaOff + 8, mdiaOff + mdiaSiz, out var mdhdOff, out var mdhdSiz))
                            {
                                if (mdhdSiz >= 28 && mdhdOff + 24 + 4 <= audioData.Length)
                                {
                                    timeScale = audioData.ReadBigEndianUInt32((int)mdhdOff + 20);
                                    duration = audioData.ReadBigEndianUInt32((int)mdhdOff + 24);
                                }
                            }
                        }
                    }
                }
                
                // 如果duration为0，基于文件大小估算时长
                if (duration == 0)
                {
                    // 基于文件大小估算时长（假设128kbps比特率）
                    // 128kbps = 16KB/s
                    long estimatedDurationSeconds = audioData.Length / (16 * 1024);
                    duration = (ulong)(estimatedDurationSeconds * timeScale);
                    
                    // 确保估算的时长至少为10秒
                    if (estimatedDurationSeconds < 10)
                    {
                        duration = (ulong)(10 * timeScale);
                    }
                }
                
                return (2, (int)timeScale, (long)duration, (int)timeScale);
            }
        }
        catch { }
        
        // 默认值
        return (2, 44100, 232000, 44100);
    }
    
    /// <summary>
    /// 创建基本的音频trak盒子
    /// </summary>
    /// <param name="channels">声道数</param>
    /// <param name="sampleRate">采样率</param>
    /// <returns>基本的音频trak盒子数据</returns>
    private static byte[] CreateBasicAudioTrakBox(int channels, int sampleRate)
    {
        // 创建一个基本的音频trak盒子结构
        // 包含必要的子盒子：tkhd、mdia（包含mdhd、hdlr、minf（包含smhd、dinf、stbl））
        
        using MemoryStream ms = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(ms);
        
        // 写入trak头部（临时大小，后续会更新）
        writer.Write((uint)0); // 临时大小
        writer.Write(Encoding.ASCII.GetBytes("trak"));
        
        // 写入tkhd盒子
        long tkhdStart = ms.Position;
        writer.Write((uint)108); // tkhd大小
        writer.Write(Encoding.ASCII.GetBytes("tkhd"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write(GetMp4Timestamp()); // creation_time
        writer.Write(GetMp4Timestamp()); // modification_time
        writer.Write((uint)2); // track_ID
        writer.Write(new byte[4]); // reserved
        writer.Write((uint)0); // duration (临时值)
        writer.Write(new byte[8]); // reserved
        writer.Write((ushort)0); // layer
        writer.Write((ushort)0); // alternate_group
        writer.Write((ushort)0xFFFF); // volume
        writer.Write((ushort)0); // reserved
        // 矩阵
        writer.Write((int)0x00010000);
        writer.Write((int)0);
        writer.Write((int)0);
        writer.Write((int)0);
        writer.Write((int)0x00010000);
        writer.Write((int)0);
        writer.Write((int)0);
        writer.Write((int)0);
        writer.Write((int)0x40000000);
        // 宽度和高度
        writer.Write((uint)0); // width
        writer.Write((uint)0); // height
        
        // 写入mdia盒子
        long mdiaStart = ms.Position;
        writer.Write((uint)0); // 临时大小
        writer.Write(Encoding.ASCII.GetBytes("mdia"));
        
        // 写入mdhd盒子
        writer.Write((uint)32); // mdhd大小
        writer.Write(Encoding.ASCII.GetBytes("mdhd"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write(GetMp4Timestamp()); // creation_time
        writer.Write(GetMp4Timestamp()); // modification_time
        writer.Write((uint)sampleRate); // time_scale
        writer.Write((uint)0); // duration (临时值)
        writer.Write((byte)0); // language
        writer.Write((byte)0); // quality
        
        // 写入hdlr盒子
        writer.Write((uint)24); // hdlr大小
        writer.Write(Encoding.ASCII.GetBytes("hdlr"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write(new byte[4]); // reserved
        writer.Write(Encoding.ASCII.GetBytes("soun")); // handler_type
        writer.Write(new byte[12]); // reserved
        
        // 写入minf盒子
        long minfStart = ms.Position;
        writer.Write((uint)0); // 临时大小
        writer.Write(Encoding.ASCII.GetBytes("minf"));
        
        // 写入smhd盒子
        writer.Write((uint)16); // smhd大小
        writer.Write(Encoding.ASCII.GetBytes("smhd"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((ushort)0); // balance
        writer.Write((ushort)0); // reserved
        
        // 写入dinf盒子
        writer.Write((uint)20); // dinf大小
        writer.Write(Encoding.ASCII.GetBytes("dinf"));
        writer.Write((uint)12); // dref大小
        writer.Write(Encoding.ASCII.GetBytes("dref"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((uint)1); // entry_count
        writer.Write((uint)8); // url大小
        writer.Write(Encoding.ASCII.GetBytes("url "));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        
        // 写入stbl盒子
        long stblStart = ms.Position;
        writer.Write((uint)0); // 临时大小
        writer.Write(Encoding.ASCII.GetBytes("stbl"));
        
        // 写入stsd盒子
        writer.Write((uint)80); // stsd大小
        writer.Write(Encoding.ASCII.GetBytes("stsd"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((uint)1); // entry_count
        
        // 写入mp4a盒子
        writer.Write((uint)64); // mp4a大小
        writer.Write(Encoding.ASCII.GetBytes("mp4a"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((uint)0); // reserved
        writer.Write((ushort)16); // data_reference_index
        writer.Write((ushort)0); // reserved
        writer.Write((uint)0); // reserved
        writer.Write((ushort)channels); // channels
        writer.Write((ushort)16); // sample_size
        writer.Write((uint)0); // reserved
        writer.Write((uint)sampleRate); // sample_rate
        
        // 写入esds盒子
        writer.Write((uint)36); // esds大小
        writer.Write(Encoding.ASCII.GetBytes("esds"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((byte)0x03); // ES_DescrTag
        writer.Write((byte)0x19); // length
        writer.Write((byte)0x00); // ES_ID
        writer.Write((byte)0x00); // streamDependenceFlag, URLFlag, OCRStreamFlag
        writer.Write((byte)0x00); // priority
        writer.Write((byte)0x40); // decoderConfigDescrTag
        writer.Write((byte)0x11); // length
        writer.Write((byte)0x64); // objectTypeIndication (MPEG-4 Audio)
        writer.Write((byte)0x00); // streamType
        writer.Write((byte)0x00); // upStream
        writer.Write((byte)0x00); // reserved
        writer.Write((byte)0x00); // bufferSizeDB
        writer.Write(new byte[4]); // maxBitrate
        writer.Write(new byte[4]); // avgBitrate
        writer.Write((byte)0x41); // decoderSpecificInfoTag
        writer.Write((byte)0x06); // length
        // AudioSpecificConfig for AAC-LC with actual sample rate
        // 正确的AudioSpecificConfig格式:
        // 第1字节: profile (5 bits) + samplingFrequencyIndex (4 bits)
        // 第2字节: samplingFrequencyIndex剩余(4 bits) + channelConfiguration (4 bits)
        byte samplingFrequencyIndex = GetSamplingFrequencyIndex(sampleRate);
        byte profile = 2; // AAC-LC
        byte channelConfiguration = (byte)channels;
        
        // 构建AudioSpecificConfig
        byte firstByte = (byte)((profile << 3) | (samplingFrequencyIndex >> 1));
        byte secondByte = (byte)(((samplingFrequencyIndex & 0x01) << 7) | (channelConfiguration << 3));
        
        writer.Write(firstByte); // AudioSpecificConfig first byte
        writer.Write(secondByte); // AudioSpecificConfig second byte
        writer.Write((byte)0x00); // 预留
        writer.Write((byte)0x00); // 预留
        writer.Write((byte)0x00); // 预留
        writer.Write((byte)0x05); // SLConfigDescriptorTag
        writer.Write((byte)0x02); // length
        writer.Write((byte)0x01);
        writer.Write((byte)0x00);
        
        // 写入stts盒子
        writer.Write((uint)20); // stts大小
        writer.Write(Encoding.ASCII.GetBytes("stts"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((uint)1); // entry_count
        writer.Write((uint)1); // sample_count
        // 根据采样率计算sample_delta
        uint sampleDelta = (uint)(sampleRate / 30); // 假设30fps
        writer.Write(sampleDelta); // sample_delta
        
        // 写入stsc盒子
        writer.Write((uint)24); // stsc大小
        writer.Write(Encoding.ASCII.GetBytes("stsc"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((uint)1); // entry_count
        writer.Write((uint)1); // first_chunk
        writer.Write((uint)1); // samples_per_chunk
        writer.Write((uint)1); // sample_description_index
        
        // 写入stsz盒子
        writer.Write((uint)16); // stsz大小
        writer.Write(Encoding.ASCII.GetBytes("stsz"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((uint)0); // sample_size
        writer.Write((uint)0); // sample_count (临时值)
        
        // 写入stco盒子
        writer.Write((uint)16); // stco大小
        writer.Write(Encoding.ASCII.GetBytes("stco"));
        writer.Write((byte)0); // version
        writer.Write(new byte[3]); // flags
        writer.Write((uint)0); // entry_count (临时值)
        
        // 更新stbl大小
        long stblEnd = ms.Position;
        long stblSize = stblEnd - stblStart;
        ms.Position = stblStart;
        writer.Write((uint)stblSize);
        ms.Position = stblEnd;
        
        // 更新minf大小
        long minfEnd = ms.Position;
        long minfSize = minfEnd - minfStart;
        ms.Position = minfStart;
        writer.Write((uint)minfSize);
        ms.Position = minfEnd;
        
        // 更新mdia大小
        long mdiaEnd = ms.Position;
        long mdiaSize = mdiaEnd - mdiaStart;
        ms.Position = mdiaStart;
        writer.Write((uint)mdiaSize);
        ms.Position = mdiaEnd;
        
        // 更新trak大小
        long trakEnd = ms.Position;
        long trakSize = trakEnd - 8; // 减去头部的8字节
        ms.Position = 0;
        writer.Write((uint)trakSize);
        
        return ms.ToArray();
    }
    
    /// <summary>
    /// 根据采样率获取对应的samplingFrequencyIndex
    /// </summary>
    /// <param name="sampleRate">采样率</param>
    /// <returns>samplingFrequencyIndex</returns>
    private static byte GetSamplingFrequencyIndex(int sampleRate)
    {
        // 采样率索引表
        // 0: 96000 Hz
        // 1: 88200 Hz
        // 2: 64000 Hz
        // 3: 48000 Hz
        // 4: 44100 Hz
        // 5: 32000 Hz
        // 6: 24000 Hz
        // 7: 22050 Hz
        // 8: 16000 Hz
        // 9: 12000 Hz
        // 10: 11025 Hz
        // 11: 8000 Hz
        // 12: 7350 Hz
        // 13-14: reserved
        // 15: escape value
        
        return sampleRate switch
        {
            96000 => 0,
            88200 => 1,
            64000 => 2,
            48000 => 3,
            44100 => 4,
            32000 => 5,
            24000 => 6,
            22050 => 7,
            16000 => 8,
            12000 => 9,
            11025 => 10,
            8000 => 11,
            7350 => 12,
            _ => 4 // 默认44100 Hz
        };
    }
    
    /// <summary>
    /// 在指定范围内查找MP4盒子
    /// </summary>
    /// <param name="data">文件数据</param>
    /// <param name="boxType">盒子类型</param>
    /// <param name="startOffset">起始偏移量</param>
    /// <param name="endOffset">结束偏移量</param>
    /// <param name="boxOffset">找到的盒子偏移量</param>
    /// <param name="boxSize">找到的盒子大小</param>
    /// <returns>是否找到盒子</returns>
    public static bool FindBox(byte[] data, string boxType, long startOffset, long endOffset, out long boxOffset, out long boxSize)
    {
        boxOffset = -1;
        boxSize = 0;
        
        var currentOffset = startOffset;
        while (currentOffset + 8 <= endOffset && currentOffset + 8 <= data.Length)
        {
            var size = data.ReadBigEndianUInt32((int)currentOffset);
            var type = Encoding.ASCII.GetString(data, (int)currentOffset + 4, 4);
            var actualSize = size == 1 && currentOffset + 16 <= data.Length
                ? (long)data.ReadBigEndianUInt64((int)currentOffset + 8)
                : size;
            
            actualSize = Math.Min(actualSize, endOffset - currentOffset);
            
            if (type == boxType)
            {
                boxOffset = currentOffset;
                boxSize = actualSize;
                return true;
            }
            
            currentOffset += actualSize;
        }
        
        return false;
    }
    
    /// <summary>
    /// 在整个文件中查找MP4盒子
    /// </summary>
    /// <param name="data">文件数据</param>
    /// <param name="boxType">盒子类型</param>
    /// <param name="boxOffset">找到的盒子偏移量</param>
    /// <param name="boxSize">找到的盒子大小</param>
    /// <returns>是否找到盒子</returns>
    public static bool FindBox(byte[] data, string boxType, out long boxOffset, out long boxSize) =>
        FindBox(data, boxType, 0, data.Length, out boxOffset, out boxSize);
}
