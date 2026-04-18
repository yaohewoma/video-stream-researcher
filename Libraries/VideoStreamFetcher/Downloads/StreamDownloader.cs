using System.Net.Http.Headers;
using VideoStreamFetcher.Parsers;

namespace VideoStreamFetcher.Downloads;

/// <summary>
/// 流下载器，负责下载单个视频流
/// </summary>
public sealed class StreamDownloader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HlsDownloader _hlsDownloader;
    private string? _cookieString;

    public StreamDownloader(HttpClient httpClient, HlsDownloader hlsDownloader)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _hlsDownloader = hlsDownloader ?? throw new ArgumentNullException(nameof(hlsDownloader));
    }

    /// <summary>
    /// 设置Cookie字符串
    /// </summary>
    public void SetCookieString(string cookieString)
    {
        _cookieString = cookieString;
    }

    /// <summary>
    /// 下载视频流
    /// </summary>
    public async Task<long> DownloadStreamAsync(
        VideoStreamInfo stream,
        string outputPath,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        VideoDownloadOptions options,
        string streamType,
        CancellationToken cancellationToken)
    {
        var url = stream.Url ?? string.Empty;
        var isM3U8 = stream.Format.Equals("m3u8", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase);

        if (isM3U8)
        {
            statusCallback?.Invoke($"开始下载{streamType}...");
            return await _hlsDownloader.DownloadAsync(
                url,
                Path.GetDirectoryName(outputPath) ?? Path.GetTempPath(),
                outputPath,
                progressCallback,
                statusCallback,
                speedCallback,
                options.HlsMaxConcurrency,
                options.HttpRetryCount,
                options.HlsMaxSegments,
                options.HlsPreviewEdit,
                cancellationToken);
        }

        statusCallback?.Invoke($"开始下载{streamType}...");
        return await DownloadFileAsync(url, outputPath, progressCallback, statusCallback, speedCallback, options.HttpRetryCount, cancellationToken);
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    private async Task<long> DownloadFileAsync(
        string url,
        string outputPath,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        int retryCount,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("无效的下载 URL", nameof(url));
        }

        Exception? last = null;
        for (var attempt = 0; attempt <= Math.Max(0, retryCount); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

                using var request = CreateDownloadRequest(uri);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                long downloadedBytes = 0;
                long bytesSinceLastReport = 0;
                var lastSpeedUpdate = DateTime.UtcNow;

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);

                var buffer = new byte[1024 * 1024];
                int read;
                while ((read = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloadedBytes += read;
                    bytesSinceLastReport += read;

                    if (totalBytes > 0)
                    {
                        progressCallback?.Invoke(Math.Min(99, downloadedBytes * 100.0 / totalBytes));
                    }

                    var now = DateTime.UtcNow;
                    if ((now - lastSpeedUpdate).TotalSeconds >= 1)
                    {
                        speedCallback?.Invoke(bytesSinceLastReport);
                        bytesSinceLastReport = 0;
                        lastSpeedUpdate = now;
                    }
                }

                speedCallback?.Invoke(bytesSinceLastReport);
                await fileStream.FlushAsync(cancellationToken);
                statusCallback?.Invoke($"下载完成: {outputPath}");
                return fileStream.Length;
            }
            catch (Exception ex) when (attempt < retryCount)
            {
                last = ex;
                TryDeleteFile(outputPath);
                await Task.Delay(TimeSpan.FromMilliseconds(400 * (attempt + 1)), cancellationToken);
            }
        }

        throw last ?? new HttpRequestException($"下载失败: {url}");
    }

    /// <summary>
    /// 创建下载请求
    /// </summary>
    private HttpRequestMessage CreateDownloadRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // 设置浏览器模拟请求头
        request.Headers.Add("User-Agent", HttpRequestConfig.DefaultUserAgent);
        request.Headers.Add("Accept", HttpRequestConfig.DefaultAccept);
        request.Headers.Add("Accept-Language", HttpRequestConfig.DefaultAcceptLanguage);
        request.Headers.Add("Accept-Encoding", HttpRequestConfig.DefaultAcceptEncoding);
        request.Headers.Add("Connection", HttpRequestConfig.DefaultConnection);
        request.Headers.Add("Upgrade-Insecure-Requests", HttpRequestConfig.DefaultUpgradeInsecureRequests);
        request.Headers.Add("Sec-Fetch-Dest", HttpRequestConfig.DefaultSecFetchDest);
        request.Headers.Add("Sec-Fetch-Mode", HttpRequestConfig.DefaultSecFetchMode);
        request.Headers.Add("Sec-Fetch-Site", HttpRequestConfig.DefaultSecFetchSite);
        request.Headers.Add("Sec-Fetch-User", HttpRequestConfig.DefaultSecFetchUser);

        // 设置Referer
        var referrer = ReferrerResolver.GuessReferrer(uri);
        if (referrer != null)
        {
            request.Headers.Add("Referer", referrer.ToString());
        }

        // 添加Cookie
        if (!string.IsNullOrEmpty(_cookieString))
        {
            request.Headers.Add("Cookie", _cookieString);
        }

        return request;
    }

    /// <summary>
    /// 尝试删除文件
    /// </summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"删除文件失败: {path}, 错误: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// HTTP请求配置
/// </summary>
public static class HttpRequestConfig
{
    public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    public const string DefaultAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
    public const string DefaultAcceptLanguage = "zh-CN,zh;q=0.8,zh-TW;q=0.7,zh-HK;q=0.5,en-US;q=0.3,en;q=0.2";
    public const string DefaultAcceptEncoding = "gzip, deflate, br";
    public const string DefaultConnection = "keep-alive";
    public const string DefaultUpgradeInsecureRequests = "1";
    public const string DefaultSecFetchDest = "document";
    public const string DefaultSecFetchMode = "navigate";
    public const string DefaultSecFetchSite = "none";
    public const string DefaultSecFetchUser = "?1";
}

/// <summary>
/// Referrer解析器
/// </summary>
public static class ReferrerResolver
{
    /// <summary>
    /// 根据URL猜测Referer
    /// </summary>
    public static Uri? GuessReferrer(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();

        // 快手
        if (host.Contains("yximgs") || host.Contains("kuaishou"))
        {
            return new Uri("https://live.kuaishou.com/");
        }

        // Bilibili
        if (host.Contains("bilibili") ||
            host.Contains("bilivideo") ||
            host.Contains("hdslb") ||
            host.Contains("mcdn") ||
            host.Contains("edge"))
        {
            return new Uri("https://www.bilibili.com/");
        }

        // 米游社
        if (host.Contains("miyoushe") ||
            host.Contains("mihoyo") ||
            host.Contains("hoyoverse") ||
            host.Contains("hoyolab") ||
            host.Contains("qpic") ||
            host.Contains("douyin"))
        {
            return new Uri("https://www.miyoushe.com/");
        }

        return null;
    }
}
