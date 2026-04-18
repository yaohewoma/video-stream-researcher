using System;

namespace VideoStreamFetcher.Remux;

internal ref struct BitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _byteIndex;
    private int _bitIndex;

    public BitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
    }

    public uint ReadBits(int count)
    {
        if (count <= 0 || count > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        uint value = 0;
        for (var i = 0; i < count; i++)
        {
            value = (value << 1) | ReadBit();
        }
        return value;
    }

    public uint ReadBit()
    {
        if (_byteIndex >= _data.Length)
        {
            return 0;
        }

        var b = _data[_byteIndex];
        var bit = (uint)((b >> (7 - _bitIndex)) & 1);
        _bitIndex++;
        if (_bitIndex == 8)
        {
            _bitIndex = 0;
            _byteIndex++;
        }

        return bit;
    }

    public uint ReadUE()
    {
        var zeros = 0;
        while (ReadBit() == 0 && zeros < 32)
        {
            zeros++;
        }

        if (zeros == 0)
        {
            return 0;
        }

        var suffix = ReadBits(zeros);
        return ((uint)1 << zeros) - 1 + suffix;
    }

    public int ReadSE()
    {
        var ue = ReadUE();
        var val = (int)((ue + 1) >> 1);
        return (ue & 1) == 0 ? -val : val;
    }
}
