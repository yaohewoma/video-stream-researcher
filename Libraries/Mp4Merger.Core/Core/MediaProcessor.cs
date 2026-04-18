using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mp4Merger.Core.Localization;
using Mp4Merger.Core.Media;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Core;

/// <summary>
/// 媒体处理器，负责处理媒体数据的提取和解析
/// </summary>
public class MediaProcessor
{
    /// <summary>
    /// 处理媒体文件，提取相关信息和数据
    /// </summary>
    /// <param name="videoPath">视频文件路径</param>
    /// <param name="audioPath">音频文件路径</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>处理后的媒体数据</returns>
    public async Task<ProcessedMediaData> ProcessAsync(string videoPath, string audioPath, Action<string>? statusCallback)
    {
        var result = new ProcessedMediaData();
        
        // 读取文件数据
        statusCallback?.Invoke(MergerLocalization.GetString("Processor.ReadingVideo"));
        byte[] videoData = await File.ReadAllBytesAsync(videoPath);
        
        statusCallback?.Invoke(MergerLocalization.GetString("Processor.ReadingAudio"));
        byte[] audioData = await File.ReadAllBytesAsync(audioPath);
        
        // 检查音频文件格式
        bool isMP3Audio = audioData.IsMP3File();
        bool isAACAudio = audioData.IsAACAudioFile();
        
        string audioFormat = isAACAudio ? MergerLocalization.GetString("Common.AAC") : 
                            isMP3Audio ? MergerLocalization.GetString("Common.MP3") : 
                            MergerLocalization.GetString("Common.Other");
        statusCallback?.Invoke(MergerLocalization.GetString("Processor.AudioFormat", audioFormat));
        
        // 初始化文件信息对象
        var videoFileInfo = new MP4FileInfo();
        var audioFileInfo = new MP4FileInfo();

        // 提取真实的媒体信息
        statusCallback?.Invoke(MergerLocalization.GetString("Processor.ExtractingVideoInfo"));
        var videoInfo = videoData.ExtractVideoInfo(videoFileInfo);

        statusCallback?.Invoke(MergerLocalization.GetString("Processor.ExtractingAudioInfo"));
        var audioInfo = audioData.ExtractAudioInfo(audioFileInfo);

        // 检查是否为DASH分段格式
        bool isDashVideo = videoFileInfo.SidxInfo != null && videoFileInfo.SidxInfo.References.Count > 0;
        if (isDashVideo)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Processor.DashDetected", videoFileInfo.SidxInfo!.References.Count));
            statusCallback?.Invoke(MergerLocalization.GetString("Processor.RebuildingSampleTable"));
        }

        // 设置文件信息
        SetVideoFileInfo(videoFileInfo, videoInfo);
        SetAudioFileInfo(audioFileInfo, audioInfo);

        double videoSeconds = videoInfo.TimeScale > 0 ? (double)videoInfo.Duration / videoInfo.TimeScale : 0;
        double audioSeconds = audioInfo.TimeScale > 0 ? (double)audioInfo.Duration / audioInfo.TimeScale : 0;
        statusCallback?.Invoke(MergerLocalization.GetString("Processor.VideoInfo", videoInfo.Width, videoInfo.Height, TimeSpan.FromSeconds(videoSeconds)));
        statusCallback?.Invoke(MergerLocalization.GetString("Processor.AudioInfo", audioInfo.Channels, audioInfo.SampleRate, TimeSpan.FromSeconds(audioSeconds)));
        
        result.VideoFileInfo = videoFileInfo;
        result.AudioFileInfo = audioFileInfo;

        // 提取媒体数据
        statusCallback?.Invoke(MergerLocalization.GetString("Processor.ExtractingVideoData"));
        
        if (isDashVideo)
        {
            // DASH格式：提取视频数据并获取每个mdat数据块的内容
            var (data, sizes, contents, samples) = await videoData.ExtractH264MediaDataWithContentsAsync(statusCallback);
            result.ExtractedVideoData = data;
            result.VideoMdatSizes = sizes;
            result.VideoMdatContents = contents;
            result.Fmp4VideoSamples = samples;
        }
        else
        {
            // 非DASH格式：只提取视频数据
            var (data, sizes) = await videoData.ExtractH264MediaDataWithSizesAsync(statusCallback);
            result.ExtractedVideoData = data;
            result.VideoMdatSizes = sizes;
            result.VideoMdatContents = new List<byte[]>();
        }

        statusCallback?.Invoke(MergerLocalization.GetString("Processor.ExtractingAudioData"));
        if (isMP3Audio)
        {
            result.ExtractedAudioData = audioData.ProcessMP3AudioData(statusCallback);
            result.AudioMdatSizes = new List<uint> { (uint)result.ExtractedAudioData.Length };
        }
        else
        {
            // 使用新的方法，支持fMP4音频解析
            var (data, sizes, samples) = await audioData.ExtractAACMediaDataWithSamplesAsync(statusCallback);
            result.ExtractedAudioData = data;
            result.AudioMdatSizes = sizes;
            result.Fmp4AudioSamples = samples;
            
            if (result.Fmp4AudioSamples != null)
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Processor.FMP4AudioDetected", result.Fmp4AudioSamples.Count));
            }
        }
        
        return result;
    }

    /// <summary>
    /// 设置视频文件信息
    /// </summary>
    private void SetVideoFileInfo(MP4FileInfo fileInfo, (int Width, int Height, long Duration, int TimeScale) videoInfo)
    {
        fileInfo.VideoWidth = videoInfo.Width;
        fileInfo.VideoHeight = videoInfo.Height;
        fileInfo.VideoTimeScale = (uint)videoInfo.TimeScale;
        fileInfo.VideoDuration = (uint)videoInfo.Duration;
        fileInfo.SampleDelta = 512; // 默认值，对应30fps
    }
    
    /// <summary>
    /// 设置音频文件信息
    /// </summary>
    private void SetAudioFileInfo(MP4FileInfo fileInfo, (int Channels, int SampleRate, long Duration, int TimeScale) audioInfo)
    {
        fileInfo.AudioChannels = audioInfo.Channels;
        fileInfo.AudioTimeScale = (uint)audioInfo.TimeScale;
        fileInfo.AudioDuration = (uint)audioInfo.Duration;
    }
}
