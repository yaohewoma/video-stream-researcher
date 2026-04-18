# VideoPreviewer

## Introduction

VideoPreviewer is a Media Foundation-based video preview library designed for .NET 10.0-windows. Provides high-performance video decoding, frame extraction, and format conversion.

## Features

- 🎬 **Video Decoding** - Based on Media Foundation API
- 🖼️ **Frame Extraction** - Efficient video frame extraction
- 🎨 **Format Conversion** - NV12 to RGB32 hardware-accelerated conversion
- 🔊 **Audio Playback** - Integrated NAudio audio output
- ⚡ **Performance** - ArrayPool memory management, parallel processing
- 🖥️ **Hardware Acceleration** - Support DXVA hardware decoding

## Installation

```bash
dotnet add package VideoPreviewer
```

## Quick Start

```csharp
using VideoPreviewer.MediaFoundation;

// Create video reader
using var reader = new MfVideoReader();

// Open video
reader.Open("video.mp4");

// Read frame
var frame = reader.ReadFrame();
if (frame != null)
{
    // Process frame data
    byte[] data = frame.Data;
    int width = frame.Width;
    int height = frame.Height;
}

// Seek to specific time
reader.Seek(TimeSpan.FromSeconds(10));
```

## Project Structure

```
VideoPreviewer/
├── MediaFoundation/   # Media Foundation wrappers
│   ├── MfVideoReader.cs
│   └── MfAudioReader.cs
├── Rendering/         # Rendering related
├── Utils/             # Utilities
└── VideoPreviewer.cs  # Main class
```

## Technical Details

### Color Space Conversion
```csharp
// NV12 to RGB32 conversion
// Small resolution: single-threaded processing
// Large resolution: parallel processing optimization
```

### Memory Management
```csharp
// Use ArrayPool to reduce GC pressure
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

## Dependencies

- .NET 10.0-windows
- NAudio 2.2.1
- Windows Media Foundation

## Platform Requirements

- Windows 10/11 64-bit
- Media Foundation support

## License

MIT License - For technical research and educational purposes only

---

**Maintainer**: yaohewoma  
**Version**: v2.0  
**Last Updated**: 2026-04-18
