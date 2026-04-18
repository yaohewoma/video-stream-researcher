using System;

namespace VideoStreamFetcher.Remux;

internal static class H264SpsParser
{
    public static bool TryParseDimensions(ReadOnlySpan<byte> spsNal, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (spsNal.Length < 4)
        {
            return false;
        }

        var nalType = spsNal[0] & 0x1F;
        if (nalType != 7)
        {
            return false;
        }

        var rbsp = RemoveEmulationPreventionBytes(spsNal.Slice(1));
        var br = new BitReader(rbsp);

        var profileIdc = (byte)br.ReadBits(8);
        br.ReadBits(8);
        br.ReadBits(8);
        br.ReadUE();
        var chromaFormatIdc = 1u;

        if (profileIdc is 100 or 110 or 122 or 244 or 44 or 83 or 86 or 118 or 128 or 138 or 139 or 134 or 135)
        {
            chromaFormatIdc = br.ReadUE();
            if (chromaFormatIdc == 3)
            {
                br.ReadBit();
            }

            br.ReadUE();
            br.ReadUE();
            br.ReadBit();

            var seqScalingMatrixPresent = br.ReadBit();
            if (seqScalingMatrixPresent == 1)
            {
                var count = chromaFormatIdc != 3 ? 8 : 12;
                for (var i = 0; i < count; i++)
                {
                    var present = br.ReadBit();
                    if (present == 1)
                    {
                        SkipScalingList(br, i < 6 ? 16 : 64);
                    }
                }
            }
        }

        br.ReadUE();
        var picOrderCntType = br.ReadUE();
        if (picOrderCntType == 0)
        {
            br.ReadUE();
        }
        else if (picOrderCntType == 1)
        {
            br.ReadBit();
            br.ReadSE();
            br.ReadSE();
            var num = br.ReadUE();
            for (var i = 0; i < num; i++)
            {
                br.ReadSE();
            }
        }

        br.ReadUE();
        br.ReadBit();

        var picWidthInMbsMinus1 = br.ReadUE();
        var picHeightInMapUnitsMinus1 = br.ReadUE();
        var frameMbsOnlyFlag = br.ReadBit();
        if (frameMbsOnlyFlag == 0)
        {
            br.ReadBit();
        }

        br.ReadBit();

        var frameCroppingFlag = br.ReadBit();
        uint cropLeft = 0;
        uint cropRight = 0;
        uint cropTop = 0;
        uint cropBottom = 0;
        if (frameCroppingFlag == 1)
        {
            cropLeft = br.ReadUE();
            cropRight = br.ReadUE();
            cropTop = br.ReadUE();
            cropBottom = br.ReadUE();
        }

        var widthInMbs = (int)(picWidthInMbsMinus1 + 1);
        var heightInMapUnits = (int)(picHeightInMapUnitsMinus1 + 1);
        var frameHeightInMbs = (2 - (int)frameMbsOnlyFlag) * heightInMapUnits;

        width = widthInMbs * 16;
        height = frameHeightInMbs * 16;

        var cropUnitX = 1;
        var cropUnitY = 2 - (int)frameMbsOnlyFlag;

        if (profileIdc is 100 or 110 or 122 or 244 or 44 or 83 or 86 or 118 or 128 or 138 or 139 or 134 or 135)
        {
            if (chromaFormatIdc == 1)
            {
                cropUnitX = 2;
                cropUnitY *= 2;
            }
            else if (chromaFormatIdc == 2)
            {
                cropUnitX = 2;
            }
        }

        width -= (int)((cropLeft + cropRight) * (uint)cropUnitX);
        height -= (int)((cropTop + cropBottom) * (uint)cropUnitY);

        return width > 0 && height > 0;
    }

    private static void SkipScalingList(BitReader br, int size)
    {
        var lastScale = 8;
        var nextScale = 8;
        for (var i = 0; i < size; i++)
        {
            if (nextScale != 0)
            {
                var deltaScale = br.ReadSE();
                nextScale = (lastScale + deltaScale + 256) % 256;
            }

            lastScale = nextScale == 0 ? lastScale : nextScale;
        }
    }

    private static byte[] RemoveEmulationPreventionBytes(ReadOnlySpan<byte> data)
    {
        var output = new byte[data.Length];
        var count = 0;
        for (var i = 0; i < data.Length; i++)
        {
            if (i + 2 < data.Length && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 3)
            {
                output[count++] = data[i];
                output[count++] = data[i + 1];
                i += 2;
                continue;
            }

            output[count++] = data[i];
        }

        return output.AsSpan(0, count).ToArray();
    }
}
