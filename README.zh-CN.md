# 视频流研究器

![版本](https://img.shields.io/badge/版本-2.0-blue.svg?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg?style=flat-square&logo=dotnet)
![Avalonia](https://img.shields.io/badge/Avalonia-11.0+-8B00FF.svg?style=flat-square)
![平台](https://img.shields.io/badge/平台-Windows%2010%2F11-0078D6.svg?style=flat-square&logo=windows)
![许可](https://img.shields.io/badge/许可-仅限研究-orange.svg?style=flat-square)

[English](./README.md) | **简体中文** | [Русский](./README.ru-RU.md)

---

## 📋 免责声明

> **本工具仅用于技术研究和学习目的，禁止用于商业用途。**

使用本软件即表示您同意以下条款：
- 遵守所有适用的法律法规和网站服务条款
- 尊重知识产权
- 视频内容版权归原作者和平台所有
- 下载后请在24小时内删除
- 开发者不对用户的违法行为承担任何责任
- 使用本软件下载受版权保护的内容可能违反相关法律法规，用户应自行承担所有风险和责任

---

## ✨ 功能特性

- 🔍 **多平台支持** - 支持B站、快手、米游社等平台的视频解析和下载
- 🎬 **视频预览** - 支持视频预览和片段选择
- 🔐 **B站登录** - 支持登录获取高清视频
- 🔧 **内置 MP4 合并器** - 纯 C# 实现，无需 FFmpeg 依赖
- 🌍 **多语言** - English, 简体中文, Русский
- ⚡ **高性能** - 异步I/O、1MB缓冲区、流式处理
- 🛡️ **安全加固** - 路径遍历防护、文件名安全清理
- 📊 **进度追踪** - 实时速度监控和进度显示

---

## 📥 下载与安装

### 系统要求

- Windows 10/11 64位系统
- 无需安装 .NET 运行库（已内置）

### 快速开始

1. 从 [Releases](../../releases) 下载 `video-stream-researcher.exe`
2. 双击运行程序
3. 阅读并同意免责声明
4. 输入视频URL开始解析

---

## 🚀 使用方法

### 基础下载

1. 将视频URL粘贴到输入框
2. 选择保存路径（默认为桌面）
3. 选择下载选项：
   - 完整视频（合并音视频）
   - 仅音频
   - 仅视频
   - 不合并（保留分离的流）
4. 点击"下载"按钮

### B站登录

1. 在URL输入框中输入 `bili`
2. 使用B站APP扫描二维码
3. 登录成功后粘贴视频URL
4. 享受高清视频下载

---

## 🏗️ 架构设计

```
┌─────────────────────────────────────────────────────────────┐
│                    视频流研究器                               │
├─────────────────────────────────────────────────────────────┤
│  表示层 (Avalonia UI)                                        │
│  ├── MainWindow.axaml                                       │
│  ├── QrCodeWindow.axaml                                     │
│  └── PreviewWindow.axaml                                    │
├─────────────────────────────────────────────────────────────┤
│  ViewModel层 (ReactiveUI)                                   │
│  └── MainWindowViewModel.cs                                 │
├─────────────────────────────────────────────────────────────┤
│  服务层                                                      │
│  ├── DownloadFlowManager                                    │
│  ├── DownloadManager                                        │
│  ├── VideoParserWrapper                                     │
│  └── ConfigManager                                          │
├─────────────────────────────────────────────────────────────┤
│  核心库                                                      │
│  ├── VideoStreamFetcher (解析与下载)                         │
│  ├── Mp4Merger.Core (MP4合并)                               │
│  └── NativeVideoProcessor (视频处理)                        │
└─────────────────────────────────────────────────────────────┘
```

详细架构请查看 [架构设计文档](./Docs/ZH/Architecture.md)

---

## 🛠️ 技术栈

| 组件 | 版本 | 用途 |
|------|------|------|
| .NET | 10.0 | 运行时框架 |
| C# | 14.0 | 编程语言 |
| Avalonia | 11.0+ | 跨平台UI框架 |
| ReactiveUI | 19.5+ | 响应式UI框架 |
| Mp4Merger | 自定义 | MP4音视频合并 |

---

## 📊 性能指标

| 指标 | 数值 |
|------|------|
| 缓冲区大小 | 1 MB |
| I/O模式 | 异步 |
| HTTP处理 | 流式 |
| 构建成功率 | 100% |
| 发布就绪度 | 91/100 ⭐⭐⭐⭐⭐ |

---

## 📚 文档

- [架构设计](./Docs/ZH/Architecture.md)
- [代码审查报告](./Docs/ZH/CodeReviewReport.md)
- [多语言支持](./Docs/Multilingual_Support.md)
- [流程图](./Docs/Flowchart/)
- [编译说明](./Releases/BUILD.zh-CN.md)

---

## 🔄 版本历史

### v2.0 (2026-04-18)

- ✅ 升级到 .NET 10.0
- ✅ 多语言支持（英文/中文/俄文）
- ✅ 安全加固
- ✅ 性能优化
- ✅ 模块化架构重构

查看完整 [发布说明](./RELEASE_NOTES.md)

---

## 🤝 贡献

本项目仅供技术研究和学习使用。欢迎反馈和建议：

- B站：https://b23.tv/lMgf5eL
- 邮箱：yhwm2026@outlook.com

---

## 👤 作者

**yaohewoma**

- GitHub：[@yaohewoma](https://github.com/yaohewoma)
- B站：https://b23.tv/lMgf5eL
- 邮箱：yhwm2026@outlook.com

---

## 📄 许可证

本项目仅供**技术研究和学习使用**。

**禁止商业用途。**

使用本软件即表示您同意免责声明中的所有条款和条件。

---

用 ❤️ 为视频流研究而构建
