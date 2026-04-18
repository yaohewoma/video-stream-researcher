using System;
using System.IO;
using System.Threading.Tasks;
using video_stream_researcher.Interfaces;

namespace video_stream_researcher.Services;

/// <summary>
/// 预览服务
/// 负责处理视频预览相关的逻辑
/// </summary>
public class PreviewService
{
    private readonly ILogManager _logManager;
    private readonly Action<string> _showPreviewWindow;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logManager">日志管理器</param>
    /// <param name="showPreviewWindow">显示预览窗口的委托</param>
    public PreviewService(ILogManager logManager, Action<string> showPreviewWindow)
    {
        _logManager = logManager;
        _showPreviewWindow = showPreviewWindow;
    }

    /// <summary>
    /// 尝试打开预览
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="previewEnabled">是否启用预览编辑</param>
    /// <param name="autoPreviewAfterDownload">是否下载完成后自动预览</param>
    /// <returns>是否成功打开预览</returns>
    public bool TryOpenPreview(string? outputPath, bool previewEnabled, bool autoPreviewAfterDownload)
    {
        // 检查是否应该预览
        if (!ShouldPreview(previewEnabled, autoPreviewAfterDownload))
        {
            return false;
        }

        // 检查输出路径
        if (!ValidateOutputPath(outputPath))
        {
            return false;
        }

        // 解析预览路径
        var previewPath = ResolvePreviewPath(outputPath!);
        if (string.IsNullOrWhiteSpace(previewPath))
        {
            return false;
        }

        // 打开预览窗口
        return OpenPreviewWindow(previewPath);
    }

    /// <summary>
    /// 异步尝试打开预览
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="previewEnabled">是否启用预览编辑</param>
    /// <param name="autoPreviewAfterDownload">是否下载完成后自动预览</param>
    /// <returns>是否成功打开预览</returns>
    public async Task<bool> TryOpenPreviewAsync(string? outputPath, bool previewEnabled, bool autoPreviewAfterDownload)
    {
        return await Task.Run(() => TryOpenPreview(outputPath, previewEnabled, autoPreviewAfterDownload));
    }

    /// <summary>
    /// 检查是否应该预览
    /// </summary>
    private bool ShouldPreview(bool previewEnabled, bool autoPreviewAfterDownload)
    {
        bool shouldPreview = previewEnabled || autoPreviewAfterDownload;
        
        _logManager.UpdateLog($"📋 预览检查: 启用预览={shouldPreview}");
        
        if (!shouldPreview)
        {
            _logManager.UpdateLog("ℹ️ 预览未启用");
        }
        
        return shouldPreview;
    }

    /// <summary>
    /// 验证输出路径
    /// </summary>
    private bool ValidateOutputPath(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            _logManager.UpdateLog("⚠️ 警告: 输出路径为空，无法打开预览");
            return false;
        }

        _logManager.UpdateLog($"🎬 准备预览文件: {outputPath}");
        return true;
    }

    /// <summary>
    /// 解析预览路径
    /// </summary>
    private string? ResolvePreviewPath(string outputPath)
    {
        // 检查文件是否存在
        if (File.Exists(outputPath))
        {
            // 对于 MP4 文件，优先使用 TS 版本（如果存在）
            if (outputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                var tsPath = Path.ChangeExtension(outputPath, ".ts");
                if (File.Exists(tsPath))
                {
                    _logManager.UpdateLog($"📝 使用 TS 版本进行预览: {tsPath}");
                    return tsPath;
                }
            }
            
            return outputPath;
        }

        // 文件不存在，尝试查找替代文件
        _logManager.UpdateLog($"⚠️ 警告: 预览文件不存在: {outputPath}");
        
        return FindAlternativeFile(outputPath);
    }

    /// <summary>
    /// 查找替代文件
    /// </summary>
    private string? FindAlternativeFile(string originalPath)
    {
        // 如果是 MP4 文件，尝试查找 TS 版本
        if (originalPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            var tsPath = Path.ChangeExtension(originalPath, ".ts");
            if (File.Exists(tsPath))
            {
                _logManager.UpdateLog($"✅ 找到替代文件: {tsPath}");
                return tsPath;
            }
        }
        
        // 如果是 TS 文件，尝试查找 MP4 版本
        if (originalPath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            var mp4Path = Path.ChangeExtension(originalPath, ".mp4");
            if (File.Exists(mp4Path))
            {
                _logManager.UpdateLog($"✅ 找到替代文件: {mp4Path}");
                return mp4Path;
            }
        }

        _logManager.UpdateLog("❌ 错误: 无法找到可预览的文件");
        return null;
    }

    /// <summary>
    /// 打开预览窗口
    /// </summary>
    private bool OpenPreviewWindow(string previewPath)
    {
        try
        {
            _logManager.UpdateLog($"🎬 正在打开预览窗口...");
            _showPreviewWindow(previewPath);
            _logManager.UpdateLog($"✅ 预览窗口已打开");
            return true;
        }
        catch (Exception ex)
        {
            _logManager.UpdateLog($"❌ 打开预览窗口失败: {ex.Message}");
            return false;
        }
    }
}
