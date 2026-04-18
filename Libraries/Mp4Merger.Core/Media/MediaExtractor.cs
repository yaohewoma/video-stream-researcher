using System.Text;
using Mp4Merger.Core.Localization;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Media;

/// <summary>
/// 媒体数据提取器类，用于从视频和音频文件中提取实际的媒体数据
/// </summary>
public static class MediaExtractor
{
    /// <summary>
    /// 从视频文件中提取H.264媒体数据
    /// </summary>
    /// <param name="videoData">视频文件数据</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的H.264媒体数据</returns>
    public static async Task<byte[]> ExtractH264MediaDataAsync(this byte[] videoData, Action<string>? statusCallback = null) =>
        await ExtractMediaDataAsync(videoData, MergerLocalization.GetString("Common.Video"), 
            (data, offset, size) => data.IsValidH264Data(offset, size),
            FindH264StartPosition, MergerLocalization.GetString("Common.H264"), statusCallback);
    
    /// <summary>
    /// 从视频文件中提取H.264媒体数据和每个mdat块的大小
    /// </summary>
    /// <param name="videoData">视频文件数据</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的H.264媒体数据和每个mdat块的大小列表</returns>
    public static async Task<(byte[] Data, List<uint> MdatSizes)> ExtractH264MediaDataWithSizesAsync(this byte[] videoData, Action<string>? statusCallback = null) =>
        await ExtractMediaDataWithSizesAsync(videoData, MergerLocalization.GetString("Common.Video"),
            (data, offset, size) => data.IsValidH264Data(offset, size),
            FindH264StartPosition, MergerLocalization.GetString("Common.H264"), statusCallback);

    /// <summary>
    /// 从视频文件中提取H.264媒体数据、每个mdat块的大小和内容
    /// </summary>
    /// <param name="videoData">视频文件数据</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的H.264媒体数据、每个mdat块的大小列表、内容列表和fMP4样本信息</returns>
    public static async Task<(byte[] Data, List<uint> MdatSizes, List<byte[]> MdatContents, List<FMP4Parser.SampleInfo>? FMP4Samples)> ExtractH264MediaDataWithContentsAsync(this byte[] videoData, Action<string>? statusCallback = null)
    {
        // 检查是否为fMP4格式
        if (IsFMP4Format(videoData))
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Extractor.FMP4DetectedVideo"));
            return ExtractFMP4VideoData(videoData, statusCallback);
        }

        var (data, sizes, contents) = await ExtractMediaDataWithContentsAsync(videoData, MergerLocalization.GetString("Common.Video"),
            (data, offset, size) => data.IsValidH264Data(offset, size),
            FindH264StartPosition, MergerLocalization.GetString("Common.H264"), statusCallback);

        return (data, sizes, contents, null);
    }

    /// <summary>
    /// 检查是否为fMP4格式
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <returns>是否为fMP4格式</returns>
    private static bool IsFMP4Format(byte[] fileData)
    {
        // 检查文件中是否包含moof盒子
        for (int i = 0; i < fileData.Length - 8; i++)
        {
            if (i + 4 < fileData.Length)
            {
                string type = Encoding.ASCII.GetString(fileData, i + 4, 4);
                if (type == "moof")
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 提取fMP4格式的视频数据
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>提取的媒体数据、样本大小列表、内容列表和fMP4样本信息</returns>
    private static (byte[] Data, List<uint> MdatSizes, List<byte[]> MdatContents, List<FMP4Parser.SampleInfo>? FMP4Samples) ExtractFMP4VideoData(
        byte[] fileData,
        Action<string>? statusCallback)
    {
        var (samples, mediaData) = FMP4Parser.ExtractSamples(fileData, statusCallback);

        // 将样本转换为mdat大小和内容列表
        var mdatSizes = new List<uint>();
        var mdatContents = new List<byte[]>();

        foreach (var sample in samples)
        {
            mdatSizes.Add(sample.Size);

            // 提取样本数据
            byte[] sampleData = new byte[sample.Size];
            Array.Copy(mediaData, sample.Offset, sampleData, 0, (int)sample.Size);
            mdatContents.Add(sampleData);
        }

        return (mediaData, mdatSizes, mdatContents, samples);
    }
    
    /// <summary>
    /// 从音频文件中提取AAC媒体数据
    /// </summary>
    /// <param name="audioData">音频文件数据</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的AAC媒体数据</returns>
    public static async Task<byte[]> ExtractAACMediaDataAsync(this byte[] audioData, Action<string>? statusCallback = null) =>
        await ExtractMediaDataAsync(audioData, MergerLocalization.GetString("Common.Audio"), 
            (data, offset, size) => data.IsValidAACData(offset, size),
            FindAACStartPosition, "AAC", statusCallback);
    
    /// <summary>
    /// 从音频文件中提取AAC媒体数据、每个mdat块的大小和fMP4样本信息
    /// </summary>
    /// <param name="audioData">音频文件数据</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的AAC媒体数据、每个mdat块的大小列表和fMP4样本信息</returns>
    public static async Task<(byte[] Data, List<uint> MdatSizes, List<FMP4Parser.SampleInfo>? FMP4Samples)> ExtractAACMediaDataWithSamplesAsync(this byte[] audioData, Action<string>? statusCallback = null)
    {
        // 检查是否为fMP4格式
        if (IsFMP4Format(audioData))
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Extractor.FMP4DetectedAudio"));
            var (data, sizes, _, samples) = ExtractFMP4VideoData(audioData, statusCallback); // 复用FMP4提取逻辑
            return (data, sizes, samples);
        }

        var (aacData, aacSizes) = await ExtractMediaDataWithSizesAsync(audioData, MergerLocalization.GetString("Common.Audio"), 
            (d, offset, size) => d.IsValidAACData(offset, size),
            FindAACStartPosition, "AAC", statusCallback);

        return (aacData, aacSizes, null);
    }
    
    /// <summary>
    /// 处理MP3音频数据
    /// </summary>
    /// <param name="mp3Data">MP3文件数据</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>处理后的MP3音频数据</returns>
    public static byte[] ProcessMP3AudioData(this byte[] mp3Data, Action<string>? statusCallback = null)
    {
        try
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Extractor.MP3Size", mp3Data.Length / 1024.0 / 1024.0));
            return mp3Data;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Extractor.MP3ProcessingError", ex.Message));
            return mp3Data;
        }
    }
    
    /// <summary>
    /// 通用媒体数据提取方法
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="mediaType">媒体类型</param>
    /// <param name="isValidData">数据有效性检查函数</param>
    /// <param name="findStartPosition">起始位置查找函数</param>
    /// <param name="formatName">格式名称</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的媒体数据</returns>
    private static async Task<byte[]> ExtractMediaDataAsync(
        byte[] fileData, 
        string mediaType, 
        Func<byte[], int, int, bool> isValidData, 
        Func<byte[], int?> findStartPosition, 
        string formatName, 
        Action<string>? statusCallback)
    {
        var (data, _) = await ExtractMediaDataWithSizesAsync(fileData, mediaType, isValidData, findStartPosition, formatName, statusCallback);
        return data;
    }
    
    /// <summary>
    /// 通用媒体数据提取方法（返回数据和大小列表）
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="mediaType">媒体类型</param>
    /// <param name="isValidData">数据有效性检查函数</param>
    /// <param name="findStartPosition">起始位置查找函数</param>
    /// <param name="formatName">格式名称</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的媒体数据和每个mdat块的大小列表</returns>
    private static async Task<(byte[] Data, List<uint> MdatSizes)> ExtractMediaDataWithSizesAsync(
        byte[] fileData, 
        string mediaType, 
        Func<byte[], int, int, bool> isValidData, 
        Func<byte[], int?> findStartPosition, 
        string formatName, 
        Action<string>? statusCallback)
    {
        using MemoryStream outputStream = new MemoryStream();
        List<uint> mdatSizes = new List<uint>();
        
        try
        {
            var (foundMdat, extractedSize) = await ExtractMdatBoxesAsync(
                fileData, outputStream, mediaType, isValidData, mdatSizes, statusCallback);
            
            if (!foundMdat || outputStream.Length == 0)
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Extractor.MdatNotFound", mediaType, formatName));
                
                // 尝试找到媒体数据起始位置并写入
                var startPos = findStartPosition(fileData);
                if (startPos.HasValue)
                {
                    int size = fileData.Length - startPos.Value;
                    await outputStream.WriteAsync(fileData, startPos.Value, size);
                    mdatSizes.Add((uint)size);
                    statusCallback?.Invoke(MergerLocalization.GetString("Extractor.FoundStartCode", formatName, outputStream.Length / 1024.0 / 1024.0));
                }
                
                if (outputStream.Length == 0)
                {
                    statusCallback?.Invoke(MergerLocalization.GetString("Extractor.NoDataFound", formatName));
                    mdatSizes.Add((uint)fileData.Length);
                    return (fileData, mdatSizes);
                }
            }
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Extractor.ExtractionError", formatName, ex.Message));
            mdatSizes.Add((uint)fileData.Length);
            return (fileData, mdatSizes);
        }
        
        return (outputStream.ToArray(), mdatSizes);
    }

    /// <summary>
    /// 通用媒体数据提取方法（返回数据、大小列表和内容列表）
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <param name="mediaType">媒体类型</param>
    /// <param name="isValidData">数据有效性检查函数</param>
    /// <param name="findStartPosition">起始位置查找函数</param>
    /// <param name="formatName">格式名称</param>
    /// <param name="statusCallback">状态回调函数</param>
    /// <returns>提取的媒体数据、每个mdat块的大小列表和内容列表</returns>
    private static async Task<(byte[] Data, List<uint> MdatSizes, List<byte[]> MdatContents)> ExtractMediaDataWithContentsAsync(
        byte[] fileData,
        string mediaType,
        Func<byte[], int, int, bool> isValidData,
        Func<byte[], int?> findStartPosition,
        string formatName,
        Action<string>? statusCallback)
    {
        using MemoryStream outputStream = new MemoryStream();
        List<uint> mdatSizes = new List<uint>();
        List<byte[]> mdatContents = new List<byte[]>();

        try
        {
            var (foundMdat, extractedSize) = await ExtractMdatBoxesWithContentsAsync(
                fileData, outputStream, mediaType, isValidData, mdatSizes, mdatContents, statusCallback);

            if (!foundMdat || outputStream.Length == 0)
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Extractor.MdatNotFound", mediaType, formatName));

                // 尝试找到媒体数据起始位置并写入
                var startPos = findStartPosition(fileData);
                if (startPos.HasValue)
                {
                    int size = fileData.Length - startPos.Value;
                    await outputStream.WriteAsync(fileData, startPos.Value, size);
                    mdatSizes.Add((uint)size);
                    mdatContents.Add(fileData.Skip(startPos.Value).Take(size).ToArray());
                    statusCallback?.Invoke(MergerLocalization.GetString("Extractor.FoundStartCode", formatName, outputStream.Length / 1024.0 / 1024.0));
                }

                if (outputStream.Length == 0)
                {
                    statusCallback?.Invoke(MergerLocalization.GetString("Extractor.NoDataFound", formatName));
                    mdatSizes.Add((uint)fileData.Length);
                    mdatContents.Add(fileData);
                    return (fileData, mdatSizes, mdatContents);
                }
            }
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Extractor.ExtractionError", formatName, ex.Message));
            mdatSizes.Add((uint)fileData.Length);
            mdatContents.Add(fileData);
            return (fileData, mdatSizes, mdatContents);
        }

        return (outputStream.ToArray(), mdatSizes, mdatContents);
    }

    /// <summary>
    /// 提取mdat盒子中的媒体数据（内部通用方法）
    /// </summary>
    private static async Task<(bool FoundMdat, long ExtractedSize)> ExtractMdatBoxesInternalAsync(
        byte[] fileData,
        MemoryStream outputStream,
        string mediaType,
        Func<byte[], int, int, bool> isValidData,
        List<uint> mdatSizes,
        List<byte[]>? mdatContents,
        Action<string>? statusCallback)
    {
        bool foundMdat = false;
        int mdatCount = 0;
        long extractedSize = 0;
        int maxLogCount = 3;

        await foreach (var (offset, size) in EnumerateMdatBoxesAsync(fileData))
        {
            foundMdat = true;
            mdatCount++;
            int dataSize = (int)size - 8;

            if (dataSize > 0 && !(dataSize < 1024 && fileData.IsAllZeroes((int)offset + 8, dataSize)))
            {
                if (isValidData(fileData, (int)offset + 8, dataSize))
                {
                    await outputStream.WriteAsync(fileData, (int)offset + 8, dataSize);
                    extractedSize += dataSize;
                    mdatSizes.Add((uint)dataSize);

                    if (mdatContents != null)
                    {
                        byte[] content = new byte[dataSize];
                        Array.Copy(fileData, (int)offset + 8, content, 0, dataSize);
                        mdatContents.Add(content);
                    }

                    if (mdatCount <= maxLogCount)
                    {
                        statusCallback?.Invoke(MergerLocalization.GetString("Extractor.ExtractingMdat", mediaType, mdatCount, dataSize, dataSize / 1024.0));
                    }
                    else if (mdatCount == maxLogCount + 1)
                    {
                        statusCallback?.Invoke(MergerLocalization.GetString("Extractor.ExtractingRemaining", mediaType));
                    }
                }
                else if (mdatCount <= maxLogCount)
                {
                    statusCallback?.Invoke(MergerLocalization.GetString("Extractor.SkippingInvalid", mediaType, mdatCount));
                }
            }
            else if (mdatCount <= maxLogCount)
            {
                statusCallback?.Invoke(MergerLocalization.GetString("Extractor.SkippingEmpty", mediaType, mdatCount));
            }
        }

        if (mdatCount > 0)
        {
            statusCallback?.Invoke(MergerLocalization.GetString("Extractor.TotalExtracted", mdatCount, mediaType, extractedSize / 1024.0));
        }

        return (foundMdat, extractedSize);
    }

    /// <summary>
    /// 提取mdat盒子中的媒体数据（包含内容列表）
    /// </summary>
    private static Task<(bool FoundMdat, long ExtractedSize)> ExtractMdatBoxesWithContentsAsync(
        byte[] fileData,
        MemoryStream outputStream,
        string mediaType,
        Func<byte[], int, int, bool> isValidData,
        List<uint> mdatSizes,
        List<byte[]> mdatContents,
        Action<string>? statusCallback) =>
        ExtractMdatBoxesInternalAsync(fileData, outputStream, mediaType, isValidData, mdatSizes, mdatContents, statusCallback);

    /// <summary>
    /// 提取mdat盒子中的媒体数据
    /// </summary>
    private static Task<(bool FoundMdat, long ExtractedSize)> ExtractMdatBoxesAsync(
        byte[] fileData,
        MemoryStream outputStream,
        string mediaType,
        Func<byte[], int, int, bool> isValidData,
        List<uint> mdatSizes,
        Action<string>? statusCallback) =>
        ExtractMdatBoxesInternalAsync(fileData, outputStream, mediaType, isValidData, mdatSizes, null, statusCallback);
    
    /// <summary>
    /// 枚举文件中的所有mdat盒子
    /// </summary>
    /// <param name="fileData">文件数据</param>
    /// <returns>mdat盒子的偏移量和大小</returns>
    private static async IAsyncEnumerable<(long Offset, long Size)>
        EnumerateMdatBoxesAsync(byte[] fileData)
    {
        long offset = 0;
        while (offset + 8 <= fileData.Length)
        {
            uint size = fileData.ReadBigEndianUInt32((int)offset);
            string type = Encoding.ASCII.GetString(fileData, (int)offset + 4, 4);
            long actualSize = size == 1 && offset + 16 <= fileData.Length
                ? (long)fileData.ReadBigEndianUInt64((int)offset + 8)
                : size;
            
            actualSize = Math.Min(actualSize, fileData.Length - offset);
            
            if (type == "mdat")
            {
                yield return (offset, actualSize);
            }
            
            offset += actualSize;
        }
    }
    
    /// <summary>
    /// 查找H.264数据的起始位置
    /// </summary>
    /// <param name="data">数据数组</param>
    /// <returns>H.264数据的起始位置，如果没有找到则返回null</returns>
    private static int? FindH264StartPosition(byte[] data)
    {
        for (int i = 0; i + 4 <= data.Length; i++)
        {
            if ((i + 3 <= data.Length && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 1) ||
                (i + 4 <= data.Length && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 0 && data[i + 3] == 1))
            {
                return i;
            }
        }
        return null;
    }
    
    /// <summary>
    /// 查找AAC数据的起始位置
    /// </summary>
    /// <param name="data">数据数组</param>
    /// <returns>AAC数据的起始位置，如果没有找到则返回null</returns>
    private static int? FindAACStartPosition(byte[] data)
    {
        for (int i = 0; i + 7 <= data.Length; i++)
        {
            if ((data[i] & 0xFF) == 0xFF && (data[i + 1] & 0xF0) == 0xF0)
            {
                return i;
            }
        }
        return null;
    }
}
