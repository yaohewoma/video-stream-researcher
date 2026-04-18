using System.Text.RegularExpressions;

namespace VideoStreamFetcher.Parsers.Utils;

/// <summary>
/// 字符串辅助类
/// 负责处理字符串相关的操作
/// </summary>
public static class StringUtils
{
    /// <summary>
    /// HTML 实体解码
    /// </summary>
    /// <param name="html">HTML 文本</param>
    /// <returns>解码后的文本</returns>
    public static string HtmlDecode(string html)
    {
        if (string.IsNullOrEmpty(html))
            return html;
        
        // 替换常见的 HTML 实体
        return html
            .Replace("&quot;", "\"")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&nbsp;", " ")
            .Replace("&copy;", "©")
            .Replace("&reg;", "®")
            .Replace("&trade;", "™");
    }
    
    /// <summary>
    /// 检查文本是否为乱码
    /// </summary>
    /// <param name="text">要检查的文本</param>
    /// <returns>是否为乱码</returns>
    public static bool IsGarbageText(string text)
    {
        // 检查文本中不可打印字符的比例
        int garbageCount = 0;
        int totalCount = Math.Min(text.Length, 1000); // 只检查前 1000 个字符
        
        for (int i = 0; i < totalCount; i++)
        {
            char c = text[i];
            if (c < 32 && c != 9 && c != 10 && c != 13) // 只允许制表符、换行符、回车符
            {
                garbageCount++;
            }
        }
        
        // 如果乱码字符比例超过 20%，认为是乱码
        return ((double)garbageCount / totalCount) > 0.2;
    }
    
    /// <summary>
    /// 从 HTML 中提取标题
    /// </summary>
    /// <param name="html">HTML 内容</param>
    /// <param name="defaultTitle">默认标题</param>
    /// <returns>提取的标题</returns>
    public static string ExtractTitleFromHtml(string html, string defaultTitle = "未命名视频")
    {
        // 尝试从页面内容中提取标题（适用于米游社等平台）
        var contentTitleMatch = Regex.Match(html, @"《(.*?)》", RegexOptions.IgnoreCase);
        if (contentTitleMatch.Success)
        {
            string title = "《" + contentTitleMatch.Groups[1].Value.Trim() + "》";
            // 解码 HTML 实体
            title = HtmlDecode(title);
            return title;
        }
        
        // 尝试从meta标签提取
        var metaMatch = Regex.Match(html, @"<meta[^>]*name=""title""[^>]*content=""(.*?)""", RegexOptions.IgnoreCase);
        if (metaMatch.Success)
        {
            string title = metaMatch.Groups[1].Value.Trim();
            // 解码 HTML 实体
            title = HtmlDecode(title);
            return title;
        }
        
        // 尝试从og:title提取
        var ogMatch = Regex.Match(html, @"<meta[^>]*property=""og:title""[^>]*content=""(.*?)""", RegexOptions.IgnoreCase);
        if (ogMatch.Success)
        {
            string title = ogMatch.Groups[1].Value.Trim();
            // 解码 HTML 实体
            title = HtmlDecode(title);
            return title;
        }
        
        // 首先尝试提取米游社页面中的实际视频标题
        var miyousheTitleMatch = Regex.Match(html, @"(<h1[^>]*>|<div[^>]*class[^>]*title[^>]*>|<div[^>]*id[^>]*title[^>]*>)(.*?)(</h1>|</div>)", RegexOptions.IgnoreCase);
        if (miyousheTitleMatch.Success)
        {
            string title = miyousheTitleMatch.Groups[2].Value.Trim();
            // 解码 HTML 实体
            title = HtmlDecode(title);
            // 移除可能的标记
            title = Regex.Replace(title, @"<[^>]*>", string.Empty);
            if (!string.IsNullOrEmpty(title))
            {
                return title;
            }
        }
        
        // 使用简单的正则表达式提取标题
        var titleMatch = Regex.Match(html, @"<title>(.*?)_哔哩哔哩", RegexOptions.IgnoreCase);
        if (titleMatch.Success)
        {
            string title = titleMatch.Groups[1].Value.Trim();
            // 解码 HTML 实体
            title = HtmlDecode(title);
            return title;
        }
        else
        {
            // 尝试通用标题提取
            titleMatch = Regex.Match(html, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
            {
                string title = titleMatch.Groups[1].Value.Trim();
                // 解码 HTML 实体
                title = HtmlDecode(title);
                return title;
            }
            else
            {
                return defaultTitle;
            }
        }
    }
    
    /// <summary>
    /// 截断字符串，超出部分用省略号表示
    /// </summary>
    /// <param name="str">原始字符串</param>
    /// <param name="maxLength">最大长度</param>
    /// <returns>截断后的字符串</returns>
    public static string Truncate(string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
        {
            return str;
        }
        return str.Substring(0, maxLength) + "...";
    }
    
    /// <summary>
    /// 移除字符串中的无效字符
    /// </summary>
    /// <param name="str">原始字符串</param>
    /// <returns>清理后的字符串</returns>
    public static string RemoveInvalidChars(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }
        
        // 移除控制字符（除了换行符和制表符）
        return Regex.Replace(str, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
    }
    
    /// <summary>
    /// 检查字符串是否包含指定的任何关键字
    /// </summary>
    /// <param name="str">要检查的字符串</param>
    /// <param name="keywords">关键字数组</param>
    /// <returns>是否包含任何关键字</returns>
    public static bool ContainsAny(string str, params string[] keywords)
    {
        if (string.IsNullOrEmpty(str) || keywords == null || keywords.Length == 0)
        {
            return false;
        }
        
        foreach (var keyword in keywords)
        {
            if (str.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 从字符串中提取数字
    /// </summary>
    /// <param name="str">原始字符串</param>
    /// <returns>提取的数字字符串</returns>
    public static string ExtractNumbers(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return string.Empty;
        }
        
        return Regex.Replace(str, @"[^0-9]", "");
    }
    
    /// <summary>
    /// 将字节大小转换为可读格式
    /// </summary>
    /// <param name="bytes">字节大小</param>
    /// <returns>可读的大小字符串</returns>
    public static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        double number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number = number / 1024;
            counter++;
        }
        
        return $"{number:n2} {suffixes[counter]}";
    }
}