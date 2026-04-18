# Mp4Merger.Core

## 简介

Mp4Merger.Core 是一个高性能的 MP4 音视频合并库，专为 .NET 10.0 设计。支持 DASH、fMP4 等多种格式，提供异步处理和内存优化。

## 功能特性

- 🎬 **MP4 音视频合并** - 支持分离的音视频流合并
- 📦 **DASH 格式支持** - 自动处理 DASH 分段视频
- 🔄 **fMP4 解析** - 支持 fragmented MP4 格式
- ⚡ **异步处理** - 全异步 I/O 操作
- 🛡️ **类型安全** - 完整的泛型支持
- 🌍 **多语言** - 支持中/英/俄三种语言

## 安装

```bash
dotnet add package Mp4Merger.Core
```

## 快速开始

```csharp
using Mp4Merger.Core.Core;
using Mp4Merger.Core.Services;

// 使用 MP4Merger
using var merger = new MP4Merger();
var result = await merger.MergeVideoAudioAsync(
    "video.mp4", 
    "audio.mp3", 
    "output.mp4",
    status => Console.WriteLine(status)
);

// 使用 Mp4MergeService
var service = new Mp4MergeService();
var result = await service.MergeAsync(
    "video.mp4",
    "audio.mp3", 
    "output.mp4",
    status => Console.WriteLine(status),
    convertToNonFragmented: true
);
```

## 项目结构

```
Mp4Merger.Core/
├── Boxes/          # MP4 盒子定义
├── Builders/       # 轨道构建器
├── Core/           # 核心处理类
├── Extensions/     # 扩展方法
├── Localization/   # 多语言支持
├── Media/          # 媒体提取
├── Models/         # 数据模型
└── Services/       # 公共服务
```

## 依赖

- .NET 10.0
- System.Memory

## 许可证

MIT License - 仅供技术研究和学习使用

---

**维护者**: yaohewoma  
**版本**: v2.0  
**最后更新**: 2026-04-18
