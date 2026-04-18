using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace VideoPreviewer.Preview;

public static class PreviewSegmentMerger
{
    public static int FindSegmentIndex(List<double> durations, double startSeconds)
    {
        var cum = 0d;
        for (var i = 0; i < durations.Count; i++)
        {
            var d = Math.Max(0, durations[i]);
            var next = cum + d;
            if (next > startSeconds)
            {
                return i;
            }
            cum = next;
        }
        return durations.Count - 1;
    }

    public static int FindSegmentEndIndex(List<double> durations, double endSeconds)
    {
        var cum = 0d;
        for (var i = 0; i < durations.Count; i++)
        {
            if (cum >= endSeconds)
            {
                return i;
            }
            cum += Math.Max(0, durations[i]);
        }
        return durations.Count;
    }

    public static async Task MergeSegmentsRangeAsync(string segmentsDir, List<string> files, int startIndex, int endIndex, string outputTs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputTs) ?? ".");
        await using var output = new FileStream(outputTs, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);
        var buffer = new byte[1024 * 1024];
        for (var i = startIndex; i < endIndex && i < files.Count; i++)
        {
            var path = Path.Combine(segmentsDir, files[i]);
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await output.WriteAsync(buffer, 0, read);
            }
        }
        await output.FlushAsync();
    }
}
