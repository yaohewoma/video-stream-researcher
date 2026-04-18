using System.Text;
using Mp4Merger.Core.Utils;

namespace Mp4Merger.Core.Boxes;

/// <summary>
/// ftyp盒子类，用于标识MP4文件类型
/// </summary>
public class FtypBox : BoxBase
{
    /// <summary>
    /// 主品牌
    /// </summary>
    public string MajorBrand { get; } = "isom";
    
    /// <summary>
    /// 主品牌版本
    /// </summary>
    public uint MinorVersion { get; } = 0x00000200;
    
    /// <summary>
    /// 兼容品牌列表
    /// </summary>
    public List<string> CompatibleBrands { get; } = new List<string> { "isom", "iso2", "avc1", "mp41" };
    
    /// <summary>
    /// 构造函数
    /// </summary>
    public FtypBox() : base("ftyp") =>
        Size = CalculateSize();
    
    /// <summary>
    /// 计算盒子大小
    /// </summary>
    /// <returns>盒子大小</returns>
    protected override uint CalculateSize() =>
        16 + (uint)(CompatibleBrands.Count * 4);
    
    /// <summary>
    /// 写入盒子数据到流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <returns>写入的字节数</returns>
    public override async Task<long> WriteToStreamAsync(Stream stream)
    {
        byte[] buffer = new byte[Size];
        
        // 写入头部
        MP4Utils.WriteBigEndianUInt32(buffer, 0, Size);
        Array.Copy(Encoding.ASCII.GetBytes(Type), 0, buffer, 4, 4);
        
        // 写入主品牌
        Array.Copy(Encoding.ASCII.GetBytes(MajorBrand), 0, buffer, 8, 4);
        
        // 写入版本
        MP4Utils.WriteBigEndianUInt32(buffer, 12, MinorVersion);
        
        // 写入兼容品牌
        CompatibleBrands.Select((brand, index) => (brand, index))
            .ToList()
            .ForEach(item => Array.Copy(Encoding.ASCII.GetBytes(item.brand), 0, buffer, 16 + item.index * 4, 4));
        
        await stream.WriteAsync(buffer, 0, buffer.Length);
        return buffer.Length;
    }
    
    /// <summary>
    /// 验证盒子数据
    /// </summary>
    /// <returns>是否有效</returns>
    public override bool Validate() =>
        base.Validate() && !string.IsNullOrEmpty(MajorBrand) && MajorBrand.Length == 4 && CompatibleBrands.All(brand => brand.Length == 4);
}
