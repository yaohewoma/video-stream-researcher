using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VideoStreamFetcher.Parsers.Utils;
using VideoStreamFetcher.Localization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoStreamFetcher.Parsers.PlatformParsers;

/// <summary>
/// B 站视频解析器
/// 负责解析 B 站视频相关的逻辑
/// </summary>
public class BilibiliParser : IPlatformParser
{
    private readonly HttpHelper _httpHelper;

    /// <summary>
    /// 初始化 B 站视频解析器
    /// </summary>
    /// <param name="httpHelper">HTTP 请求辅助类</param>
    public BilibiliParser(HttpHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    /// <summary>
    /// 判断该解析器是否支持解析给定的 URL
    /// </summary>
    /// <param name="url">视频 URL</param>
    /// <returns>是否支持</returns>
    public bool CanParse(string url)
    {
        return url.Contains("bilibili.com") || url.Contains("b23.tv") || url.Contains("hdslb.com");
    }

    /// <summary>
    /// 解析视频信息
    /// </summary>
    /// <param name="url">视频 URL</param>
    /// <param name="html">页面 HTML 内容（如果已下载）</param>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>解析出的视频信息</returns>
    public async Task<VideoInfo?> ParseAsync(string url, string? html, Action<string>? statusCallback, CancellationToken cancellationToken = default)
    {
        // 如果 html 为空，尝试下载（虽然目前架构是 VideoParser 负责下载，但为了接口完整性，这里可以保留下载逻辑）
        if (string.IsNullOrEmpty(html))
        {
             // 这里假设 VideoParser 会传递 HTML，如果为空，可能需要抛出异常或尝试下载
             // 为了保持现有行为，我们假设调用者负责提供 HTML
             statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.NeedHtml"));
             return null;
        }

        string title = StringUtils.ExtractTitleFromHtml(html);
        var videoInfo = new VideoInfo { Title = title };

        string videoData = ExtractVideoDataFromHtml(html, statusCallback);
        if (string.IsNullOrEmpty(videoData))
        {
            statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.NoVideoData"));
            return null;
        }

        // 清理视频数据，确保只包含纯 JSON
        videoData = JsonHelper.CleanJsonData(videoData);

        if (!JsonHelper.IsValidJson(videoData))
        {
             statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.InvalidJson"));
             return null;
        }

        bool success = ParseBilibiliVideoData(videoData, videoInfo, statusCallback);
        return success ? videoInfo : null;
    }

    /// <summary>
    /// 从页面中提取 B 站视频数据
    /// </summary>
    /// <param name="html">HTML 内容</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的视频数据</returns>
    private string ExtractVideoDataFromHtml(string html, Action<string>? statusCallback)
    {
        // 使用多种正则表达式模式提取视频数据
        statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.ExtractingVideoData"));
        string videoData = string.Empty;

        // 尝试多种视频数据提取模式
        var patterns = new List<string>
        {
            // 完整的 window.__playinfo__ = {...};
            @"window\.__playinfo__\s*=\s*({[\s\S]*?});\s*(?:var|let|const|</script>|$)",
            // 无分号版本
            @"window\.__playinfo__\s*=\s*({[\s\S]*?})\s*</script>",
            // 简化版本，不考虑后续内容
            @"window\.__playinfo__\s*=\s*({[\s\S]*?})(?:$|\n|\r)",
            // 匹配可能被压缩的版本
            @"playinfo\s*:\s*({[\s\S]*?})(?:,|\}|\s*;)",
            // 匹配包含 playinfo 的大括号对
            @"({[\s\S]*?playinfo[\s\S]*?})",
            // 匹配页面中所有可能的 JSON 数据块
            @"({""code"":0[\s\S]*?})",
            // 匹配 window.__playinfo__ = {...}，不考虑分号
            @"window\.__playinfo__\s*=\s*({[\s\S]*?})"
        };

        // 遍历所有模式，尝试提取视频数据
        foreach (var pattern in patterns)
        {
            var playInfoMatch = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (playInfoMatch.Success)
            {
                videoData = playInfoMatch.Groups[1].Value;
                statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.PatternMatched", pattern.Substring(0, Math.Min(pattern.Length, 50))));

                // 验证 JSON 格式
                if (JsonHelper.IsValidJson(videoData))
                {
                    break;
                }
                else
                {
                    statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.InvalidJsonContinue"));
                    videoData = string.Empty;
                }
            }
        }

        return videoData;
    }

    /// <summary>
    /// 解析 B 站视频数据，提取视频和音频流信息
    /// </summary>
    /// <param name="videoData">视频数据 JSON</param>
    /// <param name="videoInfo">视频信息对象</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>是否解析成功</returns>
    private bool ParseBilibiliVideoData(string videoData, VideoInfo videoInfo, Action<string>? statusCallback)
    {
        try
        {
            dynamic? jsonData = JsonHelper.DeserializeObject(videoData);
            if (jsonData == null)
            {
                statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.JsonParseFailed"));
                return false;
            }

            // 检查是否有 dash 节点（分离的音视频流）
            if (jsonData?.data?.dash != null)
            {
                statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.DashFormat"));

                // 解析视频流
                if (jsonData.data.dash.video != null)
                {
                    var videoStreams = ((Newtonsoft.Json.Linq.JArray)jsonData.data.dash.video).ToObject<List<dynamic>>();
                    if (videoStreams != null && videoStreams.Count > 0)
                    {
                        // 打印所有可用的视频流信息，便于调试
                        statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.VideoStreamsFound", videoStreams.Count));
                        foreach (var stream in videoStreams)
                        {
                            string qualityId = stream?.id?.ToString() ?? "unknown";
                            string bandwidth = stream?.bandwidth?.ToString() ?? "unknown";
                            string size = stream?.size?.ToString() ?? "unknown";
                            string url = stream?.baseUrl?.ToString() ?? stream?.backupUrl?[0]?.ToString() ?? "unknown";
                            string resolution = ResolutionUtils.GetResolutionFromQualityId(qualityId);
                            statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.StreamInfo", resolution, qualityId, bandwidth, size, url.Substring(0, Math.Min(url.Length, 100))));
                        }

                        // 获取分辨率优先级映射
                        var qualityPriority = ResolutionUtils.GetQualityPriorityMap();

                        // 按照分辨率优先级排序，如果优先级相同则按照带宽排序
                        var sortedStreams = videoStreams
                            .OrderByDescending(v =>
                            {
                                string qualityId = v?.id?.ToString() ?? "";
                                return qualityPriority.TryGetValue(qualityId, out int priority) ? priority : 0;
                            })
                            .ThenByDescending(v => v?.bandwidth ?? 0)
                            .ToList();

                        // 打印排序后的视频流
                        statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.SortedStreams"));
                        for (int i = 0; i < sortedStreams.Count; i++)
                        {
                            var stream = sortedStreams[i];
                            string qualityId = stream?.id?.ToString() ?? "unknown";
                            string resolution = ResolutionUtils.GetResolutionFromQualityId(qualityId);
                            string bandwidth = stream?.bandwidth?.ToString() ?? "unknown";
                            statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.StreamEntry", i + 1, resolution, qualityId, bandwidth));
                        }

                        // 选择排序后的第一个视频流（优先级最高的）
                        var bestVideo = sortedStreams.First();

                        videoInfo.VideoStream = new VideoStreamInfo
                        {
                            Url = bestVideo?.baseUrl?.ToString() ?? bestVideo?.backupUrl?[0]?.ToString() ?? "",
                            Size = bestVideo?.size?.ToObject<long?>() ?? 0,
                            Quality = bestVideo?.id?.ToString() ?? "",
                            Format = "mp4"
                        };

                        // 添加分辨率信息到日志
                        string selectedResolution = ResolutionUtils.GetResolutionFromQualityId(videoInfo.VideoStream.Quality);
                        statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.VideoStreamUrl", videoInfo.VideoStream.Url));
                        statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.SelectedResolution", selectedResolution, videoInfo.VideoStream.Quality));

                        // 检查是否成功获取到视频流 URL
                        if (string.IsNullOrEmpty(videoInfo.VideoStream.Url))
                        {
                            statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.EmptyVideoUrl"));
                            // 尝试使用第二个视频流
                            if (sortedStreams.Count > 1)
                            {
                                var secondBestVideo = sortedStreams[1];
                                videoInfo.VideoStream = new VideoStreamInfo
                                {
                                    Url = secondBestVideo?.baseUrl?.ToString() ?? secondBestVideo?.backupUrl?[0]?.ToString() ?? "",
                                    Size = secondBestVideo?.size?.ToObject<long?>() ?? 0,
                                    Quality = secondBestVideo?.id?.ToString() ?? "",
                                    Format = "mp4"
                                };
                                selectedResolution = ResolutionUtils.GetResolutionFromQualityId(videoInfo.VideoStream.Quality);
                                statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.SecondVideoStream", videoInfo.VideoStream.Url));
                                statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.SecondResolution", selectedResolution, videoInfo.VideoStream.Quality));
                            }
                        }
                    }
                }

                // 解析音频流
                if (jsonData.data.dash.audio != null)
                {
                    var audioStreams = ((Newtonsoft.Json.Linq.JArray)jsonData.data.dash.audio).ToObject<List<dynamic>>();
                    if (audioStreams != null && audioStreams.Count > 0)
                    {
                        var bestAudio = audioStreams.OrderByDescending(a => a?.bandwidth ?? 0).First();
                        videoInfo.AudioStream = new VideoStreamInfo
                        {
                            Url = bestAudio?.baseUrl?.ToString() ?? bestAudio?.backupUrl?[0]?.ToString() ?? "",
                            Size = bestAudio?.size?.ToObject<long?>() ?? 0,
                            Quality = bestAudio?.id?.ToString() ?? "",
                            Format = "mp3"
                        };
                        statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.AudioStreamUrl", videoInfo.AudioStream.Url));
                    }
                }
            }
            // 检查是否有 durl 节点（合并的音视频流）
            else if (jsonData?.data?.durl != null)
            {
                statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.DurlFormat"));

                // 解析合并流逻辑...
                var combinedStreams = ((Newtonsoft.Json.Linq.JArray)jsonData.data.durl).ToObject<List<dynamic>>();
                if (combinedStreams != null && combinedStreams.Count > 0)
                {
                    videoInfo.CombinedStreams = new List<VideoStreamInfo>();
                    foreach (var stream in combinedStreams)
                    {
                        videoInfo.CombinedStreams.Add(new VideoStreamInfo
                        {
                            Url = stream?.url?.ToString() ?? "",
                            Size = stream?.size?.ToObject<long?>() ?? 0,
                            Quality = "",
                            Format = "flv"
                        });
                    }
                    statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.CombinedStreams", videoInfo.CombinedStreams.Count));
                }
            }
            else
            {
                statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.NoStreamInfo"));
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(FetcherLocalization.GetString("Bilibili.ParseFailed", ex.Message));
            return false;
        }
    }
}
