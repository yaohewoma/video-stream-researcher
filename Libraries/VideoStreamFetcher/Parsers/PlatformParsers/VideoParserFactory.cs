using System;
using System.Collections.Generic;
using System.Linq;
using VideoStreamFetcher.Parsers.Utils;

namespace VideoStreamFetcher.Parsers.PlatformParsers;

/// <summary>
/// 视频解析器工厂
/// 负责根据 URL 获取合适的解析器
/// </summary>
public class VideoParserFactory
{
    private readonly IEnumerable<IPlatformParser> _parsers;

    public VideoParserFactory(IEnumerable<IPlatformParser> parsers)
    {
        _parsers = parsers;
    }

    /// <summary>
    /// 获取合适的解析器
    /// </summary>
    /// <param name="url">视频 URL</param>
    /// <returns>匹配的解析器，如果没有匹配则返回 null</returns>
    public IPlatformParser? GetParser(string url)
    {
        return _parsers.FirstOrDefault(p => p.CanParse(url));
    }
}
