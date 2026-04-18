# VideoStreamFetcher

## Introduction

VideoStreamFetcher is a powerful video stream parsing and downloading library supporting multiple video platforms. Designed for .NET 10.0 with async downloading, HLS support, and security validation.

## Features

- 🔍 **Multi-Platform Support** - Bilibili, Kuaishou, Miyoushe, etc.
- 📥 **Video Download** - Support MP4, FLV, HLS/M3U8 formats
- 🎵 **Audio Extraction** - Download audio streams separately
- 🔄 **HLS Download** - Support segmented video download and merge
- 🛡️ **Security Validation** - Path traversal protection and filename sanitization
- ⚡ **Async Processing** - Full async I/O with progress callbacks
- 🌍 **Multilingual** - Support Chinese/English/Russian

## Installation

```bash
dotnet add package VideoStreamFetcher
```

## Quick Start

```csharp
using VideoStreamFetcher;
using VideoStreamFetcher.Downloads;
using VideoStreamFetcher.Parsers;

// Create client
var client = new VideoStreamClient();

// Parse video
var videoInfo = await client.ParseAsync(
    "https://www.bilibili.com/video/BV...",
    status => Console.WriteLine(status)
);

// Download video
using var downloader = new VideoDownloader();
var result = await downloader.DownloadAsync(
    videoInfo,
    @"C:\Downloads",
    progress => Console.WriteLine($"Progress: {progress:F2}%"),
    status => Console.WriteLine(status),
    speed => Console.WriteLine($"Speed: {speed} bytes/s")
);
```

## Project Structure

```
VideoStreamFetcher/
├── Auth/              # Authentication management
├── Downloads/         # Download functionality
│   ├── VideoDownloader.cs
│   ├── StreamDownloader.cs
│   ├── HlsDownloader.cs
│   ├── RemuxService.cs
│   └── PathSecurityValidator.cs
├── Localization/      # Multilingual support
├── Parsers/           # Video parsers
│   ├── PlatformParsers/
│   │   ├── IPlatformParser.cs
│   │   ├── BilibiliParser.cs
│   │   └── VideoParserFactory.cs
│   └── VideoParser.cs
└── Remux/             # Remux functionality
```

## Supported Platforms

| Platform | Status | Notes |
|----------|--------|-------|
| Bilibili | ✅ | Support DASH and DURL formats |
| Kuaishou | ✅ | Support live and VOD |
| Miyoushe | ✅ | Support Genshin, Honkai, etc. |
| Generic | ✅ | Support standard video links |

## Security Features

```csharp
// Path security validation
PathSecurityValidator.ValidatePathOrThrow(outputPath, baseDirectory);

// Filename sanitization
var safeName = PathSecurityValidator.SanitizeFileName(fileName);
```

## Dependencies

- .NET 10.0
- Mp4Merger.Core
- Newtonsoft.Json 13.0.4
- QRCoder 1.6.0
- Selenium.WebDriver 4.20.0

## License

MIT License - For technical research and educational purposes only

---

**Maintainer**: yaohewoma  
**Version**: v2.0  
**Last Updated**: 2026-04-18
