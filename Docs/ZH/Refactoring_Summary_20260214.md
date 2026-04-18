# 视频流研究员项目重构总结报告 (2026-02-14)

## 1. 重构背景与目标

原有的 `MainWindowViewModel` 承担了过多的职责，不仅负责 UI 状态的管理，还包含了复杂的下载业务流程控制（如解析、文件检查、进度回调、错误处理等）。这种“胖 ViewModel”导致代码难以维护、难以测试，且业务逻辑与 UI 耦合严重。

本次重构的主要目标是：
1.  **分离关注点**：将下载业务逻辑从 ViewModel 中剥离，封装到专门的服务层。
2.  **增强可扩展性**：引入工厂模式和接口，使视频解析器的扩展更加容易。
3.  **优化代码结构**：规范化参数传递，减少“基本类型偏执” (Primitive Obsession)。

## 2. 核心变更内容

### 2.1 引入业务流程层 (Application Layer)

创建了新的接口 `IDownloadFlowManager` 及其实现 `DownloadFlowManager`。

*   **职责**：负责协调视频解析、文件存在性检查、下载执行、日志记录和状态更新等全流程。
*   **优势**：ViewModel 不再关心下载的具体步骤，只需调用 `ExecuteDownloadAsync` 方法。
*   **文件**：
    *   `Interfaces/IDownloadFlowManager.cs`
    *   `Services/DownloadFlowManager.cs`

### 2.2 参数对象化 (Parameter Object)

创建了 `DownloadOptions` 类，用于封装下载相关的配置参数。

*   **变更前**：方法签名包含大量布尔值参数（如 `bool audioOnly, bool videoOnly, bool noMerge...`）。
*   **变更后**：统一传递 `DownloadOptions` 对象，提高了代码的可读性和可维护性。
*   **文件**：
    *   `Models/DownloadOptions.cs`

### 2.3 解析器架构升级

为了支持多平台解析的扩展，对解析器模块进行了重构。

*   **抽象接口**：定义了 `IPlatformParser` 接口，规定了所有平台解析器必须实现的行为。
*   **具体实现**：重构了 `BilibiliParser` 和 `MiyousheParser` 以实现该接口。
*   **工厂模式**：引入 `VideoParserFactory`，根据 URL 自动分发到对应的解析器。
*   **文件**：
    *   `VideoStreamFetcher/Parsers/IPlatformParser.cs`
    *   `VideoStreamFetcher/Parsers/VideoParserFactory.cs`

### 2.4 ViewModel 瘦身

大幅简化了 `MainWindowViewModel.cs`。

*   **移除**：删除了数百行的 `HandleProcessingClick` 和 `StartProcessing` 逻辑。
*   **保留**：仅保留 UI 属性定义、命令绑定以及对 `DownloadFlowManager` 的调用。

## 3. 架构设计更新

新的架构层次如下：

1.  **UI Layer (View)**: `MainWindow` - 负责界面展示。
2.  **Presentation Layer (ViewModel)**: `MainWindowViewModel` - 负责 UI 逻辑和状态绑定。
3.  **Application Layer (Flow Manager)**: `DownloadFlowManager` - 负责业务流程编排。
4.  **Domain/Service Layer**:
    *   `DownloadManager`: 核心下载逻辑。
    *   `VideoParser`: 视频解析逻辑（通过 `VideoParserFactory` 路由）。
    *   `LogManager`, `NetworkSpeedMonitor`: 基础设施服务。

## 4. 文件变更清单

| 类型 | 文件路径 | 说明 |
| :--- | :--- | :--- |
| **新增** | `Interfaces/IDownloadFlowManager.cs` | 下载流程管理器接口 |
| **新增** | `Services/DownloadFlowManager.cs` | 下载流程管理器实现 |
| **新增** | `Models/DownloadOptions.cs` | 下载参数模型 |
| **新增** | `VideoStreamFetcher/Parsers/IPlatformParser.cs` | 平台解析器接口 |
| **新增** | `VideoStreamFetcher/Parsers/VideoParserFactory.cs` | 解析器工厂 |
| **修改** | `ViewModels/MainWindowViewModel.cs` | 移除业务逻辑，调用 FlowManager |
| **修改** | `UI/MainWindow.axaml.cs` | 注入新服务 |
| **修改** | `Infrastructure/DependencyInjectionConfig.cs` | 注册新服务 |
| **修改** | `Models/AppModels.cs` | 清理冗余定义 |

## 5. 后续建议

1.  **单元测试**：由于业务逻辑已独立，建议为 `DownloadFlowManager` 编写单元测试。
2.  **UI 优化**：当前的日志和状态更新通过 `Action` 回调传递，未来可考虑使用 `IProgress<T>` 或事件流 (Rx) 进一步解耦。
3.  **异常处理**：目前的异常处理主要集中在 FlowManager 中，可以进一步细化特定异常的处理策略。
