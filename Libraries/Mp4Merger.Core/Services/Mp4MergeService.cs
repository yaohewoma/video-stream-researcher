using System.IO;
using Mp4Merger.Core.Core;
using Mp4Merger.Core.Localization;
using Mp4Merger.Core.Models;

namespace Mp4Merger.Core.Services;

/// <summary>
/// MP4合并服务类，提供简洁的公共接口
/// </summary>
public class Mp4MergeService
{
    /// <summary>
    /// 合并视频和音频文件
    /// </summary>
    /// <param name="videoPath">视频文件路径</param>
    /// <param name="audioPath">音频文件路径</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <param name="convertToNonFragmented">是否转换为非分片MP4</param>
    /// <returns>合并结果信息</returns>
    public async Task<MergeResult> MergeAsync(string videoPath, string audioPath, string outputPath, Action<string>? statusCallback = null, bool convertToNonFragmented = false)
    {
        using MP4Merger merger = new MP4Merger();
        return await merger.MergeVideoAudioAsync(videoPath, audioPath, outputPath, statusCallback, convertToNonFragmented);
    }
    
    /// <summary>
    /// 批量合并视频和音频文件
    /// </summary>
    /// <param name="mergeTasks">合并任务列表</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>合并结果信息列表</returns>
    public async Task<List<MergeResult>> BatchMergeAsync(List<MergeTask> mergeTasks, Action<string>? statusCallback = null)
    {
        List<MergeResult> results = new List<MergeResult>();
        
        for (int i = 0; i < mergeTasks.Count; i++)
        {
            MergeTask task = mergeTasks[i];
            statusCallback?.Invoke(MergerLocalization.GetString("Service.StartingTask", i + 1, mergeTasks.Count, Path.GetFileName(task.OutputPath)));
            
            MergeResult result = await MergeAsync(task.VideoPath, task.AudioPath, task.OutputPath, statusCallback, task.ConvertToNonFragmented);
            results.Add(result);
            
            string status = result.Success ? MergerLocalization.GetString("Service.Success") : MergerLocalization.GetString("Service.Failed");
            statusCallback?.Invoke(MergerLocalization.GetString("Service.TaskCompleted", i + 1, mergeTasks.Count, status));
        }
        
        return results;
    }
}

/// <summary>
/// 合并任务类，用于批量合并
/// </summary>
public class MergeTask
{
    /// <summary>
    /// 视频文件路径
    /// </summary>
    public string VideoPath { get; set; }
    
    /// <summary>
    /// 音频文件路径
    /// </summary>
    public string AudioPath { get; set; }
    
    /// <summary>
    /// 输出文件路径
    /// </summary>
    public string OutputPath { get; set; }
    
    /// <summary>
    /// 是否转换为非分片MP4
    /// </summary>
    public bool ConvertToNonFragmented { get; set; }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="videoPath">视频文件路径</param>
    /// <param name="audioPath">音频文件路径</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="convertToNonFragmented">是否转换为非分片MP4</param>
    public MergeTask(string videoPath, string audioPath, string outputPath, bool convertToNonFragmented = false)
    {
        VideoPath = videoPath;
        AudioPath = audioPath;
        OutputPath = outputPath;
        ConvertToNonFragmented = convertToNonFragmented;
    }
}
