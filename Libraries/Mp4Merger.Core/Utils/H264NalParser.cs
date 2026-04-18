using System;
using System.Collections.Generic;
using System.Linq;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// H.264 NAL单元解析器
/// </summary>
public static class H264NalParser
{
    /// <summary>
    /// NAL单元信息
    /// </summary>
    public class NalUnitInfo
    {
        /// <summary>
        /// NAL单元在数据中的偏移量
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// NAL单元大小（包括4字节长度前缀）
        /// </summary>
        public uint Size { get; set; }

        /// <summary>
        /// NAL单元类型
        /// </summary>
        public byte NalType { get; set; }

        /// <summary>
        /// 是否为关键帧（IDR帧）
        /// </summary>
        public bool IsKeyframe { get; set; }
    }

    /// <summary>
    /// 视频帧信息
    /// </summary>
    public class FrameInfo
    {
        /// <summary>
        /// 帧在数据中的偏移量
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// 帧大小（包括所有NAL单元和4字节长度前缀）
        /// </summary>
        public uint Size { get; set; }

        /// <summary>
        /// 是否为关键帧
        /// </summary>
        public bool IsKeyframe { get; set; }

        /// <summary>
        /// 该帧包含的NAL单元列表
        /// </summary>
        public List<NalUnitInfo> NalUnits { get; set; } = new();
    }

    /// <summary>
    /// 解析H.264数据中的NAL单元
    /// </summary>
    /// <param name="data">H.264数据</param>
    /// <returns>NAL单元列表</returns>
    public static List<NalUnitInfo> ParseNalUnits(byte[] data)
    {
        var nalUnits = new List<NalUnitInfo>();

        if (data == null || data.Length < 4)
            return nalUnits;

        int offset = 0;
        while (offset < data.Length - 4)
        {
            // 读取NAL单元长度（4字节大端序）
            uint nalLength = (uint)((data[offset] << 24) | (data[offset + 1] << 16) |
                                   (data[offset + 2] << 8) | data[offset + 3]);

            // 验证NAL长度是否合理
            if (nalLength == 0 || nalLength > data.Length - offset - 4)
                break;

            // 读取NAL单元类型（第5个字节）
            byte nalType = (byte)(data[offset + 4] & 0x1F);

            // 判断是否为关键帧（NAL类型5 = IDR帧）
            bool isKeyframe = nalType == 5;

            nalUnits.Add(new NalUnitInfo
            {
                Offset = offset,
                Size = nalLength + 4, // 包括4字节长度前缀
                NalType = nalType,
                IsKeyframe = isKeyframe
            });

            // 移动到下一个NAL单元
            offset += (int)(nalLength + 4);
        }

        return nalUnits;
    }

    /// <summary>
    /// 解析H.264数据中的视频帧
    /// 在H.264中，一个视频帧通常由多个NAL单元组成
    /// 帧边界通常由新的AUD（Access Unit Delimiter，NAL类型9）或IDR/非IDR切片标记
    /// </summary>
    /// <param name="data">H.264数据</param>
    /// <returns>视频帧列表</returns>
    public static List<FrameInfo> ParseFrames(byte[] data)
    {
        var frames = new List<FrameInfo>();
        var nalUnits = ParseNalUnits(data);

        if (nalUnits.Count == 0)
            return frames;

        FrameInfo? currentFrame = null;

        foreach (var nalUnit in nalUnits)
        {
            // 检查是否是新的帧的开始
            // 在H.264中，AUD（NAL类型9）标记访问单元的开始
            // 或者，如果遇到IDR切片（NAL类型5）或非IDR切片（NAL类型1），
            // 且当前帧已经包含切片，则开始新帧
            bool isNewFrame = false;

            if (nalUnit.NalType == 9) // AUD - Access Unit Delimiter
            {
                // AUD标记新的访问单元（帧）的开始
                isNewFrame = true;
            }
            else if (nalUnit.NalType == 5 || nalUnit.NalType == 1) // IDR或非IDR切片
            {
                // 如果当前帧已经包含切片，则开始新帧
                if (currentFrame != null && currentFrame.NalUnits.Any(n => n.NalType == 5 || n.NalType == 1))
                {
                    isNewFrame = true;
                }
            }

            if (isNewFrame && currentFrame != null)
            {
                // 完成当前帧
                FinalizeFrame(currentFrame);
                frames.Add(currentFrame);
                currentFrame = null;
            }

            // 创建新帧或添加到当前帧
            if (currentFrame == null)
            {
                currentFrame = new FrameInfo
                {
                    Offset = nalUnit.Offset,
                    IsKeyframe = nalUnit.IsKeyframe
                };
            }

            currentFrame.NalUnits.Add(nalUnit);

            // 更新关键帧状态
            if (nalUnit.IsKeyframe)
            {
                currentFrame.IsKeyframe = true;
            }
        }

        // 处理最后一个帧
        if (currentFrame != null)
        {
            FinalizeFrame(currentFrame);
            frames.Add(currentFrame);
        }

        return frames;
    }

    /// <summary>
    /// 完成帧信息计算
    /// </summary>
    private static void FinalizeFrame(FrameInfo frame)
    {
        if (frame.NalUnits.Count == 0)
            return;

        // 计算帧的总大小
        frame.Size = (uint)frame.NalUnits.Sum(n => n.Size);

        // 确保偏移量是第一个NAL单元的偏移量
        frame.Offset = frame.NalUnits[0].Offset;
    }

    /// <summary>
    /// 将NAL单元列表转换为样本大小列表
    /// </summary>
    /// <param name="nalUnits">NAL单元列表</param>
    /// <returns>样本大小列表</returns>
    public static List<uint> GetSampleSizes(List<NalUnitInfo> nalUnits)
    {
        var sampleSizes = new List<uint>();

        foreach (var nalUnit in nalUnits)
        {
            sampleSizes.Add(nalUnit.Size);
        }

        return sampleSizes;
    }

    /// <summary>
    /// 将帧列表转换为样本大小列表
    /// </summary>
    /// <param name="frames">帧列表</param>
    /// <returns>样本大小列表</returns>
    public static List<uint> GetFrameSampleSizes(List<FrameInfo> frames)
    {
        var sampleSizes = new List<uint>();

        foreach (var frame in frames)
        {
            sampleSizes.Add(frame.Size);
        }

        return sampleSizes;
    }

    /// <summary>
    /// 将NAL单元列表转换为样本偏移量列表
    /// </summary>
    /// <param name="nalUnits">NAL单元列表</param>
    /// <param name="baseOffset">基础偏移量（mdat数据起始位置）</param>
    /// <returns>样本偏移量列表</returns>
    public static List<uint> GetSampleOffsets(List<NalUnitInfo> nalUnits, long baseOffset)
    {
        var sampleOffsets = new List<uint>();

        foreach (var nalUnit in nalUnits)
        {
            sampleOffsets.Add((uint)(baseOffset + nalUnit.Offset));
        }

        return sampleOffsets;
    }

    /// <summary>
    /// 将帧列表转换为样本偏移量列表
    /// </summary>
    /// <param name="frames">帧列表</param>
    /// <param name="baseOffset">基础偏移量（mdat数据起始位置）</param>
    /// <returns>样本偏移量列表</returns>
    public static List<uint> GetFrameSampleOffsets(List<FrameInfo> frames, long baseOffset)
    {
        var sampleOffsets = new List<uint>();

        foreach (var frame in frames)
        {
            sampleOffsets.Add((uint)(baseOffset + frame.Offset));
        }

        return sampleOffsets;
    }

    /// <summary>
    /// 获取关键帧索引列表
    /// </summary>
    /// <param name="nalUnits">NAL单元列表</param>
    /// <returns>关键帧索引列表（从1开始）</returns>
    public static List<uint> GetKeyframeIndices(List<NalUnitInfo> nalUnits)
    {
        var keyframeIndices = new List<uint>();

        for (int i = 0; i < nalUnits.Count; i++)
        {
            if (nalUnits[i].IsKeyframe)
            {
                keyframeIndices.Add((uint)(i + 1)); // 从1开始计数
            }
        }

        return keyframeIndices;
    }

    /// <summary>
    /// 获取帧级别的关键帧索引列表
    /// </summary>
    /// <param name="frames">帧列表</param>
    /// <returns>关键帧索引列表（从1开始）</returns>
    public static List<uint> GetFrameKeyframeIndices(List<FrameInfo> frames)
    {
        var keyframeIndices = new List<uint>();

        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].IsKeyframe)
            {
                keyframeIndices.Add((uint)(i + 1)); // 从1开始计数
            }
        }

        return keyframeIndices;
    }
}
