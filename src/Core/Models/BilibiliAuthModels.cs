using System;
using System.Text.Json.Serialization;

namespace video_stream_researcher.Models;

/// <summary>
/// B站登录状态枚举
/// </summary>
public enum BilibiliLoginStatus
{
    /// <summary>
    /// 未登录
    /// </summary>
    NotLoggedIn,

    /// <summary>
    /// 等待扫码
    /// </summary>
    WaitingForScan,

    /// <summary>
    /// 已扫码，等待确认
    /// </summary>
    ScannedWaitingConfirm,

    /// <summary>
    /// 登录成功
    /// </summary>
    LoggedIn,

    /// <summary>
    /// 登录失效
    /// </summary>
    LoginExpired,

    /// <summary>
    /// 登录失败
    /// </summary>
    LoginFailed
}

/// <summary>
/// B站登录凭证信息
/// </summary>
public class BilibiliCredentials
{
    /// <summary>
    /// SESSDATA Cookie
    /// </summary>
    [JsonPropertyName("sessdata")]
    public string SessData { get; set; } = string.Empty;

    /// <summary>
    /// bili_jct (CSRF Token)
    /// </summary>
    [JsonPropertyName("bili_jct")]
    public string BiliJct { get; set; } = string.Empty;

    /// <summary>
    /// DedeUserID
    /// </summary>
    [JsonPropertyName("dede_user_id")]
    public string DedeUserId { get; set; } = string.Empty;

    /// <summary>
    /// DedeUserID__ckMd5
    /// </summary>
    [JsonPropertyName("dede_user_id_ckmd5")]
    public string DedeUserIdCkMd5 { get; set; } = string.Empty;

    /// <summary>
    /// sid
    /// </summary>
    [JsonPropertyName("sid")]
    public string Sid { get; set; } = string.Empty;

    /// <summary>
    /// 登录时间
    /// </summary>
    [JsonPropertyName("login_time")]
    public DateTime LoginTime { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    [JsonPropertyName("expire_time")]
    public DateTime ExpireTime { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;
}
