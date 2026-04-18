# Build Instructions

**English** | [简体中文](./BUILD.zh-CN.md) | [Русский](./BUILD.ru-RU.md)

---

## Environment Requirements

- Windows 10/11 64-bit
- .NET 10.0 SDK or higher
- Visual Studio 2022 (recommended) or VS Code

## Quick Start

### Option 1: Visual Studio 2022 (Recommended)

1. Open `video-stream-researcher.sln` solution file
2. Select `Release` configuration
3. Right-click solution → Build Solution
4. Or use Publish feature for single-file executable

### Option 2: Command Line

```bash
# Clone repository
git clone https://github.com/yaohewoma/video-stream-researcher.git
cd video-stream-researcher

# Restore dependencies
dotnet restore video-stream-researcher.sln

# Build solution
dotnet build video-stream-researcher.sln -c Release

# Publish single-file version
dotnet publish video-stream-researcher.csproj -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish
```

## Project Structure

```
video-stream-researcher/
├── video-stream-researcher.sln       # Solution file
├── video-stream-researcher.csproj    # Main project
├── Core/                             # Core interfaces and models
├── Logging/                          # Logging system
├── Resources/                        # Multilingual resources
├── Services/                         # Business services
├── UI/                               # Avalonia UI
├── Libraries/                        # External libraries
│   ├── Mp4Merger.Core/
│   ├── VideoStreamFetcher/
│   ├── VideoPreviewer/
│   └── NativeVideoProcessor/
└── ...
```

## Configuration

The project is configured for:
- ✅ Single-file publishing
- ✅ Self-contained deployment
- ✅ Compression enabled
- ✅ Windows 10/11 64-bit support

## Output

Compiled executable will be at:
```
publish/video-stream-researcher.exe
```

## Tech Stack

- .NET 10.0
- Avalonia UI 12.0
- C# 14.0
- Pure C# implementation, no FFmpeg dependency
