using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VideoStreamFetcher.Parsers;

/// <summary>
/// URL 辅助类
/// 负责处理 URL 相关的操作
/// </summary>
public static class UrlHelper
{
    /// <summary>
    /// 解析 B 站短链接，获取真实 URL
    /// </summary>
    /// <param name="shortUrl">B 站短链接，如 https://b23.tv/xxxxxx</param>
    /// <param name="httpHelper">HTTP 请求辅助类</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>真实的 B 站视频 URL</returns>
    public static async Task<string> ResolveBilibiliShortUrl(string shortUrl, HttpHelper httpHelper, Action<string>? statusCallback)
    {
        try
        {
            // 直接使用原始 URL，暂时不解析短链接，避免复杂的重定向处理
            statusCallback?.Invoke("跳过短链接解析，直接使用原始 URL");
            return shortUrl;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"解析短链接失败: {ex.Message}");
            // 如果解析失败，返回原始 URL
            return shortUrl;
        }
    }
    
    /// <summary>
    /// 从 URL 中提取视频 ID
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>视频 ID</returns>
    public static string ExtractVideoIdFromUrl(string url)
    {
        var match = Regex.Match(url, @"article/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        match = Regex.Match(url, @"video/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// 从 HTML 中提取视频 ID
    /// </summary>
    /// <param name="html">HTML 内容</param>
    /// <returns>视频 ID</returns>
    public static string ExtractVideoIdFromHtml(string html)
    {
        var match = Regex.Match(html, @"videoId\s*=\s*[""'](\d+)[""']", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        match = Regex.Match(html, @"articleId\s*=\s*[""'](\d+)[""']", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        match = Regex.Match(html, @"""id""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// 构造完整的 URL
    /// </summary>
    /// <param name="baseUrl">基础 URL</param>
    /// <param name="relativePath">相对路径</param>
    /// <returns>完整的 URL</returns>
    public static string GetFullUrl(string baseUrl, string relativePath)
    {
        Uri baseUri = new Uri(baseUrl);
        return new Uri(baseUri, relativePath).ToString();
    }
    
    /// <summary>
    /// 获取 URL 的基础部分（协议 + 域名）
    /// </summary>
    /// <param name="url">完整 URL</param>
    /// <returns>基础 URL</returns>
    public static string GetBaseUrl(string url)
    {
        return new Uri(url).GetLeftPart(UriPartial.Authority);
    }
    
    /// <summary>
    /// 检查 URL 是否为视频文件
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>是否为视频文件</returns>
    public static bool IsVideoUrl(string url)
    {
        string[] videoExtensions = { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".flv", ".m4v" };
        foreach (var extension in videoExtensions)
        {
            if (url.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 从 URL 中提取文件名
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>文件名</returns>
    public static string ExtractFileNameFromUrl(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            string path = uri.LocalPath;
            return Path.GetFileName(path);
        }
        catch
        {
            // 如果解析失败，尝试从字符串末尾提取
            int lastSlashIndex = url.LastIndexOf('/');
            if (lastSlashIndex >= 0 && lastSlashIndex < url.Length - 1)
            {
                return url.Substring(lastSlashIndex + 1);
            }
            return string.Empty;
        }
    }
    
    // 添加 Path 类的引用
    private static class Path
    {
        public static string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            
            int lastSlashIndex = path.LastIndexOf('/');
            if (lastSlashIndex >= 0 && lastSlashIndex < path.Length - 1)
            {
                return path.Substring(lastSlashIndex + 1);
            }
            return path;
        }
    }
}