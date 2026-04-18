using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NativeVideoProcessor.Interfaces;
using Vortice.MediaFoundation;
using Vortice.Mathematics;

namespace NativeVideoProcessor.Engine
{
    public class MfMediaEngine : IMediaEngine
    {
        private bool _isInitialized = false;

        public MfMediaEngine()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (!_isInitialized)
            {
                MediaFactory.MFStartup();
                _isInitialized = true;
            }
        }

        public async Task<MediaInfo> GetMediaInfoAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                using var attribute = MediaFactory.MFCreateAttributes(1);
                // 启用硬件加速 (DXVA)
                attribute.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, 1);

                using var reader = MediaFactory.MFCreateSourceReaderFromURL(filePath, attribute);
                
                var info = new MediaInfo();

                // 读取第一个视频流
                int streamIndex = (int)SourceReaderIndex.FirstVideoStream;
                
                // 获取媒体类型
                using var mediaType = reader.GetCurrentMediaType(streamIndex);
                
                // 解析宽高等信息
                ulong frameSize = mediaType.GetUInt64(MediaTypeAttributeKeys.FrameSize);
                info.Width = (int)(frameSize >> 32);
                info.Height = (int)(frameSize & 0xFFFF);
                
                ulong frameRate = mediaType.GetUInt64(MediaTypeAttributeKeys.FrameRate);
                uint frNumerator = (uint)(frameRate >> 32);
                uint frDenominator = (uint)(frameRate & 0xFFFF);
                info.FrameRate = frDenominator == 0 ? 0 : (double)frNumerator / frDenominator;

                var durationVar = reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration);
                long durationVal = 0;
                if (durationVar.Value is long l) durationVal = l;
                else if (durationVar.Value is ulong ul) durationVal = (long)ul;
                
                info.Duration = TimeSpan.FromTicks(durationVal); // 100ns units = ticks

                Guid subtype = mediaType.GetGUID(MediaTypeAttributeKeys.Subtype);
                info.VideoCodec = GetCodecName(subtype);

                return info;
            });
        }

        public async Task TranscodeAsync(string inputFile, string outputFile, TranscodeOptions options, IProgress<double> progress, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                // 1. 创建 Source Reader
                using var attributes = MediaFactory.MFCreateAttributes(2);
                attributes.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, 1);
                // 启用 DXVA 解码
                // attributes.Set(SourceReaderAttributeKeys.D3DManager, (object)null); // 暂时注释，避免编译错误

                using var reader = MediaFactory.MFCreateSourceReaderFromURL(inputFile, attributes);

                // 2. 创建 Sink Writer
                using var writer = MediaFactory.MFCreateSinkWriterFromURL(outputFile, null, null);

                // 3. 配置视频流
                int streamIndex = (int)SourceReaderIndex.FirstVideoStream;
                
                // 3.1 配置输出媒体类型 (H.264)
                using var outputType = MediaFactory.MFCreateMediaType();
                outputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                outputType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
                outputType.Set(MediaTypeAttributeKeys.AvgBitrate, options.VideoBitrate);
                outputType.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
                outputType.Set(MediaTypeAttributeKeys.FrameSize, ((long)options.TargetWidth << 32) | (uint)options.TargetHeight);
                outputType.Set(MediaTypeAttributeKeys.FrameRate, ((long)30 << 32) | 1); // 简化：强制 30fps，实际应读取源

                int writerStreamIndex = writer.AddStream(outputType);

                // 3.2 配置输入媒体类型 (NV12 用于硬件编码输入)
                using var inputType = MediaFactory.MFCreateMediaType();
                inputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                inputType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.NV12);
                inputType.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
                inputType.Set(MediaTypeAttributeKeys.FrameSize, ((long)options.TargetWidth << 32) | (uint)options.TargetHeight);
                
                writer.SetInputMediaType(writerStreamIndex, inputType, null);

                // 3.3 配置 Reader 输出为 NV12 (自动转换)
                reader.SetCurrentMediaType(streamIndex, inputType);

                // 4. 开始处理
                writer.BeginWriting();

                var durationVar = reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration);
                long durationTicks = 0;
                if (durationVar.Value is long l) durationTicks = l;
                else if (durationVar.Value is ulong ul) durationTicks = (long)ul;
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var sample = reader.ReadSample(streamIndex, SourceReaderControlFlag.None, out int actualStreamIndex, out SourceReaderFlag flags, out long timestamp);

                        if (flags.HasFlag(SourceReaderFlag.EndOfStream))
                        {
                            break;
                        }

                        if (sample != null)
                        {
                            sample.SampleTime = timestamp;
                            writer.WriteSample(writerStreamIndex, sample);
                            sample.Dispose();

                            // 进度报告
                            if (durationTicks > 0)
                            {
                                double percent = (double)timestamp / durationTicks * 100.0;
                                progress?.Report(percent);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 处理流读取错误或结束
                        break;
                    }
                }

                writer.Finalize();
            }, cancellationToken);
        }

        private string GetCodecName(Guid subtype)
        {
            if (subtype == VideoFormatGuids.H264) return "H.264";
            if (subtype == VideoFormatGuids.H265) return "H.265";
            return subtype.ToString();
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                MediaFactory.MFShutdown();
                _isInitialized = false;
            }
        }
    }
}
