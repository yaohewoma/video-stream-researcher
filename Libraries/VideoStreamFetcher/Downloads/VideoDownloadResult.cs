namespace VideoStreamFetcher.Downloads;

public sealed class VideoDownloadResult
{
    public required bool Success { get; init; }
    public required string OutputDirectory { get; init; }
    public string? VideoPath { get; init; }
    public string? AudioPath { get; init; }
    public string? OutputPath { get; init; }
    public long BytesWritten { get; init; }
    public string? Message { get; init; }
}
