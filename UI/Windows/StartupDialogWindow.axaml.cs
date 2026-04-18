using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace video_stream_researcher.UI;

/// <summary>
/// 启动弹窗窗口，显示免责声明和使用条款
/// </summary>
public partial class StartupDialogWindow : Window
{
    /// <summary>
    /// 倒计时总秒数
    /// </summary>
    private const int CountdownSeconds = 5;

    /// <summary>
    /// 倒计时取消令牌源
    /// </summary>
    private CancellationTokenSource? _countdownCts;

    /// <summary>
    /// 用户是否点击了确认按钮
    /// </summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public StartupDialogWindow()
    {
        InitializeComponent();

        // 启动倒计时
        StartCountdown();
    }

    /// <summary>
    /// 启动倒计时计时器
    /// </summary>
    private void StartCountdown()
    {
        _countdownCts = new CancellationTokenSource();
        _ = RunCountdownAsync(_countdownCts.Token);
    }

    /// <summary>
    /// 运行倒计时逻辑
    /// </summary>
    private async Task RunCountdownAsync(CancellationToken cancellationToken)
    {
        var confirmButton = this.FindControl<Button>("ConfirmButton");
        var countdownText = this.FindControl<TextBlock>("CountdownText");

        if (confirmButton == null || countdownText == null)
        {
            return;
        }

        try
        {
            for (int i = CountdownSeconds; i > 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 更新按钮文本和倒计时提示
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    confirmButton.Content = $"确认({i})";
                    countdownText.Text = $"请仔细阅读以上内容，确认按钮将在 {i} 秒后可用";
                });

                // 等待1秒
                await Task.Delay(1000, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 倒计时结束，启用按钮
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                confirmButton.IsEnabled = true;
                confirmButton.Content = "确认";
                countdownText.Text = "✓ 现在可以点击确认按钮继续使用";
                countdownText.Foreground = new SolidColorBrush(Color.FromRgb(100, 255, 100));
            });
        }
        catch (OperationCanceledException)
        {
            // 倒计时被取消，不做任何操作
        }
        catch (Exception)
        {
            // 发生其他错误，启用按钮以确保用户可以继续
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                confirmButton.IsEnabled = true;
                confirmButton.Content = "确认";
            });
        }
    }

    /// <summary>
    /// 确认按钮点击事件
    /// </summary>
    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        _countdownCts?.Cancel();
        Close();
    }

    /// <summary>
    /// 退出按钮点击事件
    /// </summary>
    private void ExitButton_Click(object? sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        _countdownCts?.Cancel();
        Close();
    }

    /// <summary>
    /// 作者链接按钮点击事件 - 打开B站主页
    /// </summary>
    private void AuthorLinkButton_Click(object? sender, RoutedEventArgs e)
    {
        const string authorUrl = "https://b23.tv/lMgf5eL";
        try
        {
            // 临时取消置顶，让浏览器窗口可以显示在最前面
            Topmost = false;

            Process.Start(new ProcessStartInfo
            {
                FileName = authorUrl,
                UseShellExecute = true
            });

            // 3秒后恢复置顶
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Topmost = true;
                });
            });
        }
        catch (Exception ex)
        {
            // 如果打开失败，显示错误提示
            var messageBox = new Window
            {
                Title = "打开链接失败",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBlock
                {
                    Text = $"无法打开链接: {authorUrl}\n错误: {ex.Message}",
                    Margin = new Thickness(20),
                    TextWrapping = TextWrapping.Wrap
                }
            };
            messageBox.ShowDialog(this);
        }
    }

    /// <summary>
    /// 标题栏按下事件 - 开始拖动窗口
    /// </summary>
    private void TitleBar_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    /// <summary>
    /// 窗口关闭时清理资源
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        base.OnClosing(e);
    }
}
