# NativeVideoProcessor

## Introduction

NativeVideoProcessor is a native video processing library built on Vortice.MediaFoundation and .NET 10.0. Provides hardware-accelerated video transcoding and processing capabilities.

## Features

- 🎬 **Video Transcoding** - Support multiple format transcoding
- 🚀 **Hardware Acceleration** - Support DXVA hardware acceleration
- 🎨 **Format Conversion** - Convert between various video formats
- ⚡ **High Performance** - Native performance optimization
- 🔧 **Extensible** - Plugin-based architecture

## Installation

```bash
dotnet add package NativeVideoProcessor
```

## Quick Start

```csharp
using NativeVideoProcessor;
using NativeVideoProcessor.Engine;

// Create transcoding engine
using var engine = new MfMediaEngine();

// Transcode video
await engine.TranscodeAsync(
    "input.mp4",
    "output.mp4",
    new TranscodeOptions
    {
        TargetWidth = 1920,
        TargetHeight = 1080,
        VideoBitrate = 5000000
    },
    progress => Console.WriteLine($"Progress: {progress:P}")
);
```

## Project Structure

```
NativeVideoProcessor/
├── Engine/            # Transcoding engines
│   └── MfMediaEngine.cs
├── Interfaces/        # Interface definitions
│   └── IMediaEngine.cs
├── Models/            # Data models
│   ├── MediaInfo.cs
│   └── TranscodeOptions.cs
└── NativeVideoProcessor.cs
```

## Dependencies

- .NET 10.0
- Vortice.MediaFoundation
- Windows Media Foundation

## Platform Requirements

- Windows 10/11 64-bit
- Media Foundation support
- Optional: DXVA-capable GPU

## License

MIT License - For technical research and educational purposes only

---

**Maintainer**: yaohewoma  
**Version**: v2.0  
**Last Updated**: 2026-04-18
