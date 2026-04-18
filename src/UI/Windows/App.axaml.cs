using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Threading.Tasks;

namespace video_stream_researcher.UI;

/// <summary>
/// 应用程序主类
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 初始化应用程序
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 框架初始化完成时调用
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 桌面平台 - 使用异步初始化
            _ = InitializeAsync(desktop);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // 单视图平台（如Android）
            singleView.MainView = new Grid();
            base.OnFrameworkInitializationCompleted();
        }
        else
        {
            base.OnFrameworkInitializationCompleted();
        }
    }

    /// <summary>
    /// 异步初始化应用程序
    /// </summary>
    private async Task InitializeAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // 注释掉启动弹窗，直接显示主窗口
        // var startupDialog = new StartupDialogWindow();
        // var tcs = new TaskCompletionSource<bool>();
        // startupDialog.Closed += (s, e) => tcs.TrySetResult(startupDialog.IsConfirmed);
        // startupDialog.Show();
        // bool isConfirmed = await tcs.Task;
        // if (!isConfirmed)
        // {
        //     desktop.Shutdown();
        //     return;
        // }

        // 直接显示主窗口
        desktop.MainWindow = new MainWindow();
        desktop.MainWindow.Show();

        // 调用基类方法完成初始化
        base.OnFrameworkInitializationCompleted();
    }
}