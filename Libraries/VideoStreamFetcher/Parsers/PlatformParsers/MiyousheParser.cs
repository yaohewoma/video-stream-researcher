using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Threading;

namespace VideoStreamFetcher.Parsers.PlatformParsers;

/// <summary>
/// 米游社视频解析器
/// 负责解析米游社视频相关的逻辑
/// </summary>
public class MiyousheParser : IPlatformParser
{
    private readonly HttpHelper _httpHelper;
    
    /// <summary>
    /// 初始化米游社视频解析器
    /// </summary>
    /// <param name="httpHelper">HTTP 请求辅助类</param>
    public MiyousheParser(HttpHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }
    
    /// <summary>
    /// 判断该解析器是否支持解析给定的 URL
    /// </summary>
    /// <param name="url">视频 URL</param>
    /// <returns>是否支持</returns>
    public bool CanParse(string url)
    {
        return url.Contains("miyoushe.com");
    }

    /// <summary>
    /// 解析视频信息
    /// </summary>
    /// <param name="url">视频 URL</param>
    /// <param name="html">页面 HTML 内容（如果已下载）</param>
    /// <param name="statusCallback">状态回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>解析出的视频信息</returns>
    public async Task<VideoInfo?> ParseAsync(string url, string? html, Action<string>? statusCallback, CancellationToken cancellationToken = default)
    {
        statusCallback?.Invoke("开始米游社视频解析流程...");
        
        // 提取视频 ID（虽然浏览器模拟可能不需要，但保留作为备份）
        if (!string.IsNullOrEmpty(html))
        {
            string videoId = ExtractVideoId(url, html);
            if (!string.IsNullOrEmpty(videoId))
            {
                statusCallback?.Invoke($"提取到视频 ID: {videoId}");
            }
        }

        // 使用 Selenium 模拟浏览器提取视频信息（包含标题和视频流）
        // 相比分步提取，这里一次性提取所有信息，避免重复启动浏览器
        return await ExtractVideoInfoWithBrowser(url, statusCallback, cancellationToken);
    }

    /// <summary>
    /// 使用浏览器模拟提取完整视频信息（标题 + 视频流）
    /// </summary>
    private async Task<VideoInfo?> ExtractVideoInfoWithBrowser(string url, Action<string>? statusCallback, CancellationToken cancellationToken = default)
    {
        statusCallback?.Invoke("启动浏览器模拟...");
        
        // 检查是否已取消
        cancellationToken.ThrowIfCancellationRequested();
        
        // 使用 Selenium 模拟真实浏览器行为
        IWebDriver? driver = null;
        
        try
        {
            // 配置 Chrome 浏览器
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--headless"); // 无头模式，不显示浏览器窗口
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-infobars");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            // 配置 ChromeDriver 服务，隐藏命令窗口
            ChromeDriverService service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true; // 隐藏命令窗口
            
            // 在后台线程中执行浏览器操作
            return await Task.Run(async () =>
            {
                try
                {
                    // 初始化浏览器驱动
                    statusCallback?.Invoke("初始化浏览器驱动...|10");
                    driver = new ChromeDriver(service, options);
                    
                    // 设置页面加载超时
                    driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
                    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
                    
                    // 导航到 URL
                    statusCallback?.Invoke($"导航到 URL: {url}|20");
                    driver.Navigate().GoToUrl(url);
                    
                    // 等待页面加载完成
                    statusCallback?.Invoke("等待页面加载完成...|30");
                    await Task.Delay(3000, cancellationToken);
                    
                    // 检查是否已取消
                    cancellationToken.ThrowIfCancellationRequested();

                    // 1. 提取标题
                    string title = ExtractTitleFromDriver(driver, statusCallback);
                    statusCallback?.Invoke($"提取到标题: {title}|35");

                    // 2. 提取视频流
                    // 尝试模拟点击播放按钮
                    statusCallback?.Invoke("尝试模拟点击播放按钮...|40");
                    await SimulatePlayButtonClick(driver, statusCallback);
                    
                    // 检查是否已取消
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // 等待视频加载
                    statusCallback?.Invoke("等待视频加载...|50");
                    await Task.Delay(5000, cancellationToken);
                    
                    // 提取视频 URL
                    statusCallback?.Invoke("提取视频 URL...|60");
                    string videoUrl = await ExtractVideoUrlFromBrowser(driver, statusCallback, cancellationToken);
                    
                    if (string.IsNullOrEmpty(videoUrl))
                    {
                        // 尝试从页面源码提取
                        statusCallback?.Invoke("尝试从页面源码中提取视频 URL...|80");
                        string pageSource = driver.PageSource;
                        videoUrl = ExtractVideoUrlsFromHtml(pageSource, statusCallback);
                    }

                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        statusCallback?.Invoke($"成功提取到视频 URL: {videoUrl}|90");
                        
                        // 构建 VideoInfo
                        return new VideoInfo
                        {
                            Title = !string.IsNullOrEmpty(title) ? title : "未命名米游社视频",
                            CombinedStreams = new List<VideoStreamInfo>
                            {
                                new VideoStreamInfo
                                {
                                    Url = videoUrl,
                                    Size = 0,
                                    Quality = "默认",
                                    Format = "mp4"
                                }
                            }
                        };
                    }
                    
                    statusCallback?.Invoke("未能提取到有效的视频 URL|95");
                    return null;
                }
                finally
                {
                    if (driver != null)
                    {
                        try
                        {
                            statusCallback?.Invoke("关闭浏览器...|100");
                            driver.Quit();
                        }
                        catch { }
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"浏览器模拟失败: {ex.Message}");
            return null;
        }
        finally
        {
            if (driver != null)
            {
                try { driver.Quit(); } catch { }
            }
        }
    }

    // 提取标题逻辑复用原 ExtractTitleFromBrowser 中的逻辑，但改为接收 driver 实例
    private string ExtractTitleFromDriver(IWebDriver driver, Action<string>? statusCallback)
    {
        try
        {
            string pageSource = driver.PageSource;
            string title = string.Empty;

            // 尝试提取复杂的标题格式
            var complexTitleMatch = Regex.Match(pageSource, @"《(.*?)》.*?(过场动画|PV|预告|宣传片).*?[「「](.*?)[」」]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (complexTitleMatch.Success)
            {
                title = $"《{complexTitleMatch.Groups[1].Value.Trim()}》{complexTitleMatch.Groups[2].Value.Trim()}-「{complexTitleMatch.Groups[3].Value.Trim()}」";
                return title;
            }
            
            // 尝试提取包含多个《》的标题
            var multiQuoteMatch = Regex.Match(pageSource, @"《(.*?)》.*?《(.*?)》", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (multiQuoteMatch.Success)
            {
                title = $"《{multiQuoteMatch.Groups[1].Value.Trim()}》{multiQuoteMatch.Value.Substring(multiQuoteMatch.Groups[1].Length + 2, multiQuoteMatch.Value.Length - (multiQuoteMatch.Groups[1].Length + 2 + multiQuoteMatch.Groups[2].Length + 2)).Trim()}《{multiQuoteMatch.Groups[2].Value.Trim()}》";
                return title;
            }
            
            // 尝试提取简单的《》格式标题
            var singleQuoteMatch = Regex.Match(pageSource, @"《(.*?)》", RegexOptions.IgnoreCase);
            if (singleQuoteMatch.Success)
            {
                title = $"《{singleQuoteMatch.Groups[1].Value.Trim()}》";
                return title;
            }
            
            // 尝试从h1标签提取
            var h1Elements = driver.FindElements(By.TagName("h1"));
            if (h1Elements.Count > 0) return h1Elements[0].Text.Trim();
            
            // 尝试从包含title的元素提取
            var titleElements = driver.FindElements(By.CssSelector("div[class*='title'], h2[class*='title'], h3[class*='title']"));
            if (titleElements.Count > 0) return titleElements[0].Text.Trim();
            
            // 尝试从meta标签提取
            var metaElements = driver.FindElements(By.CssSelector("meta[name='title']"));
            if (metaElements.Count > 0) return metaElements[0].GetAttribute("content")?.Trim() ?? string.Empty;
            
            // 尝试从og:title标签提取
            var ogElements = driver.FindElements(By.CssSelector("meta[property='og:title']"));
            if (ogElements.Count > 0) return ogElements[0].GetAttribute("content")?.Trim() ?? string.Empty;
            
            // 尝试从title标签提取
            var titleTagElements = driver.FindElements(By.TagName("title"));
            if (titleTagElements.Count > 0) return titleTagElements[0].Text.Trim();

            return string.Empty;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"提取标题时出错: {ex.Message}");
            return string.Empty;
        }
    }
    
    // ... (保留 ExtractVideoUrlsFromHtml, SimulatePlayButtonClick, ExtractVideoUrlFromBrowser, IsValidVideoUrl, ExtractVideoId 等私有方法，不做大改动，直接复用原逻辑)
    
    /// <summary>
    /// 从页面中提取任何可能的视频 URL 或 CDN 链接
    /// </summary>
    private string ExtractVideoUrlsFromHtml(string html, Action<string>? statusCallback)
    {
        // 尝试提取各种可能的视频 URL 模式
        var videoUrlPatterns = new List<string>
        {
            // 标准视频格式
            @"https?://[^\s""']*?\.mp4[^\s""']*",
            @"https?://[^\s""']*?\.m4v[^\s""']*",
            @"https?://[^\s""']*?\.webm[^\s""']*",
            @"https?://[^\s""']*?\.flv[^\s""']*",
            
            // 抖音 CDN 视频链接
            @"https?://v[0-9]*-?ysdouyin[^\s""',]+",
            @"https?://v\.ysdouyin\.com/[^\s""',]+",
            @"https?://[^\s""',]*?ysdouyin[^\s""',]+",
            
            // QQ CDN 视频链接
            @"https?://[^\s""',]*?qpic\.cn[^\s""',]+",
            @"https?://[^\s""',]*?qq\.com[^\s""',]*?video[^\s""',]+",
            
            // 更通用的视频链接模式
            @"https?://[^\s""']*?video[^\s""']*?\.(mp4|m4v|webm|flv)[^\s""']*",
            @"https?://[^\s""']*?media[^\s""']*?\.(mp4|m4v|webm|flv)[^\s""']*",
            @"https?://[^\s""']*?stream[^\s""']*?\.(mp4|m4v|webm|flv)[^\s""']*",
        };
        
        // 尝试所有视频 URL 模式
        foreach (var pattern in videoUrlPatterns)
        {
            var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                string potentialUrl = match.Value;
                if (Uri.IsWellFormedUriString(potentialUrl, UriKind.Absolute) && IsValidVideoUrl(potentialUrl))
                {
                    statusCallback?.Invoke($"找到有效的视频 URL: {potentialUrl}");
                    return potentialUrl;
                }
            }
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// 模拟点击播放按钮
    /// </summary>
    private async Task SimulatePlayButtonClick(IWebDriver driver, Action<string>? statusCallback)
    {
        try
        {
            // 尝试找到并点击播放按钮
            var playButtons = driver.FindElements(By.CssSelector("button[class*='play']"));
            if (playButtons.Count > 0)
            {
                statusCallback?.Invoke($"找到 {playButtons.Count} 个播放按钮，尝试点击第一个...");
                try
                {
                    playButtons[0].Click();
                    statusCallback?.Invoke("成功点击播放按钮");
                    return;
                }
                catch (Exception ex)
                {
                    statusCallback?.Invoke($"点击播放按钮时遇到问题，尝试其他方法: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 100))}...");
                }
            }
            
            // 尝试找到视频元素并点击
            var videoElements = driver.FindElements(By.TagName("video"));
            if (videoElements.Count > 0)
            {
                statusCallback?.Invoke($"找到 {videoElements.Count} 个视频元素，尝试点击第一个...");
                try
                {
                    videoElements[0].Click();
                    statusCallback?.Invoke("成功点击视频元素");
                    return;
                }
                catch (Exception ex)
                {
                    statusCallback?.Invoke($"点击视频元素时遇到问题，尝试其他方法: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 100))}...");
                }
            }
            
            // 尝试找到包含播放图标的元素
            var playIcons = driver.FindElements(By.CssSelector("div[class*='play'], span[class*='play'], i[class*='play']"));
            if (playIcons.Count > 0)
            {
                statusCallback?.Invoke($"找到 {playIcons.Count} 个包含播放图标的元素，尝试点击第一个...");
                try
                {
                    playIcons[0].Click();
                    statusCallback?.Invoke("成功点击播放图标");
                    return;
                }
                catch (Exception ex)
                {
                    statusCallback?.Invoke($"点击播放图标时遇到问题，尝试其他方法: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 100))}...");
                }
            }
            
            // 尝试执行 JavaScript 来触发视频播放
            statusCallback?.Invoke("尝试执行 JavaScript 来触发视频播放...");
            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript(@"
                    // 查找所有视频元素
                    var videos = document.querySelectorAll('video');
                    if (videos.length > 0) {
                        // 尝试播放第一个视频
                        videos[0].play().catch(e => console.log('播放失败:', e));
                    }
                    
                    // 查找所有可能的播放按钮
                    var playButtons = document.querySelectorAll('button, div, span, i');
                    playButtons.forEach(button => {
                        if (button.textContent.includes('播放') || button.className.includes('play')) {
                            button.click();
                        }
                    });
                ");
                statusCallback?.Invoke("成功执行 JavaScript 触发视频播放");
            }
            catch (Exception ex)
            {
                statusCallback?.Invoke($"执行 JavaScript 时遇到问题: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 100))}...");
            }
            
            statusCallback?.Invoke("播放按钮点击尝试完成，继续提取视频 URL");
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"模拟点击播放按钮时发生警告: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 从浏览器中提取视频 URL
    /// </summary>
    private async Task<string> ExtractVideoUrlFromBrowser(IWebDriver driver, Action<string>? statusCallback, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // 尝试从视频元素中提取 src 属性
            var videoElements = driver.FindElements(By.TagName("video"));
            if (videoElements.Count > 0)
            {
                statusCallback?.Invoke($"找到 {videoElements.Count} 个视频元素，尝试提取 src 属性...");
                foreach (var video in videoElements)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string src = video.GetAttribute("src");
                    if (!string.IsNullOrEmpty(src) && IsValidVideoUrl(src)) return src;
                }
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // 尝试从 video 元素的 source 子元素中提取
            var sourceElements = driver.FindElements(By.CssSelector("video source"));
            if (sourceElements.Count > 0)
            {
                statusCallback?.Invoke($"找到 {sourceElements.Count} 个 source 元素，尝试提取 src 属性...");
                foreach (var source in sourceElements)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string src = source.GetAttribute("src");
                    if (!string.IsNullOrEmpty(src) && IsValidVideoUrl(src)) return src;
                }
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            
            // 尝试执行 JavaScript 来获取视频 URL
            statusCallback?.Invoke("尝试执行 JavaScript 来获取视频 URL...");
            var jsExecutor = (IJavaScriptExecutor)driver;
            
            string jsCode = @"
                var videoUrls = [];
                var videos = document.querySelectorAll('video');
                videos.forEach(video => {
                    if (video.src) videoUrls.push(video.src);
                    var sources = video.querySelectorAll('source');
                    sources.forEach(source => {
                        if (source.src) videoUrls.push(source.src);
                    });
                });
                if (window.performance && window.performance.getEntries) {
                    var entries = window.performance.getEntries();
                    entries.forEach(entry => {
                        if (entry.initiatorType === 'xmlhttprequest' || entry.initiatorType === 'fetch') {
                            if (entry.name.includes('.mp4') || entry.name.includes('video')) {
                                videoUrls.push(entry.name);
                            }
                        }
                    });
                }
                return videoUrls;
            ";
            
            cancellationToken.ThrowIfCancellationRequested();
            
            var result = jsExecutor.ExecuteScript(jsCode);
            if (result is System.Collections.ObjectModel.ReadOnlyCollection<object> videoUrls)
            {
                foreach (var url in videoUrls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (url is string videoUrl && IsValidVideoUrl(videoUrl))
                    {
                        statusCallback?.Invoke($"从 JavaScript 执行结果中提取到视频 URL: {videoUrl}");
                        return videoUrl;
                    }
                }
            }
            
            return string.Empty;
        }
        catch (OperationCanceledException)
        {
            statusCallback?.Invoke("视频 URL 提取已取消");
            throw;
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke($"从浏览器中提取视频 URL 失败: {ex.Message}");
            return string.Empty;
        }
    }
    
    /// <summary>
    /// 验证是否为有效的视频 URL
    /// </summary>
    private bool IsValidVideoUrl(string url)
    {
        if (url.Contains(".js") && !url.Contains(".mp4")) return false;
        if (url.Contains(".css")) return false;
        if (url.Contains(".jpg") || url.Contains(".png") || url.Contains(".gif")) return false;
        if (url.Contains(".woff") || url.Contains(".ttf")) return false;
        
        bool hasVideoKeyword = url.Contains("video") || url.Contains("media") || url.Contains("stream") || url.Contains("play") || url.Contains("cdn");
        bool hasVideoExtension = url.Contains(".mp4") || url.Contains(".m4v") || url.Contains(".webm") || url.Contains(".flv");
        bool hasVideoDomain = url.Contains("douyin") || url.Contains("qpic") || url.Contains("miyoushe") || url.Contains("mihoyo");
        
        return hasVideoKeyword || hasVideoExtension || hasVideoDomain;
    }
    
    /// <summary>
    /// 从 URL 或 HTML 中提取视频 ID
    /// </summary>
    private string ExtractVideoId(string url, string html)
    {
        string[] parts = url.Split('/');
        if (parts.Length > 0)
        {
            string lastPart = parts[parts.Length - 1];
            if (Regex.IsMatch(lastPart, @"^\d+$")) return lastPart;
        }
        
        var idMatch = Regex.Match(html, @"videoId\s*[:=]\s*['""](\d+)['""]", RegexOptions.IgnoreCase);
        if (idMatch.Success) return idMatch.Groups[1].Value;
        
        idMatch = Regex.Match(html, @"articleId\s*[:=]\s*['""](\d+)['""]", RegexOptions.IgnoreCase);
        if (idMatch.Success) return idMatch.Groups[1].Value;
        
        idMatch = Regex.Match(html, @"contentId\s*[:=]\s*['""](\d+)['""]", RegexOptions.IgnoreCase);
        if (idMatch.Success) return idMatch.Groups[1].Value;
        
        return string.Empty;
    }
}
