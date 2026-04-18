# Video Stream Researcher

![Version](https://img.shields.io/badge/version-2.0-blue.svg?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat-square&logo=dotnet)
![Avalonia](https://img.shields.io/badge/Avalonia-11.0+-8B00FF.svg?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6.svg?style=flat-square&logo=windows)
![License](https://img.shields.io/badge/license-Research%20Only-orange.svg?style=flat-square)

**English** | [简体中文](./README.zh-CN.md) | [Русский](./README.ru-RU.md)

---

## 📋 Disclaimer

> **This tool is for technical research and educational purposes only. Commercial use is strictly prohibited.**

By using this software, you agree to the following terms:
- Comply with all applicable laws, regulations, and website terms of service
- Respect intellectual property rights
- Video content copyright belongs to the original authors and platforms
- Delete downloaded content within 24 hours
- The developer assumes no responsibility for user violations
- Users bear all risks and responsibilities when downloading copyrighted content

---

## ✨ Features

- 🔍 **Multi-Platform Support** - Parse and download from Bilibili, Kuaishou, Miyoushe, and more
- 🎬 **Video Preview** - Preview videos and select specific segments
- 🔐 **Bilibili Login** - Login support for accessing high-quality videos
- 🔧 **Merge Modes** - FFmpeg / Mp4Merger (Built-in MP4 merger)
- 🌍 **Multi-Language** - English, 简体中文, Русский
- ⚡ **High Performance** - Async I/O, 1MB buffer, streaming processing
- 🛡️ **Security** - Path traversal protection, filename sanitization
- 📊 **Progress Tracking** - Real-time speed monitoring and progress display

---

## 📥 Download & Installation

### System Requirements

- Windows 10/11 64-bit
- No .NET runtime required (self-contained)

### Quick Start

1. Download `video-stream-researcher.exe` from [Releases](../../releases)
2. Double-click to run
3. Read and accept the disclaimer
4. Enter video URL to start parsing

---

## 🚀 Usage

### Basic Download

1. Paste the video URL into the input field
2. Select save path (defaults to Desktop)
3. Choose download options:
   - Full Video (with audio/video merge)
   - Audio Only
   - Video Only
   - No Merge (keep separate streams)
4. Click "Download" button

### Bilibili Login

1. Enter `bili` in the URL field
2. Scan the QR code with Bilibili app
3. After login, paste the video URL
4. Enjoy high-quality video downloads

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Video Stream Researcher                   │
├─────────────────────────────────────────────────────────────┤
│  Presentation Layer (Avalonia UI)                           │
│  ├── MainWindow.axaml                                       │
│  ├── QrCodeWindow.axaml                                     │
│  └── PreviewWindow.axaml                                    │
├─────────────────────────────────────────────────────────────┤
│  ViewModel Layer (ReactiveUI)                               │
│  └── MainWindowViewModel.cs                                 │
├─────────────────────────────────────────────────────────────┤
│  Service Layer                                              │
│  ├── DownloadFlowManager                                    │
│  ├── DownloadManager                                        │
│  ├── VideoParserWrapper                                     │
│  └── ConfigManager                                          │
├─────────────────────────────────────────────────────────────┤
│  Core Libraries                                             │
│  ├── VideoStreamFetcher (Parsing & Download)                │
│  ├── Mp4Merger.Core (MP4 Merging)                           │
│  └── NativeVideoProcessor (Video Processing)                │
└─────────────────────────────────────────────────────────────┘
```

For detailed architecture, see [Architecture Documentation](./Docs/EN/Architecture.md)

---

## 🛠️ Tech Stack

| Component | Version | Purpose |
|-----------|---------|---------|
| .NET | 10.0 | Runtime Framework |
| C# | 14.0 | Programming Language |
| Avalonia | 11.0+ | Cross-Platform UI |
| ReactiveUI | 19.5+ | Reactive UI Framework |
| Mp4Merger | Custom | MP4 Audio/Video Merge |

---

## 📊 Performance

| Metric | Value |
|--------|-------|
| Buffer Size | 1 MB |
| I/O Mode | Async |
| HTTP Processing | Streaming |
| Build Success Rate | 100% |
| Release Readiness | 91/100 ⭐⭐⭐⭐⭐ |

---

## 📚 Documentation

- [Architecture Design](./Docs/EN/Architecture.md)
- [Code Review Report](./Docs/EN/CodeReviewReport.md)
- [Multilingual Support](./Docs/Multilingual_Support.md)
- [Flowcharts](./Docs/Flowchart/)

---

## 🔄 Version History

### v2.0 (2026-04-18)

- ✅ Upgraded to .NET 10.0
- ✅ Multi-language support (EN/ZH/RU)
- ✅ Security enhancements
- ✅ Performance optimizations
- ✅ Modular architecture refactoring

See full [Release Notes](./RELEASE_NOTES.md)

---

## 🤝 Contributing

This project is for research and educational purposes. Feedback and suggestions are welcome:

- Bilibili: https://b23.tv/lMgf5eL
- Email: yhwm2026@outlook.com

---

## 👤 Author

**yaohewoma**

- GitHub: [@yaohewoma](https://github.com/yaohewoma)
- Bilibili: https://b23.tv/lMgf5eL
- Email: yhwm2026@outlook.com

---

## 📄 License

This project is for **technical research and educational purposes only**.

**Commercial use is strictly prohibited.**

By using this software, you agree to all terms and conditions stated in the disclaimer.

---

Built with ❤️ for video stream research
