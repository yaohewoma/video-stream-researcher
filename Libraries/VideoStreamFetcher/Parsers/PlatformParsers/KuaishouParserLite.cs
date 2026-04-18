using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoStreamFetcher.Parsers.Utils;

namespace VideoStreamFetcher.Parsers.PlatformParsers;

/// <summary>
/// 快手直播回放解析器 - 轻量版
/// 纯HttpClient实现，无外部依赖，支持Android
/// 核心策略：模拟完整浏览器会话流程
/// </summary>
public class KuaishouParserLite : IPlatformParser
{
    private readonly HttpHelper _httpHelper;
    private readonly CookieContainer _cookieContainer;
    private HttpClient? _httpClient;
    private readonly Random _random = new();

    // 移动端User-Agent（更适合API访问）
    private const string MobileUserAgent = "Mozilla/5.0 (Linux; Android 10; SM-G981B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    public KuaishouParserLite(HttpHelper httpHelper)
    {
        _httpHelper = httpHelper;
        _cookieContainer = new CookieContainer();
        InitializeHttpClient();
    }

    private void InitializeHttpClient()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            UseCookies = true
        };

        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", MobileUserAgent);
    }

    public bool CanParse(string url)
    {
        return url.Contains("kuaishou.com") || url.Contains("kuaishou.cn");
    }

    public async Task<VideoInfo?> ParseAsync(string url, string? html, Action<string>? statusCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            statusCallback?.Invoke("[Lite] 开始解析快手直播回放...");

            // 步骤1: 建立会话（访问主页获取初始Cookie）
            statusCallback?.Invoke("[Lite] 步骤1: 建立会话...");
            await EstablishSessionAsync(statusCallback, cancellationToken);

            // 步骤2: 访问回放页面
            statusCallback?.Invoke("[Lite] 步骤2: 访问回放页面...");
            var pageHtml = await VisitPlaybackPageAsync(url, statusCallback, cancellationToken);
            if (string.IsNullOrEmpty(pageHtml))
            {
                statusCallback?.Invoke("[Lite] 无法获取页面内容");
                return null;
            }

            // 步骤3: 提取关键参数
            statusCallback?.Invoke("[Lite] 步骤3: 提取关键参数...");
            var (liveId, did, userId) = ExtractKeyParameters(pageHtml, url);
            statusCallback?.Invoke($"[Lite] LiveId: {liveId}, Did: {did?.Substring(0, Math.Min(did?.Length ?? 0, 20))}...");

            // 步骤4: 调用快手API获取视频信息
            statusCallback?.Invoke("[Lite] 步骤4: 调用快手API...");
            var videoInfo = await CallKuaishouApiAsync(liveId, did, userId, statusCallback, cancellationToken);
            if (videoInfo != null)
            {
                return videoInfo;
            }

            // 步骤5: 尝试从页面直接提取（备用方案）
            statusCallback?.Invoke("[Lite] 步骤5: 尝试备用提取...");
            videoInfo = TryExtractFromPage(pageHtml, statusCallback);
            if (videoInfo != null)
            {
                return videoInfo;
            }

            statusCallback?.Invoke("[Lite] 所有方法均失败");
            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"[Lite] 解析异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 步骤1: 建立会话
    /// </summary>
    private async Task EstablishSessionAsync(Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        try
        {
            // 访问快手主页获取初始Cookie
            var response = await _httpClient!.GetAsync("https://live.kuaishou.com/", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // 随机延迟，模拟真实用户
            await Task.Delay(_random.Next(500, 1500), cancellationToken);

            statusCallback?.Invoke($"[Lite] 会话建立完成，Cookie数量: {_cookieContainer.Count}");
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"[Lite] 会话建立警告: {ex.Message}");
        }
    }

    /// <summary>
    /// 步骤2: 访问回放页面
    /// </summary>
    private async Task<string?> VisitPlaybackPageAsync(string url, Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        try
        {
            // 添加必要的请求头
            _httpClient!.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", MobileUserAgent);
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://live.kuaishou.com/");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            var response = await _httpClient.GetAsync(url, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            statusCallback?.Invoke($"[Lite] 页面获取成功，长度: {html.Length}");

            // 随机延迟
            await Task.Delay(_random.Next(300, 800), cancellationToken);

            return html;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"[Lite] 页面获取失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 步骤3: 提取关键参数
    /// </summary>
    private (string? liveId, string? did, string? userId) ExtractKeyParameters(string html, string pageUrl)
    {
        string? liveId = null;
        string? did = null;
        string? userId = null;

        // 提取 liveId
        var liveIdMatch = Regex.Match(html, @"liveStreamId[""']?\s*[:=]\s*[""']([a-zA-Z0-9_-]+)[""']");
        if (liveIdMatch.Success)
        {
            liveId = liveIdMatch.Groups[1].Value;
        }
        else
        {
            var urlMatch = Regex.Match(html, @"playback/([a-zA-Z0-9_-]+)");
            if (urlMatch.Success)
            {
                liveId = urlMatch.Groups[1].Value;
            }
        }

        if (string.IsNullOrEmpty(liveId))
        {
            var urlMatch = Regex.Match(pageUrl, @"playback/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
            if (urlMatch.Success)
            {
                liveId = urlMatch.Groups[1].Value;
            }
        }

        // 提取 did (设备ID)
        var didMatch = Regex.Match(html, @"did[""']?\s*[:=]\s*[""']([a-zA-Z0-9_-]+)[""']");
        if (didMatch.Success)
        {
            did = didMatch.Groups[1].Value;
        }

        // 提取 userId
        var userIdMatch = Regex.Match(html, @"userId[""']?\s*[:=]\s*[""'](\d+)[""']");
        if (userIdMatch.Success)
        {
            userId = userIdMatch.Groups[1].Value;
        }

        return (liveId, did, userId);
    }

    /// <summary>
    /// 步骤4: 调用快手API
    /// </summary>
    private async Task<VideoInfo?> CallKuaishouApiAsync(string? liveId, string? did, string? userId, Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(liveId))
        {
            return null;
        }

        try
        {
            // 构建API请求
            var apiUrl = $"https://live.kuaishou.com/live_api/playback/detail";

            // 构建查询参数
            var queryParams = new Dictionary<string, string>
            {
                ["liveStreamId"] = liveId,
                ["caver"] = "2",
                ["productId"] = liveId
            };

            if (!string.IsNullOrEmpty(did))
            {
                queryParams["did"] = did;
            }

            // 构建完整URL
            var queryString = string.Join("&", queryParams.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));
            var fullUrl = $"{apiUrl}?{queryString}";

            statusCallback?.Invoke($"[Lite] 调用API: {apiUrl}");

            // 设置API请求头
            _httpClient!.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", MobileUserAgent);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            _httpClient.DefaultRequestHeaders.Add("Referer", $"https://live.kuaishou.com/playback/{liveId}");
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://live.kuaishou.com");
            _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

            var response = await _httpClient.GetAsync(fullUrl, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            statusCallback?.Invoke($"[Lite] API响应: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            // 解析API响应
            return ParseApiResponse(json, statusCallback);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"[Lite] API调用失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 解析API响应
    /// </summary>
    private VideoInfo? ParseApiResponse(string json, Action<string>? statusCallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 检查错误码
            if (root.TryGetProperty("result", out var resultProp))
            {
                var result = resultProp.GetInt32();
                if (result != 1)
                {
                    statusCallback?.Invoke($"[Lite] API返回错误: {result}");
                    return null;
                }
            }

            // 获取数据
            if (!root.TryGetProperty("data", out var dataProp))
            {
                statusCallback?.Invoke("[Lite] API响应中无data节点");
                return null;
            }

            // 提取视频信息
            var videoInfo = new VideoInfo();

            // 提取标题
            if (dataProp.TryGetProperty("caption", out var captionProp) && captionProp.ValueKind == JsonValueKind.String)
            {
                videoInfo.Title = captionProp.GetString() ?? "快手直播回放";
            }
            else
            {
                videoInfo.Title = "快手直播回放";
            }

            // 提取播放地址
            string? playUrl = null;

            // 尝试多个可能的字段
            var urlFields = new[] { "playUrl", "hlsPlayUrl", "flvPlayUrl", "mp4PlayUrl", "url" };
            foreach (var field in urlFields)
            {
                if (dataProp.TryGetProperty(field, out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                {
                    playUrl = urlProp.GetString();
                    if (!string.IsNullOrEmpty(playUrl))
                    {
                        statusCallback?.Invoke($"[Lite] 找到播放地址: {field}");
                        break;
                    }
                }
            }

            // 兼容 data.currentWork.playUrl 结构
            if (string.IsNullOrEmpty(playUrl) && dataProp.TryGetProperty("currentWork", out var currentWorkProp) && currentWorkProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in urlFields)
                {
                    if (currentWorkProp.TryGetProperty(field, out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                    {
                        playUrl = urlProp.GetString();
                        if (!string.IsNullOrEmpty(playUrl))
                        {
                            statusCallback?.Invoke($"[Lite] 从currentWork找到播放地址: {field}");
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(playUrl) && currentWorkProp.TryGetProperty("playUrls", out var cwPlayUrlsProp) && cwPlayUrlsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in cwPlayUrlsProp.EnumerateArray())
                    {
                        if (item.TryGetProperty("url", out var urlProp))
                        {
                            playUrl = urlProp.GetString();
                            if (!string.IsNullOrEmpty(playUrl))
                            {
                                statusCallback?.Invoke("[Lite] 从currentWork.playUrls数组找到地址");
                                break;
                            }
                        }
                    }
                }
            }

            // 尝试playUrls数组
            if (string.IsNullOrEmpty(playUrl) && dataProp.TryGetProperty("playUrls", out var playUrlsProp) && playUrlsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in playUrlsProp.EnumerateArray())
                {
                    if (item.TryGetProperty("url", out var urlProp))
                    {
                        playUrl = urlProp.GetString();
                        if (!string.IsNullOrEmpty(playUrl))
                        {
                            statusCallback?.Invoke("[Lite] 从playUrls数组找到地址");
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(playUrl))
            {
                statusCallback?.Invoke("[Lite] 未找到播放地址");
                return null;
            }

            statusCallback?.Invoke($"[Lite] 播放地址: {playUrl.Substring(0, Math.Min(playUrl.Length, 80))}...");

            // 判断格式
            string format = playUrl.Contains(".m3u8") ? "m3u8" : (playUrl.Contains(".mp4") ? "mp4" : "flv");

            videoInfo.CombinedStreams = new List<VideoStreamInfo>
            {
                new VideoStreamInfo
                {
                    Url = playUrl,
                    Size = 0,
                    Quality = "默认",
                    Format = format
                }
            };

            return videoInfo;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"[Lite] 解析API响应失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 步骤5: 从页面直接提取（备用）
    /// </summary>
    private VideoInfo? TryExtractFromPage(string html, Action<string>? statusCallback)
    {
        try
        {
            // 尝试提取window.__INITIAL_STATE__
            var match = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*(\{[\s\S]*?\});", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            var json = match.Groups[1].Value;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 查找playback数据
            if (!root.TryGetProperty("playback", out var playbackProp) || playbackProp.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            // 尝试提取视频URL（可能在publicData中）
            if (playbackProp.TryGetProperty("publicData", out var publicDataProp) && publicDataProp.ValueKind != JsonValueKind.Null)
            {
                // 这里可能包含视频信息
                statusCallback?.Invoke("[Lite] 找到publicData，尝试提取...");
            }

            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"[Lite] 页面提取失败: {ex.Message}");
            return null;
        }
    }
}
