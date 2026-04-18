using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using video_stream_researcher.Interfaces;
using video_stream_researcher.Models;
using video_stream_researcher.Services;
using VideoStreamFetcher.Parsers;

namespace video_stream_researcher.ViewModels;

/// <summary>
/// 主窗口视图模型
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IDownloadManager _downloadManager;
    private readonly IDownloadFlowManager _downloadFlowManager;
    private readonly ILogManager _logManager;
    private readonly INetworkSpeedMonitor _networkSpeedMonitor;
    private readonly IConfigManager _configManager;
    private readonly IVideoParser _videoParser;
    private readonly IBilibiliLoginService _bilibiliLoginService;

    /// <summary>
    /// 获取日志管理器
    /// </summary>
    public ILogManager LogManager => _logManager;

    // 主题相关属性
    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set 
        {
            if (_isDarkTheme != value)
            {
                _isDarkTheme = value;
                RaisePropertyChanged(nameof(IsDarkTheme));
            }
        }
    }

    // 命令替换为方法
    public async Task BrowseAsync() => await BrowseAsyncImpl();
    public async Task DownloadAsync() => await DownloadAsyncImpl();
    public async Task CancelAsync() => await CancelAsyncImpl();
    public void ThemeToggle() => ThemeToggleImpl();
    public void EnterOptionMode() => EnterOptionModeImpl();

    public event Action<string>? ShowPreviewWindow;

    // 属性
    private string _url = string.Empty;
    public string Url
    {
        get => _url;
        set 
        {
            if (_url != value)
            {
                _url = value;
                RaisePropertyChanged(nameof(Url));
                // 检测输入"bili"触发登录
                CheckBiliLoginTrigger(value);
            }
        }
    }

    private string? _customBackgroundPath;
    /// <summary>
    /// 自定义背景图片路径
    /// </summary>
    public string? CustomBackgroundPath
    {
        get => _customBackgroundPath;
        set => this.RaiseAndSetIfChanged(ref _customBackgroundPath, value);
    }

    private string _savePath = string.Empty;
    public string SavePath
    {
        get => _savePath;
        set => this.RaiseAndSetIfChanged(ref _savePath, value);
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set 
        { 
            if (_isDownloading != value)
            {
                _isDownloading = value;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    this.RaisePropertyChanged(nameof(IsDownloading));
                });
            }
        }
    }

    private bool _isAudioOnly;
    public bool IsAudioOnly
    {
        get => _isAudioOnly;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _isAudioOnly, value);
            if (value)
            {
                IsDownloadAll = false;
                IsNoMerge = false;
                IsVideoOnly = false;
            }
        }
    }

    private bool _isVideoOnly;
    public bool IsVideoOnly
    {
        get => _isVideoOnly;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _isVideoOnly, value);
            if (value)
            {
                IsDownloadAll = false;
                IsNoMerge = false;
                IsAudioOnly = false;
            }
        }
    }

    private bool _isNoMerge;
    public bool IsNoMerge
    {
        get => _isNoMerge;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _isNoMerge, value);
            if (value)
            {
                IsDownloadAll = false;
                IsAudioOnly = false;
                IsVideoOnly = false;
            }
        }
    }

    private bool _isDownloadAll = true;
    public bool IsDownloadAll
    {
        get => _isDownloadAll;
        set 
        { 
            this.RaiseAndSetIfChanged(ref _isDownloadAll, value);
            if (value)
            {
                IsNoMerge = false;
                IsAudioOnly = false;
                IsVideoOnly = false;
            }
        }
    }

    private bool _previewEnabled;
    public bool PreviewEnabled
    {
        get => _previewEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _previewEnabled, value);
            RaisePropertyChanged(nameof(IsPreviewSegmentsInputEnabled));
        }
    }

    private bool _previewSegmentsEnabled;
    public bool PreviewSegmentsEnabled
    {
        get => _previewSegmentsEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _previewSegmentsEnabled, value);
            RaisePropertyChanged(nameof(IsPreviewSegmentsInputEnabled));
        }
    }

    public bool IsPreviewSegmentsInputEnabled => PreviewEnabled && PreviewSegmentsEnabled;

    private string _previewSegmentsText = "60";
    public string PreviewSegmentsText
    {
        get => _previewSegmentsText;
        set => this.RaiseAndSetIfChanged(ref _previewSegmentsText, value);
    }

    private bool _keepOriginalFiles = true;
    public bool KeepOriginalFiles
    {
        get => _keepOriginalFiles;
        set => this.RaiseAndSetIfChanged(ref _keepOriginalFiles, value);
    }

    private bool _autoPreviewAfterDownload;
    public bool AutoPreviewAfterDownload
    {
        get => _autoPreviewAfterDownload;
        set => this.RaiseAndSetIfChanged(ref _autoPreviewAfterDownload, value);
    }

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private string _speedText = "当前速度: 0 KB/s";
    public string SpeedText
    {
        get => _speedText;
        set => this.RaiseAndSetIfChanged(ref _speedText, value);
    }

    private bool _isOptionMode;
    public bool IsOptionMode
    {
        get => _isOptionMode;
        set => this.RaiseAndSetIfChanged(ref _isOptionMode, value);
    }

    private bool _isConfirmMode;
    public bool IsConfirmMode
    {
        get => _isConfirmMode;
        set => this.RaiseAndSetIfChanged(ref _isConfirmMode, value);
    }

    private VideoInfo? _pendingVideoInfo;
    public VideoInfo? PendingVideoInfo
    {
        get => _pendingVideoInfo;
        set => this.RaiseAndSetIfChanged(ref _pendingVideoInfo, value);
    }

    private bool _isFFmpegEnabled;
    public bool IsFFmpegEnabled
    {
        get => _isFFmpegEnabled;
        set => this.RaiseAndSetIfChanged(ref _isFFmpegEnabled, value);
    }

    private int _mergeMode;
    public int MergeMode
    {
        get => _mergeMode;
        set => this.RaiseAndSetIfChanged(ref _mergeMode, value);
    }

    private bool _isLoggedIn;
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => this.RaiseAndSetIfChanged(ref _isLoggedIn, value);
    }

    private string _loginStatusText = "未登录";
    public string LoginStatusText
    {
        get => _loginStatusText;
        set => this.RaiseAndSetIfChanged(ref _loginStatusText, value);
    }

    private bool _isInLoginProcess;
    public bool IsInLoginProcess
    {
        get => _isInLoginProcess;
        set => this.RaiseAndSetIfChanged(ref _isInLoginProcess, value);
    }

    private CancellationTokenSource? _loginCts;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="downloadManager">下载管理器</param>
    /// <param name="downloadFlowManager">下载流程管理器</param>
    /// <param name="logManager">日志管理器</param>
    /// <param name="networkSpeedMonitor">网络速度监控器</param>
    /// <param name="configManager">配置管理器</param>
    /// <param name="videoParser">视频解析器</param>
    /// <param name="bilibiliLoginService">B站登录服务</param>
    public MainWindowViewModel(
        IDownloadManager downloadManager,
        IDownloadFlowManager downloadFlowManager,
        ILogManager logManager,
        INetworkSpeedMonitor networkSpeedMonitor,
        IConfigManager configManager,
        IVideoParser videoParser,
        IBilibiliLoginService bilibiliLoginService)
    {
        _downloadManager = downloadManager;
        _downloadFlowManager = downloadFlowManager;
        _logManager = logManager;
        _networkSpeedMonitor = networkSpeedMonitor;
        _configManager = configManager;
        _videoParser = videoParser;
        _bilibiliLoginService = bilibiliLoginService;

        // 订阅登录事件
        _bilibiliLoginService.LoginStatusChanged += OnLoginStatusChanged;
        _bilibiliLoginService.QrCodeImageGenerated += OnQrCodeImageGenerated;
        _bilibiliLoginService.LoginSuccess += OnLoginSuccess;
        _bilibiliLoginService.LoginFailed += OnLoginFailed;

        // 初始化配置
        InitializeConfig();

        // 初始化登录状态
        _ = InitializeLoginAsync();
    }

    /// <summary>
    /// 初始化登录状态
    /// </summary>
    private async Task InitializeLoginAsync()
    {
        try
        {
            var isLoggedIn = await _bilibiliLoginService.InitializeAsync(message =>
            {
                _logManager.UpdateLog(message);
            });
            
            IsLoggedIn = isLoggedIn;
            LoginStatusText = isLoggedIn ? $"已登录: {_bilibiliLoginService.Credentials?.UserName}" : "未登录";
        }
        catch (Exception ex)
        {
            _logManager.UpdateLog(LocalizationService.Instance.GetString("LogInitLoginFailed", ex.Message));
            LoginStatusText = "登录状态检测失败";
        }
    }

    #region 多语言支持

    /// <summary>
    /// 可用语言列表
    /// </summary>
    public List<LanguageOption> AvailableLanguages => LocalizationService.SupportedLanguages;

    /// <summary>
    /// 当前选中的语言
    /// </summary>
    private LanguageOption? _selectedLanguage;
    public LanguageOption? SelectedLanguage
    {
        get
        {
            if (_selectedLanguage == null)
            {
                _selectedLanguage = LocalizationService.GetLanguageOption(LocalizationService.Instance.CurrentLanguageCode);
            }
            return _selectedLanguage;
        }
        set
        {
            if (_selectedLanguage?.Code != value?.Code && value != null)
            {
                _selectedLanguage = value;
                LocalizationService.Instance.ChangeLanguage(value.Code);
                // 同步设置 VideoStreamFetcher 库的语言
                VideoStreamFetcher.Localization.FetcherLocalization.SetLanguage(value.Code);
                RaisePropertyChanged(nameof(SelectedLanguage));
                // 触发语言标签变更，使所有绑定刷新
                RaisePropertyChanged(nameof(LanguageTag));
            }
        }
    }

    /// <summary>
    /// 语言标签 - 用于触发绑定更新
    /// </summary>
    public string LanguageTag => LocalizationService.Instance.CurrentLanguageCode;

    #endregion

    /// <summary>
    /// 检查是否触发B站登录
    /// </summary>
    /// <param name="input">输入文本</param>
    private void CheckBiliLoginTrigger(string input)
    {
        if (input.Trim().ToLower() is "bili" or "login" && !IsInLoginProcess)
        {
            // 如果已登录，先执行登出再重新登录
            if (IsLoggedIn)
            {
                _ = ReLoginAsync();
            }
            else
            {
                _ = StartBilibiliLoginAsync();
            }
        }
    }

    /// <summary>
    /// 重新登录流程（先登出再登录）
    /// </summary>
    private async Task ReLoginAsync()
    {
        try
        {
            _logManager.UpdateLog(LocalizationService.Instance["LogReLoginRequested"]);

            // 执行登出
            _bilibiliLoginService.Logout();
            IsLoggedIn = false;
            LoginStatusText = "未登录";

            _logManager.UpdateLog(LocalizationService.Instance["LogLoggedOut"]);

            // 延迟一小段时间确保登出完成
            await Task.Delay(500);

            // 开始新的登录流程
            await StartBilibiliLoginAsync();
        }
        catch (Exception ex)
        {
            _logManager.UpdateLog(LocalizationService.Instance.GetString("LogReLoginFailed", ex.Message));
            IsInLoginProcess = false;
        }
    }

    /// <summary>
    /// 检查是否触发自定义背景
    /// </summary>
    /// <param name="input">输入文本</param>
    public bool CheckCustomBackgroundTrigger(string input)
    {
        if (input.Trim().ToLower() is "自定义背景" or "custombg" or "bg" or "background")
        {
            _logManager.UpdateLog(LocalizationService.Instance["LogCustomBgDetected"]);
            _logManager.UpdateLog(LocalizationService.Instance["LogSelectBgImage"]);
            // 清空输入框
            Url = string.Empty;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 开始B站登录流程
    /// </summary>
    public async Task StartBilibiliLoginAsync()
    {
        try
        {
            IsInLoginProcess = true;
            _loginCts = new CancellationTokenSource();
            
            _logManager.UpdateLog(LocalizationService.Instance["LogLoginTriggerDetected"]);
            _logManager.UpdateLog(LocalizationService.Instance["LogLoginForHd"]);
            _logManager.UpdateLog(LocalizationService.Instance["LogStartingLogin"]);
            
            // 清空输入框
            Url = string.Empty;
            
            await _bilibiliLoginService.StartQrCodeLoginAsync(message =>
            {
                _logManager.UpdateLog(message);
            }, _loginCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logManager.UpdateLog(LocalizationService.Instance["LogLoginCancelled"]);
        }
        catch (Exception ex)
        {
            _logManager.UpdateLog(LocalizationService.Instance.GetString("LogLoginException", ex.Message));
        }
        finally
        {
            IsInLoginProcess = false;
        }
    }

    /// <summary>
    /// 登录状态变更处理
    /// </summary>
    private void OnLoginStatusChanged(object? sender, BilibiliLoginStatus status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            switch (status)
            {
                case BilibiliLoginStatus.NotLoggedIn:
                    LoginStatusText = "未登录";
                    IsLoggedIn = false;
                    break;
                case BilibiliLoginStatus.WaitingForScan:
                    LoginStatusText = "等待扫码...";
                    break;
                case BilibiliLoginStatus.ScannedWaitingConfirm:
                    LoginStatusText = "✓ 已扫码，等待确认...";
                    _logManager.UpdateLog(LocalizationService.Instance["LogQrCodeScanned"]);
                    break;
                case BilibiliLoginStatus.LoggedIn:
                    IsLoggedIn = true;
                    break;
                case BilibiliLoginStatus.LoginExpired:
                    LoginStatusText = "登录已过期，请重新登录喵";
                    IsLoggedIn = false;
                    break;
                case BilibiliLoginStatus.LoginFailed:
                    LoginStatusText = "登录失败";
                    IsLoggedIn = false;
                    IsInLoginProcess = false;
                    break;
            }
        });
    }
    
    /// <summary>
    /// 二维码图片生成事件
    /// </summary>
    public event EventHandler<byte[]>? ShowQrCodeWindow;
    
    /// <summary>
    /// 二维码图片生成处理
    /// </summary>
    private void OnQrCodeImageGenerated(object? sender, byte[] imageData)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ShowQrCodeWindow?.Invoke(this, imageData);
        });
    }

    /// <summary>
    /// 登录成功处理
    /// </summary>
    private void OnLoginSuccess(object? sender, BilibiliCredentials credentials)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsLoggedIn = true;
            IsInLoginProcess = false;
            LoginStatusText = $"已登录: {credentials.UserName}";
            _logManager.UpdateLog(LocalizationService.Instance.GetString("LogLoginSuccess", credentials.UserName));
            _logManager.UpdateLog(LocalizationService.Instance["LogLoginSuccessHint"]);
        });
    }

    /// <summary>
    /// 登录失败处理
    /// </summary>
    private void OnLoginFailed(object? sender, string error)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsLoggedIn = false;
            IsInLoginProcess = false;
            LoginStatusText = "登录失败";
            _logManager.UpdateLog(LocalizationService.Instance.GetString("LogLoginFailed", error));
            _logManager.UpdateLog(LocalizationService.Instance["LogRetryLogin"]);
        });
    }

    /// <summary>
    /// 取消登录
    /// </summary>
    public void CancelLogin()
    {
        _loginCts?.Cancel();
        _bilibiliLoginService.CancelLogin();
    }

    /// <summary>
    /// 重置登录流程状态
    /// 当用户关闭二维码窗口时调用，清理登录相关状态
    /// </summary>
    public void ResetLoginProcessState()
    {
        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _loginCts = null;
        IsInLoginProcess = false;

        // 清理输入框中的指令，避免重复触发
        if (Url.Trim().ToLower() is "bili" or "login")
        {
            Url = string.Empty;
            _logManager.UpdateLog(LocalizationService.Instance["LogLoginEnded"]);
        }

        if (!IsLoggedIn)
        {
            _logManager.UpdateLog(LocalizationService.Instance["LogLoginCancelledRetry"]);
        }
    }

    /// <summary>
    /// 取消登录流程
    /// 用于刷新二维码时取消当前登录任务
    /// </summary>
    public void CancelLoginProcess()
    {
        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _loginCts = null;
        IsInLoginProcess = false;
        _logManager.UpdateLog(LocalizationService.Instance["LogRefreshingQr"]);
    }

    /// <summary>
    /// 登出
    /// </summary>
    public void Logout()
    {
        _bilibiliLoginService.Logout();
        IsLoggedIn = false;
        LoginStatusText = "未登录";
        _logManager.UpdateLog(LocalizationService.Instance["LogLoggedOutSimple"]);
    }

    /// <summary>
    /// 初始化配置
    /// </summary>
    private void InitializeConfig()
    {
        SavePath = _configManager.ReadConfig("SavePath", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        IsFFmpegEnabled = _configManager.ReadConfig("IsFFmpegEnabled", false);
        MergeMode = _configManager.ReadConfig("MergeMode", 1);
    }

    /// <summary>
    /// 浏览文件夹
    /// </summary>
    /// <returns>任务</returns>
    private async Task BrowseAsyncImpl()
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// 设置保存路径（由 View 层调用）
    /// </summary>
    /// <param name="path">选择的文件夹路径</param>
    public void SetSavePath(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                SavePath = path;
                _configManager.SaveConfig("SavePath", SavePath);
                _logManager.UpdateLog(LocalizationService.Instance.GetString("LogSavePathSet", path));
            }
            else
            {
                _logManager.UpdateLog(LocalizationService.Instance.GetString("LogInvalidPath", path));
            }
        }
        catch (Exception ex)
        {
            _logManager.UpdateLog(LocalizationService.Instance.GetString("LogSetPathFailed", ex.Message));
        }
    }

    /// <summary>
    /// 下载
    /// </summary>
    /// <returns>任务</returns>
    private async Task DownloadAsyncImpl()
    {
        // 如果正在登录流程中，不执行下载
        if (IsInLoginProcess)
        {
            _logManager.UpdateLog(LocalizationService.Instance["LogInLoginProcess"]);
            return;
        }

        if (string.IsNullOrEmpty(Url))
        {
            _logManager.UpdateLog(LocalizationService.Instance["LogEnterUrl"]);
            return;
        }

        if (string.IsNullOrEmpty(SavePath))
        {
            _logManager.UpdateLog(LocalizationService.Instance["LogSelectSavePath"]);
            return;
        }

        // 检查是否是B站URL且未登录
        if (IsBilibiliUrl(Url) && !IsLoggedIn)
        {
            _logManager.UpdateLog(LocalizationService.Instance["LogBilibiliDetected"]);
            _logManager.UpdateLog(LocalizationService.Instance["LogLoginForHdHint"]);
            _logManager.UpdateLog(LocalizationService.Instance["LogTypeBiliToLogin"]);
        }

        if (IsConfirmMode)
        {
            // 确认模式下，直接调用下载流程，标记为已确认
            await HandleDownloadFlow(Url, SavePath, true);
            IsConfirmMode = false;
            PendingVideoInfo = null;
            return;
        }

        // 正常下载流程
        await HandleDownloadFlow(Url, SavePath, false);
    }

    /// <summary>
    /// 检查是否为B站URL
    /// </summary>
    private bool IsBilibiliUrl(string url)
    {
        return url.Contains("bilibili.com") || url.Contains("b23.tv") || url.Contains("hdslb.com");
    }

    /// <summary>
    /// 处理下载流程
    /// </summary>
    /// <param name="url">视频URL</param>
    /// <param name="savePath">保存路径</param>
    /// <param name="isConfirmed">是否已确认</param>
    /// <returns>任务</returns>
    private async Task HandleDownloadFlow(string url, string savePath, bool isConfirmed)
    {
        try
        {
            IsDownloading = true;
            await _networkSpeedMonitor.ResetProgressAnimation();

            // 构建下载选项
            var previewSegments = 0;
            if (IsPreviewSegmentsInputEnabled && !string.IsNullOrWhiteSpace(PreviewSegmentsText) && int.TryParse(PreviewSegmentsText, out var parsedSegments))
            {
                previewSegments = parsedSegments;
            }

            var options = new DownloadOptions
            {
                AudioOnly = IsAudioOnly,
                VideoOnly = IsVideoOnly,
                NoMerge = IsNoMerge,
                IsFFmpegEnabled = IsFFmpegEnabled,
                MergeMode = MergeMode,
                PreviewEnabled = PreviewEnabled,
                PreviewSegments = previewSegments > 0 ? previewSegments : 0,
                KeepOriginalFiles = KeepOriginalFiles
            };

            // 创建进度报告器
            var progressReporter = new Progress<double>(p => 
                Dispatcher.UIThread.InvokeAsync(() => 
                    _networkSpeedMonitor.UpdateProcessingProgress(p)));

            // 创建速度报告器
            var speedReporter = new Progress<long>(speed => 
            {
                _networkSpeedMonitor.OnSpeedUpdate(speed, false);
                SpeedText = $"当前速度: {FormatSpeed(speed)}";
            });

            // 创建状态报告器
            var statusReporter = new Progress<string>(status => StatusText = status);

            // 创建日志报告器
            Action<string, bool, bool> logReporter = (message, isRoot, autoCollapse) => 
                _logManager.UpdateCollapsibleLog(message, isRoot, autoCollapse);

            // 执行下载流程
            var result = await _downloadFlowManager.ExecuteDownloadAsync(
                url, 
                savePath, 
                options, 
                progressReporter, 
                speedReporter, 
                statusReporter, 
                logReporter, 
                isConfirmed);

            // 处理结果
            if (result.Success)
            {
                if (result.RequiresConfirmation)
                {
                    // 需要用户确认
                    PendingVideoInfo = result.VideoInfo as VideoInfo;
                    IsConfirmMode = true;
                    _logManager.UpdateCollapsibleLog(LocalizationService.Instance["LogDownloadAgainHint"], false, false);
                    _logManager.ResetCollapsibleLog();
                }
                else
                {
                    // 下载完成
                    _networkSpeedMonitor.MarkDownloadCompleted();
                    _logManager.UpdateLog(LocalizationService.Instance["LogDownloadCompleted"]);
                    
                    // 使用预览服务打开预览窗口
                    var previewService = new PreviewService(_logManager, path => ShowPreviewWindow?.Invoke(path));
                    await previewService.TryOpenPreviewAsync(result.OutputPath, options.PreviewEnabled, AutoPreviewAfterDownload);
                }
            }
            else
            {
                // 失败
                // 错误日志已经在 ExecuteDownloadAsync 中记录了
            }
        }
        catch (Exception ex)
        {
            _logManager.UpdateLog(LocalizationService.Instance.GetString("LogProcessException", ex.Message));
        }
        finally
        {
            // 只有在非确认模式下才重置下载状态
            // 如果需要确认，IsDownloading 设为 false，但界面保持确认状态
            if (!IsConfirmMode)
            {
                IsDownloading = false;
                _logManager.ResetCollapsibleLog();
            }
            else
            {
                IsDownloading = false; // 允许用户再次点击
            }
        }
    }

    /// <summary>
    /// 取消下载
    /// </summary>
    /// <returns>任务</returns>
    private async Task CancelAsyncImpl()
    {
        _logManager.UpdateLog(LocalizationService.Instance["LogCancellingDownload"]);
        _downloadFlowManager.CancelDownload();
        await Task.Delay(100);
    }

    /// <summary>
    /// 切换主题
    /// </summary>
    private void ThemeToggleImpl()
    {
        try
        {
            IsDarkTheme = !IsDarkTheme;
            
            var app = Avalonia.Application.Current;
            if (app != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    app.RequestedThemeVariant = IsDarkTheme ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
                    var themeName = IsDarkTheme ? LocalizationService.Instance["LogThemeDark"] : LocalizationService.Instance["LogThemeLight"];
                    _logManager.UpdateLog(LocalizationService.Instance.GetString("LogThemeSwitched", themeName));
                });
            }
            else
            {
                _logManager.UpdateLog(LocalizationService.Instance["LogCannotGetAppInstance"]);
            }
        }
        catch (Exception ex)
        {
            _logManager.UpdateLog(LocalizationService.Instance.GetString("LogThemeSwitchFailed", ex.Message));
        }
    }

    /// <summary>
    /// 进入选项模式
    /// </summary>
    private void EnterOptionModeImpl()
    {
        // 实现选项模式逻辑
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            RaisePropertyChanged(propertyName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 格式化速度
    /// </summary>
    /// <param name="bytesPerSecond">字节/秒</param>
    /// <returns>格式化后的字符串</returns>
    private string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond} B/s";
        else if (bytesPerSecond < 1024 * 1024)
            return $"{bytesPerSecond / 1024.0:F2} KB/s";
        else
            return $"{bytesPerSecond / (1024 * 1024.0):F2} MB/s";
    }
}
