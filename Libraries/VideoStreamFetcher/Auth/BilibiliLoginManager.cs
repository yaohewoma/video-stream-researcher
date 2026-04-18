using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using QRCoder;
using VideoStreamFetcher.Parsers;

namespace VideoStreamFetcher.Auth;

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

/// <summary>
/// B站登录二维码响应
/// </summary>
public class QrCodeResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("data")]
    public QrCodeData Data { get; set; } = new();
}

/// <summary>
/// B站登录二维码数据
/// </summary>
public class QrCodeData
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("qrcode_key")]
    public string QrCodeKey { get; set; } = string.Empty;
}

/// <summary>
/// B站登录状态检查响应
/// </summary>
public class LoginStatusResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("data")]
    public LoginStatusData Data { get; set; } = new();
}

/// <summary>
/// B站登录状态数据
/// </summary>
public class LoginStatusData
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
    
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// B站登录管理器
/// 负责处理B站二维码登录、状态管理和凭证持久化
/// </summary>
public class BilibiliLoginManager : IDisposable
{
    private readonly HttpHelper _httpHelper;
    private readonly string _credentialsPath;
    private BilibiliCredentials? _credentials;
    private BilibiliLoginStatus _currentStatus = BilibiliLoginStatus.NotLoggedIn;
    private CancellationTokenSource? _loginCts;
    private string? _currentQrCodeKey;
    
    /// <summary>
    /// 登录状态变更事件
    /// </summary>
    public event EventHandler<BilibiliLoginStatus>? LoginStatusChanged;
    
    /// <summary>
    /// 二维码生成事件（传递PNG图片字节数组）
    /// </summary>
    public event EventHandler<byte[]>? QrCodeImageGenerated;
    
    /// <summary>
    /// 二维码生成事件（传递URL）
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
    public BilibiliLoginStatus CurrentStatus => _currentStatus;
    
    /// <summary>
    /// 当前登录凭证
    /// </summary>
    public BilibiliCredentials? Credentials => _credentials;
    
    /// <summary>
    /// 是否已登录
    /// </summary>
    public bool IsLoggedIn => _currentStatus == BilibiliLoginStatus.LoggedIn && _credentials != null;
    
    /// <summary>
    /// 初始化B站登录管理器
    /// </summary>
    /// <param name="credentialsPath">凭证存储路径</param>
    public BilibiliLoginManager(string? credentialsPath = null)
    {
        _httpHelper = new HttpHelper();
        _credentialsPath = credentialsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VideoStreamFetcher",
            "bilibili_credentials.json");
        
        // 确保目录存在
        var directory = Path.GetDirectoryName(_credentialsPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // 加载已保存的凭证
        LoadCredentials();
    }
    
    /// <summary>
    /// 初始化登录管理器，检查登录状态
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>是否已登录</returns>
    public async Task<bool> InitializeAsync(Action<string>? statusCallback = null)
    {
        if (_credentials != null)
        {
            statusCallback?.Invoke("检测到已保存的登录凭证，正在验证...");
            var isValid = await ValidateLoginStatusAsync(statusCallback);
            if (isValid)
            {
                statusCallback?.Invoke($"欢迎回来，{_credentials.UserName}！");
                return true;
            }
            else
            {
                statusCallback?.Invoke("登录凭证已过期，请重新登录喵");
                _credentials = null;
                UpdateStatus(BilibiliLoginStatus.LoginExpired);
            }
        }
        else
        {
            statusCallback?.Invoke("未检测到登录凭证，可以启用获取更高分辨率的视频喵");
        }
        
        return false;
    }
    
    /// <summary>
    /// 开始二维码登录流程
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>登录任务</returns>
    public async Task StartQrCodeLoginAsync(Action<string>? statusCallback = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _loginCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _loginCts.Token;
            
            statusCallback?.Invoke("正在生成登录二维码...");
            
            // 获取二维码
            var qrCodeData = await GetQrCodeAsync(statusCallback);
            if (qrCodeData == null)
            {
                statusCallback?.Invoke("二维码生成失败，请重试");
                LoginFailed?.Invoke(this, "二维码生成失败");
                return;
            }
            
            _currentQrCodeKey = qrCodeData.QrCodeKey;
            
            // 生成二维码图片数据
            var qrCodeImageData = GenerateQrCodeImage(qrCodeData.Url);
            if (qrCodeImageData != null)
            {
                statusCallback?.Invoke("请扫描弹出的二维码窗口");
                QrCodeImageGenerated?.Invoke(this, qrCodeImageData);
            }
            else
            {
                statusCallback?.Invoke("二维码生成失败，请使用以下URL手动生成二维码:");
                statusCallback?.Invoke(qrCodeData.Url);
            }
            
            QrCodeGenerated?.Invoke(this, qrCodeData.Url);
            UpdateStatus(BilibiliLoginStatus.WaitingForScan);
            
            // 开始轮询登录状态
            await PollLoginStatusAsync(qrCodeData.QrCodeKey, statusCallback, token);
        }
        catch (OperationCanceledException)
        {
            statusCallback?.Invoke("登录已取消");
            UpdateStatus(BilibiliLoginStatus.NotLoggedIn);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"登录过程发生错误: {ex.Message}");
            LoginFailed?.Invoke(this, ex.Message);
            UpdateStatus(BilibiliLoginStatus.LoginFailed);
        }
    }
    
    /// <summary>
    /// 获取二维码
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>二维码数据</returns>
    private async Task<QrCodeData?> GetQrCodeAsync(Action<string>? statusCallback = null)
    {
        try
        {
            var url = "https://passport.bilibili.com/x/passport-login/web/qrcode/generate";
            var response = await _httpHelper.GetAsync(url);
            
            var qrResponse = JsonSerializer.Deserialize<QrCodeResponse>(response);
            if (qrResponse?.Code == 0 && qrResponse.Data != null)
            {
                return qrResponse.Data;
            }
            
            statusCallback?.Invoke($"获取二维码失败: {qrResponse?.Message}");
            return null;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"获取二维码异常: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 生成二维码PNG图片数据
    /// </summary>
    /// <param name="url">二维码URL</param>
    /// <returns>PNG图片字节数组</returns>
    private byte[]? GenerateQrCodeImage(string url)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            // 使用中等复杂度，平衡大小和容错率
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            
            // 生成PNG图片字节数组
            // 每个模块8像素，边框2个模块，生成约280x280像素的图片
            return qrCode.GetGraphic(8, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 }, true);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// 生成ASCII艺术二维码 - 使用标准格式，适合终端显示（备用方案）
    /// </summary>
    /// <param name="url">二维码URL</param>
    /// <returns>ASCII二维码字符串</returns>
    private string GenerateAsciiQrCode(string url)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.L);
            
            int moduleCount = qrCodeData.ModuleMatrix.Count;
            var sb = new StringBuilder();
            
            for (int y = 0; y < moduleCount; y++)
            {
                for (int x = 0; x < moduleCount; x++)
                {
                    bool isBlack = qrCodeData.ModuleMatrix[x][y];
                    sb.Append(isBlack ? "██" : "  ");
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        catch
        {
            return $"请访问以下链接扫码登录:\n{url}";
        }
    }
    
    /// <summary>
    /// 生成小型二维码（用于大URL）
    /// </summary>
    private string GenerateSmallQrCode(QRCodeData qrCodeData)
    {
        var modules = qrCodeData.ModuleMatrix;
        int size = modules.Count;
        var sb = new StringBuilder();
        int scale = size > 60 ? 3 : 2;
        
        for (int y = 0; y < size; y += scale)
        {
            for (int x = 0; x < size; x += scale)
            {
                int blackCount = 0, totalCount = 0;
                for (int dy = 0; dy < scale && y + dy < size; dy++)
                    for (int dx = 0; dx < scale && x + dx < size; dx++)
                    {
                        if (modules[x + dx][y + dy]) blackCount++;
                        totalCount++;
                    }
                sb.Append(blackCount * 2 > totalCount ? "██" : "  ");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 轮询登录状态
    /// </summary>
    /// <param name="qrCodeKey">二维码密钥</param>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    private async Task PollLoginStatusAsync(string qrCodeKey, Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        const int maxAttempts = 60; // 最多轮询60次（约3分钟）
        const int pollInterval = 3000; // 每3秒轮询一次
        int lastCode = -1; // 记录上一次状态码，用于检测状态变化

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            try
            {
                var (code, message, cookies) = await CheckLoginStatusAsync(qrCodeKey, statusCallback);

                // 状态变化时输出调试信息
                if (code != lastCode)
                {
                    statusCallback?.Invoke($"[状态变化] 扫码状态: {code}, 消息: {message}");
                    lastCode = code;
                }

                // B站二维码登录状态码:
                // 86101 = 未扫码
                // 86090 = 已扫码，等待确认
                // 0 = 登录成功
                // 86038 = 二维码已失效/过期
                // 86039 = 扫码取消
                switch (code)
                {
                    case 86101: // 未扫码
                        if (attempt == 0)
                        {
                            statusCallback?.Invoke("请使用手机B站APP扫描二维码");
                        }
                        else if (attempt % 10 == 0) // 每30秒提示一次
                        {
                            int remainingSeconds = (maxAttempts - attempt) * pollInterval / 1000;
                            statusCallback?.Invoke($"等待扫码中...（还剩{remainingSeconds}秒，可右键二维码刷新）");
                        }
                        break;

                    case 86090: // 已扫码，等待确认
                        if (_currentStatus != BilibiliLoginStatus.ScannedWaitingConfirm)
                        {
                            statusCallback?.Invoke("✓ 已检测到扫码，请在手机上确认登录");
                            UpdateStatus(BilibiliLoginStatus.ScannedWaitingConfirm);
                        }
                        break;

                    case 0: // 登录成功
                        statusCallback?.Invoke("登录成功！正在获取用户信息...");
                        if (cookies != null && cookies.Count > 0)
                        {
                            await CompleteLoginAsync(cookies, statusCallback);
                        }
                        else
                        {
                            statusCallback?.Invoke("获取登录凭证失败");
                            LoginFailed?.Invoke(this, "获取登录凭证失败");
                            UpdateStatus(BilibiliLoginStatus.LoginFailed);
                        }
                        return;

                    case 86038: // 二维码已失效/过期
                        statusCallback?.Invoke("二维码已过期，请右键刷新或重新输入'bili'");
                        LoginFailed?.Invoke(this, "二维码已过期");
                        UpdateStatus(BilibiliLoginStatus.NotLoggedIn);
                        return;

                    case 86039: // 扫码取消
                        statusCallback?.Invoke("登录已取消");
                        LoginFailed?.Invoke(this, "登录已取消");
                        UpdateStatus(BilibiliLoginStatus.NotLoggedIn);
                        return;

                    default:
                        statusCallback?.Invoke($"未知状态码: {code}, {message}");
                        break;
                }
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"检查登录状态异常: {ex.Message}");
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        statusCallback?.Invoke("登录超时，请右键二维码刷新或重新输入'bili'");
        LoginFailed?.Invoke(this, "登录超时");
        UpdateStatus(BilibiliLoginStatus.NotLoggedIn);
    }
    
    /// <summary>
    /// 检查登录状态
    /// </summary>
    /// <param name="qrCodeKey">二维码密钥</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>(状态码, 消息, Cookie字典)</returns>
    private async Task<(int code, string message, Dictionary<string, string>? cookies)> CheckLoginStatusAsync(string qrCodeKey, Action<string>? statusCallback = null)
    {
        try
        {
            var url = $"https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key={qrCodeKey}";
            
            using var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };
            
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            
            // API返回结构: { "code": 0, "message": "", "data": { "code": 0/1/2/3/4, "message": "" } }
            // 外层code为0表示请求成功，data.code表示扫码状态
            int apiCode = root.GetProperty("code").GetInt32();
            if (apiCode != 0)
            {
                string apiMessage = root.GetProperty("message").GetString() ?? "API请求失败";
                statusCallback?.Invoke($"API请求失败: {apiMessage}");
                return (apiCode, apiMessage, null);
            }
            
            // 从data中获取扫码状态
            if (!root.TryGetProperty("data", out var dataElement))
            {
                statusCallback?.Invoke("API响应格式错误，缺少data字段");
                return (-1, "API响应格式错误", null);
            }
            
            int code = dataElement.GetProperty("code").GetInt32();
            string message = dataElement.TryGetProperty("message", out var msgElement)
                ? msgElement.GetString() ?? ""
                : "";

            // 只在状态变化时输出调试信息，避免日志过多
            // statusCallback?.Invoke($"[调试] 扫码状态码: {code}, 消息: {message}");

            Dictionary<string, string>? cookies = null;

            // 如果登录成功（code=0），提取Cookie
            if (code == 0)
            {
                statusCallback?.Invoke("检测到登录成功状态，正在提取Cookie...");
                cookies = new Dictionary<string, string>();

                // 从响应头中提取Cookie
                if (response.Headers.Contains("Set-Cookie"))
                {
                    var setCookieHeaders = response.Headers.GetValues("Set-Cookie");
                    foreach (var header in setCookieHeaders)
                    {
                        ParseCookieHeader(header, cookies);
                    }
                }

                // 从CookieContainer中获取 - 使用passport.bilibili.com域名
                var uri = new Uri("https://passport.bilibili.com");
                var containerCookies = handler.CookieContainer.GetCookies(uri);
                foreach (Cookie cookie in containerCookies)
                {
                    cookies[cookie.Name] = cookie.Value;
                }

                // 同时获取bilibili.com域名的cookie
                var uri2 = new Uri("https://bilibili.com");
                var containerCookies2 = handler.CookieContainer.GetCookies(uri2);
                foreach (Cookie cookie in containerCookies2)
                {
                    cookies[cookie.Name] = cookie.Value;
                }

                statusCallback?.Invoke($"成功提取 {cookies.Count} 个Cookie");

                // 显示提取到的cookie名称（调试用）
                if (cookies.Count > 0)
                {
                    statusCallback?.Invoke($"Cookie列表: {string.Join(", ", cookies.Keys)}");
                }
            }

            return (code, message, cookies);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"检查登录状态请求异常: {ex.Message}");
            return (-1, ex.Message, null);
        }
    }
    
    /// <summary>
    /// 完成登录流程
    /// </summary>
    /// <param name="cookies">Cookie字典</param>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>任务</returns>
    private async Task CompleteLoginAsync(Dictionary<string, string> cookies, Action<string>? statusCallback = null)
    {
        try
        {
            // 创建凭证对象
            _credentials = new BilibiliCredentials
            {
                SessData = cookies.GetValueOrDefault("SESSDATA", ""),
                BiliJct = cookies.GetValueOrDefault("bili_jct", ""),
                DedeUserId = cookies.GetValueOrDefault("DedeUserID", ""),
                DedeUserIdCkMd5 = cookies.GetValueOrDefault("DedeUserID__ckMd5", ""),
                Sid = cookies.GetValueOrDefault("sid", ""),
                LoginTime = DateTime.Now,
                ExpireTime = DateTime.Now.AddDays(30) // 假设30天过期
            };
            
            // 获取用户信息
            await RefreshUserInfoAsync(statusCallback);
            
            // 保存凭证
            SaveCredentials();
            
            statusCallback?.Invoke($"登录成功！欢迎，{_credentials.UserName}");
            LoginSuccess?.Invoke(this, _credentials);
            UpdateStatus(BilibiliLoginStatus.LoggedIn);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"完成登录流程异常: {ex.Message}");
            LoginFailed?.Invoke(this, ex.Message);
        }
    }
    
    /// <summary>
    /// 解析Cookie头
    /// </summary>
    /// <param name="header">Cookie头</param>
    /// <param name="cookies">Cookie字典</param>
    private void ParseCookieHeader(string header, Dictionary<string, string> cookies)
    {
        var parts = header.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var equalIndex = trimmed.IndexOf('=');
            if (equalIndex > 0)
            {
                var name = trimmed.Substring(0, equalIndex);
                var value = trimmed.Substring(equalIndex + 1);
                cookies[name] = value;
            }
        }
    }
    
    /// <summary>
    /// 刷新用户信息
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>任务</returns>
    private async Task RefreshUserInfoAsync(Action<string>? statusCallback = null)
    {
        try
        {
            if (_credentials == null) return;
            
            var url = "https://api.bilibili.com/x/web-interface/nav";
            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler);
            
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Cookie", $"SESSDATA={_credentials.SessData}");
            
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            
            if (root.GetProperty("code").GetInt32() == 0)
            {
                var data = root.GetProperty("data");
                if (data.TryGetProperty("isLogin", out var isLogin) && isLogin.GetBoolean())
                {
                    _credentials.UserId = data.GetProperty("mid").GetInt64();
                    _credentials.UserName = data.GetProperty("uname").GetString() ?? "未知用户";
                }
            }
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"获取用户信息失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 验证登录状态
    /// </summary>
    /// <param name="statusCallback">状态回调</param>
    /// <returns>是否有效</returns>
    public async Task<bool> ValidateLoginStatusAsync(Action<string>? statusCallback = null)
    {
        try
        {
            if (_credentials == null || string.IsNullOrEmpty(_credentials.SessData))
            {
                return false;
            }
            
            // 检查是否过期
            if (DateTime.Now > _credentials.ExpireTime)
            {
                statusCallback?.Invoke("登录凭证已过期");
                return false;
            }
            
            var url = "https://api.bilibili.com/x/web-interface/nav";
            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler);
            
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Cookie", $"SESSDATA={_credentials.SessData}");
            
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            
            if (root.GetProperty("code").GetInt32() == 0)
            {
                var data = root.GetProperty("data");
                if (data.TryGetProperty("isLogin", out var isLogin) && isLogin.GetBoolean())
                {
                    // 更新用户信息
                    _credentials.UserId = data.GetProperty("mid").GetInt64();
                    _credentials.UserName = data.GetProperty("uname").GetString() ?? "未知用户";
                    UpdateStatus(BilibiliLoginStatus.LoggedIn);
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"验证登录状态异常: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 保存凭证到文件
    /// </summary>
    private void SaveCredentials()
    {
        try
        {
            if (_credentials == null) return;
            
            var json = JsonSerializer.Serialize(_credentials, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(_credentialsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存凭证失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 从文件加载凭证
    /// </summary>
    private void LoadCredentials()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
            {
                return;
            }
            
            var json = File.ReadAllText(_credentialsPath);
            _credentials = JsonSerializer.Deserialize<BilibiliCredentials>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载凭证失败: {ex.Message}");
            _credentials = null;
        }
    }
    
    /// <summary>
    /// 登出
    /// </summary>
    public void Logout()
    {
        _credentials = null;
        
        try
        {
            if (File.Exists(_credentialsPath))
            {
                File.Delete(_credentialsPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除凭证文件失败: {ex.Message}");
        }
        
        UpdateStatus(BilibiliLoginStatus.NotLoggedIn);
    }
    
    /// <summary>
    /// 获取用于HTTP请求的Cookie字符串
    /// </summary>
    /// <returns>Cookie字符串</returns>
    public string GetCookieString()
    {
        if (_credentials == null) return string.Empty;
        
        var cookies = new List<string>();
        if (!string.IsNullOrEmpty(_credentials.SessData))
            cookies.Add($"SESSDATA={_credentials.SessData}");
        if (!string.IsNullOrEmpty(_credentials.BiliJct))
            cookies.Add($"bili_jct={_credentials.BiliJct}");
        if (!string.IsNullOrEmpty(_credentials.DedeUserId))
            cookies.Add($"DedeUserID={_credentials.DedeUserId}");
        if (!string.IsNullOrEmpty(_credentials.DedeUserIdCkMd5))
            cookies.Add($"DedeUserID__ckMd5={_credentials.DedeUserIdCkMd5}");
        if (!string.IsNullOrEmpty(_credentials.Sid))
            cookies.Add($"sid={_credentials.Sid}");
        
        return string.Join("; ", cookies);
    }
    
    /// <summary>
    /// 取消当前登录流程
    /// </summary>
    public void CancelLogin()
    {
        _loginCts?.Cancel();
    }
    
    /// <summary>
    /// 更新登录状态
    /// </summary>
    /// <param name="status">新状态</param>
    private void UpdateStatus(BilibiliLoginStatus status)
    {
        if (_currentStatus != status)
        {
            _currentStatus = status;
            LoginStatusChanged?.Invoke(this, status);
        }
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _httpHelper.Dispose();
    }
}
