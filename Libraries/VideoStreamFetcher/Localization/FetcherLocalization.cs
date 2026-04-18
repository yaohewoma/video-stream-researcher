using System;
using System.Collections.Generic;
using System.Globalization;

namespace VideoStreamFetcher.Localization;

/// <summary>
/// VideoStreamFetcher 库的本地化服务
/// </summary>
public static class FetcherLocalization
{
    private static CultureInfo _currentCulture = CultureInfo.CurrentCulture;
    
    /// <summary>
    /// 当前文化
    /// </summary>
    public static CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            _currentCulture = value;
            CultureInfo.CurrentCulture = value;
            CultureInfo.CurrentUICulture = value;
        }
    }

    /// <summary>
    /// 中文资源
    /// </summary>
    private static readonly Dictionary<string, string> ZhCnResources = new()
    {
        // 解析相关
        ["Parsing.RequestingUrl"] = "正在请求 URL: {0}",
        ["Parsing.BilibiliShortLink"] = "发现 B 站短链接，正在获取真实 URL...",
        ["Parsing.ResolvedUrl"] = "解析后的真实 URL: {0}",
        ["Parsing.UsingParser"] = "使用匹配的解析器: {0}",
        ["Parsing.RequestStartTime"] = "请求开始时间: {0}",
        ["Parsing.RequestMethod"] = "请求方法: {0}",
        ["Parsing.RequestUrl"] = "请求 URL: {0}",
        ["Parsing.ResponseReceivedTime"] = "响应接收时间: {0}",
        ["Parsing.RequestDuration"] = "请求耗时: {0:F2} ms",
        ["Parsing.ResponseStatusCode"] = "响应状态码: {0}",
        ["Parsing.ReadingResponse"] = "开始读取响应内容...",
        ["Parsing.ContentLength"] = "成功获取页面内容，长度: {0} 字符",
        ["Parsing.GarbageText"] = "页面内容为乱码，可能是编码问题或压缩问题",
        ["Parsing.ContentSaved"] = "页面内容已保存到: {0}",
        ["Parsing.Failed"] = "解析视频信息失败: {0}",
        ["Parsing.GeneralExtraction"] = "正在尝试通用视频数据提取...",
        ["Parsing.TitleExtracted"] = "获取到标题: {0}",
        ["Parsing.PatternMatched"] = "使用模式成功提取视频数据: {0}...",
        ["Parsing.PatternMatchedUrl"] = "使用模式成功提取视频 URL: {0}...",
        ["Parsing.InvalidJson"] = "提取的视频数据不是有效的 JSON 格式，继续尝试其他模式...",
        ["Parsing.NoVideoData"] = "未找到视频数据",
        ["Parsing.InvalidJsonFormat"] = "提取的视频数据不是有效的 JSON 格式",
        ["Parsing.VideoUrlFound"] = "发现视频 URL",
        ["Parsing.NoStreamInfo"] = "未找到有效的视频流信息",
        ["Parsing.ParseFailed"] = "解析视频数据失败: {0}",
        
        // B站解析相关
        ["Bilibili.NeedHtml"] = "B 站解析器需要 HTML 内容",
        ["Bilibili.NoVideoData"] = "未找到 B 站视频数据",
        ["Bilibili.InvalidJson"] = "提取的 B 站视频数据不是有效的 JSON 格式",
        ["Bilibili.ExtractingVideoData"] = "正在提取 B 站视频数据...",
        ["Bilibili.PatternMatched"] = "使用模式成功提取 B 站视频数据: {0}...",
        ["Bilibili.InvalidJsonContinue"] = "提取的 B 站视频数据不是有效的 JSON 格式，继续尝试其他模式...",
        ["Bilibili.JsonParseFailed"] = "B 站视频数据 JSON 解析失败",
        ["Bilibili.DashFormat"] = "发现 B 站 dash 格式（分离的音视频流）",
        ["Bilibili.VideoStreamsFound"] = "找到 {0} 个 B 站视频流，详细信息：",
        ["Bilibili.StreamInfo"] = "  - {0} (quality id: {1}), 带宽: {2}, 大小: {3}, URL: {4}...",
        ["Bilibili.SortedStreams"] = "B 站视频流排序结果：",
        ["Bilibili.StreamEntry"] = "  {0}. {1} (quality id: {2}, 带宽: {3})",
        ["Bilibili.VideoStreamUrl"] = "找到 B 站视频流: {0}",
        ["Bilibili.SelectedResolution"] = "选择的 B 站视频分辨率: {0} (quality id: {1})",
        ["Bilibili.EmptyVideoUrl"] = "警告：B 站视频流 URL 为空，尝试使用其他视频流",
        ["Bilibili.SecondVideoStream"] = "使用第二个 B 站视频流: {0}",
        ["Bilibili.SecondResolution"] = "选择的 B 站视频分辨率: {0} (quality id: {1})",
        ["Bilibili.AudioStreamUrl"] = "找到 B 站音频流: {0}",
        ["Bilibili.DurlFormat"] = "发现 B 站 durl 格式（合并的音视频流）",
        ["Bilibili.CombinedStreams"] = "找到 {0} 个 B 站合并流",
        ["Bilibili.NoStreamInfo"] = "未找到 B 站有效的视频流信息",
        ["Bilibili.ParseFailed"] = "解析 B 站视频数据失败: {0}",
        
        // 下载相关
        ["Download.Starting"] = "开始下载: {0}",
        ["Download.VideoStream"] = "开始下载视频流: {0}",
        ["Download.AudioStream"] = "开始下载音频流: {0}",
        ["Download.ReceivingVideo"] = "开始接收视频流数据...",
        ["Download.ReceivingAudio"] = "开始接收音频流数据...",
        ["Download.Merging"] = "开始合并音视频流...",
        ["Download.Completed"] = "下载完成: {0}",
        ["Download.Cancelled"] = "下载已取消",
        ["Download.Failed"] = "下载失败: {0}",
        ["Download.Speed"] = "下载速度: {0}/s",
        ["Download.Progress"] = "下载进度: {0:F2}%",
        ["Download.VideoProgress"] = "视频流下载进度: {0:F2}%",
        ["Download.AudioProgress"] = "音频流下载进度: {0:F2}%",
        ["Download.Remuxing"] = "正在重新封装视频...",
        ["Download.RemuxCompleted"] = "重新封装完成",
        ["Download.HlsSegmentsCompleted"] = "[HLS] 分片下载完成: {0} 个",
        
        // 通用
        ["Common.Error"] = "错误: {0}",
        ["Common.Warning"] = "警告: {0}",
        ["Common.Info"] = "信息: {0}",
        ["Common.Success"] = "成功: {0}",
    };

    /// <summary>
    /// 英文资源
    /// </summary>
    private static readonly Dictionary<string, string> EnUsResources = new()
    {
        // 解析相关
        ["Parsing.RequestingUrl"] = "Requesting URL: {0}",
        ["Parsing.BilibiliShortLink"] = "Found Bilibili short link, resolving real URL...",
        ["Parsing.ResolvedUrl"] = "Resolved URL: {0}",
        ["Parsing.UsingParser"] = "Using parser: {0}",
        ["Parsing.RequestStartTime"] = "Request start time: {0}",
        ["Parsing.RequestMethod"] = "Request method: {0}",
        ["Parsing.RequestUrl"] = "Request URL: {0}",
        ["Parsing.ResponseReceivedTime"] = "Response received: {0}",
        ["Parsing.RequestDuration"] = "Request duration: {0:F2} ms",
        ["Parsing.ResponseStatusCode"] = "Response status: {0}",
        ["Parsing.ReadingResponse"] = "Reading response content...",
        ["Parsing.ContentLength"] = "Page content received, length: {0} chars",
        ["Parsing.GarbageText"] = "Page content is garbled, possible encoding or compression issue",
        ["Parsing.ContentSaved"] = "Page content saved to: {0}",
        ["Parsing.Failed"] = "Failed to parse video: {0}",
        ["Parsing.GeneralExtraction"] = "Trying general video extraction...",
        ["Parsing.TitleExtracted"] = "Title extracted: {0}",
        ["Parsing.PatternMatched"] = "Pattern matched for video data: {0}...",
        ["Parsing.PatternMatchedUrl"] = "Pattern matched for video URL: {0}...",
        ["Parsing.InvalidJson"] = "Invalid JSON format, trying other patterns...",
        ["Parsing.NoVideoData"] = "No video data found",
        ["Parsing.InvalidJsonFormat"] = "Video data is not valid JSON",
        ["Parsing.VideoUrlFound"] = "Video URL found",
        ["Parsing.NoStreamInfo"] = "No stream info found",
        ["Parsing.ParseFailed"] = "Failed to parse video data: {0}",
        
        // B站解析相关
        ["Bilibili.NeedHtml"] = "Bilibili parser requires HTML content",
        ["Bilibili.NoVideoData"] = "No Bilibili video data found",
        ["Bilibili.InvalidJson"] = "Extracted Bilibili video data is not valid JSON",
        ["Bilibili.ExtractingVideoData"] = "Extracting Bilibili video data...",
        ["Bilibili.PatternMatched"] = "Pattern matched for Bilibili video data: {0}...",
        ["Bilibili.InvalidJsonContinue"] = "Invalid JSON format, trying other patterns...",
        ["Bilibili.JsonParseFailed"] = "Bilibili video data JSON parse failed",
        ["Bilibili.DashFormat"] = "Found Bilibili dash format (separate audio/video)",
        ["Bilibili.VideoStreamsFound"] = "Found {0} Bilibili video streams, details:",
        ["Bilibili.StreamInfo"] = "  - {0} (quality id: {1}), bandwidth: {2}, size: {3}, URL: {4}...",
        ["Bilibili.SortedStreams"] = "Bilibili video streams sorted:",
        ["Bilibili.StreamEntry"] = "  {0}. {1} (quality id: {2}, bandwidth: {3})",
        ["Bilibili.VideoStreamUrl"] = "Found Bilibili video stream: {0}",
        ["Bilibili.SelectedResolution"] = "Selected Bilibili resolution: {0} (quality id: {1})",
        ["Bilibili.EmptyVideoUrl"] = "Warning: Bilibili video stream URL is empty, trying other stream",
        ["Bilibili.SecondVideoStream"] = "Using second Bilibili video stream: {0}",
        ["Bilibili.SecondResolution"] = "Selected Bilibili resolution: {0} (quality id: {1})",
        ["Bilibili.AudioStreamUrl"] = "Found Bilibili audio stream: {0}",
        ["Bilibili.DurlFormat"] = "Found Bilibili durl format (combined audio/video)",
        ["Bilibili.CombinedStreams"] = "Found {0} Bilibili combined streams",
        ["Bilibili.NoStreamInfo"] = "No valid Bilibili stream info found",
        ["Bilibili.ParseFailed"] = "Failed to parse Bilibili video data: {0}",
        
        // 下载相关
        ["Download.Starting"] = "Starting download: {0}",
        ["Download.VideoStream"] = "Starting video stream: {0}",
        ["Download.AudioStream"] = "Starting audio stream: {0}",
        ["Download.ReceivingVideo"] = "Receiving video stream data...",
        ["Download.ReceivingAudio"] = "Receiving audio stream data...",
        ["Download.Merging"] = "Merging audio and video...",
        ["Download.Completed"] = "Download completed: {0}",
        ["Download.Cancelled"] = "Download cancelled",
        ["Download.Failed"] = "Download failed: {0}",
        ["Download.Speed"] = "Speed: {0}/s",
        ["Download.Progress"] = "Progress: {0:F2}%",
        ["Download.VideoProgress"] = "Video stream progress: {0:F2}%",
        ["Download.AudioProgress"] = "Audio stream progress: {0:F2}%",
        ["Download.Remuxing"] = "Remuxing video...",
        ["Download.RemuxCompleted"] = "Remux completed",
        ["Download.HlsSegmentsCompleted"] = "[HLS] Segments download completed: {0}",
        
        // Common
        ["Common.Error"] = "Error: {0}",
        ["Common.Warning"] = "Warning: {0}",
        ["Common.Info"] = "Info: {0}",
        ["Common.Success"] = "Success: {0}",
    };

    /// <summary>
    /// 俄文资源
    /// </summary>
    private static readonly Dictionary<string, string> RuRuResources = new()
    {
        // 解析相关
        ["Parsing.RequestingUrl"] = "Запрос URL: {0}",
        ["Parsing.BilibiliShortLink"] = "Найдена короткая ссылка Bilibili, получение реального URL...",
        ["Parsing.ResolvedUrl"] = "URL получен: {0}",
        ["Parsing.UsingParser"] = "Используется парсер: {0}",
        ["Parsing.RequestStartTime"] = "Время начала запроса: {0}",
        ["Parsing.RequestMethod"] = "Метод запроса: {0}",
        ["Parsing.RequestUrl"] = "URL запроса: {0}",
        ["Parsing.ResponseReceivedTime"] = "Ответ получен: {0}",
        ["Parsing.RequestDuration"] = "Длительность: {0:F2} мс",
        ["Parsing.ResponseStatusCode"] = "Код статуса: {0}",
        ["Parsing.ReadingResponse"] = "Чтение ответа...",
        ["Parsing.ContentLength"] = "Содержимое получено, длина: {0} симв.",
        ["Parsing.GarbageText"] = "Содержимое повреждено, возможна проблема с кодировкой",
        ["Parsing.ContentSaved"] = "Содержимое сохранено: {0}",
        ["Parsing.Failed"] = "Ошибка парсинга: {0}",
        ["Parsing.GeneralExtraction"] = "Попытка общего извлечения видео...",
        ["Parsing.TitleExtracted"] = "Заголовок: {0}",
        ["Parsing.PatternMatched"] = "Шаблон найден для данных: {0}...",
        ["Parsing.PatternMatchedUrl"] = "Шаблон найден для URL: {0}...",
        ["Parsing.InvalidJson"] = "Неверный JSON, пробую другие шаблоны...",
        ["Parsing.NoVideoData"] = "Данные видео не найдены",
        ["Parsing.InvalidJsonFormat"] = "Данные не в формате JSON",
        ["Parsing.VideoUrlFound"] = "URL видео найден",
        ["Parsing.NoStreamInfo"] = "Информация о потоке не найдена",
        ["Parsing.ParseFailed"] = "Ошибка парсинга данных: {0}",
        
        // B站解析相关
        ["Bilibili.NeedHtml"] = "Парсеру Bilibili требуется HTML",
        ["Bilibili.NoVideoData"] = "Данные видео Bilibili не найдены",
        ["Bilibili.InvalidJson"] = "Извлеченные данные не являются валидным JSON",
        ["Bilibili.ExtractingVideoData"] = "Извлечение данных видео Bilibili...",
        ["Bilibili.PatternMatched"] = "Шаблон найден для данных Bilibili: {0}...",
        ["Bilibili.InvalidJsonContinue"] = "Неверный JSON, пробую другие шаблоны...",
        ["Bilibili.JsonParseFailed"] = "Ошибка парсинга JSON данных Bilibili",
        ["Bilibili.DashFormat"] = "Найден формат dash Bilibili (разделенные аудио/видео)",
        ["Bilibili.VideoStreamsFound"] = "Найдено {0} видеопотоков Bilibili, подробности:",
        ["Bilibili.StreamInfo"] = "  - {0} (id качества: {1}), пропускная способность: {2}, размер: {3}, URL: {4}...",
        ["Bilibili.SortedStreams"] = "Видеопотоки Bilibili отсортированы:",
        ["Bilibili.StreamEntry"] = "  {0}. {1} (id качества: {2}, пропускная способность: {3})",
        ["Bilibili.VideoStreamUrl"] = "Найден видеопоток Bilibili: {0}",
        ["Bilibili.SelectedResolution"] = "Выбрано разрешение Bilibili: {0} (id качества: {1})",
        ["Bilibili.EmptyVideoUrl"] = "Предупреждение: URL видеопотока Bilibili пуст, пробую другой поток",
        ["Bilibili.SecondVideoStream"] = "Используется второй видеопоток Bilibili: {0}",
        ["Bilibili.SecondResolution"] = "Выбрано разрешение Bilibili: {0} (id качества: {1})",
        ["Bilibili.AudioStreamUrl"] = "Найден аудиопоток Bilibili: {0}",
        ["Bilibili.DurlFormat"] = "Найден формат durl Bilibili (объединенные аудио/видео)",
        ["Bilibili.CombinedStreams"] = "Найдено {0} объединенных потоков Bilibili",
        ["Bilibili.NoStreamInfo"] = "Не найдена валидная информация о потоках Bilibili",
        ["Bilibili.ParseFailed"] = "Ошибка парсинга данных видео Bilibili: {0}",
        
        // 下载相关
        ["Download.Starting"] = "Начало загрузки: {0}",
        ["Download.VideoStream"] = "Начало видеопотока: {0}",
        ["Download.AudioStream"] = "Начало аудиопотока: {0}",
        ["Download.ReceivingVideo"] = "Получение видеоданных...",
        ["Download.ReceivingAudio"] = "Получение аудиоданных...",
        ["Download.Merging"] = "Объединение аудио и видео...",
        ["Download.Completed"] = "Загрузка завершена: {0}",
        ["Download.Cancelled"] = "Загрузка отменена",
        ["Download.Failed"] = "Ошибка загрузки: {0}",
        ["Download.Speed"] = "Скорость: {0}/с",
        ["Download.Progress"] = "Прогресс: {0:F2}%",
        ["Download.VideoProgress"] = "Прогресс видео: {0:F2}%",
        ["Download.AudioProgress"] = "Прогресс аудио: {0:F2}%",
        ["Download.Remuxing"] = "Перепаковка видео...",
        ["Download.RemuxCompleted"] = "Перепаковка завершена",
        ["Download.HlsSegmentsCompleted"] = "[HLS] Загрузка сегментов завершена: {0}",
        
        // Общие
        ["Common.Error"] = "Ошибка: {0}",
        ["Common.Warning"] = "Предупреждение: {0}",
        ["Common.Info"] = "Информация: {0}",
        ["Common.Success"] = "Успех: {0}",
    };

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    public static string GetString(string key, params object[] args)
    {
        var resources = GetCurrentResources();
        if (resources.TryGetValue(key, out var value))
        {
            try
            {
                return string.Format(value, args);
            }
            catch
            {
                return value;
            }
        }
        return $"[{key}]";
    }

    /// <summary>
    /// 获取当前资源字典
    /// </summary>
    private static Dictionary<string, string> GetCurrentResources()
    {
        return CurrentCulture.Name switch
        {
            "en-US" => EnUsResources,
            "ru-RU" => RuRuResources,
            _ => ZhCnResources
        };
    }

    /// <summary>
    /// 设置语言
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        try
        {
            CurrentCulture = new CultureInfo(languageCode);
        }
        catch
        {
            CurrentCulture = new CultureInfo("zh-CN");
        }
    }
}
