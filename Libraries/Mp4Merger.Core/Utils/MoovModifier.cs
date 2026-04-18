using System.Text;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// moov盒子修改器
/// 用于修改moov盒子中的时长信息
/// </summary>
public static class MoovModifier
{
    /// <summary>
    /// 修改moov盒子中的时长信息
    /// </summary>
    /// <param name="moovData">原始moov盒子数据</param>
    /// <param name="newDuration">新的时长（以timescale为单位）</param>
    /// <returns>修改后的moov盒子数据</returns>
    public static byte[] ModifyDuration(byte[] moovData, uint newDuration)
    {
        // 创建副本
        byte[] result = new byte[moovData.Length];
        Array.Copy(moovData, result, moovData.Length);
        
        // 查找mvhd盒子
        if (MP4Utils.FindBox(result, "mvhd", out var mvhdOffset, out var mvhdSize))
        {
            // 检查版本
            byte version = result[mvhdOffset + 8];
            
            if (version == 0)
            {
                // 版本0: 时长在偏移量24（4字节）
                if (mvhdOffset + 24 + 4 <= result.Length)
                {
                    MP4Utils.WriteBigEndianUInt32(result, (int)mvhdOffset + 24, newDuration);
                }
            }
            else
            {
                // 版本1: 时长在偏移量32（8字节）
                if (mvhdOffset + 32 + 8 <= result.Length)
                {
                    MP4Utils.WriteBigEndianUInt64(result, (int)mvhdOffset + 32, newDuration);
                }
            }
        }
        
        // 查找并修改所有tkhd盒子中的时长
        long offset = 0;
        while (offset < result.Length - 8)
        {
            if (MP4Utils.FindBox(result, "tkhd", offset, result.Length, out var tkhdOffset, out var tkhdSize))
            {
                byte version = result[tkhdOffset + 8];
                
                if (version == 0)
                {
                    // 版本0: 时长在偏移量28（4字节）
                    if (tkhdOffset + 28 + 4 <= result.Length)
                    {
                        MP4Utils.WriteBigEndianUInt32(result, (int)tkhdOffset + 28, newDuration);
                    }
                }
                else
                {
                    // 版本1: 时长在偏移量36（8字节）
                    if (tkhdOffset + 36 + 8 <= result.Length)
                    {
                        MP4Utils.WriteBigEndianUInt64(result, (int)tkhdOffset + 36, newDuration);
                    }
                }
                
                offset = tkhdOffset + tkhdSize;
            }
            else
            {
                break;
            }
        }
        
        // 查找并修改所有mdhd盒子中的时长
        offset = 0;
        while (offset < result.Length - 8)
        {
            if (MP4Utils.FindBox(result, "mdhd", offset, result.Length, out var mdhdOffset, out var mdhdSize))
            {
                byte version = result[mdhdOffset + 8];
                
                if (version == 0)
                {
                    // 版本0: 时长在偏移量24（4字节）
                    if (mdhdOffset + 24 + 4 <= result.Length)
                    {
                        MP4Utils.WriteBigEndianUInt32(result, (int)mdhdOffset + 24, newDuration);
                    }
                }
                else
                {
                    // 版本1: 时长在偏移量32（8字节）
                    if (mdhdOffset + 32 + 8 <= result.Length)
                    {
                        MP4Utils.WriteBigEndianUInt64(result, (int)mdhdOffset + 32, newDuration);
                    }
                }
                
                offset = mdhdOffset + mdhdSize;
            }
            else
            {
                break;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// 修改trak盒子的ID
    /// </summary>
    /// <param name="trakData">trak盒子数据</param>
    /// <param name="newTrackId">新的trak ID</param>
    /// <returns>修改后的trak盒子数据</returns>
    public static byte[] ModifyTrackId(byte[] trakData, uint newTrackId)
    {
        byte[] result = new byte[trakData.Length];
        Array.Copy(trakData, result, trakData.Length);
        
        // 查找tkhd盒子
        if (MP4Utils.FindBox(result, "tkhd", out var tkhdOffset, out _))
        {
            byte version = result[tkhdOffset + 8];
            
            if (version == 0)
            {
                // 版本0: track_ID在偏移量12（4字节）
                if (tkhdOffset + 12 + 4 <= result.Length)
                {
                    MP4Utils.WriteBigEndianUInt32(result, (int)tkhdOffset + 12, newTrackId);
                }
            }
            else
            {
                // 版本1: track_ID在偏移量20（4字节）
                if (tkhdOffset + 20 + 4 <= result.Length)
                {
                    MP4Utils.WriteBigEndianUInt32(result, (int)tkhdOffset + 20, newTrackId);
                }
            }
        }
        
        return result;
    }
}
