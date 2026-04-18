using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VideoStreamFetcher.Localization;

namespace VideoStreamFetcher.Downloads;

public sealed class HlsDownloader
{
    private readonly HttpClient _httpClient;
    private string? _cookieString;

    public HlsDownloader()
    {
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseCookies = true,
            AllowAutoRedirect = true
        })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    public void SetCookieString(string cookieString)
    {
        _cookieString = cookieString;
    }

    public async Task<long> DownloadAsync(
        string m3u8Url,
        string workingDirectory,
        string outputPath,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        int maxConcurrency,
        int retryCount,
        int? maxSegments,
        bool previewEdit,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(m3u8Url, UriKind.Absolute, out var playlistUri))
        {
            throw new ArgumentException("无效的 m3u8 URL", nameof(m3u8Url));
        }

        var manifest = await LoadManifestAsync(playlistUri, statusCallback, retryCount, cancellationToken);

        if (manifest.Variants.Count > 0)
        {
            var selected = manifest.Variants
                .OrderByDescending(v => v.Bandwidth ?? 0)
                .First();

            statusCallback?.Invoke($"[HLS] 选择码率: {selected.Resolution ?? "unknown"} ({selected.Bandwidth?.ToString(CultureInfo.InvariantCulture) ?? "unknown"})");
            manifest = await LoadManifestAsync(selected.PlaylistUri, statusCallback, retryCount, cancellationToken);
        }

        if (manifest.Segments.Count == 0)
        {
            throw new InvalidOperationException("m3u8 解析失败，未找到分片");
        }

        var segments = manifest.Segments;
        if (maxSegments.HasValue)
        {
            var limit = Math.Max(1, maxSegments.Value);
            if (segments.Count > limit)
            {
                statusCallback?.Invoke($"[HLS] 仅下载前 {limit} 个分片（总计 {segments.Count}）");
                segments = segments.Take(limit).ToList();
            }
        }

        byte[]? keyBytes = null;
        if (!string.IsNullOrWhiteSpace(manifest.KeyMethod) &&
            string.Equals(manifest.KeyMethod, "AES-128", StringComparison.OrdinalIgnoreCase) &&
            manifest.KeyUri != null)
        {
            try
            {
                keyBytes = await DownloadBytesWithRetryAsync(manifest.KeyUri, retryCount, cancellationToken);
                if (keyBytes.Length != 16)
                {
                    statusCallback?.Invoke($"[HLS] 警告: KEY 长度异常: {keyBytes.Length}");
                    keyBytes = null;
                }
                else
                {
                    statusCallback?.Invoke("[HLS] 检测到 AES-128 加密，已加载 KEY");
                }
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"[HLS] KEY 加载失败: {ex.Message}");
                keyBytes = null;
            }
        }

        var segmentsDir = Path.Combine(workingDirectory, "segments");
        Directory.CreateDirectory(segmentsDir);

        var totalSegments = segments.Count;
        var completedSegments = 0;
        long totalBytes = 0;
        long bytesSinceLastReport = 0;

        using var speedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var speedTask = RunSpeedReporterAsync(
            () => Interlocked.Exchange(ref bytesSinceLastReport, 0),
            speedCallback,
            speedCts.Token);

        try
        {
            var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency), Math.Max(1, maxConcurrency));
            var errors = new ConcurrentQueue<Exception>();

            var tasks = segments.Select(async (segmentUri, index) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var segmentPath = Path.Combine(segmentsDir, $"{index:D6}.ts");
                    var bytes = await DownloadSegmentWithRetryAsync(segmentUri.SegmentUri, segmentPath, retryCount, keyBytes, manifest.KeyIv, index, cancellationToken);
                    Interlocked.Add(ref totalBytes, bytes);
                    Interlocked.Add(ref bytesSinceLastReport, bytes);

                    var done = Interlocked.Increment(ref completedSegments);
                    progressCallback?.Invoke(Math.Min(99, done * 100.0 / totalSegments));
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            if (!errors.IsEmpty)
            {
                throw new AggregateException(errors);
            }

            statusCallback?.Invoke(FetcherLocalization.GetString("Download.HlsSegmentsCompleted", totalSegments));

            if (previewEdit)
            {
                await WritePreviewMetaAsync(segmentsDir, segments, outputPath + ".meta.json", cancellationToken);
            }

            await MergeSegmentsAsync(segmentsDir, totalSegments, outputPath, statusCallback, cancellationToken);
            progressCallback?.Invoke(100);
            var outputBytes = new FileInfo(outputPath).Length;
            return outputBytes > 0 ? outputBytes : totalBytes;
        }
        finally
        {
            speedCts.Cancel();
            try
            {
                await speedTask;
            }
            catch
            {
            }

            try
            {
                if (!previewEdit && Directory.Exists(segmentsDir))
                {
                    Directory.Delete(segmentsDir, true);
                }
            }
            catch
            {
            }
        }
    }

    private async Task<HlsManifest> LoadManifestAsync(
        Uri playlistUri,
        Action<string>? statusCallback,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var content = await DownloadTextWithRetryAsync(playlistUri, retryCount, cancellationToken);
        var lines = content
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToArray();

        var variants = new List<HlsVariant>();
        var segments = new List<HlsSegment>();
        double? pendingDuration = null;
        Uri? keyUri = null;
        string? keyMethod = null;
        byte[]? keyIv = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"#EXTINF:([0-9.]+)", RegexOptions.IgnoreCase);
                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    pendingDuration = d;
                }

                continue;
            }
            if (line.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
            {
                var bandwidth = TryParseLongAttribute(line, "BANDWIDTH");
                var resolution = TryParseStringAttribute(line, "RESOLUTION");

                if (i + 1 < lines.Length)
                {
                    var next = lines[i + 1];
                    if (!next.StartsWith("#", StringComparison.Ordinal))
                    {
                        variants.Add(new HlsVariant
                        {
                            PlaylistUri = new Uri(playlistUri, next),
                            Bandwidth = bandwidth,
                            Resolution = resolution
                        });
                        i++;
                    }
                }

                continue;
            }

            if (line.StartsWith("#EXT-X-KEY", StringComparison.OrdinalIgnoreCase))
            {
                keyMethod = TryParseStringAttribute(line, "METHOD");
                var key = TryParseStringAttribute(line, "URI");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    key = key.Trim().Trim('"');
                    keyUri = new Uri(playlistUri, key);
                }

                var ivText = TryParseStringAttribute(line, "IV");
                if (!string.IsNullOrWhiteSpace(ivText))
                {
                    keyIv = TryParseIv(ivText);
                }

                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            segments.Add(new HlsSegment
            {
                SegmentUri = new Uri(playlistUri, line),
                DurationSeconds = pendingDuration ?? 0
            });
            pendingDuration = null;
        }

        statusCallback?.Invoke($"[HLS] m3u8 解析: variant={variants.Count}, segments={segments.Count}");

        return new HlsManifest
        {
            PlaylistUri = playlistUri,
            Variants = variants,
            Segments = segments,
            KeyUri = keyUri,
            KeyMethod = keyMethod,
            KeyIv = keyIv
        };
    }

    private static async Task WritePreviewMetaAsync(string segmentsDir, List<HlsSegment> segments, string metaPath, CancellationToken cancellationToken)
    {
        var meta = new PreviewMeta
        {
            SegmentsDir = segmentsDir,
            SegmentFiles = Enumerable.Range(0, segments.Count).Select(i => $"{i:D6}.ts").ToList(),
            Durations = segments.Select(s => s.DurationSeconds).ToList()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(metaPath) ?? ".");
        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
        await File.WriteAllTextAsync(metaPath, json, Encoding.UTF8, cancellationToken);
    }

    private sealed class PreviewMeta
    {
        public required string SegmentsDir { get; init; }
        public required List<string> SegmentFiles { get; init; }
        public required List<double> Durations { get; init; }
    }

    private async Task<byte[]> DownloadBytesWithRetryAsync(Uri uri, int retryCount, CancellationToken cancellationToken)
    {
        var last = default(Exception);
        for (var i = 0; i < Math.Max(1, retryCount); i++)
        {
            try
            {
                using var req = CreateRequest(uri);
                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (Exception ex) when (i < retryCount - 1)
            {
                last = ex;
                await Task.Delay(300, cancellationToken);
            }
        }

        throw last ?? new InvalidOperationException("下载失败");
    }

    private async Task<long> DownloadSegmentWithRetryAsync(Uri segmentUri, string segmentPath, int retryCount, byte[]? keyBytes, byte[]? explicitIv, int index, CancellationToken cancellationToken)
    {
        var last = default(Exception);
        for (var i = 0; i < Math.Max(1, retryCount); i++)
        {
            try
            {
                using var req = CreateRequest(segmentUri);
                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                resp.EnsureSuccessStatusCode();
                var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);

                if (keyBytes != null)
                {
                    var iv = explicitIv ?? BuildIvFromSequence(index);
                    bytes = DecryptAes128Cbc(bytes, keyBytes, iv);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(segmentPath) ?? ".");
                await File.WriteAllBytesAsync(segmentPath, bytes, cancellationToken);
                return bytes.LongLength;
            }
            catch (Exception ex) when (i < retryCount - 1)
            {
                last = ex;
                await Task.Delay(200, cancellationToken);
            }
        }

        throw last ?? new InvalidOperationException("分片下载失败");
    }

    private static byte[] DecryptAes128Cbc(byte[] cipher, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var msIn = new MemoryStream(cipher);
        using var cs = new CryptoStream(msIn, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var msOut = new MemoryStream(cipher.Length);
        cs.CopyTo(msOut);
        return msOut.ToArray();
    }

    private static byte[] BuildIvFromSequence(int seq)
    {
        var iv = new byte[16];
        iv[12] = (byte)((seq >> 24) & 0xFF);
        iv[13] = (byte)((seq >> 16) & 0xFF);
        iv[14] = (byte)((seq >> 8) & 0xFF);
        iv[15] = (byte)(seq & 0xFF);
        return iv;
    }

    private static byte[]? TryParseIv(string text)
    {
        text = text.Trim().Trim('"');
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(2);
        }

        try
        {
            if (text.Length % 2 != 0)
            {
                return null;
            }
            var bytes = Convert.FromHexString(text);
            return bytes.Length == 16 ? bytes : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> DownloadTextWithRetryAsync(Uri uri, int retryCount, CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 0; attempt <= Math.Max(0, retryCount); attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = CreateRequest(uri);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (attempt < retryCount)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
            }
        }

        throw last ?? new HttpRequestException($"下载失败: {uri}");
    }

    private async Task MergeSegmentsAsync(
        string segmentsDir,
        int segmentCount,
        string outputPath,
        Action<string>? statusCallback,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
        var buffer = new byte[1024 * 1024];

        for (var i = 0; i < segmentCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segmentPath = Path.Combine(segmentsDir, $"{i:D6}.ts");
            await using var input = new FileStream(segmentPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await outputStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (i == 0 || i == segmentCount - 1 || (segmentCount >= 50 && (i + 1) % 50 == 0) || (segmentCount < 50 && (i + 1) % 20 == 0))
            {
                statusCallback?.Invoke($"[HLS] 合并: {i + 1}/{segmentCount}");
            }
        }

        await outputStream.FlushAsync(cancellationToken);
    }

    private static async Task RunSpeedReporterAsync(Func<long> consumeBytes, Action<long>? speedCallback, CancellationToken cancellationToken)
    {
        if (speedCallback == null)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        long last = -1;
        var lastEmit = DateTimeOffset.MinValue;
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var speed = consumeBytes();
            var now = DateTimeOffset.UtcNow;

            if (speed == 0)
            {
                continue;
            }

            var shouldEmit = false;
            if (last <= 0)
            {
                shouldEmit = true;
            }
            else
            {
                var delta = Math.Abs(speed - last);
                shouldEmit = delta >= Math.Max(256 * 1024, last / 5) || (now - lastEmit) >= TimeSpan.FromSeconds(2);
            }

            if (!shouldEmit)
            {
                continue;
            }

            last = speed;
            lastEmit = now;
            speedCallback(speed);
        }
    }

    private HttpRequestMessage CreateRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // 设置浏览器模拟请求头 - 使用与页面请求相同的配置
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.8,zh-TW;q=0.7,zh-HK;q=0.5,en-US;q=0.3,en;q=0.2");
        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        request.Headers.Add("Connection", "keep-alive");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Add("Sec-Fetch-Site", "none");
        request.Headers.Add("Sec-Fetch-User", "?1");

        // 设置Referer
        var referrer = GuessReferrer(uri);
        if (referrer != null)
        {
            request.Headers.Add("Referer", referrer.ToString());
        }

        // 添加Cookie（如果已设置）
        if (!string.IsNullOrEmpty(_cookieString))
        {
            request.Headers.Add("Cookie", _cookieString);
        }

        return request;
    }

    private static Uri? GuessReferrer(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();

        // 快手
        if (host.Contains("yximgs") || host.Contains("kuaishou"))
        {
            return new Uri("https://live.kuaishou.com/");
        }

        // Bilibili - 包括所有CDN域名
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

    private static long? TryParseLongAttribute(string line, string name)
    {
        var value = TryParseStringAttribute(line, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }

    private static string? TryParseStringAttribute(string line, string name)
    {
        var match = Regex.Match(line, $"{Regex.Escape(name)}=([^,\\s]+)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.Trim().Trim('"');
    }
}
