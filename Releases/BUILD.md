# Build Instructions

**English** | [简体中文](./BUILD.zh-CN.md) | [Русский](./BUILD.ru-RU.md)

---

## Environment Requirements

- Windows 10/11 64-bit
- .NET 10.0 SDK or higher
- Visual Studio 2022 or VS Code

## Project Structure

```
video-stream-researcher/
├── video-stream-researcher.csproj    # Main project file
├── Program.cs                        # Entry point
├── Core/                             # Core interfaces and models
├── Logging/                          # Logging system
├── Resources/                        # Multilingual resource files
├── Services/                         # Business services
├── UI/                               # Avalonia UI
├── Libraries/                        # External libraries
│   ├── Mp4Merger.Core/              # MP4 merge library
│   ├── VideoStreamFetcher/          # Video download library
│   ├── VideoPreviewer/              # Video preview library
│   └── NativeVideoProcessor/        # Native video processing
└── ...
```

## Build Steps

### 1. Clone Repository

```bash
git clone https://github.com/yaohewoma/video-stream-researcher.git
cd video-stream-researcher
```

### 2. Restore Dependencies

```bash
dotnet restore
```

### 3. Build Debug Version

```bash
dotnet build
```

### 4. Publish Single-File Version

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish
```

### 5. Build Output

The compiled single-file executable will be located at:
```
publish/video-stream-researcher.exe
```

## Configuration

The project is configured to:
- ✅ Single-file publishing (all dependencies packaged into one exe)
- ✅ Self-contained (no .NET runtime required on target machine)
- ✅ Compression enabled (reduces file size)
- ✅ Windows 10/11 64-bit support

## Notes

1. First build may require downloading NuGet packages, ensure network connection is available
2. If type conflict errors occur, clean and rebuild:
   ```bash
   dotnet clean
   dotnet build
   ```
3. Release version automatically includes all necessary dependencies

## Tech Stack

- .NET 10.0
- Avalonia UI 12.0
- C# 14.0
- Pure C# implementation, no FFmpeg dependency
