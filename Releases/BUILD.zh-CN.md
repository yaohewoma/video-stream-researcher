# 编译说明

[English](./BUILD.md) | **简体中文** | [Русский](./BUILD.ru-RU.md)

---

## 环境要求

- Windows 10/11 64位
- .NET 10.0 SDK 或更高版本
- Visual Studio 2022（推荐）或 VS Code

## 快速开始

### 方式 1: Visual Studio 2022（推荐）

1. 打开 `video-stream-researcher.sln` 解决方案文件
2. 选择 `Release` 配置
3. 右键解决方案 → 生成解决方案
4. 或使用发布功能生成单文件可执行程序

### 方式 2: 命令行

```bash
# 克隆仓库
git clone https://github.com/yaohewoma/video-stream-researcher.git
cd video-stream-researcher

# 还原依赖
dotnet restore video-stream-researcher.sln

# 编译解决方案
dotnet build video-stream-researcher.sln -c Release

# 发布单文件版本
dotnet publish video-stream-researcher.csproj -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish
```

## 项目结构

```
video-stream-researcher/
├── video-stream-researcher.sln       # 解决方案文件
├── video-stream-researcher.csproj    # 主项目
├── Core/                             # 核心接口和模型
├── Logging/                          # 日志系统
├── Resources/                        # 多语言资源
├── Services/                         # 业务服务
├── UI/                               # Avalonia UI
├── Libraries/                        # 外部库
│   ├── Mp4Merger.Core/
│   ├── VideoStreamFetcher/
│   ├── VideoPreviewer/
│   └── NativeVideoProcessor/
└── ...
```

## 配置说明

项目已配置为：
- ✅ 单文件发布
- ✅ 自包含部署
- ✅ 启用压缩
- ✅ 支持 Windows 10/11 64位

## 编译输出

编译后的可执行程序位于：
```
publish/video-stream-researcher.exe
```

## 技术栈

- .NET 10.0
- Avalonia UI 12.0
- C# 14.0
- 纯 C# 实现，无需 FFmpeg 依赖
