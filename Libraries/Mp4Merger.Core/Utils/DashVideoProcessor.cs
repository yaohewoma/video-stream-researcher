using System.Text;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// DASH视频处理器
/// 用于处理DASH分段格式的视频，重建样本表
/// </summary>
public static class DashVideoProcessor
{
    /// <summary>
    /// 处理DASH视频，重建trak盒子
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="sidxInfo">sidx信息</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量</param>
    /// <returns>重建后的trak盒子数据</returns>
    public static byte[] ProcessVideoTrak(byte[] originalTrakBox, SidxParser.SidxInfo sidxInfo, long mdatStartOffset)
    {
        // 从原始trak盒子中提取stsd盒子（样本描述）
        byte[]? stsdData = ExtractStsdBox(originalTrakBox);
        if (stsdData == null)
        {
            // 如果无法提取stsd，返回原始trak盒子
            return originalTrakBox;
        }

        // 使用SampleTableBuilder重建样本表
        var sampleTableInfo = SampleTableBuilder.BuildFromSidx(sidxInfo, mdatStartOffset);
        byte[] newStblData = SampleTableBuilder.CreateStblBox(sampleTableInfo, stsdData);

        // 重建trak盒子
        return RebuildTrakBox(originalTrakBox, newStblData);
    }

    /// <summary>
    /// 处理DASH视频，重建trak盒子（使用实际的mdat数据大小）
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="mdatSizes">实际的mdat数据大小列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量</param>
    /// <param name="timeScale">时间刻度</param>
    /// <param name="totalDuration">总持续时间</param>
    /// <returns>重建后的trak盒子数据</returns>
    public static byte[] ProcessVideoTrakWithSizes(byte[] originalTrakBox, List<uint> mdatSizes, long mdatStartOffset, uint timeScale, ulong totalDuration)
    {
        // 从原始trak盒子中提取stsd盒子（样本描述）
        byte[]? stsdData = ExtractStsdBox(originalTrakBox);
        if (stsdData == null)
        {
            return originalTrakBox;
        }

        ulong trackDuration = totalDuration;
        if (trackDuration == 0)
        {
            uint perSample = timeScale / 30;
            if (perSample == 0)
                perSample = 1;
            trackDuration = (ulong)perSample * (ulong)mdatSizes.Count;
        }

        var sampleTableInfo = SampleTableBuilder.BuildFromMdatSizes(mdatSizes, mdatStartOffset, timeScale, trackDuration);
        byte[] newStblData = SampleTableBuilder.CreateStblBox(sampleTableInfo, stsdData);

        byte[] newTrakBox = RebuildTrakBoxWithDuration(originalTrakBox, newStblData, trackDuration);
        UpdateDurations(newTrakBox, trackDuration);

        return newTrakBox;
    }

    /// <summary>
    /// 处理DASH音频，重建trak盒子
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="sidxInfo">sidx信息</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量（音频数据部分）</param>
    /// <returns>重建后的trak盒子数据</returns>
    public static byte[] ProcessAudioTrak(byte[] originalTrakBox, SidxParser.SidxInfo sidxInfo, long mdatStartOffset)
    {
        // 从原始trak盒子中提取stsd盒子
        byte[]? stsdData = ExtractStsdBox(originalTrakBox);
        if (stsdData == null)
        {
            return originalTrakBox;
        }

        // 使用SampleTableBuilder重建样本表
        var sampleTableInfo = SampleTableBuilder.BuildFromSidx(sidxInfo, mdatStartOffset);
        byte[] newStblData = SampleTableBuilder.CreateStblBox(sampleTableInfo, stsdData);

        // 重建trak盒子
        return RebuildTrakBox(originalTrakBox, newStblData);
    }

    /// <summary>
    /// 处理DASH音频，重建trak盒子（使用实际的mdat数据大小）
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="mdatSizes">实际的mdat数据大小列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量（音频数据部分）</param>
    /// <param name="timeScale">时间刻度</param>
    /// <param name="totalDuration">总持续时间</param>
    /// <returns>重建后的trak盒子数据</returns>
    public static byte[] ProcessAudioTrakWithSizes(byte[] originalTrakBox, List<uint> mdatSizes, long mdatStartOffset, uint timeScale, ulong totalDuration)
    {
        // 从原始trak盒子中提取stsd盒子
        byte[]? stsdData = ExtractStsdBox(originalTrakBox);
        if (stsdData == null)
        {
            return originalTrakBox;
        }

        // 使用SampleTableBuilder重建样本表（使用实际的mdat数据大小）
        var sampleTableInfo = SampleTableBuilder.BuildFromMdatSizes(mdatSizes, mdatStartOffset, timeScale, totalDuration);
        byte[] newStblData = SampleTableBuilder.CreateStblBox(sampleTableInfo, stsdData);

        // 重建trak盒子，并更新duration
        byte[] newTrakBox = RebuildTrakBoxWithDuration(originalTrakBox, newStblData, totalDuration);
        
        // 更新trak盒子中的mdhd和tkhd duration
        UpdateDurations(newTrakBox, totalDuration);
        
        return newTrakBox;
    }

    /// <summary>
    /// 处理DASH视频，重建trak盒子（使用NAL单元信息）
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="nalUnits">NAL单元信息列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量</param>
    /// <param name="timeScale">时间刻度</param>
    /// <param name="totalDuration">总持续时间</param>
    /// <returns>重建后的trak盒子数据</returns>
    public static byte[] ProcessVideoTrakWithNalUnits(byte[] originalTrakBox, List<H264NalParser.NalUnitInfo> nalUnits, long mdatStartOffset, uint timeScale, ulong totalDuration)
    {
        // 从原始trak盒子中提取stsd盒子（样本描述）
        byte[]? stsdData = ExtractStsdBox(originalTrakBox);
        if (stsdData == null)
        {
            return originalTrakBox;
        }

        // 使用SampleTableBuilder重建样本表（使用NAL单元信息）
        var sampleTableInfo = SampleTableBuilder.BuildFromNalUnits(nalUnits, mdatStartOffset, timeScale, totalDuration);
        byte[] newStblData = SampleTableBuilder.CreateStblBox(sampleTableInfo, stsdData);

        // 重建trak盒子
        return RebuildTrakBox(originalTrakBox, newStblData);
    }

    /// <summary>
    /// 处理DASH视频，重建trak盒子（使用帧信息）
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="frames">视频帧信息列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量</param>
    /// <param name="timeScale">时间刻度</param>
    /// <param name="totalDuration">总持续时间</param>
    /// <returns>重建后的trak盒子数据</returns>
    public static byte[] ProcessVideoTrakWithFrames(byte[] originalTrakBox, List<H264NalParser.FrameInfo> frames, long mdatStartOffset, uint timeScale, ulong totalDuration)
    {
        // 从原始trak盒子中提取stsd盒子（样本描述）
        byte[]? stsdData = ExtractStsdBox(originalTrakBox);
        if (stsdData == null)
        {
            return originalTrakBox;
        }

        // 使用SampleTableBuilder重建样本表（使用帧信息）
        var sampleTableInfo = SampleTableBuilder.BuildFromFrames(frames, mdatStartOffset, timeScale, totalDuration);
        byte[] newStblData = SampleTableBuilder.CreateStblBox(sampleTableInfo, stsdData);

        // 计算总持续时间（在时间刻度单位下）
        // 每个样本的持续时间是 timeScale / 30，样本数量是 frames.Count
        ulong trackDuration = (ulong)frames.Count * (timeScale / 30);

        // 重建trak盒子，并更新duration
        return RebuildTrakBoxWithDuration(originalTrakBox, newStblData, trackDuration);
    }

    /// <summary>
    /// 处理DASH视频，重建trak盒子（使用fMP4样本信息）
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="samples">fMP4样本信息列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量</param>
    /// <param name="timeScale">时间刻度</param>
    /// <returns>重建后的trak盒子数据</returns>
    public static byte[] ProcessVideoTrakWithFMP4Samples(byte[] originalTrakBox, List<FMP4Parser.SampleInfo> samples, long mdatStartOffset, uint timeScale)
    {
        // 从原始trak盒子中提取stsd盒子（样本描述）
        byte[]? stsdData = ExtractStsdBox(originalTrakBox);
        if (stsdData == null)
        {
            return originalTrakBox;
        }

        // 使用SampleTableBuilder重建样本表（使用fMP4样本信息）
        var sampleTableInfo = SampleTableBuilder.BuildFromFMP4Samples(samples, mdatStartOffset, timeScale);
        byte[] newStblData = SampleTableBuilder.CreateStblBox(sampleTableInfo, stsdData);

        // 计算总持续时间
        ulong totalDuration = 0;
        foreach (var sample in samples)
        {
            totalDuration += sample.Duration > 0 ? sample.Duration : (timeScale / 30);
        }

        // 重建trak盒子，并更新duration
        byte[] newTrakBox = RebuildTrakBoxWithDuration(originalTrakBox, newStblData, totalDuration);
        
        // 更新trak盒子中的mdhd和tkhd duration
        UpdateDurations(newTrakBox, totalDuration);
        
        return newTrakBox;
    }

    /// <summary>
    /// 处理DASH音频，重建trak盒子（使用fMP4样本信息）
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="samples">fMP4样本信息列表</param>
    /// <param name="mdatStartOffset">mdat盒子的起始偏移量</param>
    /// <param name="timeScale">时间刻度</param>
    /// <returns>重建后的trak盒子数据</returns>
    public static byte[] ProcessAudioTrakWithFMP4Samples(byte[] originalTrakBox, List<FMP4Parser.SampleInfo> samples, long mdatStartOffset, uint timeScale)
    {
        // 从原始trak盒子中提取stsd盒子
        byte[]? stsdData = ExtractStsdBox(originalTrakBox);
        if (stsdData == null)
        {
            return originalTrakBox;
        }

        // 使用SampleTableBuilder重建样本表（使用fMP4样本信息）
        var sampleTableInfo = SampleTableBuilder.BuildFromFMP4Samples(samples, mdatStartOffset, timeScale);
        byte[] newStblData = SampleTableBuilder.CreateStblBox(sampleTableInfo, stsdData);

        // 计算总持续时间
        ulong totalDuration = 0;
        foreach (var sample in samples)
        {
            totalDuration += sample.Duration > 0 ? sample.Duration : (timeScale > 0 ? timeScale / 43 : 1024); // AAC通常每帧1024采样，44100Hz下约23ms
        }

        // 重建trak盒子，并更新duration
        byte[] newTrakBox = RebuildTrakBoxWithDuration(originalTrakBox, newStblData, totalDuration);
        
        // 更新trak盒子中的mdhd和tkhd duration
        UpdateDurations(newTrakBox, totalDuration);
        
        return newTrakBox;
    }

    /// <summary>
    /// 从trak盒子中提取stsd盒子
    /// </summary>
    /// <param name="trakData">trak盒子数据</param>
    /// <returns>stsd盒子数据，如果没有找到则返回null</returns>
    public static byte[]? ExtractStsdBox(byte[] trakData)
    {
        // 在trak盒子中查找mdia盒子，然后在mdia中查找stsd
        if (trakData.Length <= 8)
            return null;
        
        // 首先查找mdia盒子
        if (!MP4Utils.FindBox(trakData, "mdia", 8, trakData.Length, out var mdiaOffset, out var mdiaSize))
            return null;
        
        // 在mdia盒子中查找minf盒子
        if (!MP4Utils.FindBox(trakData, "minf", (int)mdiaOffset + 8, (int)(mdiaOffset + mdiaSize), out var minfOffset, out var minfSize))
            return null;
        
        // 在minf盒子中查找stbl盒子
        if (!MP4Utils.FindBox(trakData, "stbl", (int)minfOffset + 8, (int)(minfOffset + minfSize), out var stblOffset, out var stblSize))
            return null;
        
        // 在stbl盒子中查找stsd盒子
        if (!MP4Utils.FindBox(trakData, "stsd", (int)stblOffset + 8, (int)(stblOffset + stblSize), out var stsdOffset, out var stsdSize))
            return null;
        
        byte[] stsdData = new byte[stsdSize];
        Array.Copy(trakData, stsdOffset, stsdData, 0, (int)stsdSize);
        return stsdData;
    }

    /// <summary>
    /// 从trak盒子中提取avcC盒子
    /// </summary>
    /// <param name="trakData">trak盒子数据</param>
    /// <returns>avcC盒子数据，如果没有找到则返回null</returns>
    public static byte[]? ExtractAvcCBox(byte[] trakData)
    {
        try
        {
            if (trakData == null || trakData.Length <= 8)
                return null;

            // 查找路径: trak -> mdia -> minf -> stbl -> stsd -> avc1 -> avcC
            if (!MP4Utils.FindBox(trakData, "mdia", 8, trakData.Length, out var mdiaOffset, out var mdiaSize))
                return null;

            if (!MP4Utils.FindBox(trakData, "minf", (int)mdiaOffset + 8, (int)(mdiaOffset + mdiaSize), out var minfOffset, out var minfSize))
                return null;

            if (!MP4Utils.FindBox(trakData, "stbl", (int)minfOffset + 8, (int)(minfOffset + minfSize), out var stblOffset, out var stblSize))
                return null;

            if (!MP4Utils.FindBox(trakData, "stsd", (int)stblOffset + 8, (int)(stblOffset + stblSize), out var stsdOffset, out var stsdSize))
                return null;

            // 在stsd中查找avc1
            // stsd是FullBox: size(4) + type(4) + version(1) + flags(3) + entry_count(4) = 16 bytes header
            if (!MP4Utils.FindBox(trakData, "avc1", (int)stsdOffset + 16, (int)(stsdOffset + stsdSize), out var avc1Offset, out var avc1Size))
                return null;

            // 在avc1中查找avcC
            // avc1头部通常有78字节的固定字段，然后是子盒子
            // 8 (header) + 6 (reserved) + 2 (data ref index) + 70 (visual sample entry fields) = 86 bytes
            // 但是标准通常是 8 + 78 = 86 bytes
            int avc1HeaderSize = 8 + 78;
            
            // 安全检查
            if (avc1Offset + avc1HeaderSize >= trakData.Length)
                return null;
                
            if (!MP4Utils.FindBox(trakData, "avcC", (int)avc1Offset + avc1HeaderSize, (int)(avc1Offset + avc1Size), out var avcCOffset, out var avcCSize))
                return null;

            byte[] avcCData = new byte[avcCSize];
            Array.Copy(trakData, avcCOffset, avcCData, 0, (int)avcCSize);
            return avcCData;
        }
        catch (Exception)
        {
            // 忽略任何解析错误
            return null;
        }
    }

    /// <summary>
    /// 在trak盒子中替换stsd盒子
    /// </summary>
    /// <param name="trakData">trak盒子数据</param>
    /// <param name="newStsdData">新的stsd盒子数据</param>
    /// <returns>更新后的trak盒子数据</returns>
    public static byte[] ReplaceStsdInTrak(byte[] trakData, byte[] newStsdData)
    {
        if (trakData == null || trakData.Length <= 8 || newStsdData == null || newStsdData.Length < 8)
            return trakData;

        // 在trak盒子中查找mdia -> minf -> stbl -> stsd
        if (!MP4Utils.FindBox(trakData, "mdia", 8, trakData.Length, out var mdiaOffset, out var mdiaSize))
            return trakData;

        if (!MP4Utils.FindBox(trakData, "minf", (int)mdiaOffset + 8, (int)(mdiaOffset + mdiaSize), out var minfOffset, out var minfSize))
            return trakData;

        if (!MP4Utils.FindBox(trakData, "stbl", (int)minfOffset + 8, (int)(minfOffset + minfSize), out var stblOffset, out var stblSize))
            return trakData;

        if (!MP4Utils.FindBox(trakData, "stsd", (int)stblOffset + 8, (int)(stblOffset + stblSize), out var stsdOffset, out var stsdSize))
            return trakData;

        // 计算大小变化
        int sizeChange = newStsdData.Length - (int)stsdSize;

        // 创建新的trak数据
        int newTrakSize = trakData.Length + sizeChange;
        byte[] newTrak = new byte[newTrakSize];

        // 复制stsd之前的数据
        Array.Copy(trakData, 0, newTrak, 0, (int)stsdOffset);

        // 复制新的stsd数据
        Array.Copy(newStsdData, 0, newTrak, (int)stsdOffset, newStsdData.Length);

        // 复制stsd之后的数据
        int dataAfterStsd = trakData.Length - (int)stsdOffset - (int)stsdSize;
        if (dataAfterStsd > 0)
        {
            Array.Copy(trakData, (int)stsdOffset + (int)stsdSize, newTrak, (int)stsdOffset + newStsdData.Length, dataAfterStsd);
        }

        // 更新所有相关盒子的大小
        // 1. 更新trak大小
        MP4Utils.WriteBigEndianUInt32(newTrak, 0, (uint)newTrakSize);

        // 2. 更新mdia大小
        int newMdiaSize = (int)(mdiaSize + sizeChange);
        MP4Utils.WriteBigEndianUInt32(newTrak, (int)mdiaOffset, (uint)newMdiaSize);

        // 3. 更新minf大小
        int newMinfSize = (int)(minfSize + sizeChange);
        MP4Utils.WriteBigEndianUInt32(newTrak, (int)minfOffset, (uint)newMinfSize);

        // 4. 更新stbl大小
        int newStblSize = (int)(stblSize + sizeChange);
        MP4Utils.WriteBigEndianUInt32(newTrak, (int)stblOffset, (uint)newStblSize);

        return newTrak;
    }

    /// <summary>
    /// 重建trak盒子，替换stbl盒子
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="newStblData">新的stbl盒子数据</param>
    /// <returns>重建后的trak盒子数据</returns>
    private static byte[] RebuildTrakBox(byte[] originalTrakBox, byte[] newStblData)
    {
        // 在trak盒子中查找mdia -> minf -> stbl
        if (originalTrakBox.Length <= 8)
            return originalTrakBox;

        // 查找mdia盒子
        if (!MP4Utils.FindBox(originalTrakBox, "mdia", 8, originalTrakBox.Length, out var mdiaOffset, out var mdiaSize))
            return originalTrakBox;

        // 在mdia中查找minf盒子
        if (!MP4Utils.FindBox(originalTrakBox, "minf", (int)mdiaOffset + 8, (int)(mdiaOffset + mdiaSize), out var minfOffset, out var minfSize))
            return originalTrakBox;

        // 在minf中查找stbl盒子
        if (!MP4Utils.FindBox(originalTrakBox, "stbl", (int)minfOffset + 8, (int)(minfOffset + minfSize), out var stblOffset, out var stblSize))
            return originalTrakBox;

        // 计算大小变化
        int sizeChange = newStblData.Length - (int)stblSize;
        
        // 计算新的盒子大小
        int newStblSize = newStblData.Length;
        int newMinfSize = (int)minfSize + sizeChange;
        int newMdiaSize = (int)mdiaSize + sizeChange;
        int newTrakSize = originalTrakBox.Length + sizeChange;

        // 创建新的trak盒子数据
        byte[] newTrakBox = new byte[newTrakSize];
        int offset = 0;

        // 复制trak头部和stbl之前的数据
        if (stblOffset > 0)
        {
            Array.Copy(originalTrakBox, 0, newTrakBox, offset, (int)stblOffset);
            offset += (int)stblOffset;
        }

        // 复制新的stbl数据
        Array.Copy(newStblData, 0, newTrakBox, offset, newStblData.Length);
        offset += newStblData.Length;

        // 复制stbl之后的数据
        int dataAfterStbl = originalTrakBox.Length - (int)stblOffset - (int)stblSize;
        if (dataAfterStbl > 0)
        {
            Array.Copy(originalTrakBox, (int)stblOffset + (int)stblSize, newTrakBox, offset, dataAfterStbl);
        }

        // 更新所有相关盒子的大小
        // 1. 更新trak盒子大小
        MP4Utils.WriteBigEndianUInt32(newTrakBox, 0, (uint)newTrakSize);
        
        // 2. 更新mdia盒子大小
        MP4Utils.WriteBigEndianUInt32(newTrakBox, (int)mdiaOffset, (uint)newMdiaSize);
        
        // 3. 更新minf盒子大小
        MP4Utils.WriteBigEndianUInt32(newTrakBox, (int)minfOffset, (uint)newMinfSize);
        
        // 4. 更新stbl盒子大小（虽然新数据已经包含正确大小，但再次确认）
        MP4Utils.WriteBigEndianUInt32(newTrakBox, (int)stblOffset, (uint)newStblSize);

        return newTrakBox;
    }

    /// <summary>
    /// 重建trak盒子，替换stbl盒子，并更新duration
    /// </summary>
    /// <param name="originalTrakBox">原始trak盒子数据</param>
    /// <param name="newStblData">新的stbl盒子数据</param>
    /// <param name="trackDuration">轨道持续时间（在时间刻度单位下）</param>
    /// <returns>重建后的trak盒子数据</returns>
    private static byte[] RebuildTrakBoxWithDuration(byte[] originalTrakBox, byte[] newStblData, ulong trackDuration)
    {
        // 首先调用基本的重建方法
        byte[] newTrakBox = RebuildTrakBox(originalTrakBox, newStblData);

        // 更新tkhd和mdhd中的duration
        UpdateDurations(newTrakBox, trackDuration);

        return newTrakBox;
    }

    /// <summary>
    /// 更新trak盒子中的tkhd和mdhd duration
    /// 注意：tkhd duration需要转换为movie timescale (通常是1000或600)
    /// mdhd duration使用mdhd自己的timescale
    /// </summary>
    /// <param name="trakBox">trak盒子数据</param>
    /// <param name="duration">持续时间（在track的timescale单位下）</param>
    private static void UpdateDurations(byte[] trakBox, ulong duration)
    {
        uint mediaTimescale = 0;
        
        // 1. 先查找并更新mdhd盒子中的duration
        if (MP4Utils.FindBox(trakBox, "mdia", 8, trakBox.Length, out var mdiaOffset, out var mdiaSize))
        {
            if (MP4Utils.FindBox(trakBox, "mdhd", (int)mdiaOffset + 8, (int)(mdiaOffset + mdiaSize), out var mdhdOffset, out var mdhdSize))
            {
                byte version = trakBox[mdhdOffset + 8];
                
                // 读取timescale
                if (version == 0)
                {
                    mediaTimescale = MP4Utils.ReadBigEndianUInt32(trakBox, (int)mdhdOffset + 20);
                }
                else if (version == 1)
                {
                    mediaTimescale = MP4Utils.ReadBigEndianUInt32(trakBox, (int)mdhdOffset + 28);
                }
                
                // 更新mdhd duration (使用media timescale)
                if (version == 0)
                {
                    if (mdhdSize >= 28 && mdhdOffset + 24 + 4 <= trakBox.Length)
                    {
                        MP4Utils.WriteBigEndianUInt32(trakBox, (int)mdhdOffset + 24, (uint)duration);
                    }
                }
                else if (version == 1)
                {
                    if (mdhdSize >= 36 && mdhdOffset + 32 + 8 <= trakBox.Length)
                    {
                        MP4Utils.WriteBigEndianUInt64(trakBox, (int)mdhdOffset + 32, duration);
                    }
                }
            }
        }
        
        // 2. 移除edts盒子（如果存在）
        // edts (Edit List) 盒子经常导致时间轴问题，特别是从DASH转换时
        // 简单地将其类型改为 'free'，使其变为填充字节
        if (MP4Utils.FindBox(trakBox, "edts", 8, trakBox.Length, out var edtsOffset, out var edtsSize))
        {
             // 将 "edts" 改为 "free"
             trakBox[edtsOffset + 4] = (byte)'f';
             trakBox[edtsOffset + 5] = (byte)'r';
             trakBox[edtsOffset + 6] = (byte)'e';
             trakBox[edtsOffset + 7] = (byte)'e';
        }

        // 3. 更新tkhd盒子中的duration
        // tkhd duration 必须以 movie header timescale (通常是mvhd中的timescale) 为单位
        // 由于我们这里无法访问mvhd，但MP4Merger在构建MoovBox时会处理这个转换
        // 或者我们可以在这里写入基于track timescale的duration，然后在MoovBox中修正
        // 但通常的做法是：tkhd duration = duration * (mvhd_timescale / mdhd_timescale)
        // 这里暂时写入原始duration，但在MoovBox构建时必须修正它！
        // 或者，我们可以假设mvhd timescale是1000（常见默认值），进行转换
        // 为了安全起见，这里我们只更新mdhd，tkhd的更新应该在MoovBox构建时统一处理
    }
}
