using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.IO;

namespace video_stream_researcher.UI;

/// <summary>
/// B站登录二维码窗口
/// </summary>
public partial class QrCodeWindow : Window
{
    /// <summary>
    /// 刷新二维码事件
    /// </summary>
    public event EventHandler? RefreshQrCodeRequested;

    public QrCodeWindow()
    {
        InitializeComponent();
        InitializeContextMenu();
    }

    /// <summary>
    /// 初始化右键菜单事件
    /// </summary>
    private void InitializeContextMenu()
    {
        if (RefreshQrCodeMenuItem != null)
        {
            RefreshQrCodeMenuItem.Click += (s, e) =>
            {
                RefreshQrCodeRequested?.Invoke(this, EventArgs.Empty);
            };
        }

        if (CloseWindowMenuItem != null)
        {
            CloseWindowMenuItem.Click += (s, e) =>
            {
                Close();
            };
        }
    }

    /// <summary>
    /// 设置二维码图片（从文件路径）
    /// </summary>
    /// <param name="imagePath">图片文件路径</param>
    public void SetQrCodeImage(string imagePath)
    {
        try
        {
            if (File.Exists(imagePath))
            {
                using var stream = File.OpenRead(imagePath);
                var bitmap = new Bitmap(stream);
                QrCodeImage.Source = bitmap;
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"加载二维码失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置二维码图片（从字节数组）
    /// </summary>
    /// <param name="imageData">图片字节数据</param>
    public void SetQrCodeImage(byte[] imageData)
    {
        try
        {
            using var stream = new MemoryStream(imageData);
            var bitmap = new Bitmap(stream);
            QrCodeImage.Source = bitmap;
        }
        catch (Exception ex)
        {
            UpdateStatus($"加载二维码失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新状态文本
    /// </summary>
    /// <param name="status">状态文本</param>
    public void UpdateStatus(string status)
    {
        if (StatusText == null) return;

        StatusText.Text = status;

        // 根据状态改变颜色和显示遮罩层
        if (status.Contains("成功"))
        {
            StatusText.Foreground = new SolidColorBrush(Colors.Green);
            ShowSuccessOverlay();
            UpdateTipText("登录成功！窗口即将关闭...");
        }
        else if (status.Contains("失败") || status.Contains("过期") || status.Contains("取消"))
        {
            StatusText.Foreground = new SolidColorBrush(Colors.Red);
            ShowFailedOverlay();
            UpdateTipText("请右键刷新二维码或重新输入'bili'");
        }
        else if (status.Contains("已扫码") || status.Contains("确认"))
        {
            // 已扫码等待确认状态
            StatusText.Foreground = new SolidColorBrush(Colors.LightGreen);
            UpdateTipText("✓ 扫码成功！请在手机上点击确认登录按钮");
            // 改变边框颜色表示已扫码
            if (QrCodeBorder != null)
            {
                QrCodeBorder.BorderBrush = new SolidColorBrush(Colors.LightGreen);
            }
        }
        else if (status.Contains("等待扫码") || status.Contains("还剩"))
        {
            StatusText.Foreground = new SolidColorBrush(Colors.Orange);
            UpdateTipText("请使用手机B站客户端扫描二维码（右键可刷新）");
        }
        else
        {
            StatusText.Foreground = new SolidColorBrush(Colors.Orange);
        }
    }

    /// <summary>
    /// 显示成功遮罩层
    /// </summary>
    private void ShowSuccessOverlay()
    {
        if (SuccessOverlay != null)
        {
            SuccessOverlay.IsVisible = true;
        }
        if (FailedOverlay != null)
        {
            FailedOverlay.IsVisible = false;
        }
    }

    /// <summary>
    /// 显示失败遮罩层
    /// </summary>
    private void ShowFailedOverlay()
    {
        if (SuccessOverlay != null)
        {
            SuccessOverlay.IsVisible = false;
        }
        if (FailedOverlay != null)
        {
            FailedOverlay.IsVisible = true;
        }
    }

    /// <summary>
    /// 更新提示文本
    /// </summary>
    /// <param name="tip">提示文本</param>
    private void UpdateTipText(string tip)
    {
        if (TipText != null)
        {
            TipText.Text = tip;
        }
    }

    /// <summary>
    /// 重置窗口状态（用于刷新二维码）
    /// </summary>
    public void ResetState()
    {
        // 隐藏遮罩层
        if (SuccessOverlay != null)
        {
            SuccessOverlay.IsVisible = false;
        }
        if (FailedOverlay != null)
        {
            FailedOverlay.IsVisible = false;
        }

        // 重置边框颜色
        if (QrCodeBorder != null)
        {
            QrCodeBorder.BorderBrush = new SolidColorBrush(Colors.Gray);
        }

        // 重置状态文本
        StatusText.Text = "等待扫码...";
        StatusText.Foreground = new SolidColorBrush(Colors.Orange);
        TipText.Text = "请使用手机B站客户端扫描二维码（右键可刷新）";
    }
}
