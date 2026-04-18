using System;

namespace VideoStreamFetcher.Remux;

internal sealed class AdtsReader
{
    private static readonly int[] SampleRates =
    {
        96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000, 12000, 11025, 8000, 7350
    };

    public static bool TryReadFrame(ReadOnlySpan<byte> data, out int headerSize, out int frameSize, out int sampleRate, out int channels)
    {
        headerSize = 0;
        frameSize = 0;
        sampleRate = 0;
        channels = 0;

        if (data.Length < 7)
        {
            return false;
        }

        if (data[0] != 0xFF || (data[1] & 0xF0) != 0xF0)
        {
            return false;
        }

        var protectionAbsent = (data[1] & 0x01) == 1;
        var samplingFrequencyIndex = (data[2] >> 2) & 0x0F;
        if (samplingFrequencyIndex == 0x0F)
        {
            return false;
        }

        sampleRate = samplingFrequencyIndex < SampleRates.Length ? SampleRates[samplingFrequencyIndex] : 44100;

        var channelConfig = ((data[2] & 0x01) << 2) | ((data[3] >> 6) & 0x03);
        channels = channelConfig == 0 ? 2 : channelConfig;

        frameSize = ((data[3] & 0x03) << 11) | (data[4] << 3) | ((data[5] >> 5) & 0x07);
        headerSize = protectionAbsent ? 7 : 9;

        if (frameSize <= headerSize || frameSize > data.Length)
        {
            return false;
        }

        return true;
    }

    public static bool TryReadFrame(byte[] data, int offset, int length, out int headerSize, out int frameSize, out int sampleRate, out int channels)
    {
        return TryReadFrame(new ReadOnlySpan<byte>(data, offset, length), out headerSize, out frameSize, out sampleRate, out channels);
    }
}
