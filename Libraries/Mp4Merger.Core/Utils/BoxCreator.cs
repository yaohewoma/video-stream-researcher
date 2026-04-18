using System.Text;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// MP4盒子创建器
/// 用于创建各种MP4盒子
/// </summary>
public static class BoxCreator
{
    /// <summary>
    /// 创建ESDS盒子，包含音频解码器配置信息
    /// </summary>
    /// <param name="sampleRate">采样率</param>
    /// <param name="channels">声道数</param>
    /// <returns>ESDS盒子数据</returns>
    public static byte[] CreateEsdsBox(int sampleRate = 44100, int channels = 2)
    {
        var samplingFrequencyIndex = GetSamplingFrequencyIndex(sampleRate);
        var audioObjectType = (byte)2;
        var channelConfiguration = (byte)Math.Min(7, Math.Max(0, channels));

        var asc = new byte[2];
        asc[0] = (byte)((audioObjectType << 3) | (samplingFrequencyIndex >> 1));
        asc[1] = (byte)(((samplingFrequencyIndex & 0x01) << 7) | (channelConfiguration << 3));

        var decSpecific = new byte[2 + asc.Length];
        decSpecific[0] = 0x05;
        decSpecific[1] = (byte)asc.Length;
        Array.Copy(asc, 0, decSpecific, 2, asc.Length);

        var decConfigBody = new byte[13 + decSpecific.Length];
        var offset = 0;
        decConfigBody[offset++] = 0x40;
        decConfigBody[offset++] = 0x15;
        decConfigBody[offset++] = 0x00;
        decConfigBody[offset++] = 0x00;
        decConfigBody[offset++] = 0x00;
        Array.Clear(decConfigBody, offset, 8);
        offset += 8;
        Array.Copy(decSpecific, 0, decConfigBody, offset, decSpecific.Length);

        var decConfig = new byte[2 + decConfigBody.Length];
        decConfig[0] = 0x04;
        decConfig[1] = (byte)decConfigBody.Length;
        Array.Copy(decConfigBody, 0, decConfig, 2, decConfigBody.Length);

        var slConfig = new byte[] { 0x06, 0x01, 0x02 };

        var esBody = new byte[3 + decConfig.Length + slConfig.Length];
        esBody[0] = 0x00;
        esBody[1] = 0x00;
        esBody[2] = 0x00;
        Array.Copy(decConfig, 0, esBody, 3, decConfig.Length);
        Array.Copy(slConfig, 0, esBody, 3 + decConfig.Length, slConfig.Length);

        var esDescriptor = new byte[2 + esBody.Length];
        esDescriptor[0] = 0x03;
        esDescriptor[1] = (byte)esBody.Length;
        Array.Copy(esBody, 0, esDescriptor, 2, esBody.Length);

        var esdsContent = esDescriptor;
        var esdsSize = 8 + 4 + esdsContent.Length;
        
        // 创建完整的esds盒子
        byte[] esdsBox = new byte[esdsSize];
        offset = 0;
        
        // 写入头部
        MP4Utils.WriteBigEndianUInt32(esdsBox, offset, (uint)esdsSize);
        offset += 4;
        Array.Copy(Encoding.ASCII.GetBytes("esds"), 0, esdsBox, offset, 4);
        offset += 4;
        
        // 写入版本和标志
        esdsBox[offset++] = 0;
        Array.Clear(esdsBox, offset, 3);
        offset += 3;
        
        // 写入内容
        Array.Copy(esdsContent, 0, esdsBox, offset, esdsContent.Length);
        
        return esdsBox;
    }
    
    /// <summary>
    /// 根据采样率获取对应的samplingFrequencyIndex
    /// </summary>
    /// <param name="sampleRate">采样率</param>
    /// <returns>samplingFrequencyIndex</returns>
    private static byte GetSamplingFrequencyIndex(int sampleRate)
    {
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
}
