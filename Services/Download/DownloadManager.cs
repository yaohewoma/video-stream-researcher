using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Mp4Merger.Core.Services;
using VideoStreamFetcher.Parsers;
using video_stream_researcher.Interfaces;

namespace video_stream_researcher.Services;

/// <summary>
/// 视频流处理管理器
/// 负责视频流获取和处理，仅用于技术研究和学习目的
/// </summary>
public class DownloadManager : IDownloadManager, IDisposable
{
    private HttpClient _httpClient;
    private CancellationTokenSource? _cts; // 用于取消下载的令牌源

    /// <summary>
    /// 初始化下载管理器
    /// </summary>
    public DownloadManager()
    {
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseCookies = true,
            AllowAutoRedirect = true
        });

        // 设置超时时间为10分钟
        _httpClient.Timeout = TimeSpan.FromMinutes(10);

        // 设置浏览器模拟头信息
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.8,zh-TW;q=0.7,zh-HK;q=0.5,en-US;q=0.3,en;q=0.2");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "identity"); // 禁用压缩，确保直接获取原始数据
    }

    /// <summary>
        /// 取消当前下载
        /// </summary>
        public void CancelDownload()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }
        
        /// <summary>
        /// 重置管理器状态，释放内部资源但保持实例可用
        /// 用于下载完成后释放内存
        /// </summary>
        public void Reset()
        {
            // 释放取消令牌
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            
            // 释放HttpClient
            _httpClient.Dispose();
            
            // 重新初始化HttpClient
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                UseCookies = true,
                AllowAutoRedirect = true
            });
            
            // 重新设置超时和头信息
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.8,zh-TW;q=0.7,zh-HK;q=0.5,en-US;q=0.3,en;q=0.2");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "identity");
            
            // 移除强制GC调用，避免干扰.NET运行时的垃圾回收策略
        }

    /// <summary>
    /// 检查文件是否已存在
    /// </summary>
    /// <param name="videoInfo">视频信息</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <param name="audioOnly">是否仅下载音频</param>
    /// <param name="videoOnly">是否仅下载视频</param>
    /// <param name="noMerge">是否不合并音视频流</param>
    /// <returns>文件是否已存在</returns>
    public bool CheckFileExists(object videoInfo, string savePath, Action<string> statusCallback, bool audioOnly = false, bool videoOnly = false, bool noMerge = false, string? sourceUrl = null, string? downloadVariant = null)
    {
        if (videoInfo == null || string.IsNullOrEmpty(savePath))
        {
            return false;
        }

        // 转换videoInfo为VideoInfo类型
        if (!(videoInfo is VideoInfo))
        {
            statusCallback?.Invoke("视频信息类型错误");
            return false;
        }

        var typedVideoInfo = (VideoInfo)videoInfo;

        // 清理文件名中的非法字符
        string safeTitle = CleanFileName(typedVideoInfo.Title);

        // 添加调试日志，确认检查逻辑被执行
        statusCallback?.Invoke($"🔍 开始检查文件是否已存在，模式：{ (audioOnly ? "仅音频" : (videoOnly ? "仅视频" : "完整视频")) }");
        statusCallback?.Invoke($"📁 保存路径：{savePath}");
        statusCallback?.Invoke($"📄 原始标题：{typedVideoInfo.Title}");
        statusCallback?.Invoke($"📄 安全标题：{safeTitle}");
        statusCallback?.Invoke($"📄 标题长度：{safeTitle.Length}");

        // 检查文件是否已存在
        bool fileExists = false;

        // 添加更详细的调试信息
        statusCallback?.Invoke($"📋 开始文件存在性检查 - 详细信息：");
        statusCallback?.Invoke($"   - 音频模式: {audioOnly}");
        statusCallback?.Invoke($"   - 视频模式: {videoOnly}");
        statusCallback?.Invoke($"   - 不合并模式: {noMerge}");
        statusCallback?.Invoke($"   - 保存路径存在: {Directory.Exists(savePath)}");

        if (audioOnly)
        {
            // 检查音频文件是否已存在
            string audioPath = Path.Combine(savePath, $"{safeTitle}.mp3");
            statusCallback?.Invoke($"🎵 检查音频文件：{audioPath}");
            if (File.Exists(audioPath))
            {
                fileExists = true;
                statusCallback?.Invoke($"   - ✅ 音频文件已存在");
            }
        }
        // 处理仅视频下载
        else if (videoOnly)
        {
            // 检查视频文件是否已存在
            string videoPath = Path.Combine(savePath, $"{safeTitle}.mp4");
            statusCallback?.Invoke($"🎬 检查仅视频文件：{videoPath}");
            if (File.Exists(videoPath))
            {
                fileExists = true;
                statusCallback?.Invoke($"   - ✅ 视频文件已存在");
            }
        }
        // 处理完整视频下载（默认）
        else
        {
            // 检查直接保存的视频文件是否已存在
            string videoPath = Path.Combine(savePath, $"{safeTitle}.mp4");
            statusCallback?.Invoke($"🎬 检查完整视频文件：{videoPath}");
            if (File.Exists(videoPath))
            {
                fileExists = true;
                statusCallback?.Invoke($"   - ✅ 完整视频文件已存在");
            }
            
            // 检查不合并模式下的临时文件
            if (!fileExists && noMerge)
            {
                string tempVideoPath = Path.Combine(savePath, $"{safeTitle}_video_temp.mp4");
                string tempAudioPath = Path.Combine(savePath, $"{safeTitle}_audio_temp.mp3");

                if (File.Exists(tempVideoPath) || File.Exists(tempAudioPath))
                {
                    fileExists = true;
                    statusCallback?.Invoke($"   - ✅ 临时文件已存在");
                }
            }
        }

        // 统一显示提示信息
        if (fileExists)
        {
            statusCallback?.Invoke("✨ 你已经下载过了喵，再次点击继续执行");
        }
        else
        {
            statusCallback?.Invoke($"🆕 { (audioOnly ? "音频" : "视频") }文件不存在，可以下载");
        }

        return fileExists;
    }

    /// <summary>
    /// 下载视频
    /// </summary>
    /// <param name="videoInfo">视频信息</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="progressCallback">进度回调函数</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <param name="speedCallback">网速回调函数（字节/秒）</param>
    /// <param name="audioOnly">是否仅下载音频</param>
    /// <param name="videoOnly">是否仅下载视频</param>
    /// <param name="noMerge">是否不合并音视频流</param>
    /// <param name="isFFmpegEnabled">是否启用FFmpeg</param>
    /// <param name="mergeMode">合并模式</param>
    /// <returns>实际文件大小（字节）</returns>
    public async Task<long> DownloadVideo(object videoInfo, string savePath, Action<double> progressCallback, Action<string> statusCallback, Action<long> speedCallback, bool audioOnly = false, bool videoOnly = false, bool noMerge = false, bool isFFmpegEnabled = false, int mergeMode = 1, string? sourceUrl = null, string? downloadVariant = null, bool keepOriginalFiles = true)
    {
        if (videoInfo == null)
        {
            throw new ArgumentNullException(nameof(videoInfo));
        }

        if (string.IsNullOrEmpty(savePath))
        {
            throw new ArgumentNullException(nameof(savePath));
        }

        // 转换videoInfo为VideoInfo类型
        if (!(videoInfo is VideoInfo))
        {
            throw new ArgumentException("视频信息类型错误", nameof(videoInfo));
        }

        var typedVideoInfo = (VideoInfo)videoInfo;

        // 初始化取消令牌
        _cts = new CancellationTokenSource();
        CancellationToken cancellationToken = _cts.Token;

        try
        {
            // 确保保存路径存在
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            // 清理文件名中的非法字符
            string safeTitle = CleanFileName(typedVideoInfo.Title);
            string finalVideoPath = Path.Combine(savePath, $"{safeTitle}.mp4");
            
            // 初始化实际文件大小
            long actualFileSize = 0;

            // 处理仅音频下载
            if (audioOnly)
            {
                statusCallback?.Invoke("开始下载仅音频模式...");
                
                // 检查是否有音频流
                if (typedVideoInfo.AudioStream != null)
                {
                    // 直接保存音频流为MP3文件
                    string audioPath = Path.Combine(savePath, $"{safeTitle}.mp3");
                    await DownloadStream(typedVideoInfo.AudioStream, audioPath, progressCallback, statusCallback, speedCallback, "音频流", cancellationToken);
                    statusCallback?.Invoke($"音频下载完成：{audioPath}");
                }
                // 如果没有单独的音频流，检查是否有合并流
                else if (typedVideoInfo.CombinedStreams != null && typedVideoInfo.CombinedStreams.Count > 0)
                {
                    statusCallback?.Invoke("未找到单独的音频流，将从合并流中提取音频...");
                    
                    // 下载合并流
                    string tempVideoPath = Path.Combine(savePath, $"{safeTitle}_temp.mp4");
                    var stream = typedVideoInfo.CombinedStreams[0];
                    await DownloadStream(stream, tempVideoPath, progressCallback, statusCallback, speedCallback, "视频流", cancellationToken);
                    
                    // 音频提取功能暂时仅支持通过Mp4Merger库处理
                    statusCallback?.Invoke("ℹ️ 当前版本仅支持通过Mp4Merger库处理音视频，音频提取功能需使用完整的Mp4Merger功能");
                    statusCallback?.Invoke($"视频文件：{tempVideoPath}");
                }
                else
                {
                    throw new Exception("未找到可用的音频流信息");
                }
            }
            // 处理仅视频下载
            else if (videoOnly)
            {
                statusCallback?.Invoke("开始下载仅视频模式...");
                
                // 处理dash格式（分离的视频流）
                if (typedVideoInfo.VideoStream != null)
                {
                    statusCallback?.Invoke("开始下载分离的视频流...");
                    
                    // 直接保存视频流
                    await DownloadStream(typedVideoInfo.VideoStream, finalVideoPath, progressCallback, statusCallback, speedCallback, "视频流", cancellationToken);
                    statusCallback?.Invoke($"视频下载完成：{finalVideoPath}");
                }
                // 处理durl格式（合并的视频流）
                else if (typedVideoInfo.CombinedStreams != null && typedVideoInfo.CombinedStreams.Count > 0)
                {
                    statusCallback?.Invoke("开始下载合并的视频流...");
                    
                    // 下载第一个合并流（通常只有一个）
                    var stream = typedVideoInfo.CombinedStreams[0];
                    await DownloadStream(stream, finalVideoPath, progressCallback, statusCallback, speedCallback, "视频流", cancellationToken);
                    
                    statusCallback?.Invoke($"视频下载完成：{finalVideoPath}");
                }
                else
                {
                    throw new Exception("未找到有效的视频流信息");
                }
            }
            // 处理完整视频下载（默认）
        else
        {
            // 直接保存到指定路径
            statusCallback?.Invoke("开始下载完整视频模式...");
            
            // 处理dash格式（分离的音视频流）
            if (typedVideoInfo.VideoStream != null && typedVideoInfo.AudioStream != null)
            {
                statusCallback?.Invoke("开始下载分离的音视频流...");
                
                // 下载视频流
                string tempVideoPath = Path.Combine(savePath, $"{safeTitle}_video_temp.mp4");
                await DownloadStream(typedVideoInfo.VideoStream, tempVideoPath, progressCallback, statusCallback, speedCallback, "视频流", cancellationToken);
                
                // 下载音频流
                string tempAudioPath = Path.Combine(savePath, $"{safeTitle}_audio_temp.mp3");
                await DownloadStream(typedVideoInfo.AudioStream, tempAudioPath, progressCallback, statusCallback, speedCallback, "音频流", cancellationToken);
                
                // 如果noMerge为true，不合并音视频流，直接返回
                if (noMerge)
                {
                    statusCallback?.Invoke("不合并模式，保留原始音视频文件");
                    statusCallback?.Invoke($"视频文件：{tempVideoPath}");
                    statusCallback?.Invoke($"音频文件：{tempAudioPath}");
                    // 返回视频文件大小
                    actualFileSize = new FileInfo(tempVideoPath).Length;
                    return actualFileSize;
                }
                
                statusCallback?.Invoke("开始处理音视频...");
                
                // 合并音视频流：优先使用Mp4Merger库
                bool mergeSuccess = false;
                
                try
                {
                    // 优先使用Mp4Merger库进行合并，使用using语句确保资源被正确释放
                    statusCallback?.Invoke("📦 使用Mp4Merger库开始合并音视频...");
                    
                    // 使用单独的作用域确保merger实例在合并完成后立即被释放
                    { 
                        var mergeService = new Mp4MergeService();
                        var mergeResult = await mergeService.MergeAsync(tempVideoPath, tempAudioPath, finalVideoPath, statusCallback, true);
                        mergeSuccess = mergeResult.Success;
                        
                        if (mergeSuccess)
                        {
                            statusCallback?.Invoke($"✅ Mp4Merger合并成功：{finalVideoPath}");
                        }
                        else
                        {
                            statusCallback?.Invoke($"⚠️ Mp4Merger合并失败：{mergeResult.Message}");
                        }
                    }
                    
                    // 合并完成后立即触发垃圾回收，确保VideoMerger使用的资源被回收
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    
                    statusCallback?.Invoke("♻️ 资源已回收，内存占用已下降");
                }
                catch (Exception ex)
                {
                    statusCallback?.Invoke($"⚠️ Mp4Merger合并过程中发生错误：{ex.Message}");
                }
                
                // 由于FFmpegHelper在当前项目中不可用，暂时不支持FFmpeg作为备用
                if (!mergeSuccess)
                {
                    statusCallback?.Invoke($"❌ 音视频处理失败");
                }
                
                if (mergeSuccess)
                {
                    // 删除临时文件
                    try
                    {
                        if (File.Exists(tempVideoPath))
                            File.Delete(tempVideoPath);
                        if (File.Exists(tempAudioPath))
                            File.Delete(tempAudioPath);
                        statusCallback?.Invoke("临时文件已清理");
                    }
                    catch (Exception ex)
                    {
                        statusCallback?.Invoke($"清理临时文件失败：{ex.Message}");
                    }
                }
                else
                {
                    statusCallback?.Invoke("音视频处理失败，保留原始文件");
                    statusCallback?.Invoke($"视频文件：{tempVideoPath}");
                    statusCallback?.Invoke($"音频文件：{tempAudioPath}");
                }
            }
            // 处理durl格式（合并的视频流）
            else if (typedVideoInfo.CombinedStreams != null && typedVideoInfo.CombinedStreams.Count > 0)
            {
                statusCallback?.Invoke("开始下载合并的视频流...");
                
                // 下载第一个合并流（通常只有一个）
                var stream = typedVideoInfo.CombinedStreams[0];
                await DownloadStream(stream, finalVideoPath, progressCallback, statusCallback, speedCallback, "视频流", cancellationToken);
                
                statusCallback?.Invoke($"视频下载完成：{finalVideoPath}");
            }
            else
            {
                throw new Exception("未找到有效的视频流信息");
            }
        }

            // 获取实际文件大小
            if (File.Exists(finalVideoPath))
            {
                actualFileSize = new FileInfo(finalVideoPath).Length;
                
                // 获取并显示媒体信息：当前版本使用Mp4Merger库，不依赖FFmpeg获取媒体信息
                statusCallback?.Invoke($"📊 文件大小：{FormatFileSize(actualFileSize)}");
            }
            
            // 下载完成，将进度条设置为100%
            progressCallback?.Invoke(100.0);
            
            return actualFileSize;
        }
        catch (Exception ex)
        {
            // 处理取消操作异常，显示友好的中文提示
            if (ex is OperationCanceledException)
            {
                statusCallback?.Invoke("下载已取消");
            }
            else
            {
                statusCallback?.Invoke($"下载失败: {ex.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// 下载单个流
    /// </summary>
    /// <param name="streamInfo">流信息</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="progressCallback">进度回调函数</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <param name="speedCallback">网速回调函数（字节/秒）</param>
    /// <param name="streamType">流类型（视频流/音频流）</param>
    /// <returns>下载任务</returns>
    private async Task DownloadStream(VideoStreamInfo streamInfo, string savePath, Action<double>? progressCallback, Action<string>? statusCallback, Action<long>? speedCallback, string streamType, CancellationToken cancellationToken = default)
    {
        if (streamInfo == null)
        {
            throw new ArgumentNullException(nameof(streamInfo));
        }

        if (string.IsNullOrEmpty(streamInfo.Url))
        {
            throw new Exception($"{streamType}URL为空");
        }

        // 检查是否为 m3u8 格式
        if (streamInfo.Format.Equals("m3u8", StringComparison.OrdinalIgnoreCase) ||
            streamInfo.Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            statusCallback?.Invoke($"检测到 m3u8/HLS 格式流，使用专用下载器...");
            await DownloadM3U8Stream(streamInfo.Url, savePath, progressCallback, statusCallback, speedCallback, cancellationToken);
            return;
        }

        try
        {
            statusCallback?.Invoke($"开始下载{streamType}：{streamInfo.Url}");

            // 确保保存路径所在的目录存在
            string? directoryPath = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                statusCallback?.Invoke($"创建保存目录：{directoryPath}");
            }
            
            // 检查文件是否已存在，获取已下载的大小
            long downloadedBytes = 0;
            if (File.Exists(savePath))
            {
                var fileInfo = new FileInfo(savePath);
                downloadedBytes = fileInfo.Length;
                statusCallback?.Invoke($"发现已下载文件，已下载 {FormatFileSize(downloadedBytes)}，将尝试续传");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, streamInfo.Url);
            
            // 添加防盗链头信息
            request.Headers.Add("Range", $"bytes={downloadedBytes}-");
            request.Headers.Add("Connection", "keep-alive");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Site", "cross-site");
            request.Headers.Add("Pragma", "no-cache");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.8,zh-TW;q=0.7,zh-HK;q=0.5,en-US;q=0.3,en;q=0.2");
            request.Headers.Add("Accept-Encoding", "identity");
            request.Headers.Add("DNT", "1");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            
            // 设置Referer和Origin头信息
            try
            {
                // 对于视频流，始终使用B站主站作为Referer和Origin
                request.Headers.Add("Referer", "https://www.bilibili.com/");
                request.Headers.Add("Origin", "https://www.bilibili.com/");
                statusCallback?.Invoke("使用B站主站作为Referer和Origin");
                
                // 添加更多的防盗链头信息
                request.Headers.Add("Sec-Ch-Ua", "\"Chromium\";v=\"120\", \"Not=A?Brand\";v=\"24\", \"Google Chrome\";v=\"120\"");
                request.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
                request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
                request.Headers.Add("Sec-Fetch-Site", "cross-site");
                request.Headers.Add("Sec-Fetch-Mode", "cors");
                request.Headers.Add("Sec-Fetch-Dest", "empty");
            }
            catch (Exception ex)
            {
                // 如果设置失败，记录错误但继续执行
                statusCallback?.Invoke($"设置请求头失败: {ex.Message}");
            }

            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                // 检查是否支持续传
                if (response.StatusCode == System.Net.HttpStatusCode.PartialContent || response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    long totalBytes = response.Content.Headers.ContentLength ?? streamInfo.Size;
                    if (totalBytes == 0)
                    {
                        // 如果无法获取文件大小，尝试从响应头获取
                        if (response.Headers.TryGetValues("Content-Range", out var contentRangeValues))
                        {
                            string? contentRange = contentRangeValues.FirstOrDefault();
                            if (!string.IsNullOrEmpty(contentRange))
                            {
                                // 解析Content-Range头，格式：bytes 0-1023/2048
                                var match = Regex.Match(contentRange, @"/([0-9]+)");
                                if (match.Success)
                                {
                                    totalBytes = long.Parse(match.Groups[1].Value);
                                }
                            }
                        }
                        // 如果还是无法获取文件大小，假设一个合理的初始值，确保进度可以更新
                        if (totalBytes == 0)
                        {
                            totalBytes = 1024 * 1024 * 50; // 假设50MB，实际会根据读取进度动态调整
                        }
                    }

                    statusCallback?.Invoke($"开始接收{streamType}数据...");

                    using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    using (var fileStream = new FileStream(savePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        // 设置文件流位置到已下载的末尾
                        fileStream.Position = downloadedBytes;

                        byte[] buffer = new byte[8192];
                        long totalRead = downloadedBytes;
                        int read;
                        int lastProgress = (int)((double)downloadedBytes / totalBytes * 100);
                        
                        // 网速计算变量
                        long bytesInLastSecond = 0;
                        DateTime lastSpeedUpdate = DateTime.Now;

                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                            totalRead += read;
                            bytesInLastSecond += read;

                            // 更新进度
                            if (totalBytes > 0)
                            {
                                // 如果实际读取的字节数超过了预估的总字节数，更新总字节数
                                if (totalRead > totalBytes)
                                {
                                    totalBytes = totalRead * 2; // 动态调整总字节数
                                }
                                double progress = (double)totalRead / totalBytes * 100;
                                // 确保进度不超过99%，留最后1%在下载完成后设置
                                progress = Math.Min(progress, 99);
                                if (progress > lastProgress)
                                {
                                    progressCallback?.Invoke(progress);
                                    lastProgress = (int)progress;
                                }
                            }

                            // 计算并报告网速（每秒更新一次）
                            DateTime now = DateTime.Now;
                            if ((now - lastSpeedUpdate).TotalMilliseconds >= 1000)
                            {
                                speedCallback?.Invoke(bytesInLastSecond);
                                bytesInLastSecond = 0;
                                lastSpeedUpdate = now;
                            }
                        }

                        // 最后一次更新网速
                        speedCallback?.Invoke(bytesInLastSecond);
                        
                        await fileStream.FlushAsync(cancellationToken);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    // 服务器不支持续传或范围无效，清理文件并从头开始下载
                    statusCallback?.Invoke($"服务器不支持续传，状态码：{response.StatusCode}，将从头开始下载");
                    
                    // 清理已存在的文件
                    if (File.Exists(savePath))
                    {
                        File.Delete(savePath);
                    }
                    
                    // 重新发送请求，不使用Range头
                    var newRequest = new HttpRequestMessage(HttpMethod.Get, streamInfo.Url);
                    newRequest.Headers.Add("Connection", "keep-alive");
                    newRequest.Headers.Add("Sec-Fetch-Dest", "empty");
                    newRequest.Headers.Add("Sec-Fetch-Mode", "cors");
                    newRequest.Headers.Add("Sec-Fetch-Site", "cross-site");
                    newRequest.Headers.Add("Pragma", "no-cache");
                    newRequest.Headers.Add("Cache-Control", "no-cache");
                    newRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    newRequest.Headers.Add("Accept", "*/*");
                    newRequest.Headers.Add("Accept-Language", "zh-CN,zh;q=0.8,zh-TW;q=0.7,zh-HK;q=0.5,en-US;q=0.3,en;q=0.2");
                    newRequest.Headers.Add("Accept-Encoding", "identity");
                    newRequest.Headers.Add("DNT", "1");
                    newRequest.Headers.Add("Upgrade-Insecure-Requests", "1");
                    
                    // 设置Referer和Origin头信息
                    try
                    {
                        // 对于视频流，始终使用B站主站作为Referer和Origin
                        newRequest.Headers.Add("Referer", "https://www.bilibili.com/");
                        newRequest.Headers.Add("Origin", "https://www.bilibili.com/");
                        statusCallback?.Invoke("使用B站主站作为Referer和Origin");
                        
                        // 添加更多的防盗链头信息
                        newRequest.Headers.Add("Sec-Ch-Ua", "\"Chromium\";v=\"120\", \"Not=A?Brand\";v=\"24\", \"Google Chrome\";v=\"120\"");
                        newRequest.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
                        newRequest.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
                        newRequest.Headers.Add("Sec-Fetch-Site", "cross-site");
                        newRequest.Headers.Add("Sec-Fetch-Mode", "cors");
                        newRequest.Headers.Add("Sec-Fetch-Dest", "empty");
                    }
                    catch (Exception ex)
                    {
                        // 如果设置失败，记录错误但继续执行
                        statusCallback?.Invoke($"设置请求头失败: {ex.Message}");
                    }
                    
                    using (var newResponse = await _httpClient.SendAsync(newRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        newResponse.EnsureSuccessStatusCode();
                        
                        long totalBytes = newResponse.Content.Headers.ContentLength ?? streamInfo.Size;
                        if (totalBytes == 0)
                        {
                            totalBytes = 1024 * 1024 * 50; // 假设50MB，实际会根据读取进度动态调整
                        }

                        statusCallback?.Invoke($"开始接收{streamType}数据...");

                        using (var stream = await newResponse.Content.ReadAsStreamAsync(cancellationToken))
                        using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            byte[] buffer = new byte[8192];
                            long totalRead = 0;
                            int read;
                            int lastProgress = 0;
                            
                            // 网速计算变量
                            long bytesInLastSecond = 0;
                            DateTime lastSpeedUpdate = DateTime.Now;

                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                                totalRead += read;
                                bytesInLastSecond += read;

                                // 更新进度
                                if (totalBytes > 0)
                                {
                                    // 如果实际读取的字节数超过了预估的总字节数，更新总字节数
                                    if (totalRead > totalBytes)
                                    {
                                        totalBytes = totalRead * 2; // 动态调整总字节数
                                    }
                                    double progress = (double)totalRead / totalBytes * 100;
                                // 确保进度不超过99%，留最后1%在下载完成后设置
                                progress = Math.Min(progress, 99);
                                if (progress > lastProgress)
                                {
                                    progressCallback?.Invoke(progress);
                                    lastProgress = (int)progress;
                                }
                                }

                                // 计算并报告网速（每秒更新一次）
                                DateTime now = DateTime.Now;
                                if ((now - lastSpeedUpdate).TotalMilliseconds >= 1000)
                                {
                                    speedCallback?.Invoke(bytesInLastSecond);
                                    bytesInLastSecond = 0;
                                    lastSpeedUpdate = now;
                                }
                            }

                            // 最后一次更新网速
                            speedCallback?.Invoke(bytesInLastSecond);
                            
                            await fileStream.FlushAsync(cancellationToken);
                        }
                    }
                }
                else
                {
                    throw new Exception($"服务器返回错误状态码：{response.StatusCode}");
                }
            }

            statusCallback?.Invoke($"{streamType}下载完成：{savePath}");
        }
        catch (Exception ex)
        {
            // 处理取消操作异常，显示友好的中文提示
            if (ex is OperationCanceledException)
            {
                statusCallback?.Invoke($"{streamType}下载已取消");
            }
            else
            {
                statusCallback?.Invoke($"下载{streamType}失败: {ex.Message}");
            }
            
            // 不再清理临时文件，保留已下载的内容以便续传
            throw;
        }
    }

    /// <summary>
    /// 下载 m3u8/HLS 流
    /// </summary>
    /// <param name="m3u8Url">m3u8 播放列表 URL</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="progressCallback">进度回调</param>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="speedCallback">速度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task DownloadM3U8Stream(string m3u8Url, string outputPath, Action<double>? progressCallback, Action<string>? statusCallback, Action<long>? speedCallback, CancellationToken cancellationToken)
    {
        statusCallback?.Invoke($"🎬 开始下载 m3u8 流: {m3u8Url}");

        // 创建临时文件夹存放 ts 分片
        string tempFolder = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? Path.GetTempPath(),
            Path.GetFileNameWithoutExtension(outputPath) + "_ts_temp"
        );

        if (!Directory.Exists(tempFolder))
        {
            Directory.CreateDirectory(tempFolder);
            statusCallback?.Invoke($"📁 创建临时文件夹: {tempFolder}");
        }

        try
        {
            // 下载 m3u8 播放列表
            statusCallback?.Invoke("📋 正在获取 m3u8 播放列表...");
            string m3u8Content = await DownloadTextAsync(m3u8Url, cancellationToken);

            // 解析 m3u8 获取 ts 分片 URL
            var tsUrls = ParseM3U8Content(m3u8Content, m3u8Url);
            statusCallback?.Invoke($"📋 解析到 {tsUrls.Count} 个 ts 分片");

            if (tsUrls.Count == 0)
            {
                throw new Exception("m3u8 解析失败，未找到 ts 分片");
            }

            // 下载所有 ts 分片
            long totalBytes = 0;
            int downloadedCount = 0;
            long bytesInLastSecond = 0;
            DateTime lastSpeedUpdate = DateTime.Now;

            for (int i = 0; i < tsUrls.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string tsUrl = tsUrls[i];
                string tsFilePath = Path.Combine(tempFolder, $"{i:D6}.ts");

                statusCallback?.Invoke($"📥 下载 ts 分片 [{i + 1}/{tsUrls.Count}]: {tsUrl.Substring(0, Math.Min(tsUrl.Length, 60))}...");

                long tsSize = await DownloadFileAsync(tsUrl, tsFilePath, cancellationToken);
                totalBytes += tsSize;
                bytesInLastSecond += tsSize;
                downloadedCount++;

                // 更新进度
                double progress = (double)downloadedCount / tsUrls.Count * 100;
                progressCallback?.Invoke(Math.Min(progress, 99));

                // 计算并报告网速（每秒更新一次）
                DateTime now = DateTime.Now;
                if ((now - lastSpeedUpdate).TotalMilliseconds >= 1000)
                {
                    speedCallback?.Invoke(bytesInLastSecond);
                    bytesInLastSecond = 0;
                    lastSpeedUpdate = now;
                }
            }

            // 最后一次更新网速
            speedCallback?.Invoke(bytesInLastSecond);

            statusCallback?.Invoke($"✅ 所有 ts 分片下载完成，共 {downloadedCount} 个文件");
            statusCallback?.Invoke($"📊 总大小: {FormatFileSize(totalBytes)}");

            // 合并 ts 文件
            statusCallback?.Invoke("🔗 正在合并 ts 文件...");
            await MergeTsFilesAsync(tempFolder, outputPath, tsUrls.Count, statusCallback);

            statusCallback?.Invoke($"✅ 合并完成: {outputPath}");

            // 清理临时文件夹
            try
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                    statusCallback?.Invoke("🗑️ 临时文件已清理");
                }
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"⚠️ 清理临时文件失败: {ex.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            statusCallback?.Invoke("❌ 下载已取消");
            throw;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"❌ m3u8 下载失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 解析 m3u8 内容，提取 ts 分片 URL
    /// </summary>
    /// <param name="m3u8Content">m3u8 文件内容</param>
    /// <param name="baseUrl">m3u8 文件的 URL（用于解析相对路径）</param>
    /// <returns>ts 分片 URL 列表</returns>
    private List<string> ParseM3U8Content(string m3u8Content, string baseUrl)
    {
        var tsUrls = new List<string>();
        var lines = m3u8Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // 获取基础 URL（用于解析相对路径）
        string? baseUrlDir = null;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            int lastSlashIndex = baseUrl.LastIndexOf('/');
            if (lastSlashIndex > 0)
            {
                baseUrlDir = baseUrl.Substring(0, lastSlashIndex + 1);
            }
        }

        foreach (var line in lines)
        {
            string trimmedLine = line.Trim();

            // 跳过注释行和空行
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
            {
                // 检查是否是嵌套 m3u8
                if (trimmedLine.StartsWith("#EXT-X-STREAM-INF"))
                {
                    // 这是主播放列表，需要选择一个子播放列表
                    // 暂时跳过，后续可能需要处理
                }
                continue;
            }

            // 判断是否为 ts 文件或 m3u8 文件
            if (trimmedLine.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                trimmedLine.Contains(".ts?", StringComparison.OrdinalIgnoreCase) ||
                !trimmedLine.Contains("#"))
            {
                string tsUrl;

                // 处理相对路径
                if (trimmedLine.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    tsUrl = trimmedLine;
                }
                else if (!string.IsNullOrEmpty(baseUrlDir))
                {
                    tsUrl = baseUrlDir + trimmedLine;
                }
                else
                {
                    tsUrl = trimmedLine;
                }

                tsUrls.Add(tsUrl);
            }
        }

        return tsUrls;
    }

    /// <summary>
    /// 下载文本内容
    /// </summary>
    /// <param name="url">URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文本内容</returns>
    private async Task<string> DownloadTextAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "*/*");
        request.Headers.Add("Referer", "https://live.kuaishou.com/");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// 下载单个文件
    /// </summary>
    /// <param name="url">URL</param>
    /// <param name="filePath">保存路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载的字节数</returns>
    private async Task<long> DownloadFileAsync(string url, string filePath, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "*/*");
        request.Headers.Add("Referer", "https://live.kuaishou.com/");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        byte[] buffer = new byte[8192];
        int read;
        long totalRead = 0;

        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;
        }

        return totalRead;
    }

    /// <summary>
    /// 合并 ts 文件
    /// </summary>
    /// <param name="tempFolder">临时文件夹</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="fileCount">文件数量</param>
    /// <param name="statusCallback">状态回调</param>
    private async Task MergeTsFilesAsync(string tempFolder, string outputPath, int fileCount, Action<string>? statusCallback)
    {
        // 直接合并 ts 文件（大多数播放器支持直接播放）
        // ts 文件可以直接拼接成一个文件
        using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

        byte[] buffer = new byte[65536];
        int processedCount = 0;

        for (int i = 0; i < fileCount; i++)
        {
            string tsFilePath = Path.Combine(tempFolder, $"{i:D6}.ts");

            if (!File.Exists(tsFilePath))
            {
                statusCallback?.Invoke($"⚠️ 文件不存在: {tsFilePath}");
                continue;
            }

            using var inputStream = new FileStream(tsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);

            int read;
            while ((read = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await outputStream.WriteAsync(buffer, 0, read);
            }

            processedCount++;

            if (processedCount % 50 == 0)
            {
                statusCallback?.Invoke($"🔗 合并进度: {processedCount}/{fileCount}");
            }
        }

        statusCallback?.Invoke($"✅ 合并完成，共处理 {processedCount} 个文件");
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    /// <param name="fileName">原始文件名</param>
    /// <returns>清理后的文件名</returns>
    private string CleanFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return "未命名视频";
        }

        // 移除文件名中的非法字符
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidRegex = $"[{invalidChars}]";
        return Regex.Replace(fileName, invalidRegex, "");
    }

    /// <summary>
    /// 格式化文件大小为易读格式
    /// </summary>
    /// <param name="bytes">文件大小（字节）</param>
    /// <returns>格式化后的文件大小</returns>
    private string FormatFileSize(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F2} {suffixes[suffixIndex]}";
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
        if (disposing)
        {
            // 释放托管资源
            _cts?.Cancel();
            _cts?.Dispose();
            _httpClient.Dispose();
        }
    }
}
