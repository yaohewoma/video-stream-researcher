using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace VideoStreamFetcher.Parsers;

/// <summary>
/// HTTP 请求辅助类
/// 负责处理 HTTP 请求相关的操作
/// </summary>
public class HttpHelper : IDisposable
{
    private readonly HttpClient _httpClient;
    
    /// <summary>
    /// 初始化 HTTP 辅助类
    /// </summary>
    public HttpHelper()
    {
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseCookies = true,
            AllowAutoRedirect = true,
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        });
        
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        // 设置浏览器模拟头信息
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.8,zh-TW;q=0.7,zh-HK;q=0.5,en-US;q=0.3,en;q=0.2");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com/");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
    }
    
    /// <summary>
    /// 发送 GET 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <returns>响应内容</returns>
    public async Task<string> GetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8))
        {
            return await reader.ReadToEndAsync();
        }
    }
    
    /// <summary>
    /// 发送 HEAD 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <returns>HTTP 响应消息</returns>
    public async Task<HttpResponseMessage> HeadAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Head, url);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }
    
    /// <summary>
    /// 发送 POST 请求
    /// </summary>
    /// <param name="url">请求 URL</param>
    /// <param name="content">请求内容</param>
    /// <returns>响应内容</returns>
    public async Task<string> PostAsync(string url, FormUrlEncodedContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        
        // 添加浏览器模拟头信息
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("Referer", url);
        request.Headers.Add("Origin", new Uri(url).GetLeftPart(UriPartial.Authority));
        
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }
    
    /// <summary>
    /// 发送 HTTP 请求
    /// </summary>
    /// <param name="request">HTTP 请求消息</param>
    /// <returns>HTTP 响应消息</returns>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    /// <summary>
    /// 设置Cookie字符串（用于B站登录）
    /// </summary>
    /// <param name="cookieString">Cookie字符串</param>
    public void SetCookieString(string cookieString)
    {
        if (!string.IsNullOrEmpty(cookieString))
        {
            _httpClient.DefaultRequestHeaders.Remove("Cookie");
            _httpClient.DefaultRequestHeaders.Add("Cookie", cookieString);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient.Dispose();
        }
    }
}