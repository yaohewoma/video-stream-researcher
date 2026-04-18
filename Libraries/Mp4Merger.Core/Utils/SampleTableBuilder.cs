using System;
using System.Text;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// 样本表构建器
/// 用于根据sidx信息重建样本表（stbl）
/// </summary>
public class SampleTableBuilder
{
    /// <summary>
    /// 样本表信息
    /// </summary>
    public class SampleTableInfo
    {
        /// <summary>
        /// 样本到时间映射（stts）
        /// </summary>
        public List<(uint SampleCount, uint SampleDelta)> TimeToSample { get; set; } = new();
        
        /// <summary>
        /// 样本到块映射（stsc）
        /// </summary>
        public List<(uint FirstChunk, uint SamplesPerChunk, uint SampleDescriptionIndex)> SampleToChunk { get; set; } = new();
        
        /// <summary>
        /// 样本大小（stsz）
        /// </summary>
        public List<uint> SampleSizes { get; set; } = new();
        
        /// <summary>
        /// 块偏移量（stco）
        /// </summary>
        public List<uint> ChunkOffsets { get; set; } = new();
        
        /// <summary>
        /// 关键帧样本索引列表（stss）
        /// </summary>
        public List<uint> KeyframeSamples { get; set; } = new();

        /// <summary>
        /// 合成时间偏移列表（ctts）
        /// (SampleCount, SampleOffset)
        /// </summary>
        public List<(uint SampleCount, uint SampleOffset)> CompositionTimeOffsets { get; set; } = new();
        
        /// <summary>
        /// 总样本数
        /// </summary>
        public uint SampleCount => (uint)SampleSizes.Count;
    }
    
    /// <summary>
    /// 根据sidx信息构建样本表（使用sidx中的分段大小）
    /// </summary>
    /// <param name="sidxInfo">sidx信息</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量（相对于文件开头）</param>
    /// <returns>样本表信息</returns>
    public static SampleTableInfo BuildFromSidx(SidxParser.SidxInfo sidxInfo, long mdatStartOffset)
    {
        var info = new SampleTableInfo();
        
        if (sidxInfo?.References == null || sidxInfo.References.Count == 0)
            return info;
        
        long currentOffset = mdatStartOffset + 8; // 跳过mdat头部（8字节）
        
        // 为每个分段创建一个块
        for (int i = 0; i < sidxInfo.References.Count; i++)
        {
            var reference = sidxInfo.References[i];
            
            // 每个分段作为一个样本
            // 注意：这里简化处理，每个分段作为一个样本
            // 实际上每个分段可能包含多个样本（帧）
            
            // 添加样本大小
            info.SampleSizes.Add(reference.Size);
            
            // 添加块偏移量
            info.ChunkOffsets.Add((uint)currentOffset);
            
            // 添加时间到样本映射
            // 每个样本的持续时间是分段的持续时间
            info.TimeToSample.Add((1, reference.Duration));
            
            // 添加样本到块映射
            // 每个块包含1个样本
            info.SampleToChunk.Add(((uint)(i + 1), 1, 1));
            
            // 更新偏移量
            currentOffset += reference.Size;
        }
        
        return info;
    }
    
    /// <summary>
    /// 根据实际的mdat数据大小构建样本表
    /// </summary>
    /// <param name="mdatSizes">实际的mdat数据大小列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量（相对于文件开头）</param>
    /// <param name="timeScale">时间刻度</param>
    /// <param name="totalDuration">总持续时间</param>
    /// <returns>样本表信息</returns>
    public static SampleTableInfo BuildFromMdatSizes(List<uint> mdatSizes, long mdatStartOffset, uint timeScale, ulong totalDuration)
    {
        var info = new SampleTableInfo();
        
        if (mdatSizes == null || mdatSizes.Count == 0)
            return info;
        
        long currentOffset = mdatStartOffset + 8; // 跳过mdat头部（8字节）
        
        uint sampleDuration;
        if (totalDuration > 0)
        {
            sampleDuration = (uint)(totalDuration / (ulong)mdatSizes.Count);
        }
        else if (timeScale > 0)
        {
            sampleDuration = Math.Max(1, timeScale / 30);
        }
        else
        {
            sampleDuration = 1;
        }
        
        // 为每个mdat数据块创建一个样本
        for (int i = 0; i < mdatSizes.Count; i++)
        {
            uint size = mdatSizes[i];
            
            // 添加样本大小
            info.SampleSizes.Add(size);
            
            // 添加块偏移量
            info.ChunkOffsets.Add((uint)currentOffset);
            
            // 添加时间到样本映射
            info.TimeToSample.Add((1, sampleDuration));
            
            // 添加样本到块映射
            info.SampleToChunk.Add(((uint)(i + 1), 1, 1));
            
            // 更新偏移量
            currentOffset += size;
        }
        
        return info;
    }
    
    /// <summary>
    /// 根据NAL单元信息构建样本表
    /// </summary>
    /// <param name="nalUnits">NAL单元信息列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量（相对于文件开头）</param>
    /// <param name="timeScale">时间刻度</param>
    /// <param name="totalDuration">总持续时间</param>
    /// <returns>样本表信息</returns>
    public static SampleTableInfo BuildFromNalUnits(List<H264NalParser.NalUnitInfo> nalUnits, long mdatStartOffset, uint timeScale, ulong totalDuration)
    {
        var info = new SampleTableInfo();
        
        if (nalUnits == null || nalUnits.Count == 0)
            return info;
        
        uint sampleDuration;
        if (totalDuration > 0)
        {
            sampleDuration = (uint)(totalDuration / (ulong)nalUnits.Count);
        }
        else if (timeScale > 0)
        {
            sampleDuration = Math.Max(1, timeScale / 30);
        }
        else
        {
            sampleDuration = 1;
        }
        
        // 为每个NAL单元创建一个样本
        for (int i = 0; i < nalUnits.Count; i++)
        {
            var nalUnit = nalUnits[i];
            
            // 添加样本大小
            info.SampleSizes.Add(nalUnit.Size);
            
            // 添加块偏移量（相对于文件开头）
            info.ChunkOffsets.Add((uint)(mdatStartOffset + 8 + nalUnit.Offset));
            
            // 添加时间到样本映射
            info.TimeToSample.Add((1, sampleDuration));
            
            // 添加样本到块映射
            info.SampleToChunk.Add(((uint)(i + 1), 1, 1));
            
            // 记录关键帧信息
            if (nalUnit.IsKeyframe)
            {
                info.KeyframeSamples.Add((uint)(i + 1));
            }
        }

        return info;
    }

    /// <summary>
    /// 根据视频帧信息构建样本表
    /// </summary>
    /// <param name="frames">视频帧信息列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量（相对于文件开头）</param>
    /// <param name="timeScale">时间刻度</param>
    /// <param name="totalDuration">总持续时间</param>
    /// <returns>样本表信息</returns>
    public static SampleTableInfo BuildFromFrames(List<H264NalParser.FrameInfo> frames, long mdatStartOffset, uint timeScale, ulong totalDuration)
    {
        var info = new SampleTableInfo();

        if (frames == null || frames.Count == 0)
            return info;

        // 计算每个样本的持续时间（平均分配）
        // 使用timeScale计算：如果帧率是30fps，则每个样本的持续时间应该是timeScale / 30
        uint sampleDuration;
        if (timeScale > 0)
        {
            // 假设帧率是30fps，计算每个样本的持续时间
            sampleDuration = timeScale / 30;
        }
        else
        {
            sampleDuration = (uint)(totalDuration / (ulong)frames.Count);
        }

        // 为每个帧创建一个样本
        for (int i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];

            // 添加样本大小
            info.SampleSizes.Add(frame.Size);

            // 添加块偏移量（相对于文件开头）
            info.ChunkOffsets.Add((uint)(mdatStartOffset + 8 + frame.Offset));

            // 添加时间到样本映射
            info.TimeToSample.Add((1, sampleDuration));

            // 添加样本到块映射
            info.SampleToChunk.Add(((uint)(i + 1), 1, 1));

            // 记录关键帧信息
            if (frame.IsKeyframe)
            {
                info.KeyframeSamples.Add((uint)(i + 1));
            }
        }

        return info;
    }

    /// <summary>
    /// 根据fMP4样本信息构建样本表
    /// </summary>
    /// <param name="samples">fMP4样本信息列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量（相对于文件开头）</param>
    /// <param name="timeScale">时间刻度</param>
    /// <returns>样本表信息</returns>
    public static SampleTableInfo BuildFromFMP4Samples(List<FMP4Parser.SampleInfo> samples, long mdatStartOffset, uint timeScale)
    {
        var info = new SampleTableInfo();

        if (samples == null || samples.Count == 0)
            return info;

        // 计算每个样本的持续时间
        // 如果样本本身包含duration，优先使用样本的duration
        // 仅当样本缺失duration时使用默认值
        uint fallbackDuration = timeScale > 0 ? timeScale / 30 : 512;

        // 为每个样本创建一个条目
        // 同时合并相同持续时间的TimeToSample条目
        uint currentDuration = 0;
        uint currentCount = 0;

        // 合并相同偏移量的CompositionTimeOffsets条目
        uint currentCttsOffset = 0;
        uint currentCttsCount = 0;
        bool hasCtts = false;

        // mdat数据起始位置 = mdatStartOffset + 8（mdat头部）
        long mdatDataStart = mdatStartOffset + 8;

        // 每个样本作为一个块（chunk），这是最简单和可靠的方式
        // 每个chunk包含1个样本，chunk偏移量指向样本的起始位置
        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];

            // ... (省略部分代码)

            // 处理ctts
            if (sample.CompositionTimeOffset != 0)
            {
                hasCtts = true;
            }

            if (i == 0)
            {
                currentCttsOffset = sample.CompositionTimeOffset;
                currentCttsCount = 1;
            }
            else if (sample.CompositionTimeOffset == currentCttsOffset)
            {
                currentCttsCount++;
            }
            else
            {
                info.CompositionTimeOffsets.Add((currentCttsCount, currentCttsOffset));
                currentCttsOffset = sample.CompositionTimeOffset;
                currentCttsCount = 1;
            }

            // 添加样本大小
            info.SampleSizes.Add(sample.Size);

            // 添加块偏移量（相对于文件开头）
            // chunk offset = mdat数据起始位置 + 样本在提取后的媒体数据中的偏移量
            // 注意：sample.Offset 是相对于提取后的媒体数据的偏移量（从0开始）
            long chunkOffset = mdatDataStart + sample.Offset;
            
            // 检查chunkOffset是否溢出uint范围 (stco盒子只支持32位偏移量)
            // 如果文件大于4GB，应使用co64盒子
            if (chunkOffset > uint.MaxValue)
            {
                // TODO: 实现co64盒子支持以处理大于4GB的文件
                // 目前仅做截断处理，可能会导致播放问题
            }
            
            info.ChunkOffsets.Add((uint)chunkOffset);

            // 添加样本到块映射 - 每个chunk包含1个样本
            info.SampleToChunk.Add(((uint)(i + 1), 1, 1));

            // 合并相同持续时间的TimeToSample条目
            uint duration = sample.Duration > 0 ? sample.Duration : fallbackDuration;
            if (i == 0)
            {
                currentDuration = duration;
                currentCount = 1;
            }
            else if (duration == currentDuration)
            {
                currentCount++;
            }
            else
            {
                // 不同的持续时间，添加当前组并开始新组
                info.TimeToSample.Add((currentCount, currentDuration));
                currentDuration = duration;
                currentCount = 1;
            }

            // 记录关键帧信息
            if (sample.IsKeyFrame)
            {
                info.KeyframeSamples.Add((uint)(i + 1));
            }
        }

        // 重新累加样本持续时间，确保与DASH分段的时间戳一致
        info.TimeToSample.Clear();
        currentCount = 0;
        currentDuration = 0;
        
        ulong expectedDts = 0;
        if (samples.Count > 0)
        {
            expectedDts = samples[0].DecodeTime;
        }

        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            
            // 检查是否存在时间戳跳变（gap）
            // 如果 sample.DecodeTime > expectedDts，说明有空隙，需要填补
            if (i > 0 && sample.DecodeTime > expectedDts)
            {
                // 计算gap时长
                ulong gap = sample.DecodeTime - expectedDts;
                
                // 只有当gap比较大时才处理（例如大于1帧）
                // 简单的jitter忽略不计
                if (gap > 0)
                {
                    // 策略：增加前一个sample的duration来填补gap
                    // 这比插入空sample更安全
                    if (info.TimeToSample.Count > 0)
                    {
                        var lastEntryIndex = info.TimeToSample.Count - 1;
                        var lastEntry = info.TimeToSample[lastEntryIndex];
                        
                        // 如果最后一条记录count=1，直接修改
                        // 如果count>1，需要拆分
                        if (lastEntry.SampleCount == 1)
                        {
                            info.TimeToSample[lastEntryIndex] = (1, (uint)(lastEntry.SampleDelta + gap));
                        }
                        else
                        {
                            // 减少最后一个entry的count
                            info.TimeToSample[lastEntryIndex] = (lastEntry.SampleCount - 1, lastEntry.SampleDelta);
                            // 添加新的entry，包含gap
                            info.TimeToSample.Add((1, (uint)(lastEntry.SampleDelta + gap)));
                            
                            // 更新当前循环状态
                            currentDuration = 0; // 强制下一个sample开始新entry
                            currentCount = 0;
                        }
                    }
                }
            }

            uint duration = sample.Duration > 0 ? sample.Duration : fallbackDuration;
            expectedDts = sample.DecodeTime + duration;
            
            if (currentCount == 0)
            {
                currentDuration = duration;
                currentCount = 1;
            }
            else if (duration == currentDuration)
            {
                currentCount++;
            }
            else
            {
                info.TimeToSample.Add((currentCount, currentDuration));
                currentDuration = duration;
                currentCount = 1;
            }
        }
        
        if (currentCount > 0)
        {
            info.TimeToSample.Add((currentCount, currentDuration));
        }

        // 添加最后一组CompositionTimeOffsets条目
        if (currentCttsCount > 0)
        {
            info.CompositionTimeOffsets.Add((currentCttsCount, currentCttsOffset));
        }

        // 如果所有样本的offset都为0，则不需要ctts盒子
        if (!hasCtts)
        {
            info.CompositionTimeOffsets.Clear();
        }

        return info;
    }

    /// <summary>
    /// 创建stts盒子（Time to Sample）
    /// </summary>
    /// <param name="info">样本表信息</param>
    /// <returns>stts盒子数据</returns>
    public static byte[] CreateSttsBox(SampleTableInfo info)
    {
        // 计算盒子大小
        // 头部: 8字节
        // version + flags: 4字节
        // entry_count: 4字节
        // 每个条目: 8字节 (sample_count + sample_delta)
        uint entryCount = (uint)info.TimeToSample.Count;
        uint boxSize = 8 + 4 + 4 + (entryCount * 8);
        
        byte[] data = new byte[boxSize];
        int offset = 0;
        
        // 盒子大小
        MP4Utils.WriteBigEndianUInt32(data, offset, boxSize);
        offset += 4;
        
        // 盒子类型 "stts"
        Array.Copy(Encoding.ASCII.GetBytes("stts"), 0, data, offset, 4);
        offset += 4;
        
        // version (0) + flags (0)
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        
        // entry_count
        MP4Utils.WriteBigEndianUInt32(data, offset, entryCount);
        offset += 4;
        
        // 条目
        foreach (var (sampleCount, sampleDelta) in info.TimeToSample)
        {
            MP4Utils.WriteBigEndianUInt32(data, offset, sampleCount);
            offset += 4;
            MP4Utils.WriteBigEndianUInt32(data, offset, sampleDelta);
            offset += 4;
        }
        
        return data;
    }
    
    /// <summary>
    /// 创建stsc盒子（Sample to Chunk）
    /// </summary>
    /// <param name="info">样本表信息</param>
    /// <returns>stsc盒子数据</returns>
    public static byte[] CreateStscBox(SampleTableInfo info)
    {
        // 计算盒子大小
        uint entryCount = (uint)info.SampleToChunk.Count;
        uint boxSize = 8 + 4 + 4 + (entryCount * 12);
        
        byte[] data = new byte[boxSize];
        int offset = 0;
        
        // 盒子大小
        MP4Utils.WriteBigEndianUInt32(data, offset, boxSize);
        offset += 4;
        
        // 盒子类型 "stsc"
        Array.Copy(Encoding.ASCII.GetBytes("stsc"), 0, data, offset, 4);
        offset += 4;
        
        // version (0) + flags (0)
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        
        // entry_count
        MP4Utils.WriteBigEndianUInt32(data, offset, entryCount);
        offset += 4;
        
        // 条目
        foreach (var (firstChunk, samplesPerChunk, sampleDescriptionIndex) in info.SampleToChunk)
        {
            MP4Utils.WriteBigEndianUInt32(data, offset, firstChunk);
            offset += 4;
            MP4Utils.WriteBigEndianUInt32(data, offset, samplesPerChunk);
            offset += 4;
            MP4Utils.WriteBigEndianUInt32(data, offset, sampleDescriptionIndex);
            offset += 4;
        }
        
        return data;
    }
    
    /// <summary>
    /// 创建stsz盒子（Sample Size）
    /// </summary>
    /// <param name="info">样本表信息</param>
    /// <returns>stsz盒子数据</returns>
    public static byte[] CreateStszBox(SampleTableInfo info)
    {
        // 计算盒子大小
        uint sampleCount = (uint)info.SampleSizes.Count;
        uint boxSize = 8 + 4 + 4 + 4 + (sampleCount * 4);
        
        byte[] data = new byte[boxSize];
        int offset = 0;
        
        // 盒子大小
        MP4Utils.WriteBigEndianUInt32(data, offset, boxSize);
        offset += 4;
        
        // 盒子类型 "stsz"
        Array.Copy(Encoding.ASCII.GetBytes("stsz"), 0, data, offset, 4);
        offset += 4;
        
        // version (0) + flags (0)
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        
        // sample_size (0 = 每个样本大小不同)
        MP4Utils.WriteBigEndianUInt32(data, offset, 0);
        offset += 4;
        
        // sample_count
        MP4Utils.WriteBigEndianUInt32(data, offset, sampleCount);
        offset += 4;
        
        // 样本大小列表
        foreach (var size in info.SampleSizes)
        {
            MP4Utils.WriteBigEndianUInt32(data, offset, size);
            offset += 4;
        }
        
        return data;
    }
    
    /// <summary>
    /// 创建stco盒子（Chunk Offset）
    /// </summary>
    /// <param name="info">样本表信息</param>
    /// <returns>stco盒子数据</returns>
    public static byte[] CreateStcoBox(SampleTableInfo info)
    {
        // 计算盒子大小
        uint entryCount = (uint)info.ChunkOffsets.Count;
        uint boxSize = 8 + 4 + 4 + (entryCount * 4);
        
        byte[] data = new byte[boxSize];
        int offset = 0;
        
        // 盒子大小
        MP4Utils.WriteBigEndianUInt32(data, offset, boxSize);
        offset += 4;
        
        // 盒子类型 "stco"
        Array.Copy(Encoding.ASCII.GetBytes("stco"), 0, data, offset, 4);
        offset += 4;
        
        // version (0) + flags (0)
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        
        // entry_count
        MP4Utils.WriteBigEndianUInt32(data, offset, entryCount);
        offset += 4;
        
        // 块偏移量列表
        foreach (var chunkOffset in info.ChunkOffsets)
        {
            MP4Utils.WriteBigEndianUInt32(data, offset, chunkOffset);
            offset += 4;
        }
        
        return data;
    }
    
    /// <summary>
    /// 创建ctts盒子（Composition Time to Sample）
    /// </summary>
    /// <param name="info">样本表信息</param>
    /// <returns>ctts盒子数据</returns>
    public static byte[] CreateCttsBox(SampleTableInfo info)
    {
        // 如果没有CTO信息，返回空数组（可选盒子）
        if (info.CompositionTimeOffsets == null || info.CompositionTimeOffsets.Count == 0)
        {
            return Array.Empty<byte>();
        }

        // 计算盒子大小
        // 头部: 8字节
        // version + flags: 4字节
        // entry_count: 4字节
        // 每个条目: 8字节 (sample_count + sample_offset)
        uint entryCount = (uint)info.CompositionTimeOffsets.Count;
        uint boxSize = 8 + 4 + 4 + (entryCount * 8);

        byte[] data = new byte[boxSize];
        int offset = 0;

        // 盒子大小
        MP4Utils.WriteBigEndianUInt32(data, offset, boxSize);
        offset += 4;

        // 盒子类型 "ctts"
        Array.Copy(Encoding.ASCII.GetBytes("ctts"), 0, data, offset, 4);
        offset += 4;

        // version (0) + flags (0)
        // 注意：如果存在负偏移量，应该使用version 1，但在fMP4Parser中我们总是解析为uint
        // 这里假设偏移量都处理为非负或者version 0足够
        // 如果需要支持负偏移量，可能需要升级到version 1
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;

        // entry_count
        MP4Utils.WriteBigEndianUInt32(data, offset, entryCount);
        offset += 4;

        // 条目
        foreach (var (sampleCount, sampleOffset) in info.CompositionTimeOffsets)
        {
            MP4Utils.WriteBigEndianUInt32(data, offset, sampleCount);
            offset += 4;
            MP4Utils.WriteBigEndianUInt32(data, offset, sampleOffset);
            offset += 4;
        }

        return data;
    }

    /// <summary>
    /// 创建stss盒子（Sync Sample）- 关键帧索引
    /// </summary>
    /// <param name="info">样本表信息</param>
    /// <returns>stss盒子数据</returns>
    public static byte[] CreateStssBox(SampleTableInfo info)
    {
        // 如果没有关键帧信息，或者关键帧数量等于样本总数（全I帧），可以省略stss盒子
        // 但为了兼容性，只有当没有关键帧信息时才省略
        if (info.KeyframeSamples == null || info.KeyframeSamples.Count == 0)
        {
            return Array.Empty<byte>();
        }

        // 验证关键帧索引的有效性
        // 关键帧索引必须是递增的，且不能超过样本总数
        var validKeyframes = new List<uint>();
        uint lastKeyframe = 0;
        foreach (var kf in info.KeyframeSamples)
        {
            if (kf > lastKeyframe && kf <= info.SampleCount)
            {
                validKeyframes.Add(kf);
                lastKeyframe = kf;
            }
        }
        
        if (validKeyframes.Count == 0) return Array.Empty<byte>();

        // 计算盒子大小
        uint entryCount = (uint)validKeyframes.Count;
        uint boxSize = 8 + 4 + 4 + (entryCount * 4);

        byte[] data = new byte[boxSize];
        int offset = 0;

        // 盒子大小
        MP4Utils.WriteBigEndianUInt32(data, offset, boxSize);
        offset += 4;

        // 盒子类型 "stss"
        Array.Copy(Encoding.ASCII.GetBytes("stss"), 0, data, offset, 4);
        offset += 4;

        // version (0) + flags (0)
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;
        data[offset++] = 0;

        // entry_count
        MP4Utils.WriteBigEndianUInt32(data, offset, entryCount);
        offset += 4;

        // 关键帧样本索引列表（1-based）
        foreach (var sampleNumber in validKeyframes)
        {
            MP4Utils.WriteBigEndianUInt32(data, offset, sampleNumber);
            offset += 4;
        }

        return data;
    }

    /// <summary>
    /// 创建完整的stbl盒子（Sample Table）
    /// </summary>
    /// <param name="info">样本表信息</param>
    /// <param name="stsdData">stsd盒子数据（样本描述）</param>
    /// <returns>stbl盒子数据</returns>
    public static byte[] CreateStblBox(SampleTableInfo info, byte[] stsdData)
    {
        // 创建各个子盒子
        byte[] sttsData = CreateSttsBox(info);
        byte[] stscData = CreateStscBox(info);
        byte[] stszData = CreateStszBox(info);
        byte[] stcoData = CreateStcoBox(info);
        byte[] stssData = CreateStssBox(info);
        byte[] cttsData = CreateCttsBox(info);

        // 计算stbl盒子总大小
        uint boxSize = 8 + (uint)stsdData.Length + (uint)sttsData.Length +
                       (uint)stscData.Length + (uint)stszData.Length + (uint)stcoData.Length +
                       (uint)stssData.Length + (uint)cttsData.Length;

        byte[] data = new byte[boxSize];
        int offset = 0;

        // 盒子大小
        MP4Utils.WriteBigEndianUInt32(data, offset, boxSize);
        offset += 4;

        // 盒子类型 "stbl"
        Array.Copy(Encoding.ASCII.GetBytes("stbl"), 0, data, offset, 4);
        offset += 4;

        // 复制子盒子数据
        Array.Copy(stsdData, 0, data, offset, stsdData.Length);
        offset += stsdData.Length;

        Array.Copy(sttsData, 0, data, offset, sttsData.Length);
        offset += sttsData.Length;

        Array.Copy(stscData, 0, data, offset, stscData.Length);
        offset += stscData.Length;

        Array.Copy(stszData, 0, data, offset, stszData.Length);
        offset += stszData.Length;

        // 添加stss盒子（如果有）
        if (stssData.Length > 0)
        {
            Array.Copy(stssData, 0, data, offset, stssData.Length);
            offset += stssData.Length;
        }

        // 添加ctts盒子（如果有）
        if (cttsData.Length > 0)
        {
            Array.Copy(cttsData, 0, data, offset, cttsData.Length);
            offset += cttsData.Length;
        }

        Array.Copy(stcoData, 0, data, offset, stcoData.Length);

        return data;
    }
}
