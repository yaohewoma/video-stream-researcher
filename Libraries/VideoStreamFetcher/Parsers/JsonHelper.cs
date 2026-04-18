using Newtonsoft.Json;

namespace VideoStreamFetcher.Parsers;

/// <summary>
/// JSON 辅助类
/// 负责处理 JSON 相关的操作
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// 清理 JSON 数据，移除无效字符
    /// </summary>
    /// <param name="jsonData">原始 JSON 数据</param>
    /// <returns>清理后的 JSON 数据</returns>
    public static string CleanJsonData(string jsonData)
    {
        // 移除 JSON 前后的无效字符
        jsonData = jsonData.Trim();
        
        // 检查是否为直接匹配到的视频 URL（MP4 链接）
        if (jsonData.StartsWith("http"))
        {
            return jsonData;
        }
        
        // 确保 JSON 以 { 开头，以 } 结尾
        if (!jsonData.StartsWith('{'))
        {
            int startIndex = jsonData.IndexOf('{');
            if (startIndex >= 0)
                jsonData = jsonData[startIndex..];
        }
        
        if (!jsonData.EndsWith('}'))
        {
            int endIndex = jsonData.LastIndexOf('}');
            if (endIndex >= 0)
                jsonData = jsonData[..(endIndex + 1)];
        }
        
        return jsonData;
    }
    
    /// <summary>
    /// 验证 JSON 格式是否有效
    /// </summary>
    /// <param name="jsonData">JSON 数据</param>
    /// <returns>是否为有效 JSON</returns>
    public static bool IsValidJson(string jsonData)
    {
        // 检查是否为直接匹配到的视频 URL（MP4 链接）
        if (jsonData.StartsWith("http"))
        {
            return true;
        }
        
        try
        {
            JsonConvert.DeserializeObject(jsonData);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 序列化对象为 JSON 字符串
    /// </summary>
    /// <param name="obj">要序列化的对象</param>
    /// <returns>JSON 字符串</returns>
    public static string SerializeObject(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }
    
    /// <summary>
    /// 反序列化 JSON 字符串为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="jsonData">JSON 字符串</param>
    /// <returns>反序列化后的对象</returns>
    public static T? DeserializeObject<T>(string jsonData)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(jsonData);
        }
        catch
        {
            return default;
        }
    }
    
    /// <summary>
    /// 反序列化 JSON 字符串为动态对象
    /// </summary>
    /// <param name="jsonData">JSON 字符串</param>
    /// <returns>动态对象</returns>
    public static dynamic? DeserializeObject(string jsonData)
    {
        try
        {
            return JsonConvert.DeserializeObject(jsonData);
        }
        catch
        {
            return null;
        }
    }
}