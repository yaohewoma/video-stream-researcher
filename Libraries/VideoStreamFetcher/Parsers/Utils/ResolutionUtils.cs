namespace VideoStreamFetcher.Parsers.Utils;

/// <summary>
/// 分辨率辅助类
/// 负责处理分辨率相关的操作
/// </summary>
public static class ResolutionUtils
{
    /// <summary>
    /// 根据 quality id 获取对应的分辨率描述
    /// </summary>
    /// <param name="qualityId">quality id</param>
    /// <returns>分辨率描述</returns>
    public static string GetResolutionFromQualityId(string qualityId)
    {
        var resolutionMap = new Dictionary<string, string>
        {
            { "116", "1080P 60fps" },
            { "112", "1080P 高码率" },
            { "80", "1080P" },
            { "74", "720P 60fps" },
            { "72", "720P" },
            { "64", "480P" },
            { "32", "360P" },
            { "16", "240P" }
        };
        
        return resolutionMap.TryGetValue(qualityId, out string? resolution) ? resolution : "未知分辨率";
    }
    
    /// <summary>
    /// 获取分辨率优先级映射（值越大，优先级越高）
    /// </summary>
    /// <returns>分辨率优先级映射</returns>
    public static Dictionary<string, int> GetQualityPriorityMap()
    {
        return new Dictionary<string, int>
        {
            { "116", 10 }, // 1080P 60fps
            { "112", 9 },  // 1080P 高码率
            { "80", 8 },   // 1080P
            { "74", 7 },   // 720P 60fps
            { "72", 6 },   // 720P
            { "64", 5 },   // 480P
            { "32", 4 },   // 360P
            { "16", 3 }    // 240P
        };
    }
    
    /// <summary>
    /// 解析分辨率字符串，获取宽度和高度
    /// </summary>
    /// <param name="resolution">分辨率字符串，如 "1920x1080"</param>
    /// <param name="width">输出宽度</param>
    /// <param name="height">输出高度</param>
    /// <returns>是否解析成功</returns>
    public static bool TryParseResolution(string resolution, out int width, out int height)
    {
        width = 0;
        height = 0;
        
        if (string.IsNullOrEmpty(resolution))
        {
            return false;
        }
        
        string[] parts = resolution.Split('x', 'X');
        if (parts.Length != 2)
        {
            return false;
        }
        
        return int.TryParse(parts[0], out width) && int.TryParse(parts[1], out height);
    }
    
    /// <summary>
    /// 比较两个分辨率的优先级
    /// </summary>
    /// <param name="qualityId1">第一个 quality id</param>
    /// <param name="qualityId2">第二个 quality id</param>
    /// <returns>比较结果：1 表示第一个优先级高，-1 表示第二个优先级高，0 表示优先级相同</returns>
    public static int CompareQualityPriority(string qualityId1, string qualityId2)
    {
        var priorityMap = GetQualityPriorityMap();
        
        int priority1 = priorityMap.TryGetValue(qualityId1, out int p1) ? p1 : 0;
        int priority2 = priorityMap.TryGetValue(qualityId2, out int p2) ? p2 : 0;
        
        return priority1.CompareTo(priority2);
    }
    
    /// <summary>
    /// 获取最佳质量的视频流
    /// </summary>
    /// <param name="qualityIds">quality id 列表</param>
    /// <returns>最佳质量的 quality id</returns>
    public static string GetBestQualityId(IEnumerable<string> qualityIds)
    {
        var priorityMap = GetQualityPriorityMap();
        string bestQualityId = string.Empty;
        int highestPriority = -1;
        
        foreach (var qualityId in qualityIds)
        {
            if (priorityMap.TryGetValue(qualityId, out int priority) && priority > highestPriority)
            {
                highestPriority = priority;
                bestQualityId = qualityId;
            }
        }
        
        return bestQualityId;
    }
}