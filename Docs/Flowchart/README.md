# 视频流研究器 - 流程图文档

本文档包含 `video-stream-researcher` 项目的完整应用程序流程图，使用 Mermaid 语法绘制。

## 文件清单

| 文件名 | 说明 | 内容概述 |
|-------|------|---------|
| [Architecture-Flowchart.mmd](./Architecture-Flowchart.mmd) | 架构流程图 | 展示项目整体架构层次和各层组件关系 |
| [Main-Download-Flow.mmd](./Main-Download-Flow.mmd) | 主下载流程图 | 展示用户从输入URL到下载完成的完整流程 |
| [Bilibili-Login-Flow.mmd](./Bilibili-Login-Flow.mmd) | B站登录流程图 | 展示B站二维码登录的完整流程 |
| [Data-Flow-Diagram.mmd](./Data-Flow-Diagram.mmd) | 数据流转图 | 展示数据在各组件之间的流转路径 |
| [State-Machine.mmd](./State-Machine.mmd) | 状态机图 | 展示下载、登录、任务的状态流转 |

## 查看方式

### 方式1: 使用 Mermaid Live Editor

1. 访问 [Mermaid Live Editor](https://mermaid.live/)
2. 复制 `.mmd` 文件内容到编辑器
3. 即可查看渲染后的流程图

### 方式2: 使用 VS Code 插件

安装以下插件之一：
- [Markdown Preview Mermaid Support](https://marketplace.visualstudio.com/items?itemName=bierner.markdown-mermaid)
- [Mermaid Preview](https://marketplace.visualstudio.com/items?itemName=vstirbu.vscode-mermaid-preview)

### 方式3: 导入 Figma

1. 使用 [Mermaid to Figma](https://www.figma.com/community/plugin/816856885536551458) 插件
2. 将 `.mmd` 文件内容导入 Figma
3. 即可在 Figma 中编辑和协作

## 流程图说明

### 1. 架构流程图 (Architecture-Flowchart.mmd)

展示项目的 MVVM 架构层次：
- **表示层 (UI Layer)**: MainWindow, QrCodeWindow, PreviewWindow
- **视图模型层 (ViewModel)**: MainWindowViewModel
- **应用流程层 (App Flow)**: DownloadFlowManager
- **服务层 (Services)**: DownloadManager, VideoParser, ConfigManager 等
- **接口层 (Interfaces)**: 所有服务接口定义
- **模型层 (Models)**: AppConfig, DownloadOptions, VideoInfo 等
- **日志层 (Logs)**: LogManager, NetworkSpeedMonitor
- **基础设施层 (Infrastructure)**: DependencyInjectionConfig

### 2. 主下载流程图 (Main-Download-Flow.mmd)

展示用户下载视频的完整流程：
1. 输入视频 URL
2. 检测特殊触发（如 'bili' 登录）
3. 选择保存路径
4. 选择下载选项（完整视频/仅音频/仅视频/不合并）
5. 执行下载流程
   - 检查重复任务
   - 检查文件存在
   - 解析视频信息
   - 下载视频流
   - 合并音视频（如需要）
6. 下载完成处理

### 3. B站登录流程图 (Bilibili-Login-Flow.mmd)

展示 B 站二维码登录流程：
1. 输入 'bili' 触发登录
2. 生成并显示二维码
3. 等待用户扫码
4. 状态流转：NotLoggedIn → WaitingForScan → ScannedWaitingConfirm → LoggedIn
5. 登录成功/失败处理

### 4. 数据流转图 (Data-Flow-Diagram.mmd)

展示数据在系统中的流转：
- **解析数据流**: URL → VideoParser → VideoInfo
- **下载数据流**: VideoInfo + Options → DownloadManager → 输出文件
- **进度数据流**: 进度/速度/状态 → Reporter → UI 更新
- **配置数据流**: 用户设置 → ConfigManager → config.json → ViewModel

### 5. 状态机图 (State-Machine.mmd)

展示三种核心状态机：
- **下载状态机**: Ready → Parsing → Checking → Downloading → Processing → Completed
- **登录状态机**: NotLoggedIn → WaitingForScan → ScannedWaitingConfirm → LoggedIn
- **任务状态机**: Idle → InProgress → [Duplicate/Existing/NewTask] → Executing → [Success/Failed/Cancelled]

## 设计规范

### 颜色编码

| 颜色 | 用途 | 十六进制 |
|-----|------|---------|
| 🟢 绿色 | 开始/结束/成功 | #4CAF50 |
| 🔵 蓝色 | 处理操作 | #2196F3 |
| 🟠 橙色 | 判断决策 | #FF9800 |
| 🔴 红色 | 错误/失败 | #F44336 |
| 🟣 紫色 | 子流程/状态 | #9C27B0 |
| 🔵 青色 | 用户交互 | #00BCD4 |

### 节点形状

| 形状 | 用途 |
|-----|------|
| 圆角矩形 | 开始/结束 |
| 矩形 | 处理操作 |
| 菱形 | 判断决策 |
| 子图 | 复杂流程模块 |

## 使用建议

1. **架构设计**: 先查看 Architecture-Flowchart 了解整体架构
2. **业务理解**: 查看 Main-Download-Flow 和 Bilibili-Login-Flow 理解核心业务流程
3. **数据理解**: 查看 Data-Flow-Diagram 理解数据流转
4. **状态理解**: 查看 State-Machine 理解状态流转逻辑

## 更新维护

当项目代码发生变化时，请同步更新对应的流程图：
- 新增服务/接口 → 更新 Architecture-Flowchart
- 修改下载逻辑 → 更新 Main-Download-Flow
- 修改登录流程 → 更新 Bilibili-Login-Flow
- 修改数据模型 → 更新 Data-Flow-Diagram
- 修改状态流转 → 更新 State-Machine

## 导出图片

如需导出为图片，可使用以下方式：

### Mermaid CLI

```bash
# 安装 mermaid-cli
npm install -g @mermaid-js/mermaid-cli

# 导出为 PNG
mmdc -i Architecture-Flowchart.mmd -o Architecture-Flowchart.png

# 导出为 SVG
mmdc -i Architecture-Flowchart.mmd -o Architecture-Flowchart.svg
```

### 在线工具

使用 [Mermaid Live Editor](https://mermaid.live/) 导出 PNG/SVG/PDF 格式。

---

**文档版本**: 1.0  
**创建日期**: 2026-03-20  
**项目**: video-stream-researcher
