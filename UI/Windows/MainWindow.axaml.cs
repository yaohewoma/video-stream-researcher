using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using video_stream_researcher.Infrastructure;
using video_stream_researcher.ViewModels;

namespace video_stream_researcher.UI;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private ServiceProvider? _serviceProvider;
    private PreviewPlayerWindow? _previewWindow;

    // 日志自动滚动相关
    private bool _isAutoScrollEnabled = true; // 默认开启自动滚动
    private bool _isUserScrolling = false; // 是否用户正在滚动

    public MainWindow()
    {
        InitializeComponent();
        
        // 订阅Loaded事件，确保UI元素完全初始化后再进行操作
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 检查UI元素是否初始化成功
        if (statusContainer == null || speedChartCanvas == null || speedText == null)
        {
            throw new InvalidOperationException("UI elements not initialized properly");
        }
        
        // 初始化依赖注入
        _serviceProvider = DependencyInjectionConfig.ConfigureServices();
        
        // 创建需要UI元素的服务实例
        var logManager = new Logs.LogManager(statusContainer, this);
        var networkSpeedMonitor = new Logs.NetworkSpeedMonitor(speedChartCanvas, speedText, statusText);
        
        // 获取其他服务
        var configManager = _serviceProvider.GetRequiredService<Interfaces.IConfigManager>();
        var downloadManager = _serviceProvider.GetRequiredService<Interfaces.IDownloadManager>();
        var downloadFlowManager = _serviceProvider.GetRequiredService<Interfaces.IDownloadFlowManager>();
        var videoParser = _serviceProvider.GetRequiredService<Interfaces.IVideoParser>();
        var bilibiliLoginService = _serviceProvider.GetRequiredService<Interfaces.IBilibiliLoginService>();
        
        // 手动创建ViewModel并注入所有依赖
        _viewModel = new ViewModels.MainWindowViewModel(
            downloadManager,
            downloadFlowManager,
            logManager,
            networkSpeedMonitor,
            configManager,
            videoParser,
            bilibiliLoginService
        );
        DataContext = _viewModel;

        // 绑定事件
        InitializeEvents();
        
        // 订阅二维码窗口显示事件
        _viewModel.ShowQrCodeWindow += ShowQrCodeWindow;
        _viewModel.ShowPreviewWindow += OpenPreviewWindow;

        // 显示程序功能说明
        ShowAppFeatures(logManager);

        // 订阅语言变更事件，切换语言时只清除帮助信息并重新输出功能说明
        Services.LocalizationService.Instance.LanguageChanged += (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                // 只清除帮助信息，不清空全部日志
                logManager.ClearHelpInfoLogs();
                ShowAppFeatures(logManager);
            });
        };

        // 添加Canvas的点击事件处理
        if (cancelButtonCanvas != null)
        {
            cancelButtonCanvas.PointerPressed += BtnCancel_Click;
        }

        // 加载保存的自定义背景
        LoadCustomBackground();
    }

    /// <summary>
    /// 打开预览窗口并加载媒体文件
    /// </summary>
    /// <param name="path">媒体文件路径</param>
    private void OpenPreviewWindow(string path)
    {
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_previewWindow == null || !_previewWindow.IsVisible)
                {
                    _previewWindow = new PreviewPlayerWindow();
                    _previewWindow.Show();
                }

                _previewWindow.LoadMedia(path, _viewModel?.KeepOriginalFiles ?? true);
                _previewWindow.Activate();
            });
        }
        catch
        {
            // 忽略窗口打开异常
        }
    }

    /// <summary>
    /// 初始化UI事件绑定
    /// </summary>
    private void InitializeEvents()
    {
        // 绑定滚动事件
        statusScrollViewer.ScrollChanged += StatusScrollViewer_ScrollChanged;
        statusScrollViewer.PointerWheelChanged += StatusScrollViewer_PointerWheelChanged;

        // 绑定键盘事件
        txtUrl.KeyDown += TxtUrl_KeyDown;

        // 绑定右键菜单事件
        txtUrl.ContextMenu = CreatePasteContextMenu();
    }

    /// <summary>
    /// 创建粘贴右键菜单
    /// </summary>
    private ContextMenu CreatePasteContextMenu()
    {
        var contextMenu = new ContextMenu();

        var pasteMenuItem = new MenuItem
        {
            Header = "粘贴",
            InputGesture = new KeyGesture(Key.V, KeyModifiers.Control)
        };
        pasteMenuItem.Click += async (s, e) => await PasteFromClipboard();

        contextMenu.Items.Add(pasteMenuItem);

        return contextMenu;
    }

    /// <summary>
    /// 从剪贴板粘贴文本到URL输入框
    /// </summary>
    private async Task PasteFromClipboard()
    {
        if (txtUrl == null || _viewModel == null) return;

        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            // Avalonia 12 API: GetTextAsync() 方法已更改
            // 使用反射或替代方法获取剪贴板文本
            var text = await GetClipboardTextAsync(clipboard);
            if (!string.IsNullOrEmpty(text))
            {
                _viewModel.Url = text;
            }
        }
    }

    /// <summary>
    /// 获取剪贴板文本的辅助方法，兼容 Avalonia 12 API 变更
    /// </summary>
    private async Task<string?> GetClipboardTextAsync(IClipboard clipboard)
    {
        try
        {
            // 尝试使用 Avalonia 12 的新 API
            var method = clipboard.GetType().GetMethod("GetTextAsync");
            if (method != null)
            {
                var result = method.Invoke(clipboard, null);
                if (result is Task<string?> task)
                    return await task;
                if (result is ValueTask<string?> valueTask)
                    return await valueTask;
            }
            
            // 尝试使用 GetText 方法
            var getTextMethod = clipboard.GetType().GetMethod("GetText");
            if (getTextMethod != null)
            {
                var result = getTextMethod.Invoke(clipboard, null);
                if (result is string str)
                    return str;
                if (result is Task<string?> task)
                    return await task;
                if (result is ValueTask<string?> valueTask)
                    return await valueTask;
            }
        }
        catch
        {
            // 忽略反射错误
        }
        return null;
    }

    /// <summary>
    /// 鼠标滚轮事件处理 - 向上滚动时关闭自动滚动
    /// </summary>
    private void StatusScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // 如果是向上滚动，关闭自动滚动
        if (e.Delta.Y > 0)
        {
            _isAutoScrollEnabled = false;
            _isUserScrolling = true;
        }
    }
    
    /// <summary>
    /// 滚动位置变化事件处理 - 检测是否滚动到最底部
    /// </summary>
    private void StatusScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            // 检查是否滚动到了最底部
            bool isAtBottom = scrollViewer.Offset.Y >= scrollViewer.ScrollBarMaximum.Y - 1;
            
            if (isAtBottom && _isUserScrolling)
            {
                // 滚动到最底部，重新开启自动滚动
                _isAutoScrollEnabled = true;
                _isUserScrolling = false;
            }
        }
    }
    
    /// <summary>
    /// 添加日志时的滚动处理 - 如果启用自动滚动则滚动到底部
    /// </summary>
    public void ScrollToBottomIfNeeded()
    {
        if (_isAutoScrollEnabled && statusScrollViewer != null)
        {
            // 使用Avalonia内置的滚动方法，简单高效
            statusScrollViewer.ScrollToEnd();
        }
    }

    /// <summary>
    /// URL输入框键盘事件处理 - 回车键触发下载或自定义背景设置
    /// </summary>
    private async void TxtUrl_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // 检查是否触发自定义背景
            if (_viewModel != null && _viewModel.CheckCustomBackgroundTrigger(_viewModel.Url))
            {
                await SelectCustomBackgroundAsync();
                return;
            }

            // 如果正在登录流程中，不执行下载
            if (_viewModel?.IsInLoginProcess == true)
            {
                return;
            }
            
            // 显示取消按钮
            ShowCancelButton();
            // 触发下载方法
            if (_viewModel != null)
            {
                await _viewModel.DownloadAsync();
            }
            // 下载完成后隐藏取消按钮
            HideCancelButton();
        }
    }

    /// <summary>
    /// 选择自定义背景图片
    /// </summary>
    private async Task SelectCustomBackgroundAsync()
    {
        try
        {
            var storageProvider = this.StorageProvider;
            if (storageProvider == null)
            {
                System.Diagnostics.Debug.WriteLine("StorageProvider is null");
                return;
            }

            // 使用 StorageProvider 打开文件选择对话框
            var options = new FilePickerOpenOptions
            {
                Title = "选择背景图片",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片文件")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                    }
                }
            };

            System.Diagnostics.Debug.WriteLine("正在打开文件选择对话框...");
            var result = await storageProvider.OpenFilePickerAsync(options);
            System.Diagnostics.Debug.WriteLine($"文件选择结果: {result.Count} 个文件");

            if (result.Count > 0)
            {
                var selectedFile = result[0];
                System.Diagnostics.Debug.WriteLine($"选择的文件: {selectedFile.Name}");

                // 获取文件的本地路径
                string selectedPath;
                if (selectedFile.Path.IsFile)
                {
                    selectedPath = selectedFile.Path.LocalPath;
                    System.Diagnostics.Debug.WriteLine($"文件路径: {selectedPath}");

                    if (File.Exists(selectedPath))
                    {
                        System.Diagnostics.Debug.WriteLine("文件存在，开始设置背景...");
                        await SetCustomBackgroundAsync(selectedPath);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"文件不存在: {selectedPath}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"不是本地文件: {selectedFile.Path}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("未选择图片，操作已取消");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"选择背景图片失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 设置自定义背景
    /// </summary>
    /// <param name="imagePath">图片路径</param>
    private async Task SetCustomBackgroundAsync(string imagePath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"开始设置背景图片: {imagePath}");

            if (!File.Exists(imagePath))
            {
                System.Diagnostics.Debug.WriteLine($"图片文件不存在: {imagePath}");
                return;
            }

            // 在UI线程上设置背景图片
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("正在加载图片...");

                    // 加载图片
                    using var stream = File.OpenRead(imagePath);
                    var bitmap = new Bitmap(stream);
                    System.Diagnostics.Debug.WriteLine($"图片加载成功，尺寸: {bitmap.Size}");

                    // 设置背景图片
                    if (backgroundImage != null)
                    {
                        backgroundImage.Source = bitmap;
                        backgroundImage.IsVisible = true;
                        System.Diagnostics.Debug.WriteLine("背景图片已设置到UI");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("backgroundImage 控件为null");
                    }

                    // 保存路径到ViewModel
                    if (_viewModel != null)
                    {
                        _viewModel.CustomBackgroundPath = imagePath;
                        System.Diagnostics.Debug.WriteLine("路径已保存到ViewModel");
                    }

                    // 保存到配置
                    SaveBackgroundConfig(imagePath);

                    System.Diagnostics.Debug.WriteLine($"✅ 背景图片设置成功: {Path.GetFileName(imagePath)}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 加载图片失败: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ 设置背景失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 加载保存的自定义背景
    /// </summary>
    private void LoadCustomBackground()
    {
        try
        {
            // 从配置文件读取背景路径
            var configPath = GetBackgroundConfigPath();
            if (File.Exists(configPath))
            {
                var savedPath = File.ReadAllText(configPath).Trim();
                if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
                {
                    _ = SetCustomBackgroundAsync(savedPath);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载背景配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存背景配置
    /// </summary>
    /// <param name="imagePath">图片路径</param>
    private void SaveBackgroundConfig(string imagePath)
    {
        try
        {
            var configPath = GetBackgroundConfigPath();
            var configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            File.WriteAllText(configPath, imagePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存背景配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取背景配置文件路径
    /// </summary>
    /// <returns>配置文件路径</returns>
    private string GetBackgroundConfigPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "VideoStreamResearcher");
        return Path.Combine(appFolder, "background.config");
    }

    private async void BtnCancel_Click(object? sender, PointerPressedEventArgs e)
    {
        // 触发取消方法
        if (_viewModel != null)
        {
            await _viewModel.CancelAsync();
        }
        // 取消后隐藏取消按钮
        HideCancelButton();
    }

    private void BtnThemeToggle_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 触发主题切换方法
        _viewModel?.ThemeToggle();
    }

    private async void BtnBrowse_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var storageProvider = this.StorageProvider;
            if (storageProvider == null)
            {
                System.Diagnostics.Debug.WriteLine("StorageProvider is null");
                return;
            }

            // 使用 StorageProvider 打开文件夹选择对话框
            var options = new FolderPickerOpenOptions
            {
                Title = "选择保存路径",
                AllowMultiple = false
            };

            // 如果当前有保存路径，设置为默认路径
            if (!string.IsNullOrEmpty(_viewModel?.SavePath) && Directory.Exists(_viewModel.SavePath))
            {
                try
                {
                    options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(_viewModel.SavePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设置默认路径失败: {ex.Message}");
                }
            }

            var result = await storageProvider.OpenFolderPickerAsync(options);

            if (result.Count > 0)
            {
                string selectedPath = result[0].Path.LocalPath;
                _viewModel?.SetSavePath(selectedPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"浏览文件夹失败: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
    }

    private async void BtnDownload_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 显示取消按钮
        ShowCancelButton();
        // 触发下载方法
        if (_viewModel != null)
        {
            await _viewModel.DownloadAsync();
        }
        // 下载完成后隐藏取消按钮
        HideCancelButton();
    }

    /// <summary>
    /// 显示取消按钮，无动画
    /// </summary>
    private void ShowCancelButton()
    {
        if (cancelButtonCanvas != null)
        {
            cancelButtonCanvas.IsVisible = true;
        }
    }
    
    /// <summary>
    /// 隐藏取消按钮，无动画
    /// </summary>
    private void HideCancelButton()
    {
        if (cancelButtonCanvas != null)
        {
            cancelButtonCanvas.IsVisible = false;
        }
    }
    
    private QrCodeWindow? _qrCodeWindow;

    /// <summary>
    /// 显示二维码窗口
    /// </summary>
    private void ShowQrCodeWindow(object? sender, byte[] imageData)
    {
        try
        {
            // 关闭之前的窗口
            _qrCodeWindow?.Close();

            _qrCodeWindow = new QrCodeWindow();
            _qrCodeWindow.SetQrCodeImage(imageData);

            // 订阅登录状态变更事件来更新窗口状态
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(_viewModel.LoginStatusText) && _qrCodeWindow != null)
                    {
                        _qrCodeWindow.UpdateStatus(_viewModel.LoginStatusText);
                    }

                    // 登录成功或失败时自动关闭二维码窗口
                    if (e.PropertyName == nameof(_viewModel.IsLoggedIn) && _viewModel.IsLoggedIn)
                    {
                        // 登录成功，延迟关闭窗口以显示成功状态
                        Dispatcher.UIThread.Post(async () =>
                        {
                            await Task.Delay(1500); // 等待1.5秒让用户看到成功状态
                            CloseQrCodeWindow();
                        });
                    }
                    else if (e.PropertyName == nameof(_viewModel.LoginStatusText))
                    {
                        // 检查是否登录失败
                        var status = _viewModel.LoginStatusText;
                        if (status.Contains("失败") || status.Contains("过期") || status.Contains("取消"))
                        {
                            // 登录失败，延迟关闭窗口
                            Dispatcher.UIThread.Post(async () =>
                            {
                                await Task.Delay(2000); // 等待2秒让用户看到失败原因
                                CloseQrCodeWindow();
                            });
                        }
                    }
                };
            }

            // 订阅刷新二维码事件
            _qrCodeWindow.RefreshQrCodeRequested += (s, e) =>
            {
                // 取消当前的登录流程
                _viewModel?.CancelLoginProcess();
                // 重新触发登录
                _viewModel?.StartBilibiliLoginAsync();
            };

            // 窗口关闭时重置登录流程状态
            _qrCodeWindow.Closed += (s, e) =>
            {
                _qrCodeWindow = null;
                _viewModel?.ResetLoginProcessState();
            };

            _qrCodeWindow.Show(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"显示二维码窗口失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 关闭二维码窗口
    /// </summary>
    private void CloseQrCodeWindow()
    {
        if (_qrCodeWindow != null)
        {
            try
            {
                _qrCodeWindow.Close();
                _qrCodeWindow = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭二维码窗口失败: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 显示下载完成的对勾
    /// </summary>
    private async Task ShowCompletion()
    {
        if (cancelXPath == null)
            return;
            
        // 保存原始样式
        Geometry? originalGeometry = cancelXPath.Data as Geometry;
        var originalPathStroke = cancelXPath.Stroke ?? new SolidColorBrush(Colors.Red);
        
        // 将X符号改为对勾符号，适配36x36按钮尺寸
        cancelXPath.Data = Geometry.Parse("M10,18 L15,23 L26,12");
        cancelXPath.Stroke = new SolidColorBrush(Colors.Green);
        
        // 等待1秒后隐藏按钮
        await Task.Delay(1000);
        
        // 隐藏按钮
        HideCancelButton();
        
        // 恢复X符号
        if (originalGeometry != null)
        {
            cancelXPath.Data = originalGeometry;
        }
        else
        {
            cancelXPath.Data = Geometry.Parse("M10,10 L26,26 M26,10 L10,26");
        }
        // 恢复X符号颜色
        cancelXPath.Stroke = originalPathStroke;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _serviceProvider?.Dispose();
    }

    /// <summary>
    /// 显示程序功能说明
    /// </summary>
    private void ShowAppFeatures(Logs.LogManager logManager)
    {
        var loc = Services.LocalizationService.Instance;

        logManager.UpdateHelpInfoLog("");
        logManager.UpdateHelpInfoLog(loc["AppFeatureTitle"]);
        logManager.UpdateHelpInfoLog(loc["AppFeatureHeader"]);
        logManager.UpdateHelpInfoLog(loc["AppFeatureSeparator"]);
        logManager.UpdateHelpInfoLog(loc["AppFeature1"]);
        logManager.UpdateHelpInfoLog(loc["AppFeature2"]);
        logManager.UpdateHelpInfoLog(loc["AppFeature3"]);
        logManager.UpdateHelpInfoLog(loc["AppFeature4"]);
        logManager.UpdateHelpInfoLog(loc["AppFeatureFooter"]);
        logManager.UpdateHelpInfoLog("");
    }
}
