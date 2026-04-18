using VideoStreamFetcher.Remux;

namespace VideoStreamFetcher.Downloads;

/// <summary>
/// 转封装服务，负责TS到MP4的格式转换
/// </summary>
public sealed class RemuxService
{
    /// <summary>
    /// 如果需要，将TS文件转封装为MP4
    /// </summary>
    public async Task<(string path, long bytes)> RemuxTsIfNeededAsync(
        string outputPath,
        long bytesWritten,
        bool shouldRemux,
        bool keepTsFile,
        Action<string>? statusCallback,
        CancellationToken cancellationToken)
    {
        if (!shouldRemux)
        {
            return (outputPath, bytesWritten);
        }

        if (!outputPath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
        {
            return (outputPath, bytesWritten);
        }

        var mp4Path = Path.ChangeExtension(outputPath, ".mp4");
        statusCallback?.Invoke("[REMUX] 开始 TS→MP4 转封装...");
        await TsToMp4Remuxer.RemuxAsync(outputPath, mp4Path, statusCallback, cancellationToken);

        if (!keepTsFile)
        {
            TryDeleteFile(outputPath, statusCallback);
        }

        var len = new FileInfo(mp4Path).Length;
        return (mp4Path, len > 0 ? len : bytesWritten);
    }

    /// <summary>
    /// 尝试删除文件
    /// </summary>
    private static void TryDeleteFile(string path, Action<string>? statusCallback)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                statusCallback?.Invoke($"[REMUX] 已删除临时TS文件: {path}");
            }
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"[REMUX] 删除TS文件失败: {ex.Message}");
        }
    }
}
