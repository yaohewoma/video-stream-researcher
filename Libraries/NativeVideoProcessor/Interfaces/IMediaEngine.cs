namespace NativeVideoProcessor.Interfaces;

/// <summary>
/// 媒体引擎接口
/// </summary>
public interface IMediaEngine : IDisposable
{
    /// <summary>
    /// 获取媒体信息
    /// </summary>
    Task<MediaInfo> GetMediaInfoAsync(string filePath);

    /// <summary>
    /// 转码
    /// </summary>
    Task TranscodeAsync(string inputFile, string outputFile, TranscodeOptions options, IProgress<double> progress, CancellationToken cancellationToken = default);
}
