namespace VideoStreamFetcher.Downloads;

internal sealed class HlsManifest
{
    public required Uri PlaylistUri { get; init; }
    public required List<HlsVariant> Variants { get; init; }
    public required List<HlsSegment> Segments { get; init; }
    public Uri? KeyUri { get; init; }
    public string? KeyMethod { get; init; }
    public byte[]? KeyIv { get; init; }
}

internal sealed class HlsVariant
{
    public required Uri PlaylistUri { get; init; }
    public long? Bandwidth { get; init; }
    public string? Resolution { get; init; }
}

internal sealed class HlsSegment
{
    public required Uri SegmentUri { get; init; }
    public double DurationSeconds { get; init; }
}
