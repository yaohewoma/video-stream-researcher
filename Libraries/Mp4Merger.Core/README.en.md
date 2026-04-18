# Mp4Merger.Core

## Introduction

Mp4Merger.Core is a high-performance MP4 audio/video merging library designed for .NET 10.0. Supports DASH, fMP4 and other formats with asynchronous processing and memory optimization.

## Features

- 🎬 **MP4 Audio/Video Merge** - Support merging separate audio and video streams
- 📦 **DASH Format Support** - Automatic DASH segmented video processing
- 🔄 **fMP4 Parsing** - Support fragmented MP4 format
- ⚡ **Async Processing** - Full asynchronous I/O operations
- 🛡️ **Type Safety** - Complete generic support
- 🌍 **Multilingual** - Support Chinese/English/Russian

## Installation

```bash
dotnet add package Mp4Merger.Core
```

## Quick Start

```csharp
using Mp4Merger.Core.Core;
using Mp4Merger.Core.Services;

// Using MP4Merger
using var merger = new MP4Merger();
var result = await merger.MergeVideoAudioAsync(
    "video.mp4", 
    "audio.mp3", 
    "output.mp4",
    status => Console.WriteLine(status)
);

// Using Mp4MergeService
var service = new Mp4MergeService();
var result = await service.MergeAsync(
    "video.mp4",
    "audio.mp3", 
    "output.mp4",
    status => Console.WriteLine(status),
    convertToNonFragmented: true
);
```

## Project Structure

```
Mp4Merger.Core/
├── Boxes/          # MP4 box definitions
├── Builders/       # Track builders
├── Core/           # Core processing classes
├── Extensions/     # Extension methods
├── Localization/   # Multilingual support
├── Media/          # Media extraction
├── Models/         # Data models
└── Services/       # Public services
```

## Dependencies

- .NET 10.0
- System.Memory

## License

MIT License - For technical research and educational purposes only

---

**Maintainer**: yaohewoma  
**Version**: v2.0  
**Last Updated**: 2026-04-18
