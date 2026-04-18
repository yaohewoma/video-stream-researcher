namespace VideoStreamFetcher.Downloads;

public sealed class VideoDownloadOptions
{
    public bool AudioOnly { get; init; }
    public bool VideoOnly { get; init; }
    public bool NoMerge { get; init; }
    public bool ConvertToNonFragmentedMp4 { get; init; } = true;

    public bool CreateSubfolderInTargetDirectory { get; init; } = true;
    public string? OutputFolderName { get; init; }
    public string? OutputFileBaseName { get; init; }

    public int HlsMaxConcurrency { get; init; } = 8;
    public int? HlsMaxSegments { get; init; }
    public bool HlsPreviewEdit { get; init; }
    public bool HlsRemuxToMp4 { get; init; } = true;
    public bool HlsKeepTsFile { get; init; }
    public int HttpRetryCount { get; init; } = 3;
}
