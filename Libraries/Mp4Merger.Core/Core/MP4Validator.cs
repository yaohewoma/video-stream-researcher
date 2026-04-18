using System;
using System.IO;
using Mp4Merger.Core.Localization;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Core;

/// <summary>
/// MP4验证器，负责验证输入文件和输出路径
/// </summary>
public class MP4Validator
{
    /// <summary>
    /// 验证输入文件
    /// </summary>
    /// <param name="videoPath">视频文件路径</param>
    /// <param name="audioPath">音频文件路径</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>验证是否通过，如果不通过返回错误消息</returns>
    public (bool IsValid, string? ErrorMessage) ValidateInputFiles(string videoPath, string audioPath, Action<string>? statusCallback)
    {
        // 检查输入文件是否存在
        if (!File.Exists(videoPath))
            return (false, MergerLocalization.GetString("Validator.VideoNotFound"));
        
        if (!File.Exists(audioPath))
            return (false, MergerLocalization.GetString("Validator.AudioNotFound"));
        
        // 检查文件大小
        var videoFile = new FileInfo(videoPath);
        var audioFile = new FileInfo(audioPath);
        
        statusCallback?.Invoke(MergerLocalization.GetString("Validator.VideoSize", videoFile.Length / 1024.0 / 1024.0));
        statusCallback?.Invoke(MergerLocalization.GetString("Validator.AudioSize", audioFile.Length / 1024.0 / 1024.0));
        
        if (videoFile.Length < 1024 * 1024) // 小于1MB
            statusCallback?.Invoke(MergerLocalization.GetString("Validator.VideoTooSmall"));
        
        if (audioFile.Length < 1024 * 100) // 小于100KB
            statusCallback?.Invoke(MergerLocalization.GetString("Validator.AudioTooSmall"));

        return (true, null);
    }

    /// <summary>
    /// 确保输出目录存在
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="statusCallback">状态回调</param>
    public void EnsureOutputDirectoryExists(string outputPath, Action<string>? statusCallback)
    {
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Validator.CreatingOutputDir", outputDir));
            Directory.CreateDirectory(outputDir);
        }
    }

    /// <summary>
    /// 验证输出文件
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>是否验证通过</returns>
    public bool ValidateOutputFile(string outputPath, Action<string>? statusCallback)
    {
        if (!File.Exists(outputPath))
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Validator.OutputNotCreated"));
            return false;
        }
        
        var outputFile = new FileInfo(outputPath);
        statusCallback?.Invoke(MergerLocalization.GetString("Validator.OutputSize", outputFile.Length / 1024.0 / 1024.0));
        
        if (outputFile.Length < 1024 * 1024) // 小于1MB
            statusCallback?.Invoke(MergerLocalization.GetString("Validator.OutputTooSmall"));

        return true;
    }
}
