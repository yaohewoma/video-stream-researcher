using System.Security;

namespace VideoStreamFetcher.Downloads;

/// <summary>
/// 路径安全验证器，防止路径遍历攻击
/// </summary>
public static class PathSecurityValidator
{
    /// <summary>
    /// 验证输出路径是否安全（防止路径遍历攻击）
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="baseDirectory">基础目录</param>
    /// <returns>验证是否通过</returns>
    public static bool IsPathSafe(string outputPath, string baseDirectory)
    {
        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            var fullBaseDirectory = Path.GetFullPath(baseDirectory);

            // 确保输出路径在基础目录下
            if (!fullOutputPath.StartsWith(fullBaseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 检查路径中是否包含非法字符
            var invalidChars = Path.GetInvalidPathChars();
            if (outputPath.IndexOfAny(invalidChars) >= 0)
            {
                return false;
            }

            // 检查文件名中是否包含非法字符
            var fileName = Path.GetFileName(outputPath);
            var invalidFileChars = Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(invalidFileChars) >= 0)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证输出路径是否安全，如果不安全则抛出异常
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="baseDirectory">基础目录</param>
    public static void ValidatePathOrThrow(string outputPath, string baseDirectory)
    {
        if (!IsPathSafe(outputPath, baseDirectory))
        {
            throw new SecurityException($"非法的文件路径: {outputPath}。路径必须在 {baseDirectory} 目录下。");
        }
    }

    /// <summary>
    /// 清理文件名，移除非法字符
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "unnamed";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Select(c => invalidChars.Contains(c) ? '_' : c)
            .ToArray());

        // 限制文件名长度
        const int maxLength = 200;
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength);
        }

        return sanitized;
    }
}
