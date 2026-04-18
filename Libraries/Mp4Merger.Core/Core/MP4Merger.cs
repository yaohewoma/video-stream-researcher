using Mp4Merger.Core.Extensions;
using Mp4Merger.Core.Localization;
using Mp4Merger.Core.Media;
using Mp4Merger.Core.Models;

namespace Mp4Merger.Core.Core;

/// <summary>
/// MP4合并器类，负责合并视频和音频文件
/// </summary>
public class MP4Merger : IDisposable
{
    private bool _disposedValue = false;
    private readonly MP4Validator _validator;
    private readonly MP4Writer _writer;
    private readonly MediaProcessor _mediaProcessor;

    public MP4Merger()
    {
        _validator = new MP4Validator();
        _writer = new MP4Writer();
        _mediaProcessor = new MediaProcessor();
    }

    /// <summary>
    /// 合并视频和音频文件
    /// </summary>
    /// <param name="videoPath">视频文件路径</param>
    /// <param name="audioPath">音频文件路径</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <param name="convertToNonFragmented">是否转换为非分片MP4</param>
    /// <returns>合并结果信息</returns>
    public async Task<MergeResult> MergeVideoAudioAsync(string videoPath, string audioPath, string outputPath, Action<string>? statusCallback = null, bool convertToNonFragmented = false)
    {
        DateTime startTime = DateTime.Now;
        statusCallback?.Invoke(MergerLocalization.GetString("Merge.StartTask", startTime));

        try
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.InputFiles", videoPath, audioPath));
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.OutputFile", outputPath));

            // 1. 验证输入文件
            var (isValid, errorMessage) = _validator.ValidateInputFiles(videoPath, audioPath, statusCallback);
            if (!isValid)
            {
                return CreateErrorResult(errorMessage!, videoPath, audioPath, statusCallback);
            }

            // 2. 确保输出目录存在
            _validator.EnsureOutputDirectoryExists(outputPath, statusCallback);

            // 3. 处理媒体数据
            var processedData = await _mediaProcessor.ProcessAsync(videoPath, audioPath, statusCallback);

            // 5. 创建输出文件
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.CreatingFile"));
            await _writer.CreateOutputFileAsync(
                outputPath,
                processedData.VideoFileInfo,
                processedData.AudioFileInfo,
                processedData.ExtractedVideoData,
                processedData.ExtractedAudioData,
                processedData.VideoMdatSizes,
                processedData.AudioMdatSizes,
                processedData.VideoMdatContents,
                processedData.Fmp4VideoSamples,
                processedData.Fmp4AudioSamples,
                statusCallback);

            // 6. 验证输出文件
            if (!_validator.ValidateOutputFile(outputPath, statusCallback))
            {
                return CreateErrorResult(MergerLocalization.GetString("Merge.OutputValidationFailed"), videoPath, audioPath, statusCallback);
            }

            // 7. 处理非分片MP4转换
            string? nonFragmentedPath = await HandleNonFragmentedConversion(outputPath, convertToNonFragmented, statusCallback);

            TimeSpan elapsed = DateTime.Now - startTime;
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.Completed", elapsed.TotalSeconds));
            return new MergeResult(true, MergerLocalization.GetString("Service.Success"), nonFragmentedPath, null, videoPath, audioPath);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.Failed", ex.Message));
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.ExceptionType", ex.GetType().Name));
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.ExceptionDetails", ex.ToString()));

            // 清理可能的不完整文件
            TryCleanupIncompleteFile(outputPath, statusCallback);

            return new MergeResult(false, ex.Message, null, null, videoPath, audioPath);
        }
    }

    /// <summary>
    /// 创建错误结果
    /// </summary>
    private MergeResult CreateErrorResult(string message, string videoPath, string audioPath, Action<string>? statusCallback)
    {
        statusCallback?.Invoke($"❌ {message}");
        return new MergeResult(false, message, null, null, videoPath, audioPath);
    }


    /// <summary>
    /// 处理非分片MP4转换
    /// </summary>
    private async Task<string?> HandleNonFragmentedConversion(string outputPath, bool convertToNonFragmented, Action<string>? statusCallback)
    {
        if (!convertToNonFragmented)
            return outputPath;

        statusCallback?.Invoke(MergerLocalization.GetString("Merge.ConvertingToNonFragmented"));
        try
        {
            // 对于当前实现，我们直接使用生成的文件作为非分片MP4
            // 因为我们已经在创建时使用了非分片格式
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.NonFragmentedComplete"));
            return outputPath;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.NonFragmentedFailed", ex.Message));
            statusCallback?.Invoke(MergerLocalization.GetString("Merge.UsingOriginalFile"));
            return outputPath;
        }
    }

    /// <summary>
    /// 尝试清理不完整的文件
    /// </summary>
    private void TryCleanupIncompleteFile(string outputPath, Action<string>? statusCallback) =>
        File.Exists(outputPath).IfTrue(() =>
        {
            try
            {
                File.Delete(outputPath);
                statusCallback?.Invoke(MergerLocalization.GetString("Merge.CleanupFile"));
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"[清理] 删除不完整文件失败: {ex.Message}");
            }
        });

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _disposedValue = true;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
