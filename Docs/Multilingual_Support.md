# Video Stream Researcher - 多语言适配文档

## 📋 概述

Video Stream Researcher 支持三种语言：
- 🇨🇳 **简体中文** (zh-CN) - 默认语言
- 🇺🇸 **English** (en-US)
- 🇷🇺 **Русский** (ru-RU)

## 🏗️ 架构设计

### 多语言服务架构

```
┌─────────────────────────────────────────────────────────────┐
│                    多语言适配架构                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────────┐    ┌─────────────────────┐        │
│  │  FetcherLocalization │    │  MergerLocalization │        │
│  │  (VideoStreamFetcher)│    │  (Mp4Merger.Core)   │        │
│  └──────────┬──────────┘    └──────────┬──────────┘        │
│             │                          │                   │
│             ▼                          ▼                   │
│  ┌─────────────────────────────────────────────┐          │
│  │              资源字典 (Dictionary)            │          │
│  ├─────────────────────────────────────────────┤          │
│  │  ZhCnResources  │  EnUsResources  │ RuRuResources │    │
│  │   (中文)        │   (English)     │  (Русский)   │    │
│  └─────────────────────────────────────────────┘          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## 📁 文件结构

### 本地化服务文件

```
src/
├── Mp4Merger.Core/
│   └── Localization/
│       └── MergerLocalization.cs      # MP4合并库本地化
│
├── VideoStreamFetcher/
│   └── Localization/
│       └── FetcherLocalization.cs     # 视频流获取库本地化
│
└── video-stream-researcher/
    └── Docs/
        ├── EN/                        # 英文文档
        ├── RU/                        # 俄文文档
        └── ZH/                        # 中文文档
```

## 🔧 实现方式

### 1. 基于字典的本地化实现

#### FetcherLocalization.cs
```csharp
public static class FetcherLocalization
{
    private static CultureInfo _currentCulture = CultureInfo.CurrentCulture;
    
    // 中文资源
    private static readonly Dictionary<string, string> ZhCnResources = new()
    {
        ["Parsing.RequestingUrl"] = "正在请求 URL: {0}",
        ["Download.Starting"] = "开始下载: {0}",
        // ...
    };
    
    // 英文资源
    private static readonly Dictionary<string, string> EnUsResources = new()
    {
        ["Parsing.RequestingUrl"] = "Requesting URL: {0}",
        ["Download.Starting"] = "Starting download: {0}",
        // ...
    };
    
    // 俄文资源
    private static readonly Dictionary<string, string> RuRuResources = new()
    {
        ["Parsing.RequestingUrl"] = "Запрос URL: {0}",
        ["Download.Starting"] = "Начало загрузки: {0}",
        // ...
    };
}
```

#### MergerLocalization.cs
```csharp
public static class MergerLocalization
{
    private static string _currentLanguage = "zh-CN";
    
    // 同样包含 ZhCnResources、EnUsResources、RuRuResources
    // 专注于 MP4 合并相关的本地化字符串
}
```

### 2. 使用方式

#### 基础用法
```csharp
// 获取本地化字符串
string message = FetcherLocalization.GetString("Download.Starting", videoTitle);

// 设置语言
FetcherLocalization.SetLanguage("en-US");
MergerLocalization.SetLanguage("en-US");
```

#### 在代码中的应用
```csharp
// 老版本 - 硬编码中文
statusCallback?.Invoke("开始下载视频流...");

// 新版本 - 使用本地化
statusCallback?.Invoke(FetcherLocalization.GetString("Download.VideoStream", url));
```

## 📊 本地化资源统计

### FetcherLocalization 资源统计

| 分类 | 中文 | 英文 | 俄文 | 键名前缀 |
|------|------|------|------|----------|
| **解析相关** | 27 | 27 | 27 | `Parsing.` |
| **B站解析** | 22 | 22 | 22 | `Bilibili.` |
| **下载相关** | 17 | 17 | 17 | `Download.` |
| **通用消息** | 4 | 4 | 4 | `Common.` |
| **总计** | **70** | **70** | **70** | - |

### MergerLocalization 资源统计

| 分类 | 中文 | 英文 | 俄文 | 键名前缀 |
|------|------|------|------|----------|
| **合并流程** | 11 | 11 | 11 | `Merge.` |
| **媒体处理** | 8 | 8 | 8 | `Processor.` |
| **验证器** | 9 | 9 | 9 | `Validator.` |
| **写入器** | 8 | 8 | 8 | `Writer.` |
| **提取器** | 13 | 13 | 13 | `Extractor.` |
| **解析器** | 3 | 3 | 3 | `Parser.` |
| **轨道重建** | 19 | 19 | 19 | `Reconstructor.` |
| **服务** | 3 | 3 | 3 | `Service.` |
| **通用** | 7 | 7 | 7 | `Common.` |
| **总计** | **81** | **81** | **81** | - |

### 总体统计

| 语言 | 资源键数量 | 覆盖率 |
|------|-----------|--------|
| 简体中文 | 151 | 100% |
| English | 151 | 100% |
| Русский | 151 | 100% |

## 🌐 支持的语言代码

| 语言代码 | 语言名称 | 状态 |
|----------|----------|------|
| `zh-CN` | 简体中文 | ✅ 完整支持 |
| `en-US` | English | ✅ 完整支持 |
| `ru-RU` | Русский | ✅ 完整支持 |

## 📝 键名命名规范

### 命名规则
```
[模块].[功能].[描述]
```

### 示例
```
Parsing.RequestingUrl          # 解析模块 - 请求URL
Bilibili.DashFormat           # B站解析 - Dash格式
Download.Merging              # 下载模块 - 合并中
Merge.Completed               # 合并模块 - 完成
Validator.VideoSize           # 验证器 - 视频大小
```

### 模块前缀对照表

| 前缀 | 模块 | 所在文件 |
|------|------|----------|
| `Parsing.` | 视频解析 | FetcherLocalization |
| `Bilibili.` | B站解析 | FetcherLocalization |
| `Download.` | 下载流程 | FetcherLocalization |
| `Merge.` | MP4合并 | MergerLocalization |
| `Processor.` | 媒体处理 | MergerLocalization |
| `Validator.` | 验证器 | MergerLocalization |
| `Writer.` | 文件写入 | MergerLocalization |
| `Extractor.` | 数据提取 | MergerLocalization |
| `Parser.` | fMP4解析 | MergerLocalization |
| `Reconstructor.` | 轨道重建 | MergerLocalization |
| `Service.` | 服务层 | MergerLocalization |
| `Common.` | 通用 | 两者都有 |

## 🔄 语言切换机制

### 运行时切换
```csharp
// 切换到英文
FetcherLocalization.SetLanguage("en-US");
MergerLocalization.SetLanguage("en-US");

// 切换到俄文
FetcherLocalization.SetLanguage("ru-RU");
MergerLocalization.SetLanguage("ru-RU");

// 切换回中文
FetcherLocalization.SetLanguage("zh-CN");
MergerLocalization.SetLanguage("zh-CN");
```

### 自动回退机制
```csharp
// 如果请求的语言不存在，自动回退到中文
private static Dictionary<string, string> GetCurrentResources()
{
    return CurrentCulture.Name switch
    {
        "en-US" => EnUsResources,
        "ru-RU" => RuRuResources,
        _ => ZhCnResources  // 默认中文
    };
}
```

## 🎨 格式化和占位符

### 支持的格式化
```csharp
// 字符串插值
FetcherLocalization.GetString("Parsing.RequestingUrl", url);
// 中文: "正在请求 URL: https://..."
// 英文: "Requesting URL: https://..."

// 数字格式化
MergerLocalization.GetString("Validator.VideoSize", 15.5);
// 中文: "📊 视频大小：15.50 MB"
// 英文: "📊 Video size: 15.50 MB"

// 日期格式化
MergerLocalization.GetString("Merge.StartTask", DateTime.Now);
// 中文: "📋 开始合并任务：2024-01-15 10:30:00"
// 英文: "📋 Starting merge task: 2024-01-15 10:30:00"

// 多参数
FetcherLocalization.GetString("Bilibili.StreamInfo", 
    "1080P", 80, "5000K", "50MB", "https://...");
```

## 📦 项目文件配置

### video-stream-researcher.csproj
```xml
<PropertyGroup>
    <NeutralLanguage>zh-CN</NeutralLanguage>
    <SatelliteResourceLanguages>zh-CN;en-US;ru-RU</SatelliteResourceLanguages>
</PropertyGroup>
```

## 🚀 扩展新语言

### 添加新语言的步骤

1. **在 FetcherLocalization.cs 中添加资源字典**
```csharp
private static readonly Dictionary<string, string> FrFrResources = new()
{
    ["Parsing.RequestingUrl"] = "Demande d'URL: {0}",
    // ... 其他键值对
};
```

2. **在 GetCurrentResources 方法中添加分支**
```csharp
private static Dictionary<string, string> GetCurrentResources()
{
    return CurrentCulture.Name switch
    {
        "en-US" => EnUsResources,
        "ru-RU" => RuRuResources,
        "fr-FR" => FrFrResources,  // 新增
        _ => ZhCnResources
    };
}
```

3. **在 MergerLocalization.cs 中重复上述步骤**

4. **更新项目文件**
```xml
<SatelliteResourceLanguages>zh-CN;en-US;ru-RU;fr-FR</SatelliteResourceLanguages>
```

## ✅ 最佳实践

### 1. 键名设计
- 使用有意义的键名，如 `Download.Starting` 而非 `Msg001`
- 按模块分组，便于维护
- 使用驼峰命名法

### 2. 字符串设计
- 保持简洁明了
- 使用占位符 `{0}`, `{1}` 等代替硬编码值
- 添加适当的表情符号增强可读性 📊 ✅ ❌

### 3. 一致性
- 同一术语在不同地方保持一致
- 三种语言的键名完全一致
- 格式化参数数量和顺序一致

### 4. 测试
- 每种语言都要完整测试
- 验证占位符替换正确
- 检查特殊字符显示正常

## 📚 相关文档

- [架构设计](Architecture.md)
- [代码审查报告](CodeReviewReport.md)
- [持续优化](ContinuousOptimization.md)

---

**最后更新**: 2026-04-18  
**版本**: v2.0  
**维护者**: yaohewoma
