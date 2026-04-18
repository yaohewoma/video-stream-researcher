using System;
using System.Threading;
using System.Threading.Tasks;
using video_stream_researcher.Interfaces;
using video_stream_researcher.Models;

namespace video_stream_researcher.Services;

/// <summary>
/// B站登录服务包装器
/// 包装VideoStreamFetcher.Auth.BilibiliLoginManager，实现IBilibiliLoginService接口
/// </summary>
public class BilibiliLoginServiceWrapper : IBilibiliLoginService, IDisposable
{
    private readonly VideoStreamFetcher.Auth.BilibiliLoginManager _loginManager;
    
    /// <summary>
    /// 登录状态变更事件
    /// </summary>
    public event EventHandler<BilibiliLoginStatus>? LoginStatusChanged;
    
    /// <summary>
    /// 二维码生成事件（PNG图片数据）
    /// </summary>
    public event EventHandler<byte[]>? QrCodeImageGenerated;
    
    /// <summary>
    /// 二维码生成事件（URL）
    /// </summary>
    public event EventHandler<string>? QrCodeGenerated;
    
    /// <summary>
    /// 登录成功事件
    /// </summary>
    public event EventHandler<BilibiliCredentials>? LoginSuccess;
    
    /// <summary>
    /// 登录失败事件
    /// </summary>
    public event EventHandler<string>? LoginFailed;
    
    /// <summary>
    /// 当前登录状态
    /// </summary>
    public BilibiliLoginStatus CurrentStatus => ConvertStatus(_loginManager.CurrentStatus);
    
    /// <summary>
    /// 当前登录凭证
    /// </summary>
    public BilibiliCredentials? Credentials => ConvertCredentials(_loginManager.Credentials);
    
    /// <summary>
    /// 是否已登录
    /// </summary>
    public bool IsLoggedIn => _loginManager.IsLoggedIn;
    
    /// <summary>
    /// 初始化B站登录服务包装器
    /// </summary>
    public BilibiliLoginServiceWrapper()
    {
        _loginManager = new VideoStreamFetcher.Auth.BilibiliLoginManager();
        
        // 转发事件
        _loginManager.LoginStatusChanged += (s, e) => LoginStatusChanged?.Invoke(this, ConvertStatus(e));
        _loginManager.QrCodeImageGenerated += (s, e) => QrCodeImageGenerated?.Invoke(this, e);
        _loginManager.QrCodeGenerated += (s, e) => QrCodeGenerated?.Invoke(this, e);
        _loginManager.LoginSuccess += (s, e) => LoginSuccess?.Invoke(this, ConvertCredentials(e)!);
        _loginManager.LoginFailed += (s, e) => LoginFailed?.Invoke(this, e);
    }
    
    /// <summary>
    /// 转换登录状态
    /// </summary>
    private BilibiliLoginStatus ConvertStatus(VideoStreamFetcher.Auth.BilibiliLoginStatus status)
    {
        return status switch
        {
            VideoStreamFetcher.Auth.BilibiliLoginStatus.NotLoggedIn => BilibiliLoginStatus.NotLoggedIn,
            VideoStreamFetcher.Auth.BilibiliLoginStatus.WaitingForScan => BilibiliLoginStatus.WaitingForScan,
            VideoStreamFetcher.Auth.BilibiliLoginStatus.ScannedWaitingConfirm => BilibiliLoginStatus.ScannedWaitingConfirm,
            VideoStreamFetcher.Auth.BilibiliLoginStatus.LoggedIn => BilibiliLoginStatus.LoggedIn,
            VideoStreamFetcher.Auth.BilibiliLoginStatus.LoginExpired => BilibiliLoginStatus.LoginExpired,
            VideoStreamFetcher.Auth.BilibiliLoginStatus.LoginFailed => BilibiliLoginStatus.LoginFailed,
            _ => BilibiliLoginStatus.NotLoggedIn
        };
    }
    
    /// <summary>
    /// 转换凭证信息
    /// </summary>
    private BilibiliCredentials? ConvertCredentials(VideoStreamFetcher.Auth.BilibiliCredentials? credentials)
    {
        if (credentials == null) return null;
        
        return new BilibiliCredentials
        {
            SessData = credentials.SessData,
            BiliJct = credentials.BiliJct,
            DedeUserId = credentials.DedeUserId,
            DedeUserIdCkMd5 = credentials.DedeUserIdCkMd5,
            Sid = credentials.Sid,
            LoginTime = credentials.LoginTime,
            ExpireTime = credentials.ExpireTime,
            UserId = credentials.UserId,
            UserName = credentials.UserName
        };
    }
    
    /// <summary>
    /// 初始化登录服务
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>是否已登录</returns>
    public async Task<bool> InitializeAsync(Action<string>? statusCallback = null)
    {
        return await _loginManager.InitializeAsync(statusCallback);
    }
    
    /// <summary>
    /// 开始二维码登录流程
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>登录任务</returns>
    public async Task StartQrCodeLoginAsync(Action<string>? statusCallback = null, CancellationToken cancellationToken = default)
    {
        await _loginManager.StartQrCodeLoginAsync(statusCallback, cancellationToken);
    }
    
    /// <summary>
    /// 验证登录状态
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>是否有效</returns>
    public async Task<bool> ValidateLoginStatusAsync(Action<string>? statusCallback = null)
    {
        return await _loginManager.ValidateLoginStatusAsync(statusCallback);
    }
    
    /// <summary>
    /// 登出
    /// </summary>
    public void Logout()
    {
        _loginManager.Logout();
    }
    
    /// <summary>
    /// 取消当前登录流程
    /// </summary>
    public void CancelLogin()
    {
        _loginManager.CancelLogin();
    }
    
    /// <summary>
    /// 获取用于HTTP请求的Cookie字符串
    /// </summary>
    /// <returns>Cookie字符串</returns>
    public string GetCookieString()
    {
        return _loginManager.GetCookieString();
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _loginManager.Dispose();
    }
}
