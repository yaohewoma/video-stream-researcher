using System.Threading;
using System.Threading.Tasks;
using System;

namespace VideoStreamFetcher.Parsers.PlatformParsers;

/// <summary>
/// 平台解析器接口
/// 定义了特定平台视频解析的标准操作
/// </summary>
public interface IPlatformParser
{
    /// <summary>
    /// 判断该解析器是否支持解析给定的 URL
    /// </summary>
    /// <param name="url">视频 URL</param>
    /// <returns>是否支持</returns>
    bool CanParse(string url);

    /// <summary>
    /// 解析视频信息
    /// </summary>
    /// <param name="url">视频 URL</param>
    /// <param name="html">页面 HTML 内容（如果已下载）</param>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>解析出的视频信息</returns>
    Task<VideoInfo?> ParseAsync(string url, string? html, Action<string>? statusCallback, CancellationToken cancellationToken = default);
}
