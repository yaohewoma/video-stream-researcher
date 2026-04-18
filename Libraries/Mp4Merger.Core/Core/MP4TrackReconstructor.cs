using System;
using System.Collections.Generic;
using System.Linq;
using Mp4Merger.Core.Localization;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Core;

/// <summary>
/// MP4轨道重建器，负责重建视频和音频轨道
/// </summary>
public class MP4TrackReconstructor
{
    /// <summary>
    /// 提取并处理H.264配置信息（SPS/PPS）
    /// </summary>
    public void ExtractAndProcessH264Config(
        MP4FileInfo videoFileInfo, 
        List<byte[]> videoMdatContents, 
        List<FMP4Parser.SampleInfo>? fmp4Samples, 
        Action<string>? statusCallback)
    {
        // 如果不是DASH视频或者没有样本内容，跳过
        if (videoFileInfo.SidxInfo == null || videoFileInfo.SidxInfo.References.Count == 0 || 
            videoFileInfo.OriginalVideoTrakBox == null || 
            fmp4Samples == null || fmp4Samples.Count == 0 || 
            videoMdatContents.Count == 0)
        {
            return;
        }

        statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.ExtractingH264Config"));
        
        H264ConfigExtractor.H264Config? h264Config = null;
        int samplesToCheck = Math.Min(5, videoMdatContents.Count); // 检查前5个样本
        
        for (int i = 0; i < samplesToCheck; i++)
        {
            var sampleData = videoMdatContents[i];
            if (sampleData == null || sampleData.Length < 4) continue;
            
            h264Config = H264ConfigExtractor.ExtractConfigFromSample(sampleData);
            if (h264Config != null)
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.ConfigFoundInSample", i + 1));
                break;
            }
        }
        
        if (h264Config != null)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.SPSPPSFound", h264Config.SPSList.Count, h264Config.PPSList.Count));
            
            // 创建avcC盒子
            byte[] avccData = H264ConfigExtractor.CreateAvcCBox(h264Config);
            statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.CreatingAvcC", avccData.Length));
            
            // 保存生成的avcC盒子到FileInfo，供VideoTrackBuilder使用
            videoFileInfo.GeneratedAvcCBox = avccData;
            
            // 从原始trak盒子中提取stsd
            byte[]? originalStsd = DashVideoProcessor.ExtractStsdBox(videoFileInfo.OriginalVideoTrakBox);
            if (originalStsd != null)
            {
                // 将avcC添加到stsd
                byte[] newStsd = H264ConfigExtractor.AddAvcCToStsd(originalStsd, avccData);
                statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.UpdatingStsd", originalStsd.Length, newStsd.Length));
                
                // 更新原始trak盒子中的stsd
                videoFileInfo.OriginalVideoTrakBox = DashVideoProcessor.ReplaceStsdInTrak(videoFileInfo.OriginalVideoTrakBox, newStsd);
            }
        }
        else
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.ConfigNotFound"));
            
            // 尝试从原始trak盒子中检查是否存在avcC
            byte[]? existingAvcC = DashVideoProcessor.ExtractAvcCBox(videoFileInfo.OriginalVideoTrakBox);
            if (existingAvcC != null)
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.UsingExistingAvcC", existingAvcC.Length));
                videoFileInfo.GeneratedAvcCBox = existingAvcC;
            }
            else
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.ConfigError"));
            }
        }
    }

    /// <summary>
    /// 重建轨道信息
    /// </summary>
    public (byte[] VideoTrak, byte[]? AudioTrak, uint VideoDuration, uint AudioDuration) ReconstructTracks(
        long mdatStart,
        MP4FileInfo videoFileInfo,
        MP4FileInfo audioFileInfo,
        List<uint> videoMdatSizes,
        List<uint> audioMdatSizes,
        List<byte[]> videoMdatContents,
        List<FMP4Parser.SampleInfo>? fmp4Samples,
        List<FMP4Parser.SampleInfo>? fmp4AudioSamples,
        Action<string>? statusCallback,
        bool isFirstPass)
    {
        byte[] originalVideoTrakBox = videoFileInfo.OriginalVideoTrakBox!;
        byte[]? originalAudioTrakBox = audioFileInfo.OriginalAudioTrakBox;
        byte[] tempVideoTrakBox;
        uint videoDuration = 0;
        uint audioDuration = 0;

        // 重建视频trak盒子
        if (fmp4Samples != null && fmp4Samples.Count > 0)
        {
            if (isFirstPass || !isFirstPass) // 总是输出日志
            {
                string passText = isFirstPass ? "" : MergerLocalization.GetString("Reconstructor.SecondPass");
                statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.RebuildingWithFMP4Samples", passText, fmp4Samples.Count));
            }
            
            tempVideoTrakBox = DashVideoProcessor.ProcessVideoTrakWithFMP4Samples(
                originalVideoTrakBox,
                fmp4Samples,
                mdatStart,
                videoFileInfo.VideoTimeScale);

            // 计算视频持续时间
            ulong totalDuration = 0;
            foreach (var sample in fmp4Samples)
            {
                totalDuration += sample.Duration > 0 ? sample.Duration : (videoFileInfo.VideoTimeScale / 30);
            }
            videoDuration = (uint)totalDuration;
            
            if (!isFirstPass)
                statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.UpdatingVideoDuration", videoDuration));
        }
        else if (videoMdatContents.Count > 0)
        {
            if (isFirstPass) statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.ParsingFrames"));
            
            var allFrames = new List<H264NalParser.FrameInfo>();
            long currentMdatOffset = 0;

            for (int i = 0; i < videoMdatContents.Count; i++)
            {
                // 如果是第二次pass，需要重新解析吗？或者我们可以复用第一次解析的结果并调整偏移量？
                // 为了简单起见，这里重新解析。优化点：缓存解析结果。
                var frames = H264NalParser.ParseFrames(videoMdatContents[i]);
                foreach (var frame in frames)
                {
                    frame.Offset += (int)currentMdatOffset;
                    foreach (var nalUnit in frame.NalUnits)
                    {
                        nalUnit.Offset += (int)currentMdatOffset;
                    }
                    allFrames.Add(frame);
                }
                currentMdatOffset += videoMdatSizes[i];
            }

            if (isFirstPass) statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.FramesFound", allFrames.Count));

            tempVideoTrakBox = DashVideoProcessor.ProcessVideoTrakWithFrames(
                originalVideoTrakBox,
                allFrames,
                mdatStart,
                videoFileInfo.VideoTimeScale,
                videoFileInfo.VideoDuration);
            
            if (allFrames.Count > 0 && videoFileInfo.VideoTimeScale > 0)
            {
                videoDuration = (uint)(allFrames.Count * (videoFileInfo.VideoTimeScale / 30));
                if (!isFirstPass) statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.UpdatingVideoDuration", videoDuration));
            }
        }
        else
        {
            tempVideoTrakBox = DashVideoProcessor.ProcessVideoTrakWithSizes(
                originalVideoTrakBox,
                videoMdatSizes,
                mdatStart,
                videoFileInfo.VideoTimeScale,
                videoFileInfo.VideoDuration);
            
            if (videoFileInfo.VideoTimeScale > 0 && videoMdatSizes.Count > 0)
            {
                uint perSample = videoFileInfo.VideoTimeScale / 30;
                if (perSample == 0) perSample = 1;
                videoDuration = perSample * (uint)videoMdatSizes.Count;
                if (!isFirstPass) statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.UpdatingVideoDuration", videoDuration));
            }
        }
        
        if (!isFirstPass)
            statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.VideoTrakSizeAfter", tempVideoTrakBox.Length));

        // 重建音频trak盒子
        byte[]? tempAudioTrakBox = originalAudioTrakBox;
        if (originalAudioTrakBox != null && audioMdatSizes.Count > 0)
        {
            if (!isFirstPass) statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.AudioTrakSizeBefore", originalAudioTrakBox.Length));

            long videoDataSize = videoMdatSizes.Sum(s => (long)s);
            long audioMdatStart = mdatStart + videoDataSize;

            if (fmp4AudioSamples != null && fmp4AudioSamples.Count > 0)
            {
                if (isFirstPass || !isFirstPass)
                {
                    string passText = isFirstPass ? "" : MergerLocalization.GetString("Reconstructor.SecondPass");
                    statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.RebuildingAudioWithFMP4", passText, fmp4AudioSamples.Count));
                }
                
                tempAudioTrakBox = DashVideoProcessor.ProcessAudioTrakWithFMP4Samples(
                    originalAudioTrakBox,
                    fmp4AudioSamples,
                    audioMdatStart,
                    audioFileInfo.AudioTimeScale);
                
                ulong totalDuration = 0;
                foreach (var sample in fmp4AudioSamples)
                {
                    totalDuration += sample.Duration > 0 ? sample.Duration : (audioFileInfo.AudioTimeScale > 0 ? audioFileInfo.AudioTimeScale / 43 : 1024);
                }
                audioDuration = (uint)totalDuration;
                
                if (!isFirstPass) statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.UpdatingAudioDuration", audioDuration));
            }
            else
            {
                ulong durationCalc;
                if (audioFileInfo.AudioDuration > 0)
                {
                    durationCalc = audioFileInfo.AudioDuration;
                }
                else if (audioFileInfo.AudioTimeScale > 0)
                {
                    const int samplesPerFrame = 1024;
                    durationCalc = (ulong)(audioMdatSizes.Count * samplesPerFrame);
                }
                else
                {
                    durationCalc = videoDuration > 0 ? videoDuration : (ulong)(44100 * 300);
                }

                tempAudioTrakBox = DashVideoProcessor.ProcessAudioTrakWithSizes(
                    originalAudioTrakBox,
                    audioMdatSizes,
                    audioMdatStart,
                    audioFileInfo.AudioTimeScale,
                    durationCalc);
                
                audioDuration = (uint)durationCalc;
                
                if (!isFirstPass) statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.UpdatingAudioDuration", audioDuration));
            }
            
            if (!isFirstPass) statusCallback?.Invoke(MergerLocalization.GetString("Reconstructor.AudioTrakSizeAfter", tempAudioTrakBox.Length));
        }

        return (tempVideoTrakBox, tempAudioTrakBox, videoDuration, audioDuration);
    }
}
