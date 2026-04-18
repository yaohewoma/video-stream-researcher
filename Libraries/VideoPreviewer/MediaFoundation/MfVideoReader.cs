using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VideoPreviewer.MediaFoundation;

/// <summary>
/// Media Foundation 视频解码器
/// 支持 NV12、RGB32 等格式，自动处理硬件加速和格式转换
/// </summary>
public sealed class MfVideoReader : IDisposable
{
    private static int _startup;
    private static readonly int[] _yMul = BuildYMul();
    private static readonly int[] _rAddV = BuildRAddV();
    private static readonly int[] _gAddU = BuildGAddU();
    private static readonly int[] _gAddV = BuildGAddV();
    private static readonly int[] _bAddU = BuildBAddU();

    private readonly IMFSourceReader _reader;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private readonly int _outputStride;
    private readonly long _durationHns;
    private readonly Guid _subtype;
    private readonly int _frameRateNum;
    private readonly int _frameRateDen;
    private readonly bool _isHardwareAccelerated;
    private bool _disposed;

    public int Width => _width;
    public int Height => _height;
    public int Stride => _stride;
    public int OutputStride => _outputStride;
    public long DurationHns => _durationHns;
    public Guid Subtype => _subtype;
    public int FrameRateNum => _frameRateNum;
    public int FrameRateDen => _frameRateDen;
    public double FrameRate => _frameRateDen > 0 ? (double)_frameRateNum / _frameRateDen : 30.0;
    public bool IsHardwareAccelerated => _isHardwareAccelerated;
    
    /// <summary>
    /// 获取当前使用的视频格式名称
    /// </summary>
    public string FormatName
    {
        get
        {
            if (_subtype == MfInterop.MFVideoFormat_NV12) return "NV12";
            if (_subtype == MfInterop.MFVideoFormat_RGB32) return "RGB32";
            return _subtype.ToString();
        }
    }

    /// <summary>
    /// 创建视频解码器
    /// </summary>
    /// <param name="filePath">视频文件路径</param>
    /// <param name="preferHardwareAcceleration">是否优先使用硬件加速（默认禁用，以确保色彩正确）</param>
    public MfVideoReader(string filePath, bool preferHardwareAcceleration = false)
    {
        EnsureStartup();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("文件不存在", filePath);
        }

        var fullPath = Path.GetFullPath(filePath);

        // 创建属性并配置解码器 - 激进优化版
        MfInterop.ThrowIfFailed(MfInterop.MFCreateAttributes(out var attrs, 5));
        try
        {
            if (preferHardwareAcceleration)
            {
                // 启用硬件解码器 (DXVA2/D3D11VA)
                var hwKey = MfInterop.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS;
                var hwVal = 1;
                attrs.SetUINT32(ref hwKey, hwVal);
                
                // 启用低延迟模式（关键！）
                var lowLatencyKey = MfInterop.MF_LOW_LATENCY;
                var lowLatencyVal = 1;
                attrs.SetUINT32(ref lowLatencyKey, lowLatencyVal);
                
                System.Diagnostics.Debug.WriteLine("[硬件解码] 已启用DXVA硬件加速 + 低延迟模式");
            }
            
            // 禁用视频处理（减少CPU开销，直接输出原始格式）
            // var vpKey = MfInterop.MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING;
            // var vpVal = 1;
            // attrs.SetUINT32(ref vpKey, vpVal);

            var hr = MfInterop.MFCreateSourceReaderFromURL(fullPath, attrs, out _reader);
            if (hr != MfInterop.S_OK)
            {
                throw new InvalidOperationException($"无法创建源读取器，HRESULT: 0x{hr:X8}");
            }
        }
        finally
        {
            try { Marshal.ReleaseComObject(attrs); } catch { }
        }

        _subtype = Guid.Empty;
        _isHardwareAccelerated = false;
        
        // 尝试多种输出格式，按优先级排序
        // 注意：硬件加速可能导致色彩问题，优先使用软件解码
        Guid[] preferredSubtypes =
        [
            MfInterop.MFVideoFormat_RGB32,  // 优先 RGB32，软件解码色彩更准确
            MfInterop.MFVideoFormat_NV12    // 回退到 NV12
        ];
        
        // 禁用硬件加速以确保色彩正确
        _isHardwareAccelerated = false;

        Guid? chosen = null;
        Exception? lastException = null;
        
        foreach (var subtype in preferredSubtypes)
        {
            try
            {
                if (TrySetOutputSubtype(subtype, out var selected))
                {
                    chosen = selected;
                    _isHardwareAccelerated = preferHardwareAcceleration;
                    break;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                // 继续尝试下一个格式
            }
        }

        if (!chosen.HasValue)
        {
            var errorMsg = "无法设置可解码的视频输出格式（尝试了 NV12 和 RGB32）";
            if (lastException != null)
            {
                errorMsg += $"，最后错误: {lastException.Message}";
            }
            throw new InvalidOperationException(errorMsg);
        }
        
        _subtype = chosen.Value;

        // 配置流选择
        try
        {
            _reader.SetStreamSelection(MfInterop.MF_SOURCE_READER_ALL_STREAMS, 0);
            _reader.SetStreamSelection(MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM, 1);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"配置流选择时出错: {ex.Message}");
        }

        // 获取媒体类型信息
        MfInterop.ThrowIfFailed(_reader.GetCurrentMediaType(MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM, out var current));
        try
        {
            _width = ReadSizeHi(current, MfInterop.MF_MT_FRAME_SIZE);
            _height = ReadSizeLo(current, MfInterop.MF_MT_FRAME_SIZE);

            if (_width <= 0 || _height <= 0)
            {
                throw new InvalidOperationException($"无法获取视频尺寸: {_width}x{_height}");
            }

            // 读取 stride
            var strideKey = MfInterop.MF_MT_DEFAULT_STRIDE;
            if (current.GetUINT32(ref strideKey, out var s) == MfInterop.S_OK)
            {
                _stride = s;
                System.Diagnostics.Debug.WriteLine($"MF返回的stride: {_stride}");
            }
            else
            {
                _stride = CalculateDefaultStride(_subtype, _width);
                System.Diagnostics.Debug.WriteLine($"计算的stride: {_stride}");
            }
            
            // 重新读取实际的 subtype（MF可能会改变输出格式）
            var subtypeKey = MfInterop.MF_MT_SUBTYPE;
            if (current.GetGUID(ref subtypeKey, out var actualSubtype) == MfInterop.S_OK)
            {
                if (actualSubtype != _subtype)
                {
                    System.Diagnostics.Debug.WriteLine($"MF改变了输出格式: {_subtype} -> {actualSubtype}");
                    _subtype = actualSubtype;
                    // 根据实际格式重新计算stride
                    _stride = CalculateDefaultStride(_subtype, _width);
                    System.Diagnostics.Debug.WriteLine($"根据新格式重新计算stride: {_stride}");
                }
            }
            
            // 验证stride是否合理
            int expectedRgb32Stride = _width * 4;
            int expectedNv12Stride = (_width + 15) & ~15;
            
            if (_stride != expectedRgb32Stride && _stride != expectedNv12Stride)
            {
                System.Diagnostics.Debug.WriteLine($"警告: stride({_stride})与期望值(RGB32:{expectedRgb32Stride}, NV12:{expectedNv12Stride})不匹配");
                // 根据subtype选择最接近的stride
                if (_subtype == MfInterop.MFVideoFormat_RGB32)
                {
                    _stride = expectedRgb32Stride;
                }
                else if (_subtype == MfInterop.MFVideoFormat_NV12)
                {
                    _stride = expectedNv12Stride;
                }
                System.Diagnostics.Debug.WriteLine($"修正后的stride: {_stride}");
            }

            // 读取帧率
            var frameRateKey = MfInterop.MF_MT_FRAME_RATE;
            if (current.GetUINT64(ref frameRateKey, out var frameRateVal) == MfInterop.S_OK)
            {
                _frameRateNum = (int)((ulong)frameRateVal >> 32);
                _frameRateDen = (int)((ulong)frameRateVal & 0xFFFFFFFF);
            }
            else
            {
                _frameRateNum = 30;
                _frameRateDen = 1;
            }
        }
        finally
        {
            Marshal.ReleaseComObject(current);
        }

        _outputStride = _width * 4; // 输出始终是 BGRA32

        // 获取时长
        var pv = new PropVariant();
        try
        {
            var durationKey = MfInterop.MF_PD_DURATION;
            var hr = _reader.GetPresentationAttribute(MfInterop.MF_SOURCE_READER_MEDIASOURCE, ref durationKey, out pv);
            if (hr == MfInterop.S_OK)
            {
                _durationHns = pv.AsInt64;
                if (_durationHns < 0)
                {
                    _durationHns = 0;
                }
            }
        }
        catch
        {
            _durationHns = 0;
        }
        finally
        {
            try { pv.Clear(); } catch { }
        }
    }

    /// <summary>
    /// 计算默认 stride
    /// </summary>
    private static int CalculateDefaultStride(Guid subtype, int width)
    {
        if (subtype == MfInterop.MFVideoFormat_RGB32)
            return width * 4;
        if (subtype == MfInterop.MFVideoFormat_NV12)
            // NV12 Y平面：每像素1字节，通常对齐到16字节
            return (width + 15) & ~15;
        return width;
    }

    public unsafe Frame? ReadNextFrame()
    {
        var attempts = 0;
        while (true)
        {
            var hr = _reader.ReadSample(
                MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM,
                0,
                out _,
                out var flags,
                out var timestamp,
                out var sample);

            if (hr != MfInterop.S_OK)
            {
                throw new InvalidOperationException($"读取样本失败，HRESULT: 0x{hr:X8}");
            }

            if ((flags & MfInterop.MF_SOURCE_READERF_ERROR) != 0)
            {
                throw new InvalidOperationException("读取样本时发生错误");
            }

            if ((flags & MfInterop.MF_SOURCE_READERF_ENDOFSTREAM) != 0)
            {
                return null;
            }

            if (sample == null)
            {
                var skipFlags = MfInterop.MF_SOURCE_READERF_STREAMTICK
                                | MfInterop.MF_SOURCE_READERF_NEWSTREAM
                                | MfInterop.MF_SOURCE_READERF_NATIVEMEDIATYPECHANGED
                                | MfInterop.MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED;
                if ((flags & skipFlags) != 0 && attempts++ < 8)
                {
                    continue;
                }
                return null;
            }

            try
            {
                IMFMediaBuffer buffer;
                if (sample.GetBufferCount(out var count) == MfInterop.S_OK && count == 1 && sample.GetBufferByIndex(0, out var b0) == MfInterop.S_OK)
                {
                    buffer = b0;
                }
                else
                {
                    MfInterop.ThrowIfFailed(sample.ConvertToContiguousBuffer(out buffer));
                }
                try
                {
                    MfInterop.ThrowIfFailed(buffer.Lock(out var p, out var maxLen, out var cur));
                    try
                    {
                        var src = (byte*)p;
                        
                        // 首先检测实际的像素格式，不依赖_subtype
                        // 根据缓冲区大小和stride判断：
                        // - NV12 大小 ≈ width * height * 1.5, stride ≈ width (16字节对齐)
                        // - RGB32 大小 ≈ width * height * 4, stride ≈ width * 4
                        int nv12ExpectedSize = _width * _height + (_width * _height) / 2; // NV12 = Y + UV = 1.5 * width * height
                        int rgb32ExpectedSize = _width * _height * 4;
                        
                        // 计算16字节对齐后的NV12期望大小
                        int alignedWidth = (_width + 15) & ~15;
                        int nv12AlignedExpectedSize = alignedWidth * _height + alignedWidth * ((_height + 1) / 2);
                        
                        // 检测实际格式：
                        // 1. 如果MF返回的stride是width*4，说明实际数据是RGB32格式
                        // 2. 如果缓冲区大小接近RGB32期望大小，说明是RGB32
                        // 3. 否则可能是NV12
                        bool isStrideRgb32 = _stride > 0 && Math.Abs(_stride - _width * 4) < 16; // stride等于width*4，是RGB32
                        bool isSizeRgb32 = cur >= rgb32ExpectedSize * 0.9; // 缓冲区大小接近RGB32（提高阈值到0.9）
                        bool isSizeNv12 = cur >= nv12ExpectedSize * 0.8 && cur <= nv12AlignedExpectedSize * 1.2; // 缓冲区大小在NV12合理范围内
                        
                        // 优先使用stride判断，因为MF报告的stride通常是准确的
                        bool isActuallyRgb32 = isStrideRgb32 || (isSizeRgb32 && !isSizeNv12);
                        bool isActuallyNv12 = !isActuallyRgb32 && isSizeNv12;
                        
                        System.Diagnostics.Debug.WriteLine($"格式检测: 缓冲区={cur}, NV12期望={nv12ExpectedSize}~{nv12AlignedExpectedSize}, RGB32期望={rgb32ExpectedSize}, stride={_stride}, isRgb32={isActuallyRgb32}, isNv12={isActuallyNv12}");
                        
                        // 优先判断是否为RGB32，因为RGB32数据量更大，更容易识别
                        if (isActuallyRgb32)
                        {
                            // 实际数据是 RGB32 格式，直接复制
                            var outLen = _width * _height * 4;
                            var outBuf = ArrayPool<byte>.Shared.Rent(outLen);
                            try
                            {
                                var srcRowStride = _stride > 0 ? _stride : _width * 4;
                                var dstRowStride = _width * 4;
                                
                                fixed (byte* dst = outBuf)
                                {
                                    for (int y = 0; y < _height; y++)
                                    {
                                        var srcRow = src + y * srcRowStride;
                                        var dstRow = dst + y * dstRowStride;
                                        Buffer.MemoryCopy(srcRow, dstRow, dstRowStride, dstRowStride);
                                    }
                                }
                                System.Diagnostics.Debug.WriteLine($"检测到 RGB32 数据，直接复制: {_width}x{_height}, stride={srcRowStride}");
                                return new Frame(timestamp, outBuf, outLen, true);
                            }
                            catch
                            {
                                ArrayPool<byte>.Shared.Return(outBuf);
                                throw;
                            }
                        }
                        else if (isActuallyNv12 || _subtype == MfInterop.MFVideoFormat_NV12)
                        {
                            // 按 NV12 格式处理
                            var outLen = _width * _height * 4;
                            var outBuf = ArrayPool<byte>.Shared.Rent(outLen);
                            try
                            {
                                // 计算正确的NV12 stride
                                // NV12的stride应该是width（或16字节对齐后的值），而不是width*4
                                int nv12Stride;
                                
                                // NV12的stride必须是16字节对齐的
                                int expectedNv12Stride = (_width + 15) & ~15;
                                
                                // 如果MF返回的stride是有效的NV12 stride（接近16字节对齐的width），则使用它
                                if (_stride > 0 && _stride >= _width && _stride <= expectedNv12Stride + 16)
                                {
                                    nv12Stride = _stride;
                                }
                                else
                                {
                                    // 使用计算出的16字节对齐stride
                                    nv12Stride = expectedNv12Stride;
                                }
                                
                                // 计算NV12缓冲区大小
                                var yPlaneSize = nv12Stride * _height;
                                var uvPlaneSize = nv12Stride * ((_height + 1) / 2);
                                var expectedNv12Size = yPlaneSize + uvPlaneSize;
                                
                                // 如果实际缓冲区大小与期望大小差距太大，可能是格式错误
                                if (cur < expectedNv12Size * 0.5 || cur > expectedNv12Size * 2)
                                {
                                    System.Diagnostics.Debug.WriteLine($"警告: 缓冲区大小({cur})与NV12期望大小({expectedNv12Size})差距太大，尝试按RGB32处理");
                                    // 尝试按RGB32处理
                                    var srcRowStride = _width * 4;
                                    var dstRowStride = _width * 4;
                                    fixed (byte* dst = outBuf)
                                    {
                                        for (int y = 0; y < _height; y++)
                                        {
                                            var srcRow = src + y * srcRowStride;
                                            var dstRow = dst + y * dstRowStride;
                                            Buffer.MemoryCopy(srcRow, dstRow, dstRowStride, Math.Min(srcRowStride, cur - y * srcRowStride));
                                        }
                                    }
                                    return new Frame(timestamp, outBuf, outLen, true);
                                }
                                
                                var nv12Len = Math.Min(expectedNv12Size, cur);
                                
                                System.Diagnostics.Debug.WriteLine($"NV12 转换: stride={nv12Stride}, len={nv12Len}, cur={cur}, expected={expectedNv12Size}");
                                Nv12PtrToBgra32(src, nv12Len, outBuf.AsSpan(0, outLen), _width, _height, nv12Stride);
                                return new Frame(timestamp, outBuf, outLen, true);
                            }
                            catch
                            {
                                ArrayPool<byte>.Shared.Return(outBuf);
                                throw;
                            }
                        }
                        else
                        {
                            // RGB32/BGRA 格式 - 需要处理行间距，确保输出是连续的 BGRA 数据
                            var outLen = _width * _height * 4;
                            var outBuf = ArrayPool<byte>.Shared.Rent(outLen);
                            try
                            {
                                // 逐行复制，处理可能的行间距填充
                                var srcRowStride = _stride > 0 ? _stride : _width * 4;
                                var dstRowStride = _width * 4;
                                
                                fixed (byte* dst = outBuf)
                                {
                                    for (var y = 0; y < _height; y++)
                                    {
                                        var srcRow = src + y * srcRowStride;
                                        var dstRow = dst + y * dstRowStride;
                                        Buffer.MemoryCopy(srcRow, dstRow, dstRowStride, dstRowStride);
                                    }
                                }
                                return new Frame(timestamp, outBuf, outLen, true);
                            }
                            catch
                            {
                                ArrayPool<byte>.Shared.Return(outBuf);
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        buffer.Unlock();
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(buffer);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(sample);
            }
        }
    }

    /// <summary>
    /// 将 NV12 格式数据转换为 BGRA32 格式
    /// 优化版本：使用并行处理加速转换
    /// </summary>
    /// <param name="nv12">NV12 数据指针</param>
    /// <param name="nv12Length">NV12 数据长度</param>
    /// <param name="outBytes">输出 BGRA32 数据的缓冲区</param>
    /// <param name="width">视频宽度</param>
    /// <param name="height">视频高度</param>
    /// <param name="strideY">Y 平面每行字节数（可能包含填充）</param>
    private static unsafe void Nv12PtrToBgra32(byte* nv12, int nv12Length, Span<byte> outBytes, int width, int height, int strideY)
    {
        // 确保 stride 正确计算
        if (strideY <= 0)
        {
            strideY = (width + 15) & ~15; // 16字节对齐
        }

        var strideUV = strideY;
        var ySize = strideY * height;
        var uvHeight = (height + 1) / 2;
        var uvSize = strideUV * uvHeight;
        
        // 检查数据长度
        if (nv12Length < ySize + uvSize)
        {
            outBytes.Clear();
            return;
        }

        var uvBase = ySize;
        var outRowStride = width * 4;
        
        // 对于小分辨率视频，使用单线程处理
        // 对于大分辨率视频，使用并行处理
        fixed (byte* outPtr = outBytes)
        {
            if (height < 720)
            {
                // 单线程处理
                ConvertNv12ToBgra32Sequential(nv12, outBytes, width, height, strideY, strideUV, uvBase, outRowStride);
            }
            else
            {
                // 并行处理
                ConvertNv12ToBgra32Parallel(nv12, outPtr, width, height, strideY, strideUV, uvBase, outRowStride);
            }
        }
    }
    
    /// <summary>
    /// 顺序转换 NV12 到 BGRA32 - 针对低配硬件优化版本
    /// 使用查表法和减少分支预测失败
    /// </summary>
    private static unsafe void ConvertNv12ToBgra32Sequential(byte* nv12, Span<byte> outBytes, int width, int height, int strideY, int strideUV, int uvBase, int outRowStride)
    {
        // 预计算边界
        var maxX = width & ~1; // 确保是偶数
        
        fixed (byte* outPtr = outBytes)
        {
            for (var y = 0; y < height; y++)
            {
                var yRowPtr = nv12 + y * strideY;
                var uvRowPtr = nv12 + uvBase + (y >> 1) * strideUV;
                var outRowPtr = outPtr + y * outRowStride;
                
                var x = 0;
                // 每次处理8个像素，提高缓存命中率
                for (; x <= maxX - 8; x += 8)
                {
                    // 读取4个UV值（对应8个Y像素）
                    var uvOffset = x;
                    if (uvOffset + 7 >= strideUV) break;
                    
                    // 处理8个像素
                    for (var i = 0; i < 8; i += 2)
                    {
                        var uvIdx = uvOffset + i;
                        var u = uvRowPtr[uvIdx];
                        var v = uvRowPtr[uvIdx + 1];
                        
                        var rV = _rAddV[v];
                        var gUV = _gAddU[u] + _gAddV[v];
                        var bU = _bAddU[u];
                        
                        // 处理2个Y像素
                        for (var j = 0; j < 2; j++)
                        {
                            var yVal = yRowPtr[x + i + j];
                            var c = _yMul[yVal];
                            var r = (c + rV + 128) >> 8;
                            var g = (c + gUV + 128) >> 8;
                            var b = (c + bU + 128) >> 8;
                            
                            // 使用查表法进行裁剪
                            r = r < 0 ? 0 : (r > 255 ? 255 : r);
                            g = g < 0 ? 0 : (g > 255 ? 255 : g);
                            b = b < 0 ? 0 : (b > 255 ? 255 : b);
                            
                            var outIdx = (x + i + j) * 4;
                            outRowPtr[outIdx + 0] = (byte)b;
                            outRowPtr[outIdx + 1] = (byte)g;
                            outRowPtr[outIdx + 2] = (byte)r;
                            outRowPtr[outIdx + 3] = 255;
                        }
                    }
                }
                
                // 处理剩余像素
                for (; x < maxX; x += 2)
                {
                    var uvIdx = x;
                    if (uvIdx + 1 >= strideUV) break;
                    
                    var u = uvRowPtr[uvIdx];
                    var v = uvRowPtr[uvIdx + 1];
                    
                    var rV = _rAddV[v];
                    var gUV = _gAddU[u] + _gAddV[v];
                    var bU = _bAddU[u];
                    
                    // 第一个像素
                    var y0 = yRowPtr[x];
                    var c0 = _yMul[y0];
                    var r0 = (c0 + rV + 128) >> 8;
                    var g0 = (c0 + gUV + 128) >> 8;
                    var b0 = (c0 + bU + 128) >> 8;
                    r0 = r0 < 0 ? 0 : (r0 > 255 ? 255 : r0);
                    g0 = g0 < 0 ? 0 : (g0 > 255 ? 255 : g0);
                    b0 = b0 < 0 ? 0 : (b0 > 255 ? 255 : b0);
                    var o0 = x * 4;
                    outRowPtr[o0 + 0] = (byte)b0;
                    outRowPtr[o0 + 1] = (byte)g0;
                    outRowPtr[o0 + 2] = (byte)r0;
                    outRowPtr[o0 + 3] = 255;
                    
                    // 第二个像素
                    if (x + 1 >= width) continue;
                    var y1 = yRowPtr[x + 1];
                    var c1 = _yMul[y1];
                    var r1 = (c1 + rV + 128) >> 8;
                    var g1 = (c1 + gUV + 128) >> 8;
                    var b1 = (c1 + bU + 128) >> 8;
                    r1 = r1 < 0 ? 0 : (r1 > 255 ? 255 : r1);
                    g1 = g1 < 0 ? 0 : (g1 > 255 ? 255 : g1);
                    b1 = b1 < 0 ? 0 : (b1 > 255 ? 255 : b1);
                    var o1 = o0 + 4;
                    outRowPtr[o1 + 0] = (byte)b1;
                    outRowPtr[o1 + 1] = (byte)g1;
                    outRowPtr[o1 + 2] = (byte)r1;
                    outRowPtr[o1 + 3] = 255;
                }
            }
        }
    }
    
    /// <summary>
    /// 并行转换 NV12 到 BGRA32
    /// </summary>
    private static unsafe void ConvertNv12ToBgra32Parallel(byte* nv12, byte* outBytes, int width, int height, int strideY, int strideUV, int uvBase, int outRowStride)
    {
        // 使用并行处理，按行分区
        Parallel.For(0, height, y =>
        {
            var yRowPtr = nv12 + y * strideY;
            var uvRowPtr = nv12 + uvBase + (y / 2) * strideUV;
            var outRow = y * outRowStride;
            
            for (var x = 0; x < width; x += 2)
            {
                var uvIndex = x;
                if (uvIndex + 1 >= strideUV) break;
                
                var u = uvRowPtr[uvIndex] & 0xFF;
                var v = uvRowPtr[uvIndex + 1] & 0xFF;

                var rV = _rAddV[v];
                var gUV = _gAddU[u] + _gAddV[v];
                var bU = _bAddU[u];

                // 第一个像素
                var y0 = yRowPtr[x] & 0xFF;
                var c0 = _yMul[y0];
                var r0 = (c0 + rV + 128) >> 8;
                var g0 = (c0 + gUV + 128) >> 8;
                var b0 = (c0 + bU + 128) >> 8;
                r0 = r0 < 0 ? 0 : (r0 > 255 ? 255 : r0);
                g0 = g0 < 0 ? 0 : (g0 > 255 ? 255 : g0);
                b0 = b0 < 0 ? 0 : (b0 > 255 ? 255 : b0);
                var o0 = outRow + x * 4;
                outBytes[o0 + 0] = (byte)b0;
                outBytes[o0 + 1] = (byte)g0;
                outBytes[o0 + 2] = (byte)r0;
                outBytes[o0 + 3] = 255;

                // 第二个像素
                if (x + 1 >= width) continue;
                var y1 = yRowPtr[x + 1] & 0xFF;
                var c1 = _yMul[y1];
                var r1 = (c1 + rV + 128) >> 8;
                var g1 = (c1 + gUV + 128) >> 8;
                var b1 = (c1 + bU + 128) >> 8;
                r1 = r1 < 0 ? 0 : (r1 > 255 ? 255 : r1);
                g1 = g1 < 0 ? 0 : (g1 > 255 ? 255 : g1);
                b1 = b1 < 0 ? 0 : (b1 > 255 ? 255 : b1);
                var o1 = o0 + 4;
                outBytes[o1 + 0] = (byte)b1;
                outBytes[o1 + 1] = (byte)g1;
                outBytes[o1 + 2] = (byte)r1;
                outBytes[o1 + 3] = 255;
            }
        });
    }

    /// <summary>
    /// 定位到指定位置
    /// </summary>
    /// <param name="positionHns">目标位置（100纳秒单位）</param>
    public void Seek(long positionHns)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MfVideoReader));

        if (_durationHns > 0 && positionHns > _durationHns)
        {
            positionHns = _durationHns;
        }

        var p = PropVariant.FromLong(positionHns);
        try
        {
            var guid = Guid.Empty;
            var hr = _reader.SetCurrentPosition(ref guid, ref p);
            if (hr != MfInterop.S_OK)
            {
                _reader.Flush(MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM);
                throw new InvalidOperationException($"定位失败，HRESULT: 0x{hr:X8}");
            }

            hr = _reader.Flush(MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM);
            if (hr != MfInterop.S_OK)
            {
                throw new InvalidOperationException($"刷新流失败，HRESULT: 0x{hr:X8}");
            }
        }
        finally
        {
            try { p.Clear(); } catch { }
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // 刷新并释放读取器
            try
            {
                _reader.Flush(MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM);
            }
            catch { }

            try
            {
                Marshal.ReleaseComObject(_reader);
            }
            catch { }
        }
        catch
        {
            // 忽略清理错误
        }
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~MfVideoReader()
    {
        Dispose();
    }

    private static void EnsureStartup()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _startup, 1, 0) == 0)
        {
            MfInterop.ThrowIfFailed(MfInterop.MFStartup(MfInterop.MF_VERSION, 0));
        }
    }

    private static int ReadSizeHi(IMFMediaType mt, Guid key)
    {
        var k = key;
        if (mt.GetUINT64(ref k, out var v) != MfInterop.S_OK)
        {
            return 0;
        }
        return (int)((ulong)v >> 32);
    }

    private static int ReadSizeLo(IMFMediaType mt, Guid key)
    {
        var k = key;
        if (mt.GetUINT64(ref k, out var v) != MfInterop.S_OK)
        {
            return 0;
        }
        return (int)((ulong)v & 0xFFFFFFFF);
    }

    public sealed class Frame : IDisposable
    {
        private readonly bool _pooled;
        private byte[]? _buffer;

        public long TimestampHns { get; }
        public byte[] Buffer => _buffer ?? Array.Empty<byte>();
        public int Length { get; }

        public Frame(long timestampHns, byte[] buffer, int length, bool pooled)
        {
            TimestampHns = timestampHns;
            _buffer = buffer;
            Length = length;
            _pooled = pooled;
        }

        public void Dispose()
        {
            var buf = _buffer;
            if (buf == null)
            {
                return;
            }

            _buffer = null;
            if (_pooled)
            {
                ArrayPool<byte>.Shared.Return(buf);
            }
        }
    }

    private bool TrySetOutputSubtype(Guid subtype, out Guid chosen)
    {
        chosen = Guid.Empty;
        MfInterop.ThrowIfFailed(MfInterop.MFCreateMediaType(out var mt));
        try
        {
            var majorKey = MfInterop.MF_MT_MAJOR_TYPE;
            var majorVal = MfInterop.MFMediaType_Video;
            var subKey = MfInterop.MF_MT_SUBTYPE;
            var subVal = subtype;

            var hr = mt.SetGUID(ref majorKey, ref majorVal);
            if (hr != MfInterop.S_OK)
            {
                return false;
            }

            hr = mt.SetGUID(ref subKey, ref subVal);
            if (hr != MfInterop.S_OK)
            {
                return false;
            }

            hr = _reader.SetCurrentMediaType(MfInterop.MF_SOURCE_READER_FIRST_VIDEO_STREAM, IntPtr.Zero, mt);
            if (hr != MfInterop.S_OK)
            {
                return false;
            }

            chosen = subtype;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(mt);
        }
    }

    private static int[] BuildYMul()
    {
        var t = new int[256];
        for (var i = 0; i < 256; i++)
        {
            var c = i - 16;
            if (c < 0) c = 0;
            t[i] = 298 * c;
        }
        return t;
    }

    private static int[] BuildRAddV()
    {
        var t = new int[256];
        for (var i = 0; i < 256; i++)
        {
            t[i] = 409 * (i - 128);
        }
        return t;
    }

    private static int[] BuildGAddU()
    {
        var t = new int[256];
        for (var i = 0; i < 256; i++)
        {
            t[i] = -100 * (i - 128);
        }
        return t;
    }

    private static int[] BuildGAddV()
    {
        var t = new int[256];
        for (var i = 0; i < 256; i++)
        {
            t[i] = -208 * (i - 128);
        }
        return t;
    }

    private static int[] BuildBAddU()
    {
        var t = new int[256];
        for (var i = 0; i < 256; i++)
        {
            t[i] = 516 * (i - 128);
        }
        return t;
    }
}
