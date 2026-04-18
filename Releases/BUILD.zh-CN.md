# 编译说明

[English](./BUILD.md) | **简体中文** | [Русский](./BUILD.ru-RU.md)

---

## 环境要求

- Windows 10/11 64位
- .NET 10.0 SDK 或更高版本
- Visual Studio 2022 或 VS Code

## 项目结构

```
video-stream-researcher/
├── video-stream-researcher.csproj    # 主项目文件
├── Program.cs                        # 入口点
├── Core/                             # 核心接口和模型
├── Logging/                          # 日志系统
├── Resources/                        # 多语言资源文件
├── Services/                         # 业务服务
├── UI/                               # Avalonia UI
├── Libraries/                        # 外部库
│   ├── Mp4Merger.Core/              # MP4合并库
│   ├── VideoStreamFetcher/          # 视频下载库
│   ├── VideoPreviewer/              # 视频预览库
│   └── NativeVideoProcessor/        # 原生视频处理
└── ...
```

## 编译步骤

### 1. 克隆仓库

```bash
git clone https://github.com/yaohewoma/video-stream-researcher.git
cd video-stream-researcher
```

### 2. 还原依赖

```bash
dotnet restore
```

### 3. 编译 Debug 版本

```bash
dotnet build
```

### 4. 发布单文件版本

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish
```

### 5. 编译输出

编译后的单文件可执行程序将位于：
```
publish/video-stream-researcher.exe
```

## 配置说明

项目已配置为：
- ✅ 单文件发布（所有依赖打包到一个 exe）
- ✅ 自包含（无需目标机器安装 .NET 运行库）
- ✅ 启用压缩（减小文件体积）
- ✅ 支持 Windows 10/11 64位

## 注意事项

1. 首次编译可能需要下载 NuGet 包，请确保网络连接正常
2. 如果遇到类型冲突错误，请清理解决方案后重新编译：
   ```bash
   dotnet clean
   dotnet build
   ```
3. 发布版本会自动包含所有必要的依赖库

## 技术栈

- .NET 10.0
- Avalonia UI 12.0
- C# 14.0
- 纯 C# 实现，无需 FFmpeg 依赖
