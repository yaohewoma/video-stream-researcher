using System;
using System.Threading.Tasks;
using video_stream_researcher.Interfaces;
using VideoStreamFetcher.Parsers;

namespace video_stream_researcher.Services;

/// <summary>
/// 视频解析器包装器
/// 实现IVideoParser接口，包装VideoStreamFetcher.Parsers.VideoParser
/// </summary>
public class VideoParserWrapper : IVideoParser
{
    private readonly VideoParser _videoParser;
    private readonly IBilibiliLoginService _bilibiliLoginService;

    /// <summary>
    /// 初始化视频解析器包装器
    /// </summary>
    /// <param name="bilibiliLoginService">B站登录服务</param>
    public VideoParserWrapper(IBilibiliLoginService bilibiliLoginService)
    {
        _videoParser = new VideoParser();
        _bilibiliLoginService = bilibiliLoginService;
    }

    /// <summary>
    /// 解析视频信息
    /// </summary>
    /// <param name="url">视频URL</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>视频信息</returns>
    public async Task<object?> ParseVideoInfo(string url, Action<string> statusCallback)
    {
        try
        {
            statusCallback ??= _ => { };

            // 如果已登录B站，设置Cookie以获取高清视频
            if (_bilibiliLoginService.IsLoggedIn && (url.Contains("bilibili.com") || url.Contains("b23.tv")))
            {
                var cookieString = _bilibiliLoginService.GetCookieString();
                if (!string.IsNullOrEmpty(cookieString))
                {
                    statusCallback?.Invoke("检测到B站登录状态，正在使用登录凭证获取高清视频...");
                    _videoParser.SetCookieString(cookieString);
                }
            }

            // 调用带有默认CancellationToken的ParseVideoInfo方法
            return await _videoParser.ParseVideoInfo(url, statusCallback!, default);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"视频解析失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 设置Cookie字符串（用于B站登录）
    /// </summary>
    /// <param name="cookieString">Cookie字符串</param>
    public void SetCookieString(string cookieString)
    {
        _videoParser.SetCookieString(cookieString);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _videoParser.Dispose();
    }
}
