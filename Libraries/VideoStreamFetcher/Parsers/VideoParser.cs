using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoStreamFetcher.Parsers.PlatformParsers;
using VideoStreamFetcher.Parsers.Utils;
using VideoStreamFetcher.Localization;
using System.Linq;

namespace VideoStreamFetcher.Parsers;

/// <summary>
/// 视频信息解析器
/// 负责解析视频 URL，提取视频和音频流信息，仅用于技术研究和学习目的
/// </summary>
public class VideoParser : IDisposable
{
    private readonly HttpHelper _httpHelper;
    private readonly VideoParserFactory _parserFactory;
    private readonly bool _ownsHttpHelper;
    
    /// <summary>
    /// 初始化视频解析器
    /// </summary>
    public VideoParser()
    {
        _httpHelper = new HttpHelper();
        _ownsHttpHelper = true;
        
        // 初始化平台解析器
        var parsers = new List<IPlatformParser>
        {
            new BilibiliParser(_httpHelper),
            new MiyousheParser(_httpHelper),
            new KuaishouParserLite(_httpHelper)
        };
        _parserFactory = new VideoParserFactory(parsers);
    }

    /// <summary>
    /// 初始化视频解析器（自定义平台解析器集合）
    /// </summary>
    /// <param name="httpHelper">共享的 HttpHelper（用于 Cookie 等）</param>
    /// <param name="parsers">平台解析器集合</param>
    public VideoParser(HttpHelper httpHelper, IEnumerable<IPlatformParser> parsers)
    {
        _httpHelper = httpHelper ?? throw new ArgumentNullException(nameof(httpHelper));
        _ownsHttpHelper = false;
        _parserFactory = new VideoParserFactory(parsers ?? throw new ArgumentNullException(nameof(parsers)));
    }

    /// <summary>
    /// 设置Cookie字符串（用于B站登录获取高清视频）
    /// </summary>
    /// <param name="cookieString">Cookie字符串</param>
    public void SetCookieString(string cookieString)
    {
        _httpHelper.SetCookieString(cookieString);
    }
    
    /// <summary>
    /// 解析视频 URL，获取视频信息
    /// </summary>
    /// <param name="url">视频 URL</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>视频信息</returns>
    public async Task<VideoInfo?> ParseVideoInfo(string url, Action<string> statusCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.RequestingUrl", url));

            // 处理 B 站短链接
            if (url.Contains("b23.tv"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.BilibiliShortLink"));
                url = await UrlHelper.ResolveBilibiliShortUrl(url, _httpHelper, statusCallback);
                statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.ResolvedUrl", url));
            }

            // 尝试获取特定的解析器
            var parser = _parserFactory.GetParser(url);
            if (parser != null)
            {
                statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.UsingParser", parser.GetType().Name));
            }

            // 如果是米游社解析器，不需要预先下载 HTML，直接调用解析器
            if (parser is MiyousheParser)
            {
                return await parser.ParseAsync(url, null, statusCallback, cancellationToken);
            }

            // 对于其他解析器（如 Bilibili）或通用解析，我们需要先下载 HTML
            cancellationToken.ThrowIfCancellationRequested();

            DateTime requestStartTime = DateTime.Now;
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.RequestStartTime", requestStartTime.ToString("yyyy-MM-dd HH:mm:ss.fff")));

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.RequestMethod", request.Method.ToString()));
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.RequestUrl", request.RequestUri?.ToString() ?? url));

            var response = await _httpHelper.SendAsync(request);

            cancellationToken.ThrowIfCancellationRequested();

            DateTime responseReceivedTime = DateTime.Now;
            TimeSpan requestDuration = responseReceivedTime - requestStartTime;
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.ResponseReceivedTime", responseReceivedTime.ToString("yyyy-MM-dd HH:mm:ss.fff")));
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.RequestDuration", requestDuration.TotalMilliseconds));
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.ResponseStatusCode", (int)response.StatusCode));

            response.EnsureSuccessStatusCode();

            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.ReadingResponse"));
            string html = string.Empty;
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                html = await reader.ReadToEndAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.ContentLength", html.Length));

            if (StringUtils.IsGarbageText(html))
            {
                statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.GarbageText"));
                return null;
            }

#if DEBUG
            string tempFile = Path.Combine(Path.GetTempPath(), "video_page.html");
            await File.WriteAllTextAsync(tempFile, html, System.Text.Encoding.UTF8);
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.ContentSaved", tempFile));
#endif

            // 如果有特定的解析器（非米游社，因为米游社已经在上面处理了），使用它
            if (parser != null)
            {
                return await parser.ParseAsync(url, html, statusCallback, cancellationToken);
            }

            // 如果没有特定解析器，使用通用逻辑
            return ParseWithGeneralLogic(url, html, statusCallback);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.Failed", ex.Message));
            return null;
        }
    }

    /// <summary>
    /// 使用通用逻辑解析视频
    /// </summary>
    private VideoInfo? ParseWithGeneralLogic(string url, string html, Action<string>? statusCallback)
    {
        statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.GeneralExtraction"));

        // 提取标题
        string title = StringUtils.ExtractTitleFromHtml(html);
        statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.TitleExtracted", title));

        // 使用多种正则表达式模式提取视频数据
        var patterns = new List<string>
        {
            // 模式 1: 匹配视频播放器配置
            @"videoPlayer\s*=\s*({[\s\S]*?});",
            // 模式 2: 匹配视频 URL
            @"src\s*=\s*[""'](https?://[^""']*?\.mp4)[""']",
            // 模式 3: 匹配视频数据
            @"video\s*:\s*({[\s\S]*?})",
            // 模式 4: 匹配视频信息
            @"videoInfo\s*=\s*({[\s\S]*?});",
            // 模式 5: 匹配视频配置
            @"video\s*=\s*({[\s\S]*?})",
            // 模式 6: 匹配播放器配置
            @"player\s*=\s*({[\s\S]*?})",
            // 模式 7: 匹配视频源
            @"source\s*=\s*[""'](https?://[^""']*?)[""']",
            // 模式 8: 匹配媒体配置
            @"media\s*=\s*({[\s\S]*?})",
            // 模式 9: 匹配视频数据
            @"videos\s*=\s*({[\s\S]*?})",
            // 模式 10: 匹配视频列表
            @"videoList\s*=\s*({[\s\S]*?})",
            // 模式 11: 匹配视频信息
            @"video_data\s*=\s*({[\s\S]*?})",
            // 模式 12: 匹配视频 URL
            @"videoUrl\s*=\s*[""'](https?://[^""']*?)[""']",
            // 模式 13: 匹配动态加载的视频数据
            @"data\s*=\s*({[\s\S]*?})",
            // 模式 14: 匹配 JSON 数据
            @"({[\s\S]*?})",
            // 模式 15: 匹配视频链接
            @"https?://[^""'\s]*?video[^""'\s]*?\.mp4",
            // 模式 16: 匹配视频 API
            @"https?://[^""'\s]*?api[^""'\s]*?video[^""'\s]*",
            // 模式 17: 匹配视频资源
            @"https?://[^""'\s]*?resource[^""'\s]*?video[^""'\s]*",
            // 模式 18: 匹配播放按钮触发的 API
            @"https?://[^""'\s]*?play[^""'\s]*",
            // 模式 19: 匹配视频 ID
            @"videoId\s*=\s*[""'](\d+)[""']",
            // 模式 20: 匹配视频播放 API
            @"https?://[^""'\s]*?api[^""'\s]*?play[^""'\s]*",
            // 模式 21: 匹配视频详情 API
            @"https?://[^""'\s]*?api[^""'\s]*?detail[^""'\s]*"
        };
        
        string videoData = string.Empty;
        
        // 遍历所有模式，尝试提取视频数据
        foreach (var pattern in patterns)
        {
            var playInfoMatch = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (playInfoMatch.Success)
            {
                // 检查是否为直接匹配到的视频 URL
                if (pattern.Contains(@"https?://") && pattern.Contains(@"\.mp4"))
                {
                    videoData = playInfoMatch.Value;
                    statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.PatternMatchedUrl", pattern.Substring(0, Math.Min(pattern.Length, 50))));
                    break;
                }
                else
                {
                    videoData = playInfoMatch.Groups[1].Value;
                    statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.PatternMatched", pattern.Substring(0, Math.Min(pattern.Length, 50))));

                    // 验证 JSON 格式
                    if (JsonHelper.IsValidJson(videoData))
                    {
                        break;
                    }
                    else
                    {
                        statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.InvalidJson"));
                        videoData = string.Empty;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(videoData))
        {
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.NoVideoData"));
            return null;
        }
        
        // 清理视频数据，确保只包含纯 JSON
        videoData = JsonHelper.CleanJsonData(videoData);

        // 验证 JSON 格式
        if (!JsonHelper.IsValidJson(videoData))
        {
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.InvalidJsonFormat"));
            return null;
        }

        return ParseGeneralVideoData(videoData, title, statusCallback);
    }

    /// <summary>
    /// 解析通用视频数据 JSON
    /// </summary>
    private VideoInfo? ParseGeneralVideoData(string videoData, string title, Action<string>? statusCallback)
    {
        try
        {
            var videoInfo = new VideoInfo { Title = title };

            // 检查是否为直接匹配到的视频 URL（MP4 链接）
            if (videoData.StartsWith("http"))
            {
                statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.VideoUrlFound"));
                videoInfo.CombinedStreams = new List<VideoStreamInfo>
                {
                    new VideoStreamInfo
                    {
                        Url = videoData,
                        Size = 0,
                        Quality = "default",
                        Format = "mp4"
                    }
                };
                return videoInfo;
            }
            
            dynamic? jsonData = JsonHelper.DeserializeObject(videoData);
            if (jsonData == null) return null;

            // 检查是否为通用视频数据格式
            if (jsonData?.url != null || jsonData?.src != null)
            {
                string videoUrl = jsonData?.url?.ToString() ?? jsonData?.src?.ToString() ?? "";
                if (!string.IsNullOrEmpty(videoUrl))
                {
                    videoInfo.CombinedStreams = new List<VideoStreamInfo>
                    {
                        new VideoStreamInfo
                        {
                            Url = videoUrl,
                            Size = jsonData?.size?.ToObject<long?>() ?? 0,
                            Quality = jsonData?.quality?.ToString() ?? "默认",
                            Format = "mp4"
                        }
                    };
                    return videoInfo;
                }
            }
            
            // 这里可以继续添加更多的通用解析逻辑，如 dash/durl 等，如果需要支持更多通用格式

            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.NoStreamInfo"));
            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(FetcherLocalization.GetString("Parsing.ParseFailed", ex.Message));
            return null;
        }
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _ownsHttpHelper)
        {
            _httpHelper.Dispose();
        }
    }
}
