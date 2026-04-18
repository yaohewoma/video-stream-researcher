# NetworkSpeedMonitor 类文档（弃用）

## 1. 类概述

`NetworkSpeedMonitor` 是视频流研究工具中的一个核心组件，负责实时监控和可视化显示下载速度和处理进度。该类基于 Avalonia UI 框架实现，提供了流畅的进度条显示和速度监控功能。

### 1.1 主要功能

- 实时显示当前下载或合并速度
- 动态绘制处理进度条，从底部到顶部平滑移动
- 根据处理状态自动切换颜色（处理中：绿色渐变 → 处理完成：灰绿色渐变）
- 支持进度百分比文本显示，跟随进度条移动，带有渐变背景
- 提供速度格式化显示（B/s, KB/s, MB/s, GB/s）
- 支持更新当前处理状态文本
- 实现平滑过渡动画，提高视觉体验
- 支持重置和停止监控功能

## 2. 类结构

### 2.1 命名空间

```csharp
namespace bzhan_avalonia.Logs;
```

### 2.2 核心成员变量

| 变量名                    | 类型                 | 描述                         |
| ---------------------- | ------------------ | -------------------------- |
| `_chartUpdateTimer`    | `DispatcherTimer?` | 用于定期更新图表的定时器               |
| `_isDrawing`           | `bool`             | 标记是否正在绘制图表，防止重入            |
| `_processingProgress`  | `double`           | 当前显示进度（0-100）              |
| `_targetProgress`      | `double`           | 目标进度（0-100），用于实现平滑过渡动画     |
| `_isProcessing`        | `bool`             | 标记是否正在处理文件                 |
| `_processingCompleted` | `bool`             | 标记处理是否完成，用于切换颜色            |
| `_chartCanvas`         | `Canvas?`          | 用于绘制图表的 Avalonia Canvas 控件 |
| `_speedTextBlock`      | `TextBlock?`       | 用于显示当前速度的 TextBlock 控件     |
| `_statusTextBlock`     | `TextBlock?`       | 用于显示当前状态的 TextBlock 控件（可选） |

### 2.3 构造函数

```csharp
public NetworkSpeedMonitor(Canvas chartCanvas, TextBlock speedTextBlock, TextBlock? statusTextBlock = null)
```

**参数说明：**

- `chartCanvas`: 用于绘制进度条的 Canvas 控件
- `speedTextBlock`: 用于显示当前速度的 TextBlock 控件
- `statusTextBlock`: 用于显示当前状态的 TextBlock 控件（可选参数）

**初始化操作：**

- 保存传入的控件引用
- 初始化定时器，设置刷新频率为50ms
- 启动定时器并绑定 Tick 事件
- 初始化 Canvas 绘制事件（AttachedToVisualTree 事件）

## 3. 主要方法

### 3.1 公共方法

#### 3.1.1 `UpdateCurrentStatus(string status)`

**功能：** 更新当前处理状态文本

**参数：**

- `status`: 状态文本（如 "就绪"、"处理中..." 等）

**操作：**

- 在UI线程上更新 `_statusTextBlock` 的文本
- 设置状态文本颜色为淡绿色

#### 3.1.2 `UpdateProcessingProgress(double progress)`

**功能：** 更新处理进度

**参数：**

- `progress`: 进度值（0-100）

**操作：**

- 设置 `_isProcessing` 为 `true`
- 更新 `_targetProgress` 为目标进度值（确保在0-100范围内）
- 进度会通过平滑过渡动画逐渐达到目标值

#### 3.1.3 `SetVideoDuration(long durationSeconds)`

**功能：** 设置视频时长（保留方法但不使用）

**参数：**

- `durationSeconds`: 视频时长（秒）

**操作：**

- 空实现，不使用视频时长调整数据点数量

#### 3.1.4 `MarkDownloadCompleted()`

**功能：** 标记下载完成

**操作：**

- 设置 `_processingCompleted` 为 `true`
- 设置 `_isProcessing` 为 `false`
- 触发进度条颜色从绿色变为灰绿色

#### 3.1.5 `OnSpeedUpdate(long speedBytesPerSecond, bool isMerge = false)`

**功能：** 处理实时网速数据更新

**参数：**

- `speedBytesPerSecond`: 每秒字节数
- `isMerge`: 是否为合并速度（默认：false）

**操作：**

- 格式化网速显示
- 在UI线程上更新 `_speedTextBlock` 的文本
- 区分下载速度和合并速度
- 设置速度文本颜色为淡绿色

#### 3.1.6 `Reset()`

**功能：** 重置网络速度监控

**操作：**

- 重置所有状态变量（`_processingProgress`、`_targetProgress`、`_isProcessing`、`_processingCompleted`）
- 清空速度文本
- 重绘图表

#### 3.1.7 `Stop()`

**功能：** 停止监控

**操作：**

- 停止定时器
- 移除定时器事件监听

### 3.2 私有方法

#### 3.2.1 `ChartUpdateTimer_Tick(object? sender, EventArgs e)`

**功能：** 定时器 Tick 事件处理方法

**操作：**

- 检查 `_isDrawing` 标记，防止重入
- 实现平滑过渡动画：如果当前进度与目标进度相差超过0.1，使用缓动算法逐渐接近目标进度
- 调用 `DrawSpeedChart()` 方法更新图表

#### 3.2.2 `DrawSpeedChart()`

**功能：** 绘制网速图表和进度条

**操作：**

- 清空画布
- 获取画布尺寸
- 计算进度条的Y轴位置（进度0%在底部，100%在顶部）
- 根据处理状态选择颜色（处理中：绿色渐变；处理完成：灰绿色渐变）
- 绘制水平进度条，使用线性渐变效果
- 绘制带有渐变背景的进度百分比文本，跟随进度条移动

#### 3.2.3 `FormatSpeed(long bytesPerSecond)`

**功能：** 格式化网速显示

**参数：**

- `bytesPerSecond`: 每秒字节数

**返回值：** 格式化后的网速字符串（如 "5.8 MB/s"）

**格式化规则：**

- < 1024 B/s: 显示为 B/s
- 1024 B/s - 1024 KB/s: 显示为 KB/s，保留一位小数
- 1024 KB/s - 1024 MB/s: 显示为 MB/s，保留一位小数
- <br />
  > 1024 MB/s: 显示为 GB/s，保留一位小数

## 4. 实现细节

### 4.1 进度条绘制

进度条采用水平直线设计，根据处理进度从底部向上移动：

- 进度 0% 时，位于画布底部
- 进度 100% 时，位于画布顶部（实际最高高度为画布高度的 90%，留有顶部边距）
- 使用线性渐变效果，增强视觉体验
- 实现平滑过渡动画，通过缓动算法从当前进度逐渐过渡到目标进度

### 4.2 颜色切换机制

| 状态   | 颜色    | RGB 值                                                        | 描述                 |
| ---- | ----- | ------------------------------------------------------------ | ------------------ |
| 处理中  | 绿色渐变  | 开始：(0, 200, 0) → 中间：(0, 255, 100) → 结束：(0, 200, 0)           | 从深绿到亮绿再到深绿的水平渐变    |
| 处理完成 | 灰绿色渐变 | 开始：(100, 150, 100) → 中间：(150, 200, 150) → 结束：(100, 150, 100) | 从深灰绿到亮灰绿再到深灰绿的水平渐变 |

### 4.3 进度文本显示

- 文本内容：当前进度百分比（如 "100.0%"）
- 显示位置：进度条右侧，距离右侧 60 像素，靠近图表中心
- 容器样式：带有渐变背景的圆角边框（半径 3px），内边距 5px
- 文本样式：12px 白色粗体文字，与渐变背景形成对比
- 动态跟随：文本容器始终跟随进度条垂直移动，保持垂直居中

### 4.4 性能优化

- 使用 `_isDrawing` 标记防止重入，避免并发绘制
- 定时器刷新频率设置为 50ms，平衡流畅度和性能
- 只在必要时重绘整个画布
- 采用 Avalonia 的 GPU 加速渲染
- 使用缓动算法减少绘制频率，提高性能
- 只在进度变化超过 0.1% 时才进行平滑过渡

## 5. 使用示例

### 5.1 初始化

```csharp
// 在 XAML 中定义控件
// <Canvas Name="speedChartCanvas" Width="800" Height="200" />
// <TextBlock Name="speedTextBlock" />
// <TextBlock Name="statusTextBlock" />

// 在代码中初始化
var networkSpeedMonitor = new NetworkSpeedMonitor(speedChartCanvas, speedTextBlock, statusTextBlock);

// 或不传入状态文本控件
var networkSpeedMonitor = new NetworkSpeedMonitor(speedChartCanvas, speedTextBlock);
```

### 5.2 更新状态

```csharp
// 更新当前状态
networkSpeedMonitor.UpdateCurrentStatus("就绪");
networkSpeedMonitor.UpdateCurrentStatus("处理中...");
networkSpeedMonitor.UpdateCurrentStatus("处理完成！");
```

### 5.3 更新速度

```csharp
// 更新下载速度
networkSpeedMonitor.OnSpeedUpdate(1024 * 1024 * 5, false); // 5 MB/s 下载速度

// 更新合并速度
networkSpeedMonitor.OnSpeedUpdate(1024 * 512, true); // 512 KB/s 合并速度
```

### 5.4 更新进度

```csharp
// 更新处理进度
networkSpeedMonitor.UpdateProcessingProgress(50.0); // 50%
```

### 5.5 标记完成

```csharp
// 标记下载完成
networkSpeedMonitor.MarkDownloadCompleted();
```

### 5.6 重置

```csharp
// 重置监控
networkSpeedMonitor.Reset();
```

### 5.7 停止

```csharp
// 停止监控
networkSpeedMonitor.Stop();
```

## 6. 设计思路

### 6.1 架构设计

- **UI 与逻辑分离**：将速度监控逻辑与 UI 渲染分离，提高代码可维护性
- **事件驱动**：基于定时器事件驱动，实现流畅的动态效果
- **状态管理**：清晰的状态标记，便于状态切换和颜色管理
- **平滑动画**：实现缓动算法，使进度条平滑过渡，提升用户体验
- **可扩展性**：模块化设计，便于后续功能扩展
- **可选参数支持**：构造函数支持可选的状态文本控件，提高灵活性

### 6.2 颜色设计

- **处理中**：使用鲜艳的绿色渐变，从深绿到亮绿再到深绿，给用户明确的视觉反馈
- **处理完成**：使用灰绿色渐变，从深灰绿到亮灰绿再到深灰绿，与日志内容形成和谐对比，便于查看日志
- **渐变效果**：增强视觉层次感，提升用户体验
- **文本颜色**：速度和状态文本使用淡绿色，既突出显示又不刺眼

### 6.3 性能考量

- **减少重绘区域**：只在必要时重绘整个画布
- **合理的刷新频率**：50ms 的刷新频率，在流畅度和性能之间取得平衡
- **防止内存泄漏**：在 `Stop()` 方法中正确清理资源
- **防止重入**：使用 `_isDrawing` 标记防止并发绘制
- **平滑动画优化**：只在进度变化超过 0.1% 时才进行平滑过渡，减少计算量

## 7. 代码结构

```
NetworkSpeedMonitor.cs
├── 构造函数
├── 事件处理方法
│   └── ChartUpdateTimer_Tick
├── 公共方法
│   ├── UpdateCurrentStatus
│   ├── UpdateProcessingProgress
│   ├── SetVideoDuration
│   ├── MarkDownloadCompleted
│   ├── OnSpeedUpdate
│   ├── Reset
│   └── Stop
└── 私有方法
    ├── DrawSpeedChart
    └── FormatSpeed
```

## 8. 依赖关系

| 依赖项                      | 用途                             |
| ------------------------ | ------------------------------ |
| Avalonia                 | UI 框架，提供画布和控件支持                |
| Avalonia.Controls        | 提供 Canvas、TextBlock 等控件        |
| Avalonia.Controls.Shapes | 提供 Path、PathGeometry 等绘图类      |
| Avalonia.Layout          | 提供布局相关功能                       |
| Avalonia.Media           | 提供画笔、渐变等绘图资源                   |
| Avalonia.Threading       | 提供 DispatcherTimer 和 UI 线程操作支持 |
| System                   | 提供基本类型和时间处理                    |

## 9. 总结

`NetworkSpeedMonitor` 类是视频流研究工具中的一个重要组件，它通过直观的可视化方式向用户展示下载和处理进度。该类设计简洁、性能优良，能够在不同处理阶段提供清晰的视觉反馈。

通过合理的状态管理、颜色设计和平滑动画效果，`NetworkSpeedMonitor` 类不仅实现了功能需求，还提供了出色的用户体验。其模块化的设计便于后续功能扩展和维护，可选参数支持提高了灵活性。

主要特点包括：

- 实时速度监控和显示
- 平滑过渡的进度条动画
- 根据处理状态自动切换颜色
- 支持更新当前状态文本
- 性能优化，防止重入和内存泄漏
- 灵活的构造函数，支持可选参数

## 10. 版本历史

| 版本  | 日期         | 更新内容                               |
| --- | ---------- | ---------------------------------- |
| 1.0 | 2026-01-10 | 初始版本，实现基本功能                        |
| 1.1 | 2026-01-10 | 添加处理完成状态，支持颜色切换                    |
| 1.2 | 2026-01-10 | 优化性能，添加文档                          |
| 1.3 | 2026-01-13 | 新增平滑过渡动画，优化进度条绘制                   |
| 1.4 | 2026-01-13 | 新增 UpdateCurrentStatus 方法，支持更新状态文本 |
| 1.5 | 2026-01-13 | 构造函数支持可选的状态文本控件，提高灵活性              |
| 1.6 | 2026-01-13 | 优化性能，添加 \_targetProgress 变量实现平滑过渡  |

## 11. 作者信息

- 开发团队：视频流研究工具开发组
- 联系方式：[项目地址]()
- 文档作者：AI Assistant
- 文档更新时间：2026-01-13

