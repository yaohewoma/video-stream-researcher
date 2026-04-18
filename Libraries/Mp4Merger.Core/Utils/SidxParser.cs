using System.Text;

namespace Mp4Merger.Core.Utils;

/// <summary>
/// sidx（分段索引）盒子解析器
/// 用于解析DASH分段视频的sidx盒子，获取分段信息
/// </summary>
public class SidxParser
{
    /// <summary>
    /// 分段引用信息
    /// </summary>
    public class SegmentReference
    {
        /// <summary>
        /// 分段大小（字节）
        /// </summary>
        public uint Size { get; set; }
        
        /// <summary>
        /// 分段持续时间（以timescale为单位）
        /// </summary>
        public uint Duration { get; set; }
        
        /// <summary>
        /// 是否为媒体分段（true）或sidx分段（false）
        /// </summary>
        public bool IsMediaSegment { get; set; }
    }
    
    /// <summary>
    /// sidx信息
    /// </summary>
    public class SidxInfo
    {
        /// <summary>
        /// 时间刻度
        /// </summary>
        public uint Timescale { get; set; }
        
        /// <summary>
        /// 最早呈现时间
        /// </summary>
        public ulong EarliestPresentationTime { get; set; }
        
        /// <summary>
        /// 第一个分段的偏移量
        /// </summary>
        public ulong FirstOffset { get; set; }
        
        /// <summary>
        /// 分段引用列表
        /// </summary>
        public List<SegmentReference> References { get; set; } = new();
        
        /// <summary>
        /// 总持续时间（以timescale为单位）
        /// </summary>
        public ulong TotalDuration => (ulong)References.Sum(r => (long)r.Duration);
        
        /// <summary>
        /// 总大小（字节）
        /// </summary>
        public ulong TotalSize => (ulong)References.Sum(r => (long)r.Size);
    }
    
    /// <summary>
    /// 从视频数据中解析sidx盒子
    /// </summary>
    /// <param name="videoData">视频文件数据</param>
    /// <returns>sidx信息，如果没有找到则返回null</returns>
    public static SidxInfo? ParseSidx(byte[] videoData)
    {
        // 查找sidx盒子
        if (!MP4Utils.FindBox(videoData, "sidx", out var sidxOffset, out var sidxSize))
            return null;
        
        if (sidxSize < 32)
            return null;
        
        var info = new SidxInfo();
        
        // 解析sidx头部
        // 8字节: header (size + type)
        // 1字节: version
        var version = videoData[sidxOffset + 8];
        
        // 3字节: flags
        // 4字节: reference_ID
        
        // 4字节: timescale
        info.Timescale = videoData.ReadBigEndianUInt32((int)sidxOffset + 16);
        
        int offset;
        if (version == 0)
        {
            // 版本0: 4字节 earliest_presentation_time, 4字节 first_offset
            info.EarliestPresentationTime = videoData.ReadBigEndianUInt32((int)sidxOffset + 20);
            info.FirstOffset = videoData.ReadBigEndianUInt32((int)sidxOffset + 24);
            offset = (int)sidxOffset + 28;
        }
        else
        {
            // 版本1: 8字节 earliest_presentation_time, 8字节 first_offset
            info.EarliestPresentationTime = videoData.ReadBigEndianUInt64((int)sidxOffset + 20);
            info.FirstOffset = videoData.ReadBigEndianUInt64((int)sidxOffset + 28);
            offset = (int)sidxOffset + 36;
        }
        
        // 2字节: reserved
        offset += 2;
        
        // 2字节: reference_count
        var refCount = (ushort)((videoData[offset] << 8) | videoData[offset + 1]);
        offset += 2;
        
        // 解析每个引用
        for (int i = 0; i < refCount && offset + 12 <= videoData.Length; i++)
        {
            // 4字节: reference_type (1 bit) + referenced_size (31 bits)
            var refData = videoData.ReadBigEndianUInt32(offset);
            var isMediaSegment = (refData >> 31) == 0;
            var size = refData & 0x7FFFFFFF;
            
            // 4字节: subsegment_duration
            var duration = videoData.ReadBigEndianUInt32(offset + 4);
            
            // 4字节: starts_with_SAP (1 bit) + SAP_type (3 bits) + SAP_delta_time (28 bits)
            // 忽略这些字段
            
            info.References.Add(new SegmentReference
            {
                Size = size,
                Duration = duration,
                IsMediaSegment = isMediaSegment
            });
            
            offset += 12;
        }
        
        return info;
    }
}
