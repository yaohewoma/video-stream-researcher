# VideoStreamFetcher

## 简介

VideoStreamFetcher 是一个强大的视频流解析和下载库，支持多种视频平台。专为 .NET 10.0 设计，提供异步下载、HLS 支持和安全验证。

## 功能特性

- 🔍 **多平台支持** - Bilibili、快手、米游社等
- 📥 **视频下载** - 支持 MP4、FLV、HLS/M3U8 格式
- 🎵 **音频提取** - 单独下载音频流
- 🔄 **HLS 下载** - 支持分段视频下载和合并
- 🛡️ **安全验证** - 路径遍历防护和文件名清理
- ⚡ **异步处理** - 全异步 I/O 和进度回调
- 🌍 **多语言** - 支持中/英/俄三种语言

## 安装

```bash
dotnet add package VideoStreamFetcher
```

## 快速开始

```csharp
using VideoStreamFetcher;
using VideoStreamFetcher.Downloads;
using VideoStreamFetcher.Parsers;

// 创建客户端
var client = new VideoStreamClient();

// 解析视频
var videoInfo = await client.ParseAsync(
    "https://www.bilibili.com/video/BV...",
    status => Console.WriteLine(status)
);

// 下载视频
using var downloader = new VideoDownloader();
var result = await downloader.DownloadAsync(
    videoInfo,
    @"C:\Downloads",
    progress => Console.WriteLine($"进度: {progress:F2}%"),
    status => Console.WriteLine(status),
    speed => Console.WriteLine($"速度: {speed} bytes/s")
);
```

## 项目结构

```
VideoStreamFetcher/
├── Auth/              # 认证管理
├── Downloads/         # 下载功能
│   ├── VideoDownloader.cs
│   ├── StreamDownloader.cs
│   ├── HlsDownloader.cs
│   ├── RemuxService.cs
│   └── PathSecurityValidator.cs
├── Localization/      # 多语言支持
├── Parsers/           # 视频解析
│   ├── PlatformParsers/
│   │   ├── IPlatformParser.cs
│   │   ├── BilibiliParser.cs
│   │   └── VideoParserFactory.cs
│   └── VideoParser.cs
└── Remux/             # 转封装功能
```

## 支持的平台

| 平台 | 状态 | 说明 |
|------|------|------|
| Bilibili | ✅ | 支持 DASH 和 DURL 格式 |
| 快手 | ✅ | 支持直播和点播 |
| 米游社 | ✅ | 支持原神、崩坏等 |
| 通用 | ✅ | 支持标准视频链接 |

## 安全特性

```csharp
// 路径安全验证
PathSecurityValidator.ValidatePathOrThrow(outputPath, baseDirectory);

// 文件名清理
var safeName = PathSecurityValidator.SanitizeFileName(fileName);
```

## 依赖

- .NET 10.0
- Mp4Merger.Core
- Newtonsoft.Json 13.0.4
- QRCoder 1.6.0
- Selenium.WebDriver 4.20.0

## 许可证

MIT License - 仅供技术研究和学习使用

---

**维护者**: yaohewoma  
**版本**: v2.0  
**最后更新**: 2026-04-18
