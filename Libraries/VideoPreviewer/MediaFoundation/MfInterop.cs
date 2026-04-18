using System;
using System.Runtime.InteropServices;

namespace VideoPreviewer.MediaFoundation;

internal static class MfInterop
{
    internal const int S_OK = 0;
    internal const int MF_VERSION = 0x00020070;
    internal const int D3D11_SDK_VERSION = 7;

    internal static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    internal static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    internal static readonly Guid MFMediaType_Video = new Guid("73646976-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MFVideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MFVideoFormat_NV12 = new Guid("3231564e-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MFVideoFormat_H264 = new Guid("34363248-0000-0010-8000-00aa00389b71");

    internal static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
    internal static readonly Guid MF_MT_DEFAULT_STRIDE = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    internal static readonly Guid MF_MT_FRAME_RATE = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");

    // 音频格式 GUID
    internal static readonly Guid MFMediaType_Audio = new Guid("73647561-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MFAudioFormat_PCM = new Guid("00000001-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MFAudioFormat_Float = new Guid("00000003-0000-0010-8000-00aa00389b71");
    internal static readonly Guid MFAudioFormat_AAC = new Guid("00001610-0000-0010-8000-00aa00389b71");

    // 音频属性 GUID
    internal static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new Guid("5faeeae7-0290-4c31-9e8a-c5f44ff73f48");
    internal static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new Guid("37e48bf5-645e-4c5b-89d5-993c52b9a300");
    internal static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");

    internal static readonly Guid MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING = new Guid("fb394f3d-ccf0-40b7-8d74-0a9a1f32f8f8");
    internal static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = new Guid("a634a91c-822b-41b9-a494-4de4647f47c5");
    internal static readonly Guid MF_LOW_LATENCY = new Guid("9c27891a-ed7a-40e1-88e8-b22727a024ee");

    internal const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)0xFFFFFFFC);
    internal const int MF_SOURCE_READER_FIRST_AUDIO_STREAM = unchecked((int)0xFFFFFFFD);
    internal const int MF_SOURCE_READER_ALL_STREAMS = unchecked((int)0xFFFFFFFE);
    internal const int MF_SOURCE_READER_MEDIASOURCE = unchecked((int)0xFFFFFFFF);

    internal const int MF_SOURCE_READER_CONTROLF_DRAIN = 0x00000001;

    internal const int MF_SOURCE_READERF_ERROR = 0x00000001;
    internal const int MF_SOURCE_READERF_ENDOFSTREAM = 0x00000002;
    internal const int MF_SOURCE_READERF_NEWSTREAM = 0x00000004;
    internal const int MF_SOURCE_READERF_NATIVEMEDIATYPECHANGED = 0x00000010;
    internal const int MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED = 0x00000020;
    internal const int MF_SOURCE_READERF_STREAMTICK = 0x00000100;

    internal static readonly Guid MF_PD_DURATION = new Guid("6c990d31-bb8e-477a-8591-779232a37d35");

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFStartup(int version, int flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFShutdown();

    [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern int MFCreateSourceReaderFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string pwszURL,
        IMFAttributes? pAttributes,
        out IMFSourceReader ppSourceReader);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateMediaType(out IMFMediaType ppMFType);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateAttributes(out IMFAttributes ppMFAttributes, int cInitialSize);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateMemoryBuffer(int cbMaxLength, out IMFMediaBuffer ppBuffer);

    internal static void ThrowIfFailed(int hr)
    {
        if (hr != S_OK)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }
}

[ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAttributes
{
    int GetItem([In] ref Guid guidKey, IntPtr pValue);
    int GetItemType([In] ref Guid guidKey, out int pType);
    int CompareItem([In] ref Guid guidKey, IntPtr Value, out int pbResult);
    int Compare([MarshalAs(UnmanagedType.Interface)] IMFAttributes pTheirs, int MatchType, out int pbResult);
    int GetUINT32([In] ref Guid guidKey, out int punValue);
    int GetUINT64([In] ref Guid guidKey, out long punValue);
    int GetDouble([In] ref Guid guidKey, out double pfValue);
    int GetGUID([In] ref Guid guidKey, out Guid pguidValue);
    int GetStringLength([In] ref Guid guidKey, out int pcchLength);
    int GetString([In] ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string pwszValue, int cchBufSize, out int pcchLength);
    int GetAllocatedString([In] ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out int pcchLength);
    int GetBlobSize([In] ref Guid guidKey, out int pcbBlobSize);
    int GetBlob([In] ref Guid guidKey, [Out] byte[] pBuf, int cbBufSize, out int pcbBlobSize);
    int GetAllocatedBlob([In] ref Guid guidKey, out IntPtr ip, out int pcbSize);
    int GetUnknown([In] ref Guid guidKey, [In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    int SetItem([In] ref Guid guidKey, IntPtr Value);
    int DeleteItem([In] ref Guid guidKey);
    int DeleteAllItems();
    int SetUINT32([In] ref Guid guidKey, int unValue);
    int SetUINT64([In] ref Guid guidKey, long unValue);
    int SetDouble([In] ref Guid guidKey, double fValue);
    int SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
    int SetString([In] ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    int SetBlob([In] ref Guid guidKey, [In] byte[] pBuf, int cbBufSize);
    int SetUnknown([In] ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    int LockStore();
    int UnlockStore();
    int GetCount(out int pcItems);
    int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
    int CopyAllItems([MarshalAs(UnmanagedType.Interface)] IMFAttributes pDest);
}

[ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaType : IMFAttributes
{
}

[ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSourceReader
{
    int GetStreamSelection(int dwStreamIndex, out int pfSelected);
    int SetStreamSelection(int dwStreamIndex, int fSelected);
    int GetNativeMediaType(int dwStreamIndex, int dwMediaTypeIndex, out IMFMediaType ppMediaType);
    int GetCurrentMediaType(int dwStreamIndex, out IMFMediaType ppMediaType);
    int SetCurrentMediaType(int dwStreamIndex, IntPtr pdwReserved, IMFMediaType pMediaType);
    int SetCurrentPosition([In] ref Guid guidTimeFormat, [In] ref PropVariant varPosition);
    int ReadSample(int dwStreamIndex, int dwControlFlags, out int pdwActualStreamIndex, out int pdwStreamFlags, out long pllTimestamp, out IMFSample? ppSample);
    int Flush(int dwStreamIndex);
    int GetServiceForStream(int dwStreamIndex, [In] ref Guid guidService, [In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);
    int GetPresentationAttribute(int dwStreamIndex, [In] ref Guid guidAttribute, out PropVariant pvAttribute);
}

[ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSample
{
    int GetItem([In] ref Guid guidKey, IntPtr pValue);
    int GetItemType([In] ref Guid guidKey, out int pType);
    int CompareItem([In] ref Guid guidKey, IntPtr Value, out int pbResult);
    int Compare([MarshalAs(UnmanagedType.Interface)] IMFAttributes pTheirs, int MatchType, out int pbResult);
    int GetUINT32([In] ref Guid guidKey, out int punValue);
    int GetUINT64([In] ref Guid guidKey, out long punValue);
    int GetDouble([In] ref Guid guidKey, out double pfValue);
    int GetGUID([In] ref Guid guidKey, out Guid pguidValue);
    int GetStringLength([In] ref Guid guidKey, out int pcchLength);
    int GetString([In] ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string pwszValue, int cchBufSize, out int pcchLength);
    int GetAllocatedString([In] ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out int pcchLength);
    int GetBlobSize([In] ref Guid guidKey, out int pcbBlobSize);
    int GetBlob([In] ref Guid guidKey, [Out] byte[] pBuf, int cbBufSize, out int pcbBlobSize);
    int GetAllocatedBlob([In] ref Guid guidKey, out IntPtr ip, out int pcbSize);
    int GetUnknown([In] ref Guid guidKey, [In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    int SetItem([In] ref Guid guidKey, IntPtr Value);
    int DeleteItem([In] ref Guid guidKey);
    int DeleteAllItems();
    int SetUINT32([In] ref Guid guidKey, int unValue);
    int SetUINT64([In] ref Guid guidKey, long unValue);
    int SetDouble([In] ref Guid guidKey, double fValue);
    int SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
    int SetString([In] ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    int SetBlob([In] ref Guid guidKey, [In] byte[] pBuf, int cbBufSize);
    int SetUnknown([In] ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    int LockStore();
    int UnlockStore();
    int GetCount(out int pcItems);
    int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
    int CopyAllItems([MarshalAs(UnmanagedType.Interface)] IMFAttributes pDest);

    int GetSampleFlags(out int pdwSampleFlags);
    int SetSampleFlags(int dwSampleFlags);
    int GetSampleTime(out long phnsSampleTime);
    int SetSampleTime(long hnsSampleTime);
    int GetSampleDuration(out long phnsSampleDuration);
    int SetSampleDuration(long hnsSampleDuration);
    int GetBufferCount(out int pdwBufferCount);
    int GetBufferByIndex(int dwIndex, out IMFMediaBuffer ppBuffer);
    int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
    int AddBuffer(IMFMediaBuffer pBuffer);
    int RemoveBufferByIndex(int dwIndex);
    int RemoveAllBuffers();
    int GetTotalLength(out int pcbTotalLength);
    int CopyToBuffer(IMFMediaBuffer pBuffer);
}

[ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaBuffer
{
    int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
    int Unlock();
    int GetCurrentLength(out int pcbCurrentLength);
    int SetCurrentLength(int cbCurrentLength);
    int GetMaxLength(out int pcbMaxLength);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    private ushort vt;
    private ushort wReserved1;
    private ushort wReserved2;
    private ushort wReserved3;
    private IntPtr ptr;
    private int int32;
    private long int64;

    public static PropVariant FromLong(long value)
    {
        var pv = new PropVariant();
        pv.vt = 0x0014;
        pv.int64 = value;
        return pv;
    }

    public long AsInt64 => int64;

    public void Clear()
    {
        PropVariantClear(ref this);
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int PropVariantClear(ref PropVariant pvar);
}
