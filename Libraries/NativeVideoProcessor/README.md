# NativeVideoProcessor

## 简介

NativeVideoProcessor 是一个原生视频处理库，基于 Vortice.MediaFoundation 和 .NET 10.0 构建。提供硬件加速的视频转码和处理功能。

## 功能特性

- 🎬 **视频转码** - 支持多种格式转码
- 🚀 **硬件加速** - 支持 DXVA 硬件加速
- 🎨 **格式转换** - 多种视频格式互转
- ⚡ **高性能** - 原生性能优化
- 🔧 **可扩展** - 插件式架构设计

## 安装

```bash
dotnet add package NativeVideoProcessor
```

## 快速开始

```csharp
using NativeVideoProcessor;
using NativeVideoProcessor.Engine;

// 创建转码引擎
using var engine = new MfMediaEngine();

// 转码视频
await engine.TranscodeAsync(
    "input.mp4",
    "output.mp4",
    new TranscodeOptions
    {
        TargetWidth = 1920,
        TargetHeight = 1080,
        VideoBitrate = 5000000
    },
    progress => Console.WriteLine($"进度: {progress:P}")
);
```

## 项目结构

```
NativeVideoProcessor/
├── Engine/            # 转码引擎
│   └── MfMediaEngine.cs
├── Interfaces/        # 接口定义
│   └── IMediaEngine.cs
├── Models/            # 数据模型
│   ├── MediaInfo.cs
│   └── TranscodeOptions.cs
└── NativeVideoProcessor.cs
```

## 依赖

- .NET 10.0
- Vortice.MediaFoundation
- Windows Media Foundation

## 平台要求

- Windows 10/11 64位
- 支持 Media Foundation
- 可选: 支持 DXVA 的显卡

## 许可证

MIT License - 仅供技术研究和学习使用

---

**维护者**: yaohewoma  
**版本**: v2.0  
**最后更新**: 2026-04-18
