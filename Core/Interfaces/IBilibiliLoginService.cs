using System;
using System.Threading;
using System.Threading.Tasks;
using video_stream_researcher.Models;

namespace video_stream_researcher.Interfaces;

/// <summary>
/// B站登录服务接口
/// 定义B站登录相关的操作契约
/// </summary>
public interface IBilibiliLoginService
{
    /// <summary>
    /// 登录状态变更事件
    /// </summary>
    event EventHandler<BilibiliLoginStatus> LoginStatusChanged;
    
    /// <summary>
    /// 二维码图片生成事件（PNG图片数据）
    /// </summary>
    event EventHandler<byte[]> QrCodeImageGenerated;
    
    /// <summary>
    /// 二维码生成事件（URL）
    /// </summary>
    event EventHandler<string> QrCodeGenerated;
    
    /// <summary>
    /// 登录成功事件
    /// </summary>
    event EventHandler<BilibiliCredentials> LoginSuccess;
    
    /// <summary>
    /// 登录失败事件
    /// </summary>
    event EventHandler<string> LoginFailed;
    
    /// <summary>
    /// 当前登录状态
    /// </summary>
    BilibiliLoginStatus CurrentStatus { get; }
    
    /// <summary>
    /// 当前登录凭证
    /// </summary>
    BilibiliCredentials? Credentials { get; }
    
    /// <summary>
    /// 是否已登录
    /// </summary>
    bool IsLoggedIn { get; }
    
    /// <summary>
    /// 初始化登录服务
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>是否已登录</returns>
    Task<bool> InitializeAsync(Action<string>? statusCallback = null);
    
    /// <summary>
    /// 开始二维码登录流程
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>登录任务</returns>
    Task StartQrCodeLoginAsync(Action<string>? statusCallback = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 验证登录状态
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>是否有效</returns>
    Task<bool> ValidateLoginStatusAsync(Action<string>? statusCallback = null);
    
    /// <summary>
    /// 登出
    /// </summary>
    void Logout();
    
    /// <summary>
    /// 取消当前登录流程
    /// </summary>
    void CancelLogin();
    
    /// <summary>
    /// 获取用于HTTP请求的Cookie字符串
    /// </summary>
    /// <returns>Cookie字符串</returns>
    string GetCookieString();
}
