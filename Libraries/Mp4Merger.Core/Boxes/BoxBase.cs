using System.Text;

namespace Mp4Merger.Core.Boxes;

/// <summary>
/// MP4盒子基类，定义所有盒子的基本结构和行为
/// </summary>
public abstract class BoxBase
{
    /// <summary>
    /// 盒子大小
    /// </summary>
    public uint Size { get; protected set; }
    
    /// <summary>
    /// 盒子类型
    /// </summary>
    public string Type { get; protected set; }
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="type">盒子类型</param>
    protected BoxBase(string type)
    {
        Type = type;
    }
    
    /// <summary>
    /// 写入盒子数据到流
    /// </summary>
    /// <param name="stream">目标流</param>
    /// <returns>写入的字节数</returns>
    public abstract Task<long> WriteToStreamAsync(Stream stream);
    
    /// <summary>
    /// 计算盒子大小
    /// </summary>
    /// <returns>盒子大小</returns>
    protected abstract uint CalculateSize();
    
    /// <summary>
    /// 验证盒子数据
    /// </summary>
    /// <returns>是否有效</returns>
    public virtual bool Validate()
    {
        return !string.IsNullOrEmpty(Type) && Type.Length == 4;
    }
}
