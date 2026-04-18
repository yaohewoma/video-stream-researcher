using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoStreamFetcher.Remux;

internal static class AnnexBAccessUnitParser
{
    internal sealed class Sample
    {
        public required byte[] Data { get; init; }
        public required bool IsKeyframe { get; init; }
    }

    public static List<Sample> ExtractSamples(byte[] annexB, out byte[]? sps, out byte[]? pps)
    {
        sps = null;
        pps = null;

        if (annexB == null || annexB.Length < 4)
        {
            return new List<Sample>();
        }

        var nalUnits = SplitNalUnits(annexB);
        if (nalUnits.Count == 0)
        {
            return new List<Sample>();
        }

        var samples = new List<Sample>();
        var current = new List<byte[]>();
        var currentHasSlice = false;
        var currentKey = false;

        foreach (var nal in nalUnits)
        {
            if (nal.Length == 0)
            {
                continue;
            }

            var nalType = (byte)(nal[0] & 0x1F);

            if (nalType == 7 && sps == null)
            {
                sps = nal.ToArray();
            }
            else if (nalType == 8 && pps == null)
            {
                pps = nal.ToArray();
            }

            var isAud = nalType == 9;
            var isSlice = nalType == 1 || nalType == 5;
            var isKey = nalType == 5;

            var startNew = false;
            if (isAud)
            {
                startNew = true;
            }
            else if (isSlice && currentHasSlice)
            {
                startNew = true;
            }

            if (startNew && current.Count > 0)
            {
                samples.Add(BuildSample(current, currentKey));
                current.Clear();
                currentHasSlice = false;
                currentKey = false;
            }

            if (!isAud)
            {
                current.Add(nal);
            }

            if (isSlice)
            {
                currentHasSlice = true;
            }

            if (isKey)
            {
                currentKey = true;
            }
        }

        if (current.Count > 0)
        {
            samples.Add(BuildSample(current, currentKey));
        }

        return samples;
    }

    private static Sample BuildSample(List<byte[]> nalUnits, bool isKeyframe)
    {
        var total = nalUnits.Sum(n => 4 + n.Length);
        var buffer = new byte[total];
        var offset = 0;
        foreach (var nal in nalUnits)
        {
            var len = (uint)nal.Length;
            buffer[offset] = (byte)((len >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((len >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((len >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(len & 0xFF);
            offset += 4;
            Buffer.BlockCopy(nal, 0, buffer, offset, nal.Length);
            offset += nal.Length;
        }

        return new Sample { Data = buffer, IsKeyframe = isKeyframe };
    }

    private static List<byte[]> SplitNalUnits(byte[] data)
    {
        var units = new List<byte[]>();
        var i = FindStartCode(data, 0);
        while (i >= 0)
        {
            var start = i;
            var scLen = StartCodeLength(data, start);
            var nalStart = start + scLen;
            if (nalStart >= data.Length)
            {
                break;
            }

            var next = FindStartCode(data, nalStart);
            var nalEnd = next >= 0 ? next : data.Length;
            var len = nalEnd - nalStart;
            if (len > 0)
            {
                var nal = new byte[len];
                Buffer.BlockCopy(data, nalStart, nal, 0, len);
                TrimTrailingZerosInPlace(nal, out var trimmedLen);
                if (trimmedLen > 0)
                {
                    if (trimmedLen != nal.Length)
                    {
                        nal = nal.AsSpan(0, trimmedLen).ToArray();
                    }
                    units.Add(nal);
                }
            }

            i = next;
        }

        return units;
    }

    private static int FindStartCode(byte[] data, int start)
    {
        for (var i = start; i < data.Length - 3; i++)
        {
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1)
                {
                    return i;
                }
                if (data[i + 2] == 0 && data[i + 3] == 1)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static int StartCodeLength(byte[] data, int index)
    {
        if (index + 3 < data.Length && data[index] == 0 && data[index + 1] == 0 && data[index + 2] == 0 && data[index + 3] == 1)
        {
            return 4;
        }
        return 3;
    }

    private static void TrimTrailingZerosInPlace(byte[] data, out int trimmedLength)
    {
        var end = data.Length;
        while (end > 0 && data[end - 1] == 0)
        {
            end--;
        }

        trimmedLength = end;
    }
}

