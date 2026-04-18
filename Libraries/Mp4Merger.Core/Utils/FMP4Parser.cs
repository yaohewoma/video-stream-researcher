using System.Text;
using Mp4Merger.Core.Localization;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// fMP4（fragmented MP4）解析器
/// 用于解析fMP4格式的视频文件，提取样本数据
/// </summary>
public static class FMP4Parser
{
    /// <summary>
    /// fMP4样本信息
    /// </summary>
    public class SampleInfo
    {
        /// <summary>
        /// 样本在文件中的偏移量
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// 样本大小
        /// </summary>
        public uint Size { get; set; }

        /// <summary>
        /// 样本持续时间
        /// </summary>
        public uint Duration { get; set; }

        /// <summary>
        /// 样本标志
        /// </summary>
        public uint Flags { get; set; }

        /// <summary>
        /// 合成时间偏移
        /// </summary>
        public uint CompositionTimeOffset { get; set; }

        /// <summary>
        /// 解码时间（绝对时间戳）
        /// </summary>
        public ulong DecodeTime { get; set; }

        /// <summary>
        /// 是否为关键帧
        /// </summary>
        public bool IsKeyFrame { get; set; }
    }

    /// <summary>
    /// 检测H.264样本中是否包含关键帧（IDR）
    /// </summary>
    /// <param name="sampleData">样本数据</param>
    /// <returns>如果包含IDR帧则返回true</returns>
    private static bool DetectKeyFrameInSample(byte[] sampleData)
    {
        if (sampleData == null || sampleData.Length < 5)
            return false;

        int offset = 0;
        while (offset < sampleData.Length - 4)
        {
            // 读取NAL单元长度（4字节，大端序）
            uint nalLength = (uint)((sampleData[offset] << 24) | (sampleData[offset + 1] << 16) |
                                   (sampleData[offset + 2] << 8) | sampleData[offset + 3]);

            if (nalLength == 0 || nalLength > sampleData.Length - offset - 4)
                break;

            // 检查NAL单元类型
            if (offset + 4 < sampleData.Length)
            {
                byte nalUnitType = (byte)(sampleData[offset + 4] & 0x1F);
                // NAL类型5 = IDR切片（关键帧）
                if (nalUnitType == 5)
                    return true;
            }

            offset += 4 + (int)nalLength;
        }

        return false;
    }

    /// <summary>
    /// 从fMP4文件中提取样本数据
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>样本信息列表和提取的媒体数据</returns>
    public static (List<SampleInfo> Samples, byte[] MediaData) ExtractSamples(
        byte[] fileData,
        Action<string>? statusCallback = null)
    {
        var samples = new List<SampleInfo>();
        using var mediaStream = new MemoryStream();

        // 枚举所有的moof+mdat对
        var fragments = EnumerateFragments(fileData);
        int fragmentCount = 0;

        foreach (var (moofOffset, moofSize, mdatOffset, mdatSize) in fragments)
        {
            fragmentCount++;

            // 解析moof盒子中的样本信息
            var fragmentSamples = ParseMoof(fileData, moofOffset, moofSize, mdatOffset);

            // 提取样本数据
            foreach (var sample in fragmentSamples)
            {
                // 读取样本数据
                byte[] sampleData = new byte[sample.Size];
                Array.Copy(fileData, sample.Offset, sampleData, 0, (int)sample.Size);

                // 检测样本中是否包含关键帧（IDR）
                if (!sample.IsKeyFrame)
                {
                    sample.IsKeyFrame = DetectKeyFrameInSample(sampleData);
                }

                // 写入媒体流
                mediaStream.Write(sampleData, 0, sampleData.Length);

                // 更新样本偏移量为相对于媒体流的偏移量
                sample.Offset = mediaStream.Position - sample.Size;

                samples.Add(sample);
            }

            if (fragmentCount <= 3)
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Parser.ParsingFragment", fragmentCount, fragmentSamples.Count));
            }
            else if (fragmentCount == 4)
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Parser.ParsingRemaining"));
            }
        }

        statusCallback?.Invoke(MergerLocalization.GetString("Parser.TotalFragments", fragmentCount, samples.Count));

        return (samples, mediaStream.ToArray());
    }

    /// <summary>
    /// 枚举文件中的所有moof+mdat片段
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <returns>片段信息列表（moof偏移量、moof大小、mdat偏移量、mdat大小）</returns>
    private static List<(long MoofOffset, long MoofSize, long MdatOffset, long MdatSize)> EnumerateFragments(byte[] fileData)
    {
        var fragments = new List<(long, long, long, long)>();
        long offset = 0;

        while (offset + 8 <= fileData.Length)
        {
            uint size = fileData.ReadBigEndianUInt32((int)offset);
            string type = Encoding.ASCII.GetString(fileData, (int)offset + 4, 4);

            if (size == 0 || size > fileData.Length - offset)
                break;

            if (type == "moof")
            {
                long moofOffset = offset;
                long moofSize = size;

                // 查找对应的mdat
                long nextOffset = offset + size;
                if (nextOffset + 8 <= fileData.Length)
                {
                    uint mdatSize = fileData.ReadBigEndianUInt32((int)nextOffset);
                    string mdatType = Encoding.ASCII.GetString(fileData, (int)nextOffset + 4, 4);

                    if (mdatType == "mdat")
                    {
                        fragments.Add((moofOffset, moofSize, nextOffset, mdatSize));
                    }
                }
            }

            offset += size;
        }

        return fragments;
    }

    /// <summary>
    /// 解析moof盒子，提取样本信息
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="moofOffset">moof盒子偏移量</param>
    /// <param name="moofSize">moof盒子大小</param>
    /// <param name="mdatOffset">对应的mdat盒子偏移量</param>
    /// <returns>样本信息列表</returns>
    private static List<SampleInfo> ParseMoof(byte[] fileData, long moofOffset, long moofSize, long mdatOffset)
    {
        var samples = new List<SampleInfo>();

        // 查找traf盒子
        if (!FindBox(fileData, "traf", (int)moofOffset + 8, (int)(moofOffset + moofSize), out var trafOffset, out var trafSize))
            return samples;

        // 查找tfdt盒子（包含baseMediaDecodeTime）
        ulong baseMediaDecodeTime = 0;
        if (FindBox(fileData, "tfdt", (int)trafOffset + 8, (int)(trafOffset + trafSize), out var tfdtOffset, out var tfdtSize))
        {
            baseMediaDecodeTime = ParseTfdt(fileData, tfdtOffset);
        }

        // 查找tfhd盒子（获取默认样本属性）
        uint defaultSampleDuration = 0;
        uint defaultSampleSize = 0;
        uint defaultSampleFlags = 0;
        long? baseDataOffset = null;
        
        if (FindBox(fileData, "tfhd", (int)trafOffset + 8, (int)(trafOffset + trafSize), out var tfhdOffset, out var tfhdSize))
        {
            ParseTfhd(fileData, tfhdOffset, out defaultSampleDuration, out defaultSampleSize, out defaultSampleFlags, out baseDataOffset);
        }

        // 如果tfhd没有指定baseDataOffset，默认为moofOffset
        if (!baseDataOffset.HasValue)
        {
            baseDataOffset = moofOffset;
        }

        // 查找trun盒子
        if (!FindBox(fileData, "trun", (int)trafOffset + 8, (int)(trafOffset + trafSize), out var trunOffset, out var trunSize))
            return samples;

        // 解析trun盒子
        ParseTrun(fileData, trunOffset, trunSize, baseDataOffset.Value, baseMediaDecodeTime, defaultSampleDuration, defaultSampleSize, defaultSampleFlags, samples);

        return samples;
    }

    /// <summary>
    /// 解析tfhd盒子，提取默认样本属性
    /// </summary>
    private static void ParseTfhd(byte[] fileData, long tfhdOffset, out uint defaultSampleDuration, out uint defaultSampleSize, out uint defaultSampleFlags, out long? baseDataOffset)
    {
        defaultSampleDuration = 0;
        defaultSampleSize = 0;
        defaultSampleFlags = 0;
        baseDataOffset = null;

        int offset = (int)tfhdOffset + 8;
        // byte version = fileData[offset];
        uint flags = ((uint)fileData[offset + 1] << 16) | ((uint)fileData[offset + 2] << 8) | fileData[offset + 3];
        offset += 4;

        // track_ID
        // uint trackId = fileData.ReadBigEndianUInt32(offset);
        offset += 4;

        // base_data_offset_present (0x000001)
        if ((flags & 0x000001) != 0) 
        {
            baseDataOffset = (long)fileData.ReadBigEndianUInt64(offset);
            offset += 8;
        }

        // sample_description_index_present (0x000002)
        if ((flags & 0x000002) != 0) offset += 4;

        // default_sample_duration_present (0x000008)
        if ((flags & 0x000008) != 0)
        {
            defaultSampleDuration = fileData.ReadBigEndianUInt32(offset);
            offset += 4;
        }

        // default_sample_size_present (0x000010)
        if ((flags & 0x000010) != 0)
        {
            defaultSampleSize = fileData.ReadBigEndianUInt32(offset);
            offset += 4;
        }

        // default_sample_flags_present (0x000020)
        if ((flags & 0x000020) != 0)
        {
            defaultSampleFlags = fileData.ReadBigEndianUInt32(offset);
            offset += 4;
        }
    }

    /// <summary>
    /// 解析tfdt盒子，提取baseMediaDecodeTime
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="tfdtOffset">tfdt盒子偏移量</param>
    /// <returns>baseMediaDecodeTime</returns>
    private static ulong ParseTfdt(byte[] fileData, long tfdtOffset)
    {
        int offset = (int)tfdtOffset + 8;
        byte version = fileData[offset];
        // uint flags = ((uint)fileData[offset + 1] << 16) | ((uint)fileData[offset + 2] << 8) | fileData[offset + 3];

        offset += 4; // 跳过version和flags

        if (version == 1)
        {
            // version 1: 64位
            return ((ulong)fileData.ReadBigEndianUInt32(offset) << 32) | fileData.ReadBigEndianUInt32(offset + 4);
        }
        else
        {
            // version 0: 32位
            return fileData.ReadBigEndianUInt32(offset);
        }
    }

    /// <summary>
    /// 解析trun盒子
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="trunOffset">trun盒子偏移量</param>
    /// <param name="trunSize">trun盒子大小</param>
    /// <param name="baseDataOffset">基础数据偏移量</param>
    /// <param name="baseMediaDecodeTime">片段的基准解码时间</param>
    /// <param name="defaultSampleDuration">默认样本持续时间</param>
    /// <param name="defaultSampleSize">默认样本大小</param>
    /// <param name="defaultSampleFlags">默认样本标志</param>
    /// <param name="samples">样本列表（输出）</param>
    private static void ParseTrun(byte[] fileData, long trunOffset, long trunSize, long baseDataOffset, ulong baseMediaDecodeTime, uint defaultSampleDuration, uint defaultSampleSize, uint defaultSampleFlags, List<SampleInfo> samples)
    {
        // trun盒子结构:
        // - 8字节: header
        // - 1字节: version
        // - 3字节: flags
        // - 4字节: sample_count
        // - 4字节: data_offset (如果flags & 0x01)
        // - 4字节: first_sample_flags (如果flags & 0x04)
        // - 样本条目数组

        int offset = (int)trunOffset + 8;
        byte version = fileData[offset];
        uint flags = ((uint)fileData[offset + 1] << 16) | ((uint)fileData[offset + 2] << 8) | fileData[offset + 3];
        uint sampleCount = fileData.ReadBigEndianUInt32(offset + 4);

        offset += 8; // 跳过header、version、flags、sample_count

        // 读取data_offset（如果存在）
        long dataOffset;
        if ((flags & 0x01) != 0)
        {
            int rawDataOffset = (int)fileData.ReadBigEndianUInt32(offset); // signed int32
            offset += 4;
            dataOffset = baseDataOffset + rawDataOffset;
        }
        else
        {
            // 如果没有data_offset，使用baseDataOffset
            dataOffset = baseDataOffset;
        }

        // 跳过first_sample_flags（如果存在）
        uint firstSampleFlags = 0;
        bool hasFirstSampleFlags = (flags & 0x04) != 0;
        if (hasFirstSampleFlags)
        {
            firstSampleFlags = fileData.ReadBigEndianUInt32(offset);
            offset += 4;
        }

        // 确定每个样本条目的大小
        int entrySize = 0;
        bool hasDuration = (flags & 0x0100) != 0;
        bool hasSize = (flags & 0x0200) != 0;
        bool hasFlags = (flags & 0x0400) != 0;
        bool hasCompositionTimeOffset = (flags & 0x0800) != 0;

        if (hasDuration) entrySize += 4;
        if (hasSize) entrySize += 4;
        if (hasFlags) entrySize += 4;
        if (hasCompositionTimeOffset) entrySize += 4;

        // 解析样本条目
        long currentOffset = dataOffset;
        ulong currentDts = baseMediaDecodeTime;

        // 首先收集所有样本的基本信息（大小、标志等）
        var tempSamples = new List<SampleInfo>();

        // 如果没有size信息，需要从mdat数据中解析样本大小
        // 注意：如果没有size信息，通常意味着defaultSampleSize必须存在，或者通过其他方式计算
        // 这里为了兼容性保留一些逻辑，但主要依赖正确的数据
        if (!hasSize)
        {
            // 如果没有样本大小且没有默认大小，这是一个严重的问题
            // 但为了健壮性，我们尝试继续（可能会失败）
            long mdatDataEnd = fileData.Length; // 无法准确知道mdat结束位置，假设到文件末尾或由调用者保证
            long currentSampleStart = dataOffset;

            for (int i = 0; i < sampleCount; i++)
            {
                var sample = new SampleInfo();

                if (hasDuration)
                {
                    sample.Duration = fileData.ReadBigEndianUInt32(offset);
                    offset += 4;
                }
                else
                {
                     // 使用默认值 (来自tfhd)
                     sample.Duration = defaultSampleDuration;
                }

                if (hasFlags)
                {
                    sample.Flags = fileData.ReadBigEndianUInt32(offset);
                    offset += 4;
                }
                else
                {
                    // 使用默认值
                    // 如果是第一个样本且存在first_sample_flags，则使用它
                    if (i == 0 && hasFirstSampleFlags)
                        sample.Flags = firstSampleFlags;
                    else
                        sample.Flags = defaultSampleFlags;
                }

                if (hasCompositionTimeOffset)
                {
                    if (version == 0)
                    {
                        sample.CompositionTimeOffset = fileData.ReadBigEndianUInt32(offset);
                    }
                    else
                    {
                        int signedOffset = (int)fileData.ReadBigEndianUInt32(offset);
                        sample.CompositionTimeOffset = (uint)signedOffset;
                    }
                    offset += 4;
                }

                // 设置样本偏移量
                sample.Offset = currentSampleStart;
                sample.DecodeTime = currentDts;
                currentDts += sample.Duration;

                // 计算样本大小
                if (defaultSampleSize > 0)
                {
                    sample.Size = defaultSampleSize;
                    currentSampleStart += defaultSampleSize;
                }
                else
                {
                    // 无法确定大小，只能标记为0，后续可能出错
                    sample.Size = 0; 
                }

                tempSamples.Add(sample);
            }
        }
        else
        {
            // 有size信息，正常解析
            for (int i = 0; i < sampleCount; i++)
            {
                var sample = new SampleInfo();

                if (hasDuration)
                {
                    sample.Duration = fileData.ReadBigEndianUInt32(offset);
                    offset += 4;
                }
                else
                {
                     // 使用默认值 (来自tfhd)
                     sample.Duration = defaultSampleDuration;
                }

                sample.Size = fileData.ReadBigEndianUInt32(offset);
                offset += 4;

                if (hasFlags)
                {
                    sample.Flags = fileData.ReadBigEndianUInt32(offset);
                    offset += 4;
                }
                else
                {
                    // 使用默认值
                    // 如果是第一个样本且存在first_sample_flags，则使用它
                    if (i == 0 && hasFirstSampleFlags)
                        sample.Flags = firstSampleFlags;
                    else
                        sample.Flags = defaultSampleFlags;
                }

                if (hasCompositionTimeOffset)
                {
                    if (version == 0)
                    {
                        sample.CompositionTimeOffset = fileData.ReadBigEndianUInt32(offset);
                    }
                    else
                    {
                        int signedOffset = (int)fileData.ReadBigEndianUInt32(offset);
                        sample.CompositionTimeOffset = (uint)signedOffset;
                    }
                    offset += 4;
                }

                // 设置样本偏移量
                sample.Offset = currentOffset;
                sample.DecodeTime = currentDts;
                currentDts += sample.Duration;

                currentOffset += sample.Size;

                tempSamples.Add(sample);
            }
        }

        samples.AddRange(tempSamples);
    }

    /// <summary>
    /// 在数据范围内查找指定类型的盒子
    /// </summary>
    /// <param name="data">数据数组</param>
    /// <param name="boxType">盒子类型</param>
    /// <param name="startOffset">起始偏移量</param>
    /// <param name="endOffset">结束偏移量</param>
    /// <param name="boxOffset">盒子偏移量（输出）</param>
    /// <param name="boxSize">盒子大小（输出）</param>
    /// <returns>是否找到</returns>
    private static bool FindBox(byte[] data, string boxType, int startOffset, int endOffset, out long boxOffset, out long boxSize)
    {
        boxOffset = 0;
        boxSize = 0;

        int offset = startOffset;
        while (offset + 8 <= endOffset && offset + 8 <= data.Length)
        {
            uint size = data.ReadBigEndianUInt32(offset);
            string type = Encoding.ASCII.GetString(data, offset + 4, 4);

            if (size == 0 || size > data.Length - offset)
                break;

            if (type == boxType)
            {
                boxOffset = offset;
                boxSize = size;
                return true;
            }

            offset += (int)size;
        }

        return false;
    }
}
