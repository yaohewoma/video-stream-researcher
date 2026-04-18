using System.Text;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Builders;

/// <summary>
/// 音频轨道构建器
/// 用于构建音频轨道相关的盒子
/// </summary>
public static class AudioTrackBuilder
{
    /// <summary>
    /// 创建音频轨道的stsd盒子
    /// </summary>
    /// <param name="fileInfo">文件信息</param>
    /// <returns>stsd盒子数据</returns>
    public static byte[] CreateAudioStsdBox(MP4FileInfo fileInfo)
    {
        // 创建esds盒子
        int sampleRate = 44100;
        int channels = 2;
        if (fileInfo != null)
        {
            sampleRate = (int)fileInfo.AudioTimeScale;
            channels = fileInfo.AudioChannels;
        }
        byte[] esdsBox = BoxCreator.CreateEsdsBox(sampleRate, channels);
        int esdsSize = esdsBox.Length;
        
        // 计算盒子大小
        int mp4aContentSize = 6 + 2 + 8 + 2 + 2 + 4 + 4; // 28字节
        int mp4aSize = 8 + mp4aContentSize + esdsSize;
        int stsdSize = 8 + 8 + mp4aSize;
        
        byte[] stsdBox = new byte[stsdSize];
        
        // 写入stsd盒子头部
        MP4Utils.WriteBigEndianUInt32(stsdBox, 0, (uint)stsdSize);
        Array.Copy(Encoding.ASCII.GetBytes("stsd"), 0, stsdBox, 4, 4);
        stsdBox[8] = 0;
        Array.Clear(stsdBox, 9, 3);
        MP4Utils.WriteBigEndianUInt32(stsdBox, 12, 1);
        
        // 写入mp4a盒子
        int mp4aOffset = 16;
        MP4Utils.WriteBigEndianUInt32(stsdBox, mp4aOffset, (uint)mp4aSize);
        Array.Copy(Encoding.ASCII.GetBytes("mp4a"), 0, stsdBox, mp4aOffset + 4, 4);
        
        // mp4a盒子内容 (28字节)
        int mp4aContentOffset = mp4aOffset + 8;
        // 6字节预留
        Array.Clear(stsdBox, mp4aContentOffset, 6);
        // 2字节data_reference_index
        MP4Utils.WriteBigEndianUInt16(stsdBox, mp4aContentOffset + 6, 1);
        // 8字节预留
        Array.Clear(stsdBox, mp4aContentOffset + 8, 8);
        // 2字节channelcount
        ushort channelCount = 2;
        if (fileInfo != null && fileInfo.AudioChannels > 0)
        {
            channelCount = (ushort)fileInfo.AudioChannels;
        }
        MP4Utils.WriteBigEndianUInt16(stsdBox, mp4aContentOffset + 16, channelCount);
        // 2字节samplesize
        MP4Utils.WriteBigEndianUInt16(stsdBox, mp4aContentOffset + 18, 16);
        // 4字节预留
        Array.Clear(stsdBox, mp4aContentOffset + 20, 4);
        // 4字节samplerate (16.16 fixed-point)
        uint audioTimeScale = 44100;
        if (fileInfo != null && fileInfo.AudioTimeScale > 0)
        {
            audioTimeScale = fileInfo.AudioTimeScale;
        }
        // 采样率以16.16固定点数表示，所以需要左移16位
        MP4Utils.WriteBigEndianUInt32(stsdBox, mp4aContentOffset + 24, audioTimeScale << 16);
        
        // 复制esds盒子到mp4a盒子
        Array.Copy(esdsBox, 0, stsdBox, mp4aContentOffset + 28, esdsSize);
        
        return stsdBox;
    }
}
