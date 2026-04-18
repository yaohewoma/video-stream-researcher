using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mp4Merger.Core.Builders;
using Mp4Merger.Core.Models;
using Mp4Merger.Core.Utils;

namespace VideoStreamFetcher.Remux;

public static class TsToMp4Remuxer
{
    public static async Task RemuxAsync(string tsPath, string mp4Path, Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(tsPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        await using var output = new FileStream(mp4Path, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true);

        var ftyp = CreateFtyp();
        await output.WriteAsync(ftyp, cancellationToken);

        var mdatStart = output.Position;
        await WriteMdatHeaderAsync(output, cancellationToken);
        var mdatDataStart = output.Position;

        var context = new RemuxContext(output, statusCallback);
        await context.ProcessTsAsync(input, cancellationToken);

        var mdatEnd = output.Position;
        var mdatSize = (ulong)(mdatEnd - mdatStart);
        output.Position = mdatStart + 8;
        await WriteU64Async(output, mdatSize, cancellationToken);
        output.Position = mdatEnd;

        var moov = context.BuildMoovBoxBytes((uint)ftyp.Length, (uint)mdatStart, (uint)mdatDataStart);
        await output.WriteAsync(moov, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static byte[] CreateFtyp()
    {
    
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, true);
        bw.Write(BinaryPrimitives.ReverseEndianness((uint)0));
        bw.Write(Encoding.ASCII.GetBytes("ftyp"));
        bw.Write(Encoding.ASCII.GetBytes("isom"));
        bw.Write(BinaryPrimitives.ReverseEndianness((uint)0x200));
        bw.Write(Encoding.ASCII.GetBytes("isom"));
        bw.Write(Encoding.ASCII.GetBytes("iso2"));
        bw.Write(Encoding.ASCII.GetBytes("avc1"));
        bw.Write(Encoding.ASCII.GetBytes("mp41"));
        bw.Flush();
        var bytes = ms.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), (uint)bytes.Length);
        return bytes;
    }

    private static async Task WriteMdatHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 1u);
        Encoding.ASCII.GetBytes("mdat", header.AsSpan(4, 4));
        await stream.WriteAsync(header, cancellationToken);
    }

    private static async Task WriteU64Async(Stream stream, ulong value, CancellationToken cancellationToken)
    {
        var b = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(b, value);
        await stream.WriteAsync(b, cancellationToken);
    }

    private sealed class RemuxContext
    {
        private readonly Stream _output;
        private readonly Action<string>? _status;

        private ushort? _pmtPid;
        private ushort? _videoPid;
        private ushort? _audioPid;

        private readonly PesAssembler _videoAssembler = new();
        private readonly PesAssembler _audioAssembler = new();

        private readonly List<VideoSampleMeta> _videoSamples = new();
        private readonly List<AudioSampleMeta> _audioSamples = new();

        private PendingVideoPes? _pendingVideoPes;
        private long? _lastVideoDelta90k;

        private byte[]? _videoSampleForConfig;
        private byte[]? _sps;
        private byte[]? _pps;
        private int _videoWidth = 1920;
        private int _videoHeight = 1080;

        private int _audioSampleRate = 44100;
        private int _audioChannels = 2;

        public RemuxContext(Stream output, Action<string>? statusCallback)
        {
            _output = output;
            _status = statusCallback;
        }

        public async Task ProcessTsAsync(Stream tsStream, CancellationToken cancellationToken)
        {
            var packet = new byte[188];
            var packetIndex = 0L;

            while (true)
            {
                var read = await ReadExactlyAsync(tsStream, packet, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                packetIndex++;
                if (read != 188)
                {
                    break;
                }

                if (packet[0] != 0x47)
                {
                    continue;
                }

                var pusi = (packet[1] & 0x40) != 0;
                var pid = (ushort)(((packet[1] & 0x1F) << 8) | packet[2]);
                var afc = (packet[3] >> 4) & 0x03;

                var payloadIndex = 4;
                if (afc == 0 || afc == 2)
                {
                    continue;
                }

                if (afc == 3)
                {
                    var afl = packet[4];
                    payloadIndex = 5 + afl;
                    if (payloadIndex >= 188)
                    {
                        continue;
                    }
                }

                var payloadLength = 188 - payloadIndex;

                if (pid == 0)
                {
                    if (pusi)
                    {
                        ParsePat(packet, payloadIndex, payloadLength);
                    }

                    continue;
                }

                if (_pmtPid.HasValue && pid == _pmtPid.Value)
                {
                    if (pusi)
                    {
                        ParsePmt(packet, payloadIndex, payloadLength);
                    }

                    continue;
                }

                if (_videoPid.HasValue && pid == _videoPid.Value)
                {
                    if (pusi)
                    {
                        var completed = _videoAssembler.StartNew(packet, payloadIndex, payloadLength, out var pts90k, out var dts90k);
                        if (completed != null)
                        {
                            await OnVideoPesCompletedAsync(pts90k, dts90k, completed, cancellationToken);
                        }
                    }
                    else
                    {
                        _videoAssembler.Append(packet, payloadIndex, payloadLength);
                    }

                    continue;
                }

                if (_audioPid.HasValue && pid == _audioPid.Value)
                {
                    if (pusi)
                    {
                        var completed = _audioAssembler.StartNew(packet, payloadIndex, payloadLength, out var pts90k, out _);
                        if (completed != null)
                        {
                            await OnAudioPesCompletedAsync(pts90k, completed, cancellationToken);
                        }
                    }
                    else
                    {
                        _audioAssembler.Append(packet, payloadIndex, payloadLength);
                    }
                }
            }

            var lastVideo = _videoAssembler.Flush(out var lastVideoPts, out var lastVideoDts);
            if (lastVideo != null)
            {
                await OnVideoPesCompletedAsync(lastVideoPts, lastVideoDts, lastVideo, cancellationToken);
            }

            var lastAudio = _audioAssembler.Flush(out var lastAudioPts, out _);
            if (lastAudio != null)
            {
                await OnAudioPesCompletedAsync(lastAudioPts, lastAudio, cancellationToken);
            }

            await FlushPendingVideoAsync(null, null, cancellationToken);
        }

        private void ParsePat(byte[] packet, int payloadIndex, int payloadLength)
        {
            var payload = new ReadOnlySpan<byte>(packet, payloadIndex, payloadLength);
            if (payload.Length < 8)
            {
                return;
            }

            var pointer = payload[0];
            if (1 + pointer >= payload.Length)
            {
                return;
            }

            var section = payload.Slice(1 + pointer);
            if (section.Length < 8 || section[0] != 0x00)
            {
                return;
            }

            var sectionLength = ((section[1] & 0x0F) << 8) | section[2];
            if (sectionLength + 3 > section.Length)
            {
                return;
            }

            var programInfoStart = 8;
            var programInfoEnd = 3 + sectionLength - 4;
            for (var i = programInfoStart; i + 4 <= programInfoEnd; i += 4)
            {
                var programNumber = (ushort)((section[i] << 8) | section[i + 1]);
                var programPid = (ushort)(((section[i + 2] & 0x1F) << 8) | section[i + 3]);
                if (programNumber != 0)
                {
                    if (_pmtPid != programPid)
                    {
                        _pmtPid = programPid;
                        _status?.Invoke($"[REMUX] TS PID: PMT={programPid}");
                    }
                    return;
                }
            }
        }

        private void ParsePmt(byte[] packet, int payloadIndex, int payloadLength)
        {
            var payload = new ReadOnlySpan<byte>(packet, payloadIndex, payloadLength);
            if (payload.Length < 12)
            {
                return;
            }

            var pointer = payload[0];
            if (1 + pointer >= payload.Length)
            {
                return;
            }

            var section = payload.Slice(1 + pointer);
            if (section.Length < 12 || section[0] != 0x02)
            {
                return;
            }

            var sectionLength = ((section[1] & 0x0F) << 8) | section[2];
            if (sectionLength + 3 > section.Length)
            {
                return;
            }

            var programInfoLength = ((section[10] & 0x0F) << 8) | section[11];
            var i = 12 + programInfoLength;
            var end = 3 + sectionLength - 4;

            while (i + 5 <= end)
            {
                var streamType = section[i];
                var elementaryPid = (ushort)(((section[i + 1] & 0x1F) << 8) | section[i + 2]);
                var esInfoLength = ((section[i + 3] & 0x0F) << 8) | section[i + 4];

                if (streamType == 0x1B)
                {
                    if (_videoPid != elementaryPid)
                    {
                        _videoPid = elementaryPid;
                        _status?.Invoke($"[REMUX] TS PID: H264={elementaryPid}");
                    }
                }
                else if (streamType == 0x0F)
                {
                    if (_audioPid != elementaryPid)
                    {
                        _audioPid = elementaryPid;
                        _status?.Invoke($"[REMUX] TS PID: AAC={elementaryPid}");
                    }
                }

                i += 5 + esInfoLength;
            }
        }

        private async Task OnVideoPesCompletedAsync(long? pts90k, long? dts90k, byte[] payload, CancellationToken cancellationToken)
        {
            var samples = AnnexBAccessUnitParser.ExtractSamples(payload, out var sps, out var pps);
            if (samples.Count == 0)
            {
                return;
            }

            if (_sps == null && sps != null)
            {
                _sps = sps;
                if (H264SpsParser.TryParseDimensions(sps, out var w, out var h))
                {
                    _videoWidth = w;
                    _videoHeight = h;
                }
            }

            if (_pps == null && pps != null)
            {
                _pps = pps;
            }

            var pes = new PendingVideoPes
            {
                Pts90k = pts90k,
                Dts90k = dts90k,
                Samples = samples
            };

            if (_pendingVideoPes != null)
            {
                await FlushPendingVideoAsync(pes.Pts90k, pes.Dts90k, cancellationToken);
            }

            _pendingVideoPes = pes;
        }

        private async Task FlushPendingVideoAsync(long? nextPts90k, long? nextDts90k, CancellationToken cancellationToken)
        {
            if (_pendingVideoPes == null)
            {
                return;
            }

            var pending = _pendingVideoPes;
            _pendingVideoPes = null;

            var baseDts = pending.Dts90k ?? pending.Pts90k ?? (nextDts90k ?? nextPts90k ?? 0);
            var basePts = pending.Pts90k ?? pending.Dts90k ?? (nextPts90k ?? nextDts90k ?? 0);

            var delta = nextDts90k.HasValue && pending.Dts90k.HasValue ? nextDts90k.Value - pending.Dts90k.Value :
                        (nextPts90k.HasValue && pending.Pts90k.HasValue ? nextPts90k.Value - pending.Pts90k.Value : (long?)null);

            if (delta.HasValue && delta.Value > 0)
            {
                _lastVideoDelta90k = delta.Value;
            }

            var per = delta.HasValue && delta.Value > 0
                ? Math.Max(1, delta.Value / Math.Max(1, pending.Samples.Count))
                : Math.Max(1, _lastVideoDelta90k ?? 3000);

            for (var i = 0; i < pending.Samples.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sample = pending.Samples[i];
                var dts = baseDts + i * per;
                var pts = basePts + i * per;
                await WriteVideoSampleAsync(sample.Data, sample.IsKeyframe, pts, dts, cancellationToken);
            }
        }

        private async Task WriteVideoSampleAsync(byte[] avccSample, bool isKeyframe, long pts90k, long dts90k, CancellationToken cancellationToken)
        {
            if (_videoSampleForConfig == null)
            {
                _videoSampleForConfig = avccSample;
            }

            var offset = (uint)_output.Position;
            await _output.WriteAsync(avccSample, cancellationToken);

            _videoSamples.Add(new VideoSampleMeta
            {
                Offset = offset,
                Size = (uint)avccSample.Length,
                Dts90k = dts90k,
                CtsOffset90k = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, pts90k - dts90k)),
                IsKeyframe = isKeyframe
            });
        }

        private async Task OnAudioPesCompletedAsync(long? pts90k, byte[] payload, CancellationToken cancellationToken)
        {
            var index = 0;
            while (index + 7 <= payload.Length)
            {
                if (!AdtsReader.TryReadFrame(payload, index, payload.Length - index, out var headerSize, out var frameSize, out var sr, out var ch))
                {
                    index++;
                    continue;
                }

                _audioSampleRate = sr;
                _audioChannels = ch;

                var aacData = new byte[frameSize - headerSize];
                Buffer.BlockCopy(payload, index + headerSize, aacData, 0, aacData.Length);
                var offset = (uint)_output.Position;
                await _output.WriteAsync(aacData, cancellationToken);

                _audioSamples.Add(new AudioSampleMeta
                {
                    Offset = offset,
                    Size = (uint)aacData.Length
                });

                index += frameSize;
            }
        }

        public byte[] BuildMoovBoxBytes(uint ftypSize, uint mdatStart, uint mdatDataStart)
        {
            if (_videoSamples.Count == 0)
            {
                throw new InvalidOperationException("未提取到视频样本，无法转封装");
            }

            var videoDeltas = BuildVideoDeltas();
            var videoCtts = CreateCtts(_videoSamples.Select(s => s.CtsOffset90k).ToList());
            var audioSampleCount = _audioSamples.Count;
            if (audioSampleCount == 0)
            {
                _status?.Invoke("TS: 未检测到 AAC 音频，将生成无音频的 MP4");
            }

            var videoDuration90k = videoDeltas.Sum(x => (long)x);
            var audioDuration = audioSampleCount > 0 ? audioSampleCount * 1024L : 0L;

            var movieTimeScale = 1000u;
            var videoDurationMs = (ulong)(videoDuration90k * movieTimeScale / 90000);
            var audioDurationMs = audioSampleCount > 0 ? (ulong)(audioDuration * movieTimeScale / _audioSampleRate) : 0UL;
            var movieDuration = (uint)Math.Min(uint.MaxValue, Math.Max(videoDurationMs, audioDurationMs));

            var fileInfoVideo = new MP4FileInfo
            {
                VideoWidth = _videoWidth,
                VideoHeight = _videoHeight,
                VideoTimeScale = 90000,
                VideoDuration = (uint)Math.Min(uint.MaxValue, (ulong)videoDuration90k)
            };

            var config = _videoSampleForConfig != null ? H264ConfigExtractor.ExtractConfigFromSample(_videoSampleForConfig) : null;
            if (config != null)
            {
                fileInfoVideo.GeneratedAvcCBox = H264ConfigExtractor.CreateAvcCBox(config);
            }
            else if (_sps != null && _pps != null)
            {
                var cfg = new H264ConfigExtractor.H264Config();
                cfg.SPSList.Add(_sps);
                cfg.PPSList.Add(_pps);
                if (_sps.Length >= 4)
                {
                    cfg.AVCProfileIndication = _sps[1];
                    cfg.ProfileCompatibility = _sps[2];
                    cfg.AVCLevelIndication = _sps[3];
                }
                fileInfoVideo.GeneratedAvcCBox = H264ConfigExtractor.CreateAvcCBox(cfg);
            }

            var fileInfoAudio = new MP4FileInfo
            {
                AudioTimeScale = (uint)_audioSampleRate,
                AudioChannels = _audioChannels,
                AudioSampleRate = (uint)_audioSampleRate,
                AudioDuration = audioSampleCount > 0 ? (uint)Math.Min(uint.MaxValue, (ulong)audioDuration) : 0u
            };

            using var moov = new MemoryStream();
            using var moovPayload = new MemoryStream();

            var mvhd = CreateMvhd(movieTimeScale, movieDuration);
            moovPayload.Write(mvhd, 0, mvhd.Length);

            var trakVideo = CreateTrak(
                trackId: 1,
                isVideo: true,
                movieTimeScale: movieTimeScale,
                movieDuration: movieDuration,
                mediaTimeScale: 90000,
                mediaDuration: (uint)Math.Min(uint.MaxValue, (ulong)videoDuration90k),
                width: _videoWidth,
                height: _videoHeight,
                stsd: VideoTrackBuilder.CreateVideoStsdBox(fileInfoVideo),
                stts: CreateSttsFromDeltas(videoDeltas),
                ctts: videoCtts,
                stss: CreateStss(_videoSamples),
                stsz: CreateStsz(_videoSamples.Select(s => s.Size).ToList()),
                stco: CreateStco(_videoSamples.Select(s => s.Offset).ToList()));

            moovPayload.Write(trakVideo, 0, trakVideo.Length);

            if (audioSampleCount > 0)
            {
                var trakAudio = CreateTrak(
                    trackId: 2,
                    isVideo: false,
                    movieTimeScale: movieTimeScale,
                    movieDuration: movieDuration,
                    mediaTimeScale: (uint)_audioSampleRate,
                    mediaDuration: (uint)Math.Min(uint.MaxValue, (ulong)audioDuration),
                    width: 0,
                    height: 0,
                    stsd: AudioTrackBuilder.CreateAudioStsdBox(fileInfoAudio),
                    stts: CreateSttsSingle((uint)audioSampleCount, 1024),
                    ctts: Array.Empty<byte>(),
                    stss: Array.Empty<byte>(),
                    stsz: CreateStsz(_audioSamples.Select(s => s.Size).ToList()),
                    stco: CreateStco(_audioSamples.Select(s => s.Offset).ToList()));

                moovPayload.Write(trakAudio, 0, trakAudio.Length);
            }

            var moovBytes = WrapBox("moov", moovPayload.ToArray());
            return moovBytes;
        }

        private List<uint> BuildVideoDeltas()
        {
            var dts = _videoSamples.Select(s => s.Dts90k).ToList();
            var deltas = new List<uint>(dts.Count);

            for (var i = 0; i < dts.Count; i++)
            {
                if (i == dts.Count - 1)
                {
                    var last = _lastVideoDelta90k ?? 3000;
                    deltas.Add((uint)Math.Max(1, Math.Min(uint.MaxValue, last)));
                    break;
                }

                var delta = dts[i + 1] - dts[i];
                if (delta <= 0)
                {
                    delta = _lastVideoDelta90k ?? 3000;
                }
                else
                {
                    _lastVideoDelta90k = delta;
                }

                deltas.Add((uint)Math.Max(1, Math.Min(uint.MaxValue, delta)));
            }

            return deltas;
        }

        private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
                if (read == 0)
                {
                    return offset;
                }

                offset += read;
            }

            return offset;
        }

        private sealed class VideoSampleMeta
        {
            public required uint Offset { get; init; }
            public required uint Size { get; init; }
            public required long Dts90k { get; init; }
            public required int CtsOffset90k { get; init; }
            public required bool IsKeyframe { get; init; }
        }

        private sealed class AudioSampleMeta
        {
            public required uint Offset { get; init; }
            public required uint Size { get; init; }
        }

        private sealed class PendingVideoPes
        {
            public required long? Pts90k { get; init; }
            public required long? Dts90k { get; init; }
            public required List<AnnexBAccessUnitParser.Sample> Samples { get; init; }
        }

        private static byte[] WrapBox(string type, byte[] payload)
        {
            var size = 8 + payload.Length;
            var box = new byte[size];
            BinaryPrimitives.WriteUInt32BigEndian(box.AsSpan(0, 4), (uint)size);
            Encoding.ASCII.GetBytes(type, box.AsSpan(4, 4));
            Buffer.BlockCopy(payload, 0, box, 8, payload.Length);
            return box;
        }

        private static byte[] CreateMvhd(uint timeScale, uint duration)
        {
            var payload = new byte[100];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 0u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), 0u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12, 4), timeScale);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16, 4), duration);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20, 4), 0x00010000u);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(24, 2), 0x0100);
            payload[26] = 0;
            payload[27] = 0;
            Array.Clear(payload, 28, 8);
            var matrix = new uint[]
            {
                0x00010000u, 0, 0,
                0, 0x00010000u, 0,
                0, 0, 0x40000000u
            };
            for (var i = 0; i < 9; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(36 + i * 4, 4), matrix[i]);
            }
            Array.Clear(payload, 72, 24);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(96, 4), 3u);
            return WrapBox("mvhd", payload);
        }

        private static byte[] CreateTrak(
            uint trackId,
            bool isVideo,
            uint movieTimeScale,
            uint movieDuration,
            uint mediaTimeScale,
            uint mediaDuration,
            int width,
            int height,
            byte[] stsd,
            byte[] stts,
            byte[] ctts,
            byte[] stss,
            byte[] stsz,
            byte[] stco)
        {
            var tkhd = CreateTkhd(trackId, movieDuration, isVideo, width, height);
            var mdia = CreateMdia(isVideo, mediaTimeScale, mediaDuration, stsd, stts, ctts, stss, stsz, stco);
            return WrapBox("trak", Concat(tkhd, mdia));
        }

        private static byte[] CreateTkhd(uint trackId, uint duration, bool isVideo, int width, int height)
        {
            var payload = new byte[84];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0x07;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 0u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), 0u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12, 4), trackId);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16, 4), 0u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20, 4), duration);
            Array.Clear(payload, 24, 8);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(32, 2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(34, 2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(36, 2), (ushort)(isVideo ? 0 : 0x0100));
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(38, 2), 0);
            var matrix = new uint[]
            {
                0x00010000u, 0, 0,
                0, 0x00010000u, 0,
                0, 0, 0x40000000u
            };
            for (var i = 0; i < 9; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(40 + i * 4, 4), matrix[i]);
            }
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(76, 4), (uint)(width << 16));
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(80, 4), (uint)(height << 16));
            return WrapBox("tkhd", payload);
        }

        private static byte[] CreateMdia(
            bool isVideo,
            uint mediaTimeScale,
            uint mediaDuration,
            byte[] stsd,
            byte[] stts,
            byte[] ctts,
            byte[] stss,
            byte[] stsz,
            byte[] stco)
        {
            var mdhd = CreateMdhd(mediaTimeScale, mediaDuration);
            var hdlr = CreateHdlr(isVideo);
            var minf = CreateMinf(isVideo, stsd, stts, ctts, stss, stsz, stco);
            return WrapBox("mdia", Concat(mdhd, hdlr, minf));
        }

        private static byte[] CreateMdhd(uint timeScale, uint duration)
        {
            var payload = new byte[24];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 0u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), 0u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12, 4), timeScale);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16, 4), duration);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(20, 2), 0x55C4);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(22, 2), 0);
            return WrapBox("mdhd", payload);
        }

        private static byte[] CreateHdlr(bool isVideo)
        {
            using var ms = new MemoryStream();
            WriteU32(ms, 0u);
            WriteU32(ms, 0u);
            ms.Write(Encoding.ASCII.GetBytes(isVideo ? "vide" : "soun"));
            ms.Write(new byte[12]);
            ms.Write(Encoding.ASCII.GetBytes(isVideo ? "VideoHandler\0" : "SoundHandler\0"));
            return WrapBox("hdlr", ms.ToArray());
        }

        private static byte[] CreateMinf(bool isVideo, byte[] stsd, byte[] stts, byte[] ctts, byte[] stss, byte[] stsz, byte[] stco)
        {
            var mediaHeader = isVideo ? CreateVmhd() : CreateSmhd();
            var dinf = CreateDinf();
            var stbl = CreateStbl(isVideo, stsd, stts, ctts, stss, stsz, stco);
            return WrapBox("minf", Concat(mediaHeader, dinf, stbl));
        }

        private static byte[] CreateVmhd()
        {
            var payload = new byte[12];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 1;
            Array.Clear(payload, 4, 8);
            return WrapBox("vmhd", payload);
        }

        private static byte[] CreateSmhd()
        {
            var payload = new byte[8];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(4, 2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(6, 2), 0);
            return WrapBox("smhd", payload);
        }

        private static byte[] CreateDinf()
        {
            var url = new byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(url.AsSpan(0, 4), 12u);
            Encoding.ASCII.GetBytes("url ", url.AsSpan(4, 4));
            url[8] = 0;
            url[9] = 0;
            url[10] = 0;
            url[11] = 1;

            var drefPayload = new byte[8 + url.Length];
            drefPayload[0] = 0;
            drefPayload[1] = 0;
            drefPayload[2] = 0;
            drefPayload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(drefPayload.AsSpan(4, 4), 1u);
            Buffer.BlockCopy(url, 0, drefPayload, 8, url.Length);
            var dref = WrapBox("dref", drefPayload);
            return WrapBox("dinf", dref);
        }

        private static byte[] CreateStbl(bool isVideo, byte[] stsd, byte[] stts, byte[] ctts, byte[] stss, byte[] stsz, byte[] stco)
        {
            var stsc = CreateStsc();
            var parts = new List<byte[]> { stsd, stts };
            if (ctts.Length > 0)
            {
                parts.Add(ctts);
            }

            if (isVideo && stss.Length > 0)
            {
                parts.Add(stss);
            }
            parts.Add(stsc);
            parts.Add(stsz);
            parts.Add(stco);
            return WrapBox("stbl", Concat(parts.ToArray()));
        }

        private static byte[] CreateStsc()
        {
            var entry = new byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(0, 4), 1u);
            BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(4, 4), 1u);
            BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(8, 4), 1u);
            var payload = new byte[8];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 1u);
            return WrapBox("stsc", Concat(payload, entry));
        }

        private static byte[] CreateSttsSingle(uint sampleCount, uint sampleDelta)
        {
            var payload = new byte[16];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 1u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), sampleCount);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12, 4), sampleDelta);
            return WrapBox("stts", payload);
        }

        private static byte[] CreateSttsFromDeltas(List<uint> deltas)
        {
            var entries = new List<(uint Count, uint Delta)>();
            uint currentCount = 0;
            uint currentDelta = 0;
            foreach (var d in deltas)
            {
                if (currentCount == 0)
                {
                    currentDelta = d;
                    currentCount = 1;
                    continue;
                }

                if (d == currentDelta)
                {
                    currentCount++;
                }
                else
                {
                    entries.Add((currentCount, currentDelta));
                    currentDelta = d;
                    currentCount = 1;
                }
            }
            if (currentCount > 0)
            {
                entries.Add((currentCount, currentDelta));
            }

            var payload = new byte[8 + entries.Count * 8];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), (uint)entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var (count, delta) = entries[i];
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8 + i * 8, 4), count);
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12 + i * 8, 4), delta);
            }
            return WrapBox("stts", payload);
        }

        private static byte[] CreateCtts(List<int> offsets)
        {
            if (offsets.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var anyNonZero = false;
            var anyNegative = false;
            for (var i = 0; i < offsets.Count; i++)
            {
                var v = offsets[i];
                if (v != 0)
                {
                    anyNonZero = true;
                }
                if (v < 0)
                {
                    anyNegative = true;
                }
            }

            if (!anyNonZero)
            {
                return Array.Empty<byte>();
            }

            var entries = new List<(uint Count, int Offset)>();
            uint currentCount = 0;
            var currentOffset = 0;
            foreach (var o in offsets)
            {
                if (currentCount == 0)
                {
                    currentOffset = o;
                    currentCount = 1;
                    continue;
                }

                if (o == currentOffset)
                {
                    currentCount++;
                }
                else
                {
                    entries.Add((currentCount, currentOffset));
                    currentOffset = o;
                    currentCount = 1;
                }
            }

            if (currentCount > 0)
            {
                entries.Add((currentCount, currentOffset));
            }

            var payload = new byte[8 + entries.Count * 8];
            payload[0] = anyNegative ? (byte)1 : (byte)0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), (uint)entries.Count);

            for (var i = 0; i < entries.Count; i++)
            {
                var (count, offset) = entries[i];
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8 + i * 8, 4), count);
                if (anyNegative)
                {
                    BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(12 + i * 8, 4), offset);
                }
                else
                {
                    BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12 + i * 8, 4), (uint)offset);
                }
            }

            return WrapBox("ctts", payload);
        }

        private static byte[] CreateStss(List<VideoSampleMeta> samples)
        {
            var keyIndexes = samples
                .Select((s, idx) => (s, idx))
                .Where(x => x.s.IsKeyframe)
                .Select(x => (uint)(x.idx + 1))
                .ToList();

            if (keyIndexes.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var payload = new byte[8 + keyIndexes.Count * 4];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), (uint)keyIndexes.Count);
            for (var i = 0; i < keyIndexes.Count; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8 + i * 4, 4), keyIndexes[i]);
            }
            return WrapBox("stss", payload);
        }

        private static byte[] CreateStsz(List<uint> sizes)
        {
            var payload = new byte[12 + sizes.Count * 4];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), 0u);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8, 4), (uint)sizes.Count);
            for (var i = 0; i < sizes.Count; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12 + i * 4, 4), sizes[i]);
            }
            return WrapBox("stsz", payload);
        }

        private static byte[] CreateStco(List<uint> offsets)
        {
            var payload = new byte[8 + offsets.Count * 4];
            payload[0] = 0;
            payload[1] = 0;
            payload[2] = 0;
            payload[3] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), (uint)offsets.Count);
            for (var i = 0; i < offsets.Count; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8 + i * 4, 4), offsets[i]);
            }
            return WrapBox("stco", payload);
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var len = parts.Sum(p => p.Length);
            var buf = new byte[len];
            var offset = 0;
            foreach (var p in parts)
            {
                Buffer.BlockCopy(p, 0, buf, offset, p.Length);
                offset += p.Length;
            }
            return buf;
        }

        private static void WriteU32(Stream stream, uint value)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(b, value);
            stream.Write(b);
        }
    }

    private sealed class PesAssembler
    {
        private readonly MemoryStream _buffer = new MemoryStream();
        private long? _pts90k;
        private long? _dts90k;
        private bool _active;

        public byte[]? StartNew(byte[] packet, int payloadIndex, int payloadLength, out long? pts90k, out long? dts90k)
        {
            var payload = new ReadOnlySpan<byte>(packet, payloadIndex, payloadLength);
            pts90k = null;
            dts90k = null;
            var completed = _active ? _buffer.ToArray() : null;
            var completedPts = _pts90k;
            var completedDts = _dts90k;

            _buffer.SetLength(0);
            _pts90k = null;
            _dts90k = null;
            _active = true;

            if (payload.Length < 6)
            {
                pts90k = completedPts;
                dts90k = completedDts;
                return completed;
            }

            var idx = 0;
            if (payload.Length >= 3 && payload[0] == 0x00 && payload[1] == 0x00 && payload[2] == 0x01)
            {
                idx = 3;
            }
            else
            {
                pts90k = completedPts;
                dts90k = completedDts;
                _buffer.Write(payload);
                return completed;
            }

            if (payload.Length < idx + 6)
            {
                pts90k = completedPts;
                dts90k = completedDts;
                return completed;
            }

            idx += 3;
            var flags = payload[idx + 1];
            var ptsDtsFlags = (flags >> 6) & 0x03;
            var headerLen = payload[idx + 2];
            var headerStart = idx + 3;
            long? pts = null;
            long? dts = null;

            if ((ptsDtsFlags == 2 || ptsDtsFlags == 3) && payload.Length >= headerStart + 5)
            {
                pts = ParsePts(payload.Slice(headerStart, 5));
            }

            if (ptsDtsFlags == 3 && payload.Length >= headerStart + 10)
            {
                dts = ParsePts(payload.Slice(headerStart + 5, 5));
            }

            _pts90k = pts;
            _dts90k = dts ?? pts;
            var payloadStart = headerStart + headerLen;
            if (payloadStart < payload.Length)
            {
                _buffer.Write(payload.Slice(payloadStart));
            }

            pts90k = completedPts;
            dts90k = completedDts;
            return completed;
        }

        public void Append(byte[] packet, int payloadIndex, int payloadLength)
        {
            var payload = new ReadOnlySpan<byte>(packet, payloadIndex, payloadLength);
            if (!_active || payload.Length == 0)
            {
                return;
            }

            _buffer.Write(payload);
        }

        public byte[]? Flush(out long? pts90k, out long? dts90k)
        {
            if (!_active)
            {
                pts90k = null;
                dts90k = null;
                return null;
            }

            pts90k = _pts90k;
            dts90k = _dts90k;
            _active = false;
            return _buffer.Length > 0 ? _buffer.ToArray() : null;
        }

        private static long ParsePts(ReadOnlySpan<byte> five)
        {
            var p = ((long)(five[0] & 0x0E) << 29) |
                    ((long)(five[1]) << 22) |
                    ((long)(five[2] & 0xFE) << 14) |
                    ((long)(five[3]) << 7) |
                    ((long)(five[4] & 0xFE) >> 1);
            return p;
        }
    }
}
