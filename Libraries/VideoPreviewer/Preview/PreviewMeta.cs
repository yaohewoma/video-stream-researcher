using System.Collections.Generic;

namespace VideoPreviewer.Preview;

public sealed class PreviewMeta
{
    public string SegmentsDir { get; set; } = string.Empty;
    public List<string> SegmentFiles { get; set; } = new List<string>();
    public List<double> Durations { get; set; } = new List<double>();
}
