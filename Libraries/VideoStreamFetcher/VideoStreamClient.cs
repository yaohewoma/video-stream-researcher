using VideoStreamFetcher.Downloads;
using VideoStreamFetcher.Parsers;

namespace VideoStreamFetcher;

public sealed class VideoStreamClient : IDisposable
{
    private readonly VideoParser _parser;
    private readonly VideoDownloader _downloader;

    public VideoStreamClient()
    {
        _parser = new VideoParser();
        _downloader = new VideoDownloader();
    }

    public VideoStreamClient(VideoParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _downloader = new VideoDownloader();
    }

    public void SetCookieString(string cookieString)
    {
        _parser.SetCookieString(cookieString);
        _downloader.SetCookieString(cookieString);
    }

    public Task<VideoInfo?> ParseAsync(string url, Action<string> statusCallback, CancellationToken cancellationToken = default)
    {
        return _parser.ParseVideoInfo(url, statusCallback, cancellationToken);
    }

    public Task<VideoDownloadResult> DownloadAsync(
        VideoInfo videoInfo,
        string targetDirectory,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        VideoDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _downloader.DownloadAsync(videoInfo, targetDirectory, progressCallback, statusCallback, speedCallback, options, cancellationToken);
    }

    public async Task<VideoDownloadResult> ParseAndDownloadAsync(
        string url,
        string targetDirectory,
        Action<double>? progressCallback,
        Action<string>? statusCallback,
        Action<long>? speedCallback,
        VideoDownloadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var info = await _parser.ParseVideoInfo(url, statusCallback ?? (_ => { }), cancellationToken);
        if (info == null)
        {
            throw new InvalidOperationException("解析失败，未获得视频信息");
        }

        return await _downloader.DownloadAsync(info, targetDirectory, progressCallback, statusCallback, speedCallback, options, cancellationToken);
    }

    public void CancelDownload()
    {
        _downloader.Cancel();
    }

    public void Dispose()
    {
        _parser.Dispose();
        _downloader.Dispose();
    }
}
