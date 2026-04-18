using System.Text;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Boxes;

/// <summary>
/// mdat盒子类，存储MP4文件的实际媒体数据
/// </summary>
public class MdatBox : BoxBase
{
    /// <summary>
    /// 视频数据
    /// </summary>
    private readonly byte[] _videoData;
    
    /// <summary>
    /// 音频数据
    /// </summary>
    private readonly byte[] _audioData;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="videoData">视频数据</param>
    /// <param name="audioData">音频数据</param>
    public MdatBox(byte[] videoData, byte[] audioData) : base("mdat")
    {
        _videoData = videoData ?? Array.Empty<byte>();
        _audioData = audioData ?? Array.Empty<byte>();
        Size = CalculateSize();
    }
    
    /// <summary>
    /// 计算盒子大小
    /// </summary>
    /// <returns>盒子大小</returns>
    protected override uint CalculateSize() =>
        8 + (uint)(_videoData.Length + _audioData.Length);
    
    /// <summary>
    /// 写入盒子数据到流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <returns>写入的字节数</returns>
    public override async Task<long> WriteToStreamAsync(Stream stream)
    {
        // 写入头部
        byte[] header = new byte[8];
        MP4Utils.WriteBigEndianUInt32(header, 0, Size);
        Array.Copy(Encoding.ASCII.GetBytes(Type), 0, header, 4, 4);
        await stream.WriteAsync(header, 0, header.Length);
        
        // 写入视频数据
        if (_videoData.Length > 0)
            await stream.WriteAsync(_videoData, 0, _videoData.Length);
        
        // 写入音频数据
        if (_audioData.Length > 0)
            await stream.WriteAsync(_audioData, 0, _audioData.Length);
        
        return Size;
    }
}
