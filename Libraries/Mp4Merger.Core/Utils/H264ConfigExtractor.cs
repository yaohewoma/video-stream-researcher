using System.Text;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// H.264解码器配置提取器
/// 从H.264样本数据中提取SPS和PPS，创建avcC盒子
/// </summary>
public static class H264ConfigExtractor
{
    /// <summary>
    /// H.264配置信息
    /// </summary>
    public class H264Config
    {
        /// <summary>
        /// SPS数据列表
        /// </summary>
        public List<byte[]> SPSList { get; } = new List<byte[]>();

        /// <summary>
        /// PPS数据列表
        /// </summary>
        public List<byte[]> PPSList { get; } = new List<byte[]>();

        /// <summary>
        /// 配置版本（通常为1）
        /// </summary>
        public byte ConfigurationVersion { get; set; } = 1;

        /// <summary>
        /// AVC Profile（如100表示High Profile）
        /// </summary>
        public byte AVCProfileIndication { get; set; }

        /// <summary>
        /// Profile兼容性
        /// </summary>
        public byte ProfileCompatibility { get; set; }

        /// <summary>
        /// AVC Level（如31表示Level 3.1）
        /// </summary>
        public byte AVCLevelIndication { get; set; }

        /// <summary>
        /// NAL单元长度大小减1（通常为3，表示4字节长度前缀）
        /// </summary>
        public byte LengthSizeMinusOne { get; set; } = 3;
    }

    /// <summary>
    /// 从H.264样本数据中提取SPS和PPS
    /// 支持AVCC格式（长度前缀）和Annex B格式（起始码）
    /// </summary>
    /// <param name="sampleData">样本数据</param>
    /// <returns>H.264配置信息，如果未找到则返回null</returns>
    public static H264Config? ExtractConfigFromSample(byte[] sampleData)
    {
        if (sampleData == null || sampleData.Length < 4)
            return null;

        // 检测是否为Annex B格式 (00 00 00 01 或 00 00 01)
        bool isAnnexB = (sampleData[0] == 0 && sampleData[1] == 0 && sampleData[2] == 0 && sampleData[3] == 1) ||
                        (sampleData[0] == 0 && sampleData[1] == 0 && sampleData[2] == 1);

        if (isAnnexB)
        {
            return ExtractConfigFromAnnexB(sampleData);
        }
        else
        {
            return ExtractConfigFromAVCC(sampleData);
        }
    }

    /// <summary>
    /// 从AVCC格式数据中提取配置
    /// </summary>
    private static H264Config? ExtractConfigFromAVCC(byte[] sampleData)
    {
        var config = new H264Config();
        int offset = 0;

        // 解析AVCC格式的样本数据
        while (offset < sampleData.Length - 4)
        {
            // 读取NAL单元长度（4字节，大端序）
            uint nalLength = (uint)((sampleData[offset] << 24) | (sampleData[offset + 1] << 16) |
                                   (sampleData[offset + 2] << 8) | sampleData[offset + 3]);

            if (nalLength == 0 || nalLength > sampleData.Length - offset - 4)
                break;

            offset += 4;

            // 读取NAL单元类型
            if (offset < sampleData.Length)
            {
                byte nalUnitType = (byte)(sampleData[offset] & 0x1F);

                // NAL单元类型7 = SPS
                if (nalUnitType == 7 && nalLength > 1)
                {
                    byte[] sps = new byte[nalLength];
                    Array.Copy(sampleData, offset, sps, 0, (int)nalLength);
                    // 避免重复添加
                    if (!config.SPSList.Any(s => s.SequenceEqual(sps)))
                    {
                        config.SPSList.Add(sps);

                        // 从SPS提取profile和level信息
                        if (sps.Length >= 4)
                        {
                            config.ConfigurationVersion = 1;
                            config.AVCProfileIndication = sps[1];
                            config.ProfileCompatibility = sps[2];
                            config.AVCLevelIndication = sps[3];
                        }
                    }
                }
                // NAL单元类型8 = PPS
                else if (nalUnitType == 8 && nalLength > 1)
                {
                    byte[] pps = new byte[nalLength];
                    Array.Copy(sampleData, offset, pps, 0, (int)nalLength);
                    // 避免重复添加
                    if (!config.PPSList.Any(p => p.SequenceEqual(pps)))
                    {
                        config.PPSList.Add(pps);
                    }
                }
            }

            offset += (int)nalLength;
        }

        // 如果没有找到SPS或PPS，返回null
        if (config.SPSList.Count == 0 || config.PPSList.Count == 0)
            return null;

        return config;
    }

    /// <summary>
    /// 从Annex B格式数据中提取配置
    /// </summary>
    private static H264Config? ExtractConfigFromAnnexB(byte[] sampleData)
    {
        var config = new H264Config();
        int offset = 0;

        while (offset < sampleData.Length - 4)
        {
            // 查找起始码
            int nextStartCode = FindNextStartCode(sampleData, offset);
            if (nextStartCode == -1) break;

            int dataStart = nextStartCode;
            // 跳过起始码 (3或4字节)
            if (sampleData[nextStartCode] == 0 && sampleData[nextStartCode + 1] == 0)
            {
                if (sampleData[nextStartCode + 2] == 1) dataStart += 3;
                else if (sampleData[nextStartCode + 2] == 0 && sampleData[nextStartCode + 3] == 1) dataStart += 4;
                else { offset = nextStartCode + 1; continue; } // 不是有效的起始码
            }
            else { offset = nextStartCode + 1; continue; }

            // 查找下一个起始码作为结束
            int nextNalStart = FindNextStartCode(sampleData, dataStart);
            int dataEnd = (nextNalStart == -1) ? sampleData.Length : nextNalStart;
            int nalLength = dataEnd - dataStart;

            if (nalLength > 0)
            {
                byte nalUnitType = (byte)(sampleData[dataStart] & 0x1F);

                // NAL单元类型7 = SPS
                if (nalUnitType == 7)
                {
                    byte[] sps = new byte[nalLength];
                    Array.Copy(sampleData, dataStart, sps, 0, nalLength);
                    if (!config.SPSList.Any(s => s.SequenceEqual(sps)))
                    {
                        config.SPSList.Add(sps);
                        if (sps.Length >= 4)
                        {
                            config.ConfigurationVersion = 1;
                            config.AVCProfileIndication = sps[1];
                            config.ProfileCompatibility = sps[2];
                            config.AVCLevelIndication = sps[3];
                        }
                    }
                }
                // NAL单元类型8 = PPS
                else if (nalUnitType == 8)
                {
                    byte[] pps = new byte[nalLength];
                    Array.Copy(sampleData, dataStart, pps, 0, nalLength);
                    if (!config.PPSList.Any(p => p.SequenceEqual(pps)))
                    {
                        config.PPSList.Add(pps);
                    }
                }
            }

            offset = dataEnd;
        }

        if (config.SPSList.Count == 0 || config.PPSList.Count == 0)
            return null;

        return config;
    }

    /// <summary>
    /// 查找下一个起始码
    /// </summary>
    private static int FindNextStartCode(byte[] data, int startIndex)
    {
        for (int i = startIndex; i < data.Length - 3; i++)
        {
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1) return i;
                if (data[i + 2] == 0 && data[i + 3] == 1) return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 创建avcC盒子数据
    /// </summary>
    /// <param name="config">H.264配置信息</param>
    /// <returns>avcC盒子数据</returns>
    public static byte[] CreateAvcCBox(H264Config config)
    {
        // 计算avcC盒子大小
        int spsTotalSize = 0;
        foreach (var sps in config.SPSList)
        {
            spsTotalSize += 2 + sps.Length; // 2字节长度前缀 + SPS数据
        }

        int ppsTotalSize = 0;
        foreach (var pps in config.PPSList)
        {
            ppsTotalSize += 2 + pps.Length; // 2字节长度前缀 + PPS数据
        }

        // 检查是否需要High Profile扩展
        bool hasExtensions = config.AVCProfileIndication == 100 || 
                             config.AVCProfileIndication == 110 || 
                             config.AVCProfileIndication == 122 || 
                             config.AVCProfileIndication == 144;
                             
        int extensionSize = hasExtensions ? 4 : 0;

        uint boxSize = (uint)(8 + 6 + spsTotalSize + 1 + ppsTotalSize + extensionSize);

        byte[] data = new byte[boxSize];
        int offset = 0;

        // 盒子大小
        MP4Utils.WriteBigEndianUInt32(data, offset, boxSize);
        offset += 4;

        // 盒子类型 "avcC"
        Array.Copy(Encoding.ASCII.GetBytes("avcC"), 0, data, offset, 4);
        offset += 4;

        // configurationVersion
        data[offset++] = config.ConfigurationVersion;

        // AVCProfileIndication
        data[offset++] = config.AVCProfileIndication;

        // profile_compatibility
        data[offset++] = config.ProfileCompatibility;

        // AVCLevelIndication
        data[offset++] = config.AVCLevelIndication;

        // reserved (6 bits) + lengthSizeMinusOne (2 bits)
        data[offset++] = (byte)(0xFC | (config.LengthSizeMinusOne & 0x03));

        // reserved (3 bits) + numOfSequenceParameterSets (5 bits)
        data[offset++] = (byte)(0xE0 | (config.SPSList.Count & 0x1F));

        // SPS列表
        foreach (var sps in config.SPSList)
        {
            // SPS长度（2字节）
            MP4Utils.WriteBigEndianUInt16(data, offset, (ushort)sps.Length);
            offset += 2;

            // SPS数据
            Array.Copy(sps, 0, data, offset, sps.Length);
            offset += sps.Length;
        }

        // numOfPictureParameterSets
        data[offset++] = (byte)config.PPSList.Count;

        // PPS列表
        foreach (var pps in config.PPSList)
        {
            // PPS长度（2字节）
            MP4Utils.WriteBigEndianUInt16(data, offset, (ushort)pps.Length);
            offset += 2;

            // PPS数据
            Array.Copy(pps, 0, data, offset, pps.Length);
            offset += pps.Length;
        }

        // 写入High Profile扩展
        if (hasExtensions)
        {
            // chroma_format (2 bits) = 1 (4:2:0) -> 111111 01 = 0xFD
            data[offset++] = 0xFD; 
            // bit_depth_luma_minus8 (3 bits) = 0 -> 11111 000 = 0xF8
            data[offset++] = 0xF8;
            // bit_depth_chroma_minus8 (3 bits) = 0 -> 11111 000 = 0xF8
            data[offset++] = 0xF8;
            // numOfSequenceParameterSetsExt = 0
            data[offset++] = 0;
        }

        return data;
    }

    /// <summary>
    /// 将avcC盒子添加到stsd盒子中
    /// </summary>
    /// <param name="stsdData">原始stsd盒子数据</param>
    /// <param name="avccData">avcC盒子数据</param>
    /// <returns>更新后的stsd盒子数据</returns>
    public static byte[] AddAvcCToStsd(byte[] stsdData, byte[] avccData)
    {
        if (stsdData == null || stsdData.Length < 16 || avccData == null || avccData.Length < 8)
            return stsdData;

        // 在stsd中查找avc1盒子
        if (!MP4Utils.FindBox(stsdData, "avc1", 16, stsdData.Length, out var avc1Offset, out var avc1Size))
            return stsdData;

        long avc1ContentStart = avc1Offset + 8 + 78;
        long avc1ContentEnd = avc1Offset + avc1Size;
        if (avc1ContentStart < avc1ContentEnd &&
            MP4Utils.FindBox(stsdData, "avcC", avc1ContentStart, avc1ContentEnd, out var avcCOffset, out var avcCSize))
        {
            int newSize = stsdData.Length - (int)avcCSize + avccData.Length;
            byte[] newStsd = new byte[newSize];

            Array.Copy(stsdData, 0, newStsd, 0, (int)avcCOffset);
            Array.Copy(avccData, 0, newStsd, (int)avcCOffset, avccData.Length);
            long tailStart = avcCOffset + avcCSize;
            if (tailStart < stsdData.Length)
            {
                Array.Copy(stsdData, (int)tailStart, newStsd, (int)avcCOffset + avccData.Length, stsdData.Length - (int)tailStart);
            }

            MP4Utils.WriteBigEndianUInt32(newStsd, 0, (uint)newSize);
            int newAvc1Size = (int)(avc1Size - avcCSize + avccData.Length);
            MP4Utils.WriteBigEndianUInt32(newStsd, (int)avc1Offset, (uint)newAvc1Size);

            return newStsd;
        }

        // 创建新的stsd数据
        int newSizeWithAppend = stsdData.Length + avccData.Length;
        byte[] newStsdWithAppend = new byte[newSizeWithAppend];

        // 复制stsd头部（到avc1结束位置）
        int avc1End = (int)(avc1Offset + avc1Size);
        Array.Copy(stsdData, 0, newStsdWithAppend, 0, avc1End);

        // 添加avcC数据
        Array.Copy(avccData, 0, newStsdWithAppend, avc1End, avccData.Length);

        // 复制avc1之后的数据（如果有）
        if (avc1End < stsdData.Length)
        {
            Array.Copy(stsdData, avc1End, newStsdWithAppend, avc1End + avccData.Length, stsdData.Length - avc1End);
        }

        // 更新stsd大小
        MP4Utils.WriteBigEndianUInt32(newStsdWithAppend, 0, (uint)newSizeWithAppend);

        // 更新avc1大小
        int newAvc1SizeWithAppend = (int)(avc1Size + avccData.Length);
        MP4Utils.WriteBigEndianUInt32(newStsdWithAppend, (int)avc1Offset, (uint)newAvc1SizeWithAppend);

        return newStsdWithAppend;
    }
}
