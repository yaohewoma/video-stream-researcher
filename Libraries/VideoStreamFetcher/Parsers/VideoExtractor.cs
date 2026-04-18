using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VideoStreamFetcher.Parsers;

/// <summary>
/// 视频提取器
/// 负责从各种数据源中提取视频信息
/// </summary>
public class VideoExtractor
{
    /// <summary>
    /// 解析视频数据JSON，提取视频和音频流信息
    /// </summary>
    /// <param name="videoData">视频数据JSON</param>
    /// <param name="title">视频标题</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>视频信息</returns>
    public VideoInfo? ParseVideoData(string videoData, string title, Action<string>? statusCallback)
    {
        try
        {
            var videoInfo = new VideoInfo { Title = title };
            
            // 检查是否为直接匹配到的视频URL（MP4链接）
            if (videoData.StartsWith("http"))
            {
                statusCallback?.Invoke("发现视频URL");
                videoInfo.CombinedStreams = new List<VideoStreamInfo>
                {
                    new VideoStreamInfo
                    {
                        Url = videoData,
                        Size = 0,
                        Quality = "默认",
                        Format = "mp4"
                    }
                };
                statusCallback?.Invoke($"找到视频流: {videoData}");
                return videoInfo;
            }
            
            dynamic? jsonData = JsonConvert.DeserializeObject(videoData);
            if (jsonData == null)
            {
                statusCallback?.Invoke("JSON解析失败");
                return null;
            }

            // 检查是否为通用视频数据格式
            if (jsonData?.url != null || jsonData?.src != null)
            {
                statusCallback?.Invoke("发现视频格式");
                
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
                    statusCallback?.Invoke($"找到视频流: {videoUrl}");
                    return videoInfo;
                }
            }
            
            // 检查是否有dash节点（分离的音视频流）
            if (jsonData?.data?.dash != null)
            {
                statusCallback?.Invoke("发现dash格式（分离的音视频流）");
                
                // 解析视频流
                if (jsonData.data.dash.video != null)
                {
                    var videoStreams = ((JArray)jsonData.data.dash.video).ToObject<List<dynamic>>();
                    if (videoStreams != null && videoStreams.Count > 0)
                    {
                        // 打印所有可用的视频流信息，便于调试
                        statusCallback?.Invoke($"找到 {videoStreams.Count} 个视频流，详细信息：");
                        foreach (var stream in videoStreams)
                        {
                            string qualityId = stream?.id?.ToString() ?? "未知";
                            string bandwidth = stream?.bandwidth?.ToString() ?? "未知";
                            string size = stream?.size?.ToString() ?? "未知";
                            string url = stream?.baseUrl?.ToString() ?? stream?.backupUrl?[0]?.ToString() ?? "未知";
                            string resolution = GetResolutionFromQualityId(qualityId);
                            statusCallback?.Invoke($"  - {resolution} (quality id: {qualityId}), 带宽: {bandwidth}, 大小: {size}, URL: {url.Substring(0, Math.Min(url.Length, 100))}...");
                        }
                        
                        // 定义分辨率优先级映射（值越大，优先级越高）
                        var qualityPriority = new Dictionary<string, int>
                        {
                            { "116", 10 }, // 1080P 60fps
                            { "112", 9 },  // 1080P 高码率
                            { "80", 8 },   // 1080P
                            { "74", 7 },   // 720P 60fps
                            { "72", 6 },   // 720P
                            { "64", 5 },   // 480P
                            { "32", 4 },   // 360P
                            { "16", 3 }    // 240P
                        };
                        
                        // 按照分辨率优先级排序，如果优先级相同则按照带宽排序
                        var sortedStreams = videoStreams
                            .OrderByDescending(v => 
                            {
                                string qualityId = v?.id?.ToString() ?? "";
                                return qualityPriority.ContainsKey(qualityId) ? qualityPriority[qualityId] : 0;
                            })
                            .ThenByDescending(v => v?.bandwidth ?? 0)
                            .ToList();
                            
                        // 打印排序后的视频流
                        statusCallback?.Invoke("视频流排序结果：");
                        for (int i = 0; i < sortedStreams.Count; i++)
                        {
                            var stream = sortedStreams[i];
                            string qualityId = stream?.id?.ToString() ?? "未知";
                            string resolution = GetResolutionFromQualityId(qualityId);
                            string bandwidth = stream?.bandwidth?.ToString() ?? "未知";
                            statusCallback?.Invoke($"  {i+1}. {resolution} (quality id: {qualityId}, 带宽: {bandwidth})");
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
                        string selectedResolution = GetResolutionFromQualityId(videoInfo.VideoStream.Quality);
                        statusCallback?.Invoke($"找到视频流: {videoInfo.VideoStream.Url}");
                        statusCallback?.Invoke($"选择的视频分辨率: {selectedResolution} (quality id: {videoInfo.VideoStream.Quality})");
                        
                        // 检查是否成功获取到视频流URL
                        if (string.IsNullOrEmpty(videoInfo.VideoStream.Url))
                        {
                            statusCallback?.Invoke("警告：视频流URL为空，尝试使用其他视频流");
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
                                selectedResolution = GetResolutionFromQualityId(videoInfo.VideoStream.Quality);
                                statusCallback?.Invoke($"使用第二个视频流: {videoInfo.VideoStream.Url}");
                                statusCallback?.Invoke($"选择的视频分辨率: {selectedResolution} (quality id: {videoInfo.VideoStream.Quality})");
                            }
                        }
                    }
                }

                // 解析音频流
                if (jsonData.data.dash.audio != null)
                {
                    var audioStreams = ((JArray)jsonData.data.dash.audio).ToObject<List<dynamic>>();
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
                        statusCallback?.Invoke($"找到音频流: {videoInfo.AudioStream.Url}");
                    }
                }
            }
            // 检查是否有durl节点（合并的音视频流）
            else if (jsonData?.data?.durl != null)
            {
                statusCallback?.Invoke("发现durl格式（合并的音视频流）");
                
                var combinedStreams = ((JArray)jsonData.data.durl).ToObject<List<dynamic>>();
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
                    statusCallback?.Invoke($"找到{videoInfo.CombinedStreams.Count}个合并流");
                }
            }
            else
            {
                statusCallback?.Invoke("未找到有效的视频流信息");
                return null;
            }

            return videoInfo;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"解析视频数据失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 清理JSON数据，移除无效字符
    /// </summary>
    /// <param name="jsonData">原始JSON数据</param>
    /// <returns>清理后的JSON数据</returns>
    public string CleanJsonData(string jsonData)
    {
        // 移除JSON前后的无效字符
        jsonData = jsonData.Trim();
        
        // 检查是否为直接匹配到的视频URL（MP4链接）
        if (jsonData.StartsWith("http"))
        {
            return jsonData;
        }
        
        // 确保JSON以{开头，以}结尾
        if (!jsonData.StartsWith('{'))
        {
            int startIndex = jsonData.IndexOf('{');
            if (startIndex >= 0)
                jsonData = jsonData[startIndex..];
        }
        
        if (!jsonData.EndsWith('}'))
        {
            int endIndex = jsonData.LastIndexOf('}');
            if (endIndex >= 0)
                jsonData = jsonData[..(endIndex + 1)];
        }
        
        return jsonData;
    }

    /// <summary>
    /// 验证JSON格式是否有效
    /// </summary>
    /// <param name="jsonData">JSON数据</param>
    /// <returns>是否为有效JSON</returns>
    public bool IsValidJson(string jsonData)
    {
        // 检查是否为直接匹配到的视频URL（MP4链接）
        if (jsonData.StartsWith("http"))
        {
            return true;
        }
        
        try
        {
            JsonConvert.DeserializeObject(jsonData);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 根据quality id获取对应的分辨率描述
    /// </summary>
    /// <param name="qualityId">quality id</param>
    /// <returns>分辨率描述</returns>
    public string GetResolutionFromQualityId(string qualityId)
    {
        var resolutionMap = new Dictionary<string, string>
        {
            { "116", "1080P 60fps" },
            { "112", "1080P 高码率" },
            { "80", "1080P" },
            { "74", "720P 60fps" },
            { "72", "720P" },
            { "64", "480P" },
            { "32", "360P" },
            { "16", "240P" }
        };
        
        return resolutionMap.ContainsKey(qualityId) ? resolutionMap[qualityId] : "未知分辨率";
    }

    /// <summary>
    /// 从URL中提取视频ID
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>视频ID</returns>
    public string ExtractVideoIdFromUrl(string url)
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
    /// 从HTML中提取视频ID
    /// </summary>
    /// <param name="html">HTML内容</param>
    /// <returns>视频ID</returns>
    public string ExtractVideoIdFromHtml(string html)
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
}