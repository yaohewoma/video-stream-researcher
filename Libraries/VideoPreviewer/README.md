# VideoPreviewer

## 简介

VideoPreviewer 是一个基于 Media Foundation 的视频预览库，专为 .NET 10.0-windows 设计。提供高性能的视频解码、帧提取和格式转换功能。

## 功能特性

- 🎬 **视频解码** - 基于 Media Foundation API
- 🖼️ **帧提取** - 高效提取视频帧
- 🎨 **格式转换** - NV12 到 RGB32 硬件加速转换
- 🔊 **音频播放** - 集成 NAudio 音频输出
- ⚡ **性能优化** - ArrayPool 内存管理，并行处理
- 🖥️ **硬件加速** - 支持 DXVA 硬件解码

## 安装

```bash
dotnet add package VideoPreviewer
```

## 快速开始

```csharp
using VideoPreviewer.MediaFoundation;

// 创建视频读取器
using var reader = new MfVideoReader();

// 打开视频
reader.Open("video.mp4");

// 读取帧
var frame = reader.ReadFrame();
if (frame != null)
{
    // 处理帧数据
    byte[] data = frame.Data;
    int width = frame.Width;
    int height = frame.Height;
}

// 定位到指定时间
reader.Seek(TimeSpan.FromSeconds(10));
```

## 项目结构

```
VideoPreviewer/
├── MediaFoundation/   # Media Foundation 封装
│   ├── MfVideoReader.cs
│   └── MfAudioReader.cs
├── Rendering/         # 渲染相关
├── Utils/             # 工具类
└── VideoPreviewer.cs  # 主类
```

## 技术细节

### 颜色空间转换
```csharp
// NV12 到 RGB32 转换
// 小分辨率: 单线程处理
// 大分辨率: 并行处理优化
```

### 内存管理
```csharp
// 使用 ArrayPool 减少 GC 压力
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // 使用缓冲区
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

## 依赖

- .NET 10.0-windows
- NAudio 2.2.1
- Windows Media Foundation

## 平台要求

- Windows 10/11 64位
- 支持 Media Foundation

## 许可证

MIT License - 仅供技术研究和学习使用

---

**维护者**: yaohewoma  
**版本**: v2.0  
**最后更新**: 2026-04-18
