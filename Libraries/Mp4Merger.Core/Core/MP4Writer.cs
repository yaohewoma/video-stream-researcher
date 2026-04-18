using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mp4Merger.Core.Boxes;
using Mp4Merger.Core.Localization;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Core;

/// <summary>
/// MP4写入器，负责创建最终的MP4文件
/// </summary>
public class MP4Writer
{
    private readonly MP4TrackReconstructor _reconstructor;

    public MP4Writer()
    {
        _reconstructor = new MP4TrackReconstructor();
    }

    /// <summary>
    /// 创建输出文件
    /// </summary>
    public async Task CreateOutputFileAsync(
        string outputPath, 
        MP4FileInfo videoFileInfo, 
        MP4FileInfo audioFileInfo, 
        byte[] videoData, 
        byte[] audioData, 
        List<uint> videoMdatSizes, 
        List<uint> audioMdatSizes, 
        List<byte[]> videoMdatContents, 
        List<FMP4Parser.SampleInfo>? fmp4Samples, 
        List<FMP4Parser.SampleInfo>? fmp4AudioSamples, 
        Action<string>? statusCallback)
    {
        using FileStream outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

        // 写入ftyp盒子
        statusCallback?.Invoke(MergerLocalization.GetString("Writer.WritingFtyp"));
        await new FtypBox().WriteToStreamAsync(outputStream);
        long ftypEndPosition = outputStream.Position;

        // 如果是DASH格式，需要两次重建trak盒子
        bool isDashVideo = videoFileInfo.SidxInfo != null && videoFileInfo.SidxInfo.References.Count > 0;
        if (isDashVideo && videoFileInfo.OriginalVideoTrakBox != null)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Writer.ProcessingDash"));

            // 提取H.264配置信息
            _reconstructor.ExtractAndProcessH264Config(videoFileInfo, videoMdatContents, fmp4Samples, statusCallback);

            // 第一次重建：使用估算的mdat起始位置（ftyp大小 + 估算的moov大小）
            long estimatedMdatStart = ftypEndPosition + 4000; // 估算moov大小为4000字节

            // 重建轨道（第一次）
            var (tempVideoTrakBox, tempAudioTrakBox, _, _) = _reconstructor.ReconstructTracks(
                estimatedMdatStart,
                videoFileInfo,
                audioFileInfo,
                videoMdatSizes,
                audioMdatSizes,
                videoMdatContents,
                fmp4Samples,
                fmp4AudioSamples,
                statusCallback,
                isFirstPass: true);

            // 创建临时的文件信息对象，用于构建moov盒子
            var tempVideoFileInfo = new MP4FileInfo
            {
                VideoWidth = videoFileInfo.VideoWidth,
                VideoHeight = videoFileInfo.VideoHeight,
                VideoTimeScale = videoFileInfo.VideoTimeScale,
                VideoDuration = videoFileInfo.VideoDuration,
                SampleDelta = videoFileInfo.SampleDelta,
                OriginalVideoTrakBox = tempVideoTrakBox,
                OriginalAudioTrakBox = tempAudioTrakBox,
                AudioTimeScale = audioFileInfo.AudioTimeScale,
                AudioDuration = audioFileInfo.AudioDuration,
                AudioChannels = audioFileInfo.AudioChannels,
                AudioSampleRate = audioFileInfo.AudioSampleRate
            };

            // 构建moov盒子到MemoryStream，获取实际大小
            using (var moovMemoryStream = new MemoryStream())
            {
                var tempMoovBox = new MoovBox(tempVideoFileInfo, tempVideoFileInfo, videoData, audioData);
                await tempMoovBox.WriteToStreamAsync(moovMemoryStream);
                long actualMoovSize = moovMemoryStream.Length;
                long actualMdatStart = ftypEndPosition + actualMoovSize;

                statusCallback?.Invoke(MergerLocalization.GetString("Writer.EstimatedMoovSize", 4000, actualMoovSize));
                statusCallback?.Invoke(MergerLocalization.GetString("Writer.ActualMdatStart", actualMdatStart));

                // 第二次重建：使用实际的mdat起始位置
                var (finalVideoTrakBox, finalAudioTrakBox, finalVideoDuration, finalAudioDuration) = _reconstructor.ReconstructTracks(
                    actualMdatStart,
                    videoFileInfo,
                    audioFileInfo,
                    videoMdatSizes,
                    audioMdatSizes,
                    videoMdatContents,
                    fmp4Samples,
                    fmp4AudioSamples,
                    statusCallback,
                    isFirstPass: false);

                videoFileInfo.OriginalVideoTrakBox = finalVideoTrakBox;
                if (finalVideoDuration > 0) videoFileInfo.VideoDuration = finalVideoDuration;

                if (finalAudioTrakBox != null)
                {
                    audioFileInfo.OriginalAudioTrakBox = finalAudioTrakBox;
                    if (finalAudioDuration > 0) audioFileInfo.AudioDuration = finalAudioDuration;
                }
            }
        }

        // 写入moov盒子（包含mvhd和trak等子盒子）
        statusCallback?.Invoke(MergerLocalization.GetString("Writer.WritingMoov"));
        var moovBox = new MoovBox(videoFileInfo, audioFileInfo, videoData, audioData);
        long moovStartPosition = outputStream.Position;
        long moovSize = await moovBox.WriteToStreamAsync(outputStream);

        // 计算mdat盒子的起始位置
        long mdatStartPosition = outputStream.Position;
        statusCallback?.Invoke(MergerLocalization.GetString("Writer.MdatStartPosition", mdatStartPosition));

        // 写入mdat盒子
        statusCallback?.Invoke(MergerLocalization.GetString("Writer.WritingMdat"));
        await new MdatBox(videoData, audioData).WriteToStreamAsync(outputStream);

        statusCallback?.Invoke(MergerLocalization.GetString("Writer.FileCreated"));
    }
}
