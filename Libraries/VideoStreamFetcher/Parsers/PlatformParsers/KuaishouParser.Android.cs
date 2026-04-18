using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoStreamFetcher.Parsers.Utils;

namespace VideoStreamFetcher.Parsers.PlatformParsers;

/// <summary>
/// 快手直播回放解析器 - Android 专用版本
/// 使用 WebView 拦截或第三方 API 获取视频地址
/// </summary>
public class KuaishouParserAndroid : IPlatformParser
{
    private readonly HttpHelper _httpHelper;
    private const string Referer = "https://live.kuaishou.com/";

    public KuaishouParserAndroid(HttpHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    public bool CanParse(string url)
    {
        return url.Contains("kuaishou.com") || url.Contains("kuaishou.cn");
    }

    public async Task<VideoInfo?> ParseAsync(string url, string? html, Action<string>? statusCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            statusCallback?.Invoke("开始解析快手直播回放 (Android)...");

            // 提取直播ID
            string? liveId = ExtractLiveId(url);
            if (string.IsNullOrEmpty(liveId))
            {
                statusCallback?.Invoke("无法从URL中提取直播ID");
                return null;
            }

            statusCallback?.Invoke($"提取到直播ID: {liveId}");

            // 方案1: 尝试通过第三方解析服务
            var videoInfo = await TryParseByThirdPartyServiceAsync(liveId, statusCallback, cancellationToken);
            if (videoInfo != null)
            {
                return videoInfo;
            }

            // 方案2: 尝试直接获取页面并解析
            statusCallback?.Invoke("尝试下载页面内容...");
            html = await FetchPageContentAsync(url, statusCallback, cancellationToken);
            if (!string.IsNullOrEmpty(html))
            {
                videoInfo = TryParseFromHtml(html, statusCallback);
                if (videoInfo != null)
                {
                    return videoInfo;
                }
            }

            // 方案3: 尝试通过快手分享API
            videoInfo = await TryParseByShareApiAsync(liveId, statusCallback, cancellationToken);
            if (videoInfo != null)
            {
                return videoInfo;
            }

            statusCallback?.Invoke("Android平台解析失败，建议使用PC版本或第三方工具");
            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"解析快手直播回放异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 方案1: 使用第三方解析服务
    /// </summary>
    private async Task<VideoInfo?> TryParseByThirdPartyServiceAsync(string liveId, Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        try
        {
            statusCallback?.Invoke("尝试通过第三方解析服务...");

            // 这里可以集成第三方解析API
            // 例如：you-get、youtube-dl 等工具的在线服务
            // 或者使用其他开发者提供的快手解析API

            // 示例：尝试调用公开的解析服务（需要替换为实际可用的服务）
            var thirdPartyApis = new[]
            {
                $"https://api.example.com/kuaishou/parse?url=https://live.kuaishou.com/playback/{liveId}",
                $"https://kuaishou-parser.herokuapp.com/api/parse?id={liveId}"
            };

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 10; SM-G981B)");

            foreach (var apiUrl in thirdPartyApis)
            {
                try
                {
                    statusCallback?.Invoke($"尝试: {apiUrl}");
                    var response = await client.GetAsync(apiUrl, cancellationToken);
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(content))
                    {
                        // 解析第三方API返回的数据
                        var videoInfo = ParseThirdPartyResponse(content, statusCallback);
                        if (videoInfo != null)
                        {
                            statusCallback?.Invoke("✓ 第三方解析成功");
                            return videoInfo;
                        }
                    }
                }
                catch (Exception ex)
                {
                    statusCallback?.Invoke($"第三方API失败: {ex.Message}");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"第三方服务解析失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 方案3: 使用快手分享API
    /// 通过模拟分享链接获取视频信息
    /// </summary>
    private async Task<VideoInfo?> TryParseByShareApiAsync(string liveId, Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        try
        {
            statusCallback?.Invoke("尝试通过分享API获取...");

            // 快手的分享链接格式
            var shareUrl = $"https://v.kuaishou.com/share/live/{liveId}";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 10; SM-G981B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.162 Mobile Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");

            var response = await client.GetAsync(shareUrl, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!string.IsNullOrEmpty(html))
            {
                // 从分享页面提取视频信息
                var videoInfo = ExtractFromSharePage(html, statusCallback);
                if (videoInfo != null)
                {
                    return videoInfo;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"分享API解析失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从分享页面提取视频信息
    /// </summary>
    private VideoInfo? ExtractFromSharePage(string html, Action<string>? statusCallback)
    {
        try
        {
            statusCallback?.Invoke("从分享页面提取视频信息...");

            // 尝试提取视频URL（分享页面通常包含直接的视频链接）
            var videoUrlMatch = Regex.Match(html, @"https?://[^""'\s<>]+\.m3u8[^""'\s<>]*", RegexOptions.IgnoreCase);
            if (videoUrlMatch.Success)
            {
                var videoUrl = videoUrlMatch.Value;
                statusCallback?.Invoke($"找到视频URL: {videoUrl.Substring(0, Math.Min(videoUrl.Length, 80))}...");

                // 提取标题
                var titleMatch = Regex.Match(html, @"<title>([^<]+)</title>", RegexOptions.IgnoreCase);
                var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "快手直播回放";

                return new VideoInfo
                {
                    Title = title,
                    CombinedStreams = new List<VideoStreamInfo>
                    {
                        new VideoStreamInfo
                        {
                            Url = videoUrl,
                            Size = 0,
                            Quality = "默认",
                            Format = "m3u8"
                        }
                    }
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"提取分享页面信息失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 解析第三方API响应
    /// </summary>
    private VideoInfo? ParseThirdPartyResponse(string jsonContent, Action<string>? statusCallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // 检查状态
            if (root.TryGetProperty("code", out var codeProp))
            {
                var code = codeProp.GetInt32();
                if (code != 200 && code != 0)
                {
                    statusCallback?.Invoke($"第三方API返回错误码: {code}");
                    return null;
                }
            }

            // 获取数据
            if (!root.TryGetProperty("data", out var dataProp))
            {
                statusCallback?.Invoke("第三方API响应中未找到data节点");
                return null;
            }

            // 提取视频URL
            string? videoUrl = null;
            if (dataProp.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
            {
                videoUrl = urlProp.GetString();
            }
            else if (dataProp.TryGetProperty("playUrl", out var playUrlProp) && playUrlProp.ValueKind == JsonValueKind.String)
            {
                videoUrl = playUrlProp.GetString();
            }

            if (string.IsNullOrEmpty(videoUrl))
            {
                statusCallback?.Invoke("第三方API未返回视频URL");
                return null;
            }

            // 提取标题
            string title = "快手直播回放";
            if (dataProp.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
            {
                title = titleProp.GetString() ?? title;
            }

            statusCallback?.Invoke($"第三方API解析成功: {title}");

            string format = videoUrl.Contains(".m3u8") ? "m3u8" : (videoUrl.Contains(".mp4") ? "mp4" : "flv");

            return new VideoInfo
            {
                Title = title,
                CombinedStreams = new List<VideoStreamInfo>
                {
                    new VideoStreamInfo
                    {
                        Url = videoUrl,
                        Size = 0,
                        Quality = "默认",
                        Format = format
                    }
                }
            };
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"解析第三方API响应失败: {ex.Message}");
            return null;
        }
    }

    #region 通用方法（与桌面版相同）

    private string? ExtractLiveId(string url)
    {
        var playbackMatch = Regex.Match(url, @"playback/([a-zA-Z0-9_-]+)");
        if (playbackMatch.Success)
            return playbackMatch.Groups[1].Value;

        var liveMatch = Regex.Match(url, @"/live/([a-zA-Z0-9_-]+)");
        if (liveMatch.Success)
            return liveMatch.Groups[1].Value;

        return null;
    }

    private VideoInfo? TryParseFromHtml(string html, Action<string>? statusCallback)
    {
        try
        {
            statusCallback?.Invoke("尝试从HTML中提取视频信息...");

            var match = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*(\{[\s\S]*?\});\s*<\/script>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*(\{.*?\});", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            if (!match.Success)
            {
                statusCallback?.Invoke("未找到window.__INITIAL_STATE__数据");
                return null;
            }

            var jsonData = match.Groups[1].Value;
            statusCallback?.Invoke("找到window.__INITIAL_STATE__数据");

            using var doc = JsonDocument.Parse(jsonData);
            var root = doc.RootElement;

            var paths = new[] { "playback", "liveStream", "livePlayback" };
            foreach (var path in paths)
            {
                if (root.TryGetProperty(path, out var pathData) && pathData.ValueKind != JsonValueKind.Null)
                {
                    statusCallback?.Invoke($"在路径'{path}'中找到数据");
                    return ExtractVideoInfoFromJson(pathData, statusCallback);
                }
            }

            return ExtractVideoInfoFromJson(root, statusCallback);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"解析HTML失败: {ex.Message}");
            return null;
        }
    }

    private VideoInfo? ExtractVideoInfoFromJson(JsonElement data, Action<string>? statusCallback)
    {
        try
        {
            var videoInfo = new VideoInfo();

            var titleFields = new[] { "caption", "title", "liveStreamName" };
            foreach (var field in titleFields)
            {
                if (data.TryGetProperty(field, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var title = prop.GetString();
                    if (!string.IsNullOrEmpty(title))
                    {
                        videoInfo.Title = title;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(videoInfo.Title))
                videoInfo.Title = "快手直播回放";

            statusCallback?.Invoke($"直播标题: {videoInfo.Title}");

            string? playUrl = null;
            string quality = "默认";

            var urlFields = new[] { "playUrl", "hlsPlayUrl", "flvPlayUrl", "mp4PlayUrl" };
            foreach (var field in urlFields)
            {
                if (data.TryGetProperty(field, out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                {
                    playUrl = urlProp.GetString();
                    if (!string.IsNullOrEmpty(playUrl))
                    {
                        statusCallback?.Invoke($"找到播放地址字段: {field}");
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(playUrl))
            {
                statusCallback?.Invoke("未找到有效的播放地址");
                return null;
            }

            statusCallback?.Invoke($"播放地址: {playUrl.Substring(0, Math.Min(playUrl.Length, 80))}...");

            string format = playUrl.Contains(".m3u8") ? "m3u8" : (playUrl.Contains(".mp4") ? "mp4" : "flv");
            statusCallback?.Invoke($"检测到流格式: {format}");

            videoInfo.CombinedStreams = new List<VideoStreamInfo>
            {
                new VideoStreamInfo
                {
                    Url = playUrl,
                    Size = 0,
                    Quality = quality,
                    Format = format
                }
            };

            return videoInfo;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"提取视频信息失败: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> FetchPageContentAsync(string url, Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);

            // Android 使用移动端 User-Agent
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 10; SM-G981B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.162 Mobile Safari/537.36");
            client.DefaultRequestHeaders.Add("Referer", Referer);
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");

            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            statusCallback?.Invoke($"成功获取页面内容，长度: {content.Length}字符");

            return content;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"获取页面内容失败: {ex.Message}");
            return null;
        }
    }

    #endregion
}
