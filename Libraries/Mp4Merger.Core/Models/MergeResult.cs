namespace Mp4Merger.Core.Models;

/// <summary>
/// 合并结果类，用于存储视频音频合并的结果信息
/// </summary>
public class MergeResult
{
    /// <summary>
    /// 是否合并成功
    /// </summary>
    public bool Success { get; }
    
    /// <summary>
    /// 合并结果消息
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// 输出文件路径
    /// </summary>
    public string? OutputPath { get; }
    
    /// <summary>
    /// 非分片MP4文件路径
    /// </summary>
    public string? NonFragmentedPath { get; }
    
    /// <summary>
    /// 输入视频文件路径
    /// </summary>
    public string? InputVideoPath { get; }
    
    /// <summary>
    /// 输入音频文件路径
    /// </summary>
    public string? InputAudioPath { get; }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="success">是否成功</param>
    /// <param name="message">结果消息</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="nonFragmentedPath">非分片MP4文件路径</param>
    /// <param name="inputVideoPath">输入视频文件路径</param>
    /// <param name="inputAudioPath">输入音频文件路径</param>
    public MergeResult(bool success, string message, string? outputPath, string? nonFragmentedPath, string? inputVideoPath, string? inputAudioPath)
    {
        Success = success;
        Message = message;
        OutputPath = outputPath;
        NonFragmentedPath = nonFragmentedPath;
        InputVideoPath = inputVideoPath;
        InputAudioPath = inputAudioPath;
    }
}
