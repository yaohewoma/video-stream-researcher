using System.Text;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Builders;

/// <summary>
/// 视频轨道构建器
/// 用于构建视频轨道相关的盒子
/// </summary>
public static class VideoTrackBuilder
{
    /// <summary>
    /// 创建视频轨道的stsd盒子
    /// </summary>
    /// <param name="fileInfo">文件信息</param>
    /// <returns>stsd盒子数据</returns>
    public static byte[] CreateVideoStsdBox(MP4FileInfo fileInfo)
    {
        byte[] avcCBox = CreateAvcCBox(fileInfo);
        int avcCSize = avcCBox.Length;
        int avc1ContentSize = 6 + 2 + 2 + 2 + 12 + 2 + 2 + 4 + 4 + 4 + 2 + 32 + 2 + 2; // 78字节
        int avc1Size = 8 + avc1ContentSize + avcCSize;
        int stsdSize = 8 + 8 + avc1Size; // stsd头部 + 内容头 + avc1盒子
        
        byte[] stsdBox = new byte[stsdSize];
        
        // 写入stsd盒子头部
        MP4Utils.WriteBigEndianUInt32(stsdBox, 0, (uint)stsdSize);
        Array.Copy(Encoding.ASCII.GetBytes("stsd"), 0, stsdBox, 4, 4);
        stsdBox[8] = 0;
        Array.Clear(stsdBox, 9, 3);
        MP4Utils.WriteBigEndianUInt32(stsdBox, 12, 1);
        
        // 写入avc1盒子
        int avc1Offset = 16;
        MP4Utils.WriteBigEndianUInt32(stsdBox, avc1Offset, (uint)avc1Size);
        Array.Copy(Encoding.ASCII.GetBytes("avc1"), 0, stsdBox, avc1Offset + 4, 4);
        
        // avc1内容 (78字节)
        int avc1ContentOffset = avc1Offset + 8;
        // 6字节预留
        Array.Clear(stsdBox, avc1ContentOffset, 6);
        // 2字节data_reference_index
        MP4Utils.WriteBigEndianUInt16(stsdBox, avc1ContentOffset + 6, 1);
        // 2字节pre_defined
        Array.Clear(stsdBox, avc1ContentOffset + 8, 2);
        // 2字节reserved
        Array.Clear(stsdBox, avc1ContentOffset + 10, 2);
        // 12字节pre_defined (3个)
        Array.Clear(stsdBox, avc1ContentOffset + 12, 12);
        // 2字节width
        ushort width = 1920;
        ushort height = 1080;
        if (fileInfo != null && fileInfo.VideoWidth > 0 && fileInfo.VideoHeight > 0)
        {
            width = (ushort)fileInfo.VideoWidth;
            height = (ushort)fileInfo.VideoHeight;
        }
        MP4Utils.WriteBigEndianUInt16(stsdBox, avc1ContentOffset + 24, width);
        // 2字节height
        MP4Utils.WriteBigEndianUInt16(stsdBox, avc1ContentOffset + 26, height);
        // 4字节horizresolution (72 dpi in 16.16 fixed-point)
        MP4Utils.WriteBigEndianUInt32(stsdBox, avc1ContentOffset + 28, 0x00480000);
        // 4字节vertresolution (72 dpi in 16.16 fixed-point)
        MP4Utils.WriteBigEndianUInt32(stsdBox, avc1ContentOffset + 32, 0x00480000);
        // 4字节预留
        Array.Clear(stsdBox, avc1ContentOffset + 36, 4);
        // 2字节frame_count
        MP4Utils.WriteBigEndianUInt16(stsdBox, avc1ContentOffset + 40, 1);
        // 32字节compressorname
        Array.Clear(stsdBox, avc1ContentOffset + 42, 32);
        byte[] compressorName = Encoding.ASCII.GetBytes("AVC Coding");
        Array.Copy(compressorName, 0, stsdBox, avc1ContentOffset + 42, Math.Min(compressorName.Length, 31));
        // 2字节depth
        MP4Utils.WriteBigEndianUInt16(stsdBox, avc1ContentOffset + 74, 24);
        // 2字节pre_defined
        MP4Utils.WriteBigEndianUInt16(stsdBox, avc1ContentOffset + 76, 0xFFFF);
        
        // 复制avcC盒子到avc1盒子
        Array.Copy(avcCBox, 0, stsdBox, avc1ContentOffset + 78, avcCSize);

        return stsdBox;
    }

    /// <summary>
    /// 创建AVCC盒子
    /// </summary>
    /// <param name="fileInfo">文件信息</param>
    /// <returns>AVCC盒子数据</returns>
    private static byte[] CreateAvcCBox(MP4FileInfo fileInfo)
    {
        // 优先使用从样本中生成的avcC盒子
        if (fileInfo != null && fileInfo.GeneratedAvcCBox != null)
        {
            return fileInfo.GeneratedAvcCBox;
        }

        // 从原始视频的avc1盒子中提取avcC盒子
        if (fileInfo != null && fileInfo.OriginalVideoAvc1Box != null)
        {
            byte[]? extractedAvcCBox = ExtractAvcCBoxFromOriginal(fileInfo.OriginalVideoAvc1Box);
            if (extractedAvcCBox != null)
            {
                return extractedAvcCBox;
            }
        }
        
        // 如果提取失败，使用生成的avcC盒子
        return GenerateAvcCBox(fileInfo);
    }

    /// <summary>
    /// 从原始视频的avc1盒子中提取avcC盒子
    /// </summary>
    /// <param name="originalAvc1Box">原始avc1盒子数据</param>
    /// <returns>提取的avcC盒子数据，如果未找到则返回null</returns>
    private static byte[]? ExtractAvcCBoxFromOriginal(byte[] originalAvc1Box)
    {
        try
        {
            int avc1Size = originalAvc1Box.Length;
            
            // 跳过avc1头部和SampleEntry内容
            int offset = 8 + 78; // 86字节
            int maxIterations = (avc1Size - offset) / 8 + 10;
            int iterations = 0;
            
            while (offset + 8 <= avc1Size && iterations < maxIterations)
            {
                iterations++;
                
                uint boxSize = MP4Utils.ReadBigEndianUInt32(originalAvc1Box, offset);
                string boxType = Encoding.ASCII.GetString(originalAvc1Box, offset + 4, 4);
                
                // 安全检查
                if (boxSize < 8 || boxSize > avc1Size - offset)
                {
                    break;
                }
                
                if (boxType == "avcC")
                {
                    // 提取完整的avcC盒子
                    byte[] extractedAvcCBox = new byte[boxSize];
                    Array.Copy(originalAvc1Box, offset, extractedAvcCBox, 0, (int)boxSize);
                    return extractedAvcCBox;
                }
                
                offset += (int)boxSize;
            }
            
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 生成AVCC盒子
    /// </summary>
    /// <param name="fileInfo">文件信息</param>
    /// <returns>生成的AVCC盒子数据</returns>
    private static byte[] GenerateAvcCBox(MP4FileInfo fileInfo)
    {
        using MemoryStream ms = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(ms);
        
        // avcC盒子头部
        writer.Write((uint)0); // 临时大小
        writer.Write(Encoding.ASCII.GetBytes("avcC"));
        
        // version
        writer.Write((byte)1);
        
        // profile_indication
        writer.Write((byte)100); // High Profile
        
        // profile_compatibility
        writer.Write((byte)0);
        
        // level_indication
        writer.Write((byte)30); // Level 3.0
        
        // 1字节：reserved (6 bits) + lengthSizeMinusOne (2 bits)
        writer.Write((byte)0xFC); // 0b11111100
        
        // 1字节：reserved (3 bits) + numOfSequenceParameterSets (5 bits)
        writer.Write((byte)0xE1); // 0b11100001
        
        // SPS
        byte[] sps = CreateSPS(fileInfo);
        writer.Write((byte)((sps.Length >> 8) & 0xFF));
        writer.Write((byte)(sps.Length & 0xFF));
        writer.Write(sps);
        
        // 1字节：numOfPictureParameterSets
        writer.Write((byte)1);
        
        // PPS
        byte[] pps = CreatePPS();
        writer.Write((byte)((pps.Length >> 8) & 0xFF));
        writer.Write((byte)(pps.Length & 0xFF));
        writer.Write(pps);
        
        // 更新avcC盒子大小
        byte[] avcCData = ms.ToArray();
        int avcCSize = avcCData.Length;
        MP4Utils.WriteBigEndianUInt32(avcCData, 0, (uint)avcCSize);
        
        return avcCData;
    }

    /// <summary>
    /// 创建SPS (Sequence Parameter Set)
    /// </summary>
    /// <param name="fileInfo">文件信息</param>
    /// <returns>SPS数据</returns>
    private static byte[] CreateSPS(MP4FileInfo fileInfo)
    {
        using MemoryStream ms = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(ms);
        
        // SPS NAL单元类型
        writer.Write((byte)0x67); // 0x67 = SPS
        
        // profile_idc
        writer.Write((byte)100); // High Profile
        
        // constraint_set0_flag - constraint_set5_flag
        writer.Write((byte)0);
        
        // level_idc
        writer.Write((byte)30); // Level 3.0
        
        // seq_parameter_set_id
        writer.Write((byte)0);
        
        // log2_max_frame_num_minus4
        writer.Write((byte)4);
        
        // pic_order_cnt_type
        writer.Write((byte)0);
        
        // log2_max_pic_order_cnt_lsb_minus4
        writer.Write((byte)4);
        
        // max_num_ref_frames
        writer.Write((byte)1);
        
        // gaps_in_frame_num_value_allowed_flag
        writer.Write((byte)0);
        
        // pic_width_in_mbs_minus1
        int width = 640;
        if (fileInfo != null && fileInfo.VideoWidth > 0)
        {
            width = fileInfo.VideoWidth;
        }
        int picWidthInMbsMinus1 = (width + 15) / 16 - 1;
        writer.Write((short)picWidthInMbsMinus1);
        
        // pic_height_in_map_units_minus1
        int height = 360;
        if (fileInfo != null && fileInfo.VideoHeight > 0)
        {
            height = fileInfo.VideoHeight;
        }
        int picHeightInMapUnitsMinus1 = (height + 15) / 16 - 1;
        writer.Write((short)picHeightInMapUnitsMinus1);
        
        // frame_mbs_only_flag
        writer.Write((byte)1);
        
        // mb_adaptive_frame_field_flag
        writer.Write((byte)0);
        
        // direct_8x8_inference_flag
        writer.Write((byte)1);
        
        // frame_cropping_flag
        writer.Write((byte)0);
        
        // vui_parameters_present_flag
        writer.Write((byte)1);
        
        // vui_parameters
        // aspect_ratio_info_present_flag
        writer.Write((byte)1);
        
        // aspect_ratio_idc
        writer.Write((byte)1); // Square pixels
        
        // overscan_info_present_flag
        writer.Write((byte)0);
        
        // video_signal_type_present_flag
        writer.Write((byte)0);
        
        // chroma_loc_info_present_flag
        writer.Write((byte)0);
        
        // timing_info_present_flag
        writer.Write((byte)1);
        
        // num_units_in_tick
        writer.Write((uint)1);
        
        // time_scale
        writer.Write((uint)30);
        
        // fixed_frame_rate_flag
        writer.Write((byte)1);
        
        // nal_hrd_parameters_present_flag
        writer.Write((byte)0);
        
        // vcl_hrd_parameters_present_flag
        writer.Write((byte)0);
        
        // low_delay_hrd_flag
        writer.Write((byte)0);
        
        // pic_struct_present_flag
        writer.Write((byte)0);
        
        // bitstream_restriction_flag
        writer.Write((byte)1);
        
        // motion_vectors_over_pic_boundaries_flag
        writer.Write((byte)1);
        
        // max_bytes_per_pic_denom
        writer.Write((byte)2);
        
        // max_bits_per_mb_denom
        writer.Write((byte)2);
        
        // log2_max_mv_length_horizontal
        writer.Write((byte)0x0F);
        
        // log2_max_mv_length_vertical
        writer.Write((byte)0x0F);
        
        // num_reorder_frames
        writer.Write((byte)0);
        
        // max_dec_frame_buffering
        writer.Write((byte)1);
        
        return ms.ToArray();
    }

    /// <summary>
    /// 创建PPS (Picture Parameter Set)
    /// </summary>
    /// <returns>PPS数据</returns>
    private static byte[] CreatePPS()
    {
        using MemoryStream ms = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(ms);
        
        // PPS NAL单元类型
        writer.Write((byte)0x68); // 0x68 = PPS
        
        // pic_parameter_set_id
        writer.Write((byte)0);
        
        // seq_parameter_set_id
        writer.Write((byte)0);
        
        // entropy_coding_mode_flag
        writer.Write((byte)0);
        
        // bottom_field_pic_order_in_frame_present_flag
        writer.Write((byte)0);
        
        // num_ref_idx_l0_default_active_minus1
        writer.Write((byte)0);
        
        // num_ref_idx_l1_default_active_minus1
        writer.Write((byte)0);
        
        // weighted_pred_flag
        writer.Write((byte)0);
        
        // weighted_bipred_idc
        writer.Write((byte)0);
        
        // pic_init_qp_minus26
        writer.Write((byte)0);
        
        // pic_init_qs_minus26
        writer.Write((byte)0);
        
        // chroma_qp_index_offset
        writer.Write((byte)0);
        
        // deblocking_filter_control_present_flag
        writer.Write((byte)0);
        
        // constrained_intra_pred_flag
        writer.Write((byte)0);
        
        // redundant_pic_cnt_present_flag
        writer.Write((byte)0);
        
        return ms.ToArray();
    }
}
