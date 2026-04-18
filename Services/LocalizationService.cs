using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace video_stream_researcher.Services;

/// <summary>
/// 本地化服务 - 提供多语言支持
/// </summary>
public class LocalizationService : ReactiveObject
{
    /// <summary>
    /// 单例实例
    /// </summary>
    public static LocalizationService Instance { get; } = new();

    /// <summary>
    /// 当前文化 - 默认英文
    /// </summary>
    private CultureInfo _currentCulture = new CultureInfo("en-US");

    /// <summary>
    /// 当前语言代码
    /// </summary>
    public string CurrentLanguageCode => _currentCulture.Name;

    /// <summary>
    /// 当前文化
    /// </summary>
    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        private set
        {
            if (_currentCulture.Name != value.Name)
            {
                _currentCulture = value;
                CultureInfo.CurrentCulture = value;
                CultureInfo.CurrentUICulture = value;
                this.RaisePropertyChanged(nameof(CurrentCulture));
                this.RaisePropertyChanged(nameof(CurrentLanguageCode));
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// 语言变更事件
    /// </summary>
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// 支持的语言列表
    /// </summary>
    public static readonly List<LanguageOption> SupportedLanguages = new()
    {
        new LanguageOption("中文", "zh-CN"),
        new LanguageOption("English", "en-US"),
        new LanguageOption("Русский", "ru-RU")
    };

    /// <summary>
    /// 中文资源字典
    /// </summary>
    private static readonly Dictionary<string, string> ZhCnResources = new()
    {
        // UI 文本
        ["WindowTitle"] = "视频流解析工具",
        ["LanguageLabel"] = "语言：",
        ["LabelVideoUrl"] = "视频链接：",
        ["UrlPlaceholder"] = "粘贴视频链接，支持 B站、快手、米游社",
        ["LabelSavePath"] = "保存路径：",
        ["ButtonBrowse"] = "浏览",
        ["ButtonStartProcessing"] = "开始处理",
        ["RadioButtonAll"] = "下载全部",
        ["RadioButtonNoMerge"] = "不合并",
        ["RadioButtonAudioOnly"] = "仅音频",
        ["RadioButtonVideoOnly"] = "仅视频",
        ["CheckBoxPreviewEdit"] = "预览编辑",
        ["CheckBoxKeepOriginal"] = "保留原文件",
        ["CheckBoxAutoPreview"] = "下载后自动预览",
        ["TextBlockSegmentCount"] = "分段数：",
        ["TextBlockHlsOnly"] = "（仅HLS）",
        ["TooltipToggleTheme"] = "切换主题",
        ["TooltipCancelDownload"] = "取消下载",
        ["LoginPrompt"] = "提示：登录B站账号可以获取高清视频。输入 'bili' 或 'login' 开始登录流程。",
        ["InitialSpeedText"] = "当前速度: 0 MB/s",
        ["StatusReady"] = "就绪",
        ["StatusParsing"] = "正在解析...",
        ["StatusDownloading"] = "正在下载...",
        ["StatusCompleted"] = "完成",
        ["StatusError"] = "错误",
        ["MessageUrlEmpty"] = "请输入视频链接",
        ["MessageParseError"] = "解析失败",
        ["MessageDownloadError"] = "下载失败",
        ["MessageCopySuccess"] = "已复制到剪贴板",
        
        // 日志消息
        ["LogInitLoginFailed"] = "初始化登录状态失败",
        ["LogReLoginRequested"] = "🔄 检测到重新登录请求，正在登出当前账号...",
        ["LogLoggedOut"] = "✅ 已登出，准备重新登录...",
        ["LogReLoginFailed"] = "重新登录失败",
        ["LogCustomBgDetected"] = "🎨 检测到自定义背景触发指令！",
        ["LogSelectBgImage"] = "请选择一张图片作为背景喵~",
        ["LogLoginTriggerDetected"] = "🎉 检测到登录触发指令！",
        ["LogLoginForHd"] = "可以启用获取更高分辨率的视频喵",
        ["LogStartingLogin"] = "正在启动B站登录流程...",
        ["LogLoginCancelled"] = "登录流程已取消",
        ["LogLoginException"] = "登录流程异常",
        ["LogQrCodeScanned"] = "📱 已检测到手机扫码，请在手机上确认登录喵~",
        ["LogLoginSuccess"] = "✅ 登录成功！欢迎，{0}",
        ["LogLoginSuccessHint"] = "现在可以获取更高分辨率的B站视频了喵~",
        ["LogLoginFailed"] = "❌ 登录失败: {0}",
        ["LogRetryLogin"] = "请重新输入 'bili' 或 'login' 触发登录流程",
        ["LogLoginEnded"] = "登录流程已结束，输入框已清空",
        ["LogLoginCancelledRetry"] = "登录已取消，可以重新输入 'bili' 或 'login' 触发登录",
        ["LogRefreshingQr"] = "正在刷新二维码...",
        ["LogLoggedOutSimple"] = "已登出",
        ["LogSavePathSet"] = "保存路径已设置为: {0}",
        ["LogInvalidPath"] = "无效的路径: {0}",
        ["LogSetPathFailed"] = "设置保存路径失败",
        ["LogInLoginProcess"] = "正在登录流程中，请完成登录后再下载",
        ["LogEnterUrl"] = "请输入视频URL",
        ["LogSelectSavePath"] = "请选择保存路径",
        ["LogBilibiliDetected"] = "检测到B站视频链接",
        ["LogLoginForHdHint"] = "提示：登录后可以获取更高分辨率的视频喵",
        ["LogTypeBiliToLogin"] = "在URL输入框中输入 'bili' 或 'login' 即可开始登录流程",
        ["LogDownloadAgainHint"] = "💡 再次点击【开始处理】将重新下载一份",
        ["LogDownloadCompleted"] = "✅ 下载处理完成",
        ["LogProcessException"] = "处理流程异常",
        ["LogCancellingDownload"] = "正在取消下载...",
        ["LogThemeSwitched"] = "主题切换成功，当前主题: {0}",
        ["LogThemeDark"] = "深色",
        ["LogThemeLight"] = "浅色",
        ["LogCannotGetAppInstance"] = "无法获取应用程序实例",
        ["LogThemeSwitchFailed"] = "主题切换失败",

        // 下载流程日志
        ["LogDownloadDuplicate"] = "⏳ 已检测到相同任务正在下载，跳过本次请求",
        ["LogDownloadExisting"] = "✅ 已检测到相同文件，点击【开始处理】将重新下载一份",
        ["LogDownloadOutputDir"] = "   - 输出目录: {0}",
        ["LogDownloadOutputFile"] = "   - 输出文件: {0}",
        ["LogDownloadStartRequest"] = "🚀 开始处理下载请求",
        ["LogDownloadUrl"] = "   - URL: {0}",
        ["LogDownloadSavePath"] = "   - 保存路径: {0}",
        ["LogDownloadAudioOnly"] = "   - 仅音频: {0}",
        ["LogDownloadVideoOnly"] = "   - 仅视频: {0}",
        ["LogDownloadNoMerge"] = "   - 不合并: {0}",
        ["LogDownloadParseStart"] = "📋 开始解析视频信息",
        ["LogDownloadParseSuccess"] = "✅ 视频解析成功: {0}",
        ["LogDownloadCheckFile"] = "📋 准备检查文件存在性",
        ["LogDownloadFileExists"] = "✅ 文件已存在，跳过下载",
        ["LogDownloadStart"] = "开始下载: {0}",
        ["LogDownloadOutputDirFinal"] = "📁 输出目录: {0}",
        ["LogDownloadOutputFileFinal"] = "📄 输出文件: {0}",
        ["LogDownloadWarningOutputPath"] = "⚠️ 警告: 无法确定输出路径",
        ["LogDownloadWarningOutputFile"] = "⚠️ 警告: 无法找到输出文件在 {0}",
        ["LogDownloadComplete"] = "处理完成！实际文件大小: {0}",
        ["LogDownloadCancelled"] = "下载已取消",
        ["LogDownloadFailed"] = "处理失败: {0}",
        ["LogDownloadParseFailed"] = "视频解析失败",

        // 状态文本
        ["StatusParsingVideo"] = "开始解析视频信息...",
        ["StatusDuplicate"] = "下载进行中，已跳过重复请求",
        ["StatusExisting"] = "已下载，等待确认",
        ["StatusDownloading"] = "开始下载...",
        ["StatusVideoStream"] = "视频流正在获取中...",
        ["StatusAudioStream"] = "音频流正在获取中...",
        ["StatusMerging"] = "正在处理合并中...",
        ["StatusCompleted"] = "处理完成！",
        ["StatusFileExists"] = "已下载，跳过处理",
        ["StatusCancelled"] = "下载已取消",
        ["StatusFailed"] = "处理失败",

        // 程序功能介绍
        ["AppFeatureTitle"] = "═══════════════════════════════════════",
        ["AppFeatureHeader"] = "  📋 快速使用指南",
        ["AppFeatureSeparator"] = "═══════════════════════════════════════",
        ["AppFeature1"] = "  🎯 粘贴视频链接 → 点击【开始处理】即可下载",
        ["AppFeature2"] = "  📺 支持平台: B站(bilibili.com) | 快手(kuaishou.com) | 米游社(miyoushe.com)",
        ["AppFeature3"] = "  🔑 B站登录: 输入 'bili' 或 'login' 获取高清视频",
        ["AppFeature4"] = "  🎨 自定义背景: 输入 'bg' 或 'background'",
        ["AppFeatureFooter"] = "═══════════════════════════════════════"
    };

    /// <summary>
    /// 英文资源字典
    /// </summary>
    private static readonly Dictionary<string, string> EnUsResources = new()
    {
        // UI 文本
        ["WindowTitle"] = "Video Stream Parser",
        ["LanguageLabel"] = "Language:",
        ["LabelVideoUrl"] = "Video URL:",
        ["UrlPlaceholder"] = "Paste video URL, supports Bilibili, Kuaishou, Miyoushe",
        ["LabelSavePath"] = "Save Path:",
        ["ButtonBrowse"] = "Browse",
        ["ButtonStartProcessing"] = "Start",
        ["RadioButtonAll"] = "Download All",
        ["RadioButtonNoMerge"] = "No Merge",
        ["RadioButtonAudioOnly"] = "Audio Only",
        ["RadioButtonVideoOnly"] = "Video Only",
        ["CheckBoxPreviewEdit"] = "Preview & Edit",
        ["CheckBoxKeepOriginal"] = "Keep Original",
        ["CheckBoxAutoPreview"] = "Auto Preview",
        ["TextBlockSegmentCount"] = "Segments:",
        ["TextBlockHlsOnly"] = "(HLS only)",
        ["TooltipToggleTheme"] = "Toggle Theme",
        ["TooltipCancelDownload"] = "Cancel Download",
        ["LoginPrompt"] = "Tip: Login to Bilibili for HD videos. Type 'bili' or 'login' to start.",
        ["InitialSpeedText"] = "Speed: 0 MB/s",
        ["StatusReady"] = "Ready",
        ["StatusParsing"] = "Parsing...",
        ["StatusDownloading"] = "Downloading...",
        ["StatusCompleted"] = "Completed",
        ["StatusError"] = "Error",
        ["MessageUrlEmpty"] = "Please enter video URL",
        ["MessageParseError"] = "Parse failed",
        ["MessageDownloadError"] = "Download failed",
        ["MessageCopySuccess"] = "Copied to clipboard",
        
        // 日志消息
        ["LogInitLoginFailed"] = "Failed to initialize login status",
        ["LogReLoginRequested"] = "🔄 Re-login requested, logging out...",
        ["LogLoggedOut"] = "✅ Logged out, preparing to re-login...",
        ["LogReLoginFailed"] = "Re-login failed",
        ["LogCustomBgDetected"] = "🎨 Custom background trigger detected!",
        ["LogSelectBgImage"] = "Please select an image as background~",
        ["LogLoginTriggerDetected"] = "🎉 Login trigger detected!",
        ["LogLoginForHd"] = "Login to enable HD video downloads~",
        ["LogStartingLogin"] = "Starting Bilibili login process...",
        ["LogLoginCancelled"] = "Login cancelled",
        ["LogLoginException"] = "Login exception",
        ["LogQrCodeScanned"] = "📱 QR code scanned, please confirm on phone~",
        ["LogLoginSuccess"] = "✅ Login successful! Welcome, {0}",
        ["LogLoginSuccessHint"] = "You can now download higher resolution Bilibili videos~",
        ["LogLoginFailed"] = "❌ Login failed: {0}",
        ["LogRetryLogin"] = "Please type 'bili' or 'login' to trigger login again",
        ["LogLoginEnded"] = "Login process ended, input cleared",
        ["LogLoginCancelledRetry"] = "Login cancelled, type 'bili' or 'login' to retry",
        ["LogRefreshingQr"] = "Refreshing QR code...",
        ["LogLoggedOutSimple"] = "Logged out",
        ["LogSavePathSet"] = "Save path set to: {0}",
        ["LogInvalidPath"] = "Invalid path: {0}",
        ["LogSetPathFailed"] = "Failed to set save path",
        ["LogInLoginProcess"] = "In login process, please complete login first",
        ["LogEnterUrl"] = "Please enter video URL",
        ["LogSelectSavePath"] = "Please select save path",
        ["LogBilibiliDetected"] = "Bilibili video link detected",
        ["LogLoginForHdHint"] = "Tip: Login to get higher resolution videos",
        ["LogTypeBiliToLogin"] = "Type 'bili' or 'login' in URL box to start",
        ["LogDownloadAgainHint"] = "💡 Click Start again to download another copy",
        ["LogDownloadCompleted"] = "✅ Download completed",
        ["LogProcessException"] = "Process exception",
        ["LogCancellingDownload"] = "Cancelling download...",
        ["LogThemeSwitched"] = "Theme switched, current: {0}",
        ["LogThemeDark"] = "Dark",
        ["LogThemeLight"] = "Light",
        ["LogCannotGetAppInstance"] = "Cannot get application instance",
        ["LogThemeSwitchFailed"] = "Theme switch failed",

        // 下载流程日志
        ["LogDownloadDuplicate"] = "⏳ Same task already downloading, skipping...",
        ["LogDownloadExisting"] = "✅ Same file detected, click Start to download again",
        ["LogDownloadOutputDir"] = "   - Output dir: {0}",
        ["LogDownloadOutputFile"] = "   - Output file: {0}",
        ["LogDownloadStartRequest"] = "🚀 Starting download request",
        ["LogDownloadUrl"] = "   - URL: {0}",
        ["LogDownloadSavePath"] = "   - Save path: {0}",
        ["LogDownloadAudioOnly"] = "   - Audio only: {0}",
        ["LogDownloadVideoOnly"] = "   - Video only: {0}",
        ["LogDownloadNoMerge"] = "   - No merge: {0}",
        ["LogDownloadParseStart"] = "📋 Parsing video info...",
        ["LogDownloadParseSuccess"] = "✅ Video parsed: {0}",
        ["LogDownloadCheckFile"] = "📋 Checking file existence...",
        ["LogDownloadFileExists"] = "✅ File exists, skipping download",
        ["LogDownloadStart"] = "Starting download: {0}",
        ["LogDownloadOutputDirFinal"] = "📁 Output dir: {0}",
        ["LogDownloadOutputFileFinal"] = "📄 Output file: {0}",
        ["LogDownloadWarningOutputPath"] = "⚠️ Warning: Cannot determine output path",
        ["LogDownloadWarningOutputFile"] = "⚠️ Warning: Cannot find output file in {0}",
        ["LogDownloadComplete"] = "Done! File size: {0}",
        ["LogDownloadCancelled"] = "Download cancelled",
        ["LogDownloadFailed"] = "Failed: {0}",
        ["LogDownloadParseFailed"] = "Video parsing failed",

        // 状态文本
        ["StatusParsingVideo"] = "Parsing video info...",
        ["StatusDuplicate"] = "Download in progress, skipped duplicate",
        ["StatusExisting"] = "Already downloaded, waiting for confirmation",
        ["StatusDownloading"] = "Starting download...",
        ["StatusVideoStream"] = "Getting video stream...",
        ["StatusAudioStream"] = "Getting audio stream...",
        ["StatusMerging"] = "Merging...",
        ["StatusCompleted"] = "Done!",
        ["StatusFileExists"] = "Already downloaded, skipped",
        ["StatusCancelled"] = "Download cancelled",
        ["StatusFailed"] = "Failed",

        // 程序功能介绍
        ["AppFeatureTitle"] = "═══════════════════════════════════════",
        ["AppFeatureHeader"] = "  📋 Quick Start Guide",
        ["AppFeatureSeparator"] = "═══════════════════════════════════════",
        ["AppFeature1"] = "  🎯 Paste video URL → Click 【Start】to download",
        ["AppFeature2"] = "  📺 Supported: Bilibili(bilibili.com) | Kuaishou(kuaishou.com) | Miyoushe(miyoushe.com)",
        ["AppFeature3"] = "  🔑 Bilibili Login: Type 'bili' or 'login' for HD",
        ["AppFeature4"] = "  🎨 Custom Background: Type 'bg' or 'background'",
        ["AppFeatureFooter"] = "═══════════════════════════════════════"
    };

    /// <summary>
    /// 俄文资源字典
    /// </summary>
    private static readonly Dictionary<string, string> RuRuResources = new()
    {
        // UI 文本
        ["WindowTitle"] = "Парсер видеопотока",
        ["LanguageLabel"] = "Язык:",
        ["LabelVideoUrl"] = "URL видео:",
        ["UrlPlaceholder"] = "Вставьте URL видео, поддерживает Bilibili, Kuaishou, Miyoushe",
        ["LabelSavePath"] = "Путь сохранения:",
        ["ButtonBrowse"] = "Обзор",
        ["ButtonStartProcessing"] = "Старт",
        ["RadioButtonAll"] = "Скачать все",
        ["RadioButtonNoMerge"] = "Не объединять",
        ["RadioButtonAudioOnly"] = "Только аудио",
        ["RadioButtonVideoOnly"] = "Только видео",
        ["CheckBoxPreviewEdit"] = "Предпросмотр",
        ["CheckBoxKeepOriginal"] = "Сохранить оригинал",
        ["CheckBoxAutoPreview"] = "Авто предпросмотр",
        ["TextBlockSegmentCount"] = "Сегменты:",
        ["TextBlockHlsOnly"] = "(только HLS)",
        ["TooltipToggleTheme"] = "Сменить тему",
        ["TooltipCancelDownload"] = "Отменить загрузку",
        ["LoginPrompt"] = "Совет: войдите в Bilibili для HD видео. Введите 'bili' или 'login'.",
        ["InitialSpeedText"] = "Скорость: 0 МБ/с",
        ["StatusReady"] = "Готов",
        ["StatusParsing"] = "Анализ...",
        ["StatusDownloading"] = "Загрузка...",
        ["StatusCompleted"] = "Завершено",
        ["StatusError"] = "Ошибка",
        ["MessageUrlEmpty"] = "Введите URL видео",
        ["MessageParseError"] = "Ошибка анализа",
        ["MessageDownloadError"] = "Ошибка загрузки",
        ["MessageCopySuccess"] = "Скопировано в буфер",
        
        // 日志消息
        ["LogInitLoginFailed"] = "Ошибка инициализации входа",
        ["LogReLoginRequested"] = "🔄 Запрос повторного входа, выход...",
        ["LogLoggedOut"] = "✅ Выход выполнен, подготовка к повторному входу...",
        ["LogReLoginFailed"] = "Ошибка повторного входа",
        ["LogCustomBgDetected"] = "🎨 Обнаружен триггер фона!",
        ["LogSelectBgImage"] = "Выберите изображение для фона~",
        ["LogLoginTriggerDetected"] = "🎉 Обнаружен триггер входа!",
        ["LogLoginForHd"] = "Войдите для загрузки HD видео~",
        ["LogStartingLogin"] = "Запуск процесса входа в Bilibili...",
        ["LogLoginCancelled"] = "Вход отменен",
        ["LogLoginException"] = "Исключение входа",
        ["LogQrCodeScanned"] = "📱 QR-код отсканирован, подтвердите на телефоне~",
        ["LogLoginSuccess"] = "✅ Вход успешен! Добро пожаловать, {0}",
        ["LogLoginSuccessHint"] = "Теперь можно загружать видео Bilibili в высоком разрешении~",
        ["LogLoginFailed"] = "❌ Ошибка входа: {0}",
        ["LogRetryLogin"] = "Введите 'bili' или 'login' для повторного входа",
        ["LogLoginEnded"] = "Процесс входа завершен, ввод очищен",
        ["LogLoginCancelledRetry"] = "Вход отменен, введите 'bili' или 'login'",
        ["LogRefreshingQr"] = "Обновление QR-кода...",
        ["LogLoggedOutSimple"] = "Выход выполнен",
        ["LogSavePathSet"] = "Путь сохранения: {0}",
        ["LogInvalidPath"] = "Неверный путь: {0}",
        ["LogSetPathFailed"] = "Ошибка установки пути сохранения",
        ["LogInLoginProcess"] = "В процессе входа, сначала завершите вход",
        ["LogEnterUrl"] = "Введите URL видео",
        ["LogSelectSavePath"] = "Выберите путь сохранения",
        ["LogBilibiliDetected"] = "Обнаружена ссылка на видео Bilibili",
        ["LogLoginForHdHint"] = "Совет: войдите для получения видео в высоком разрешении",
        ["LogTypeBiliToLogin"] = "Введите 'bili' или 'login' в поле URL",
        ["LogDownloadAgainHint"] = "💡 Нажмите Старт снова для загрузки копии",
        ["LogDownloadCompleted"] = "✅ Загрузка завершена",
        ["LogProcessException"] = "Исключение процесса",
        ["LogCancellingDownload"] = "Отмена загрузки...",
        ["LogThemeSwitched"] = "Тема изменена, текущая: {0}",
        ["LogThemeDark"] = "Темная",
        ["LogThemeLight"] = "Светлая",
        ["LogCannotGetAppInstance"] = "Невозможно получить экземпляр приложения",
        ["LogThemeSwitchFailed"] = "Ошибка смены темы",

        // 下载流程日志
        ["LogDownloadDuplicate"] = "⏳ Такая задача уже выполняется, пропуск...",
        ["LogDownloadExisting"] = "✅ Файл найден, нажмите Старт для повторной загрузки",
        ["LogDownloadOutputDir"] = "   - Папка: {0}",
        ["LogDownloadOutputFile"] = "   - Файл: {0}",
        ["LogDownloadStartRequest"] = "🚀 Начало загрузки",
        ["LogDownloadUrl"] = "   - URL: {0}",
        ["LogDownloadSavePath"] = "   - Путь: {0}",
        ["LogDownloadAudioOnly"] = "   - Только аудио: {0}",
        ["LogDownloadVideoOnly"] = "   - Только видео: {0}",
        ["LogDownloadNoMerge"] = "   - Без объединения: {0}",
        ["LogDownloadParseStart"] = "📋 Анализ видео...",
        ["LogDownloadParseSuccess"] = "✅ Видео проанализировано: {0}",
        ["LogDownloadCheckFile"] = "📋 Проверка файла...",
        ["LogDownloadFileExists"] = "✅ Файл существует, пропуск",
        ["LogDownloadStart"] = "Начало загрузки: {0}",
        ["LogDownloadOutputDirFinal"] = "📁 Папка: {0}",
        ["LogDownloadOutputFileFinal"] = "📄 Файл: {0}",
        ["LogDownloadWarningOutputPath"] = "⚠️ Предупреждение: Невозможно определить путь",
        ["LogDownloadWarningOutputFile"] = "⚠️ Предупреждение: Файл не найден в {0}",
        ["LogDownloadComplete"] = "Готово! Размер: {0}",
        ["LogDownloadCancelled"] = "Загрузка отменена",
        ["LogDownloadFailed"] = "Ошибка: {0}",
        ["LogDownloadParseFailed"] = "Ошибка анализа видео",

        // 状态文本
        ["StatusParsingVideo"] = "Анализ видео...",
        ["StatusDuplicate"] = "Загрузка выполняется, пропуск дубликата",
        ["StatusExisting"] = "Уже загружено, ожидание подтверждения",
        ["StatusDownloading"] = "Начало загрузки...",
        ["StatusVideoStream"] = "Получение видеопотока...",
        ["StatusAudioStream"] = "Получение аудиопотока...",
        ["StatusMerging"] = "Объединение...",
        ["StatusCompleted"] = "Готово!",
        ["StatusFileExists"] = "Уже загружено, пропуск",
        ["StatusCancelled"] = "Загрузка отменена",
        ["StatusFailed"] = "Ошибка",

        // 程序功能介绍
        ["AppFeatureTitle"] = "═══════════════════════════════════════",
        ["AppFeatureHeader"] = "  📋 Быстрый старт",
        ["AppFeatureSeparator"] = "═══════════════════════════════════════",
        ["AppFeature1"] = "  🎯 Вставьте URL → Нажмите 【Старт】для загрузки",
        ["AppFeature2"] = "  📺 Платформы: Bilibili(bilibili.com) | Kuaishou(kuaishou.com) | Miyoushe(miyoushe.com)",
        ["AppFeature3"] = "  🔑 Вход Bilibili: Введите 'bili' или 'login' для HD",
        ["AppFeature4"] = "  🎨 Фон: Введите 'bg' или 'background'",
        ["AppFeatureFooter"] = "═══════════════════════════════════════"
    };

    /// <summary>
    /// 私有构造函数
    /// </summary>
    private LocalizationService()
    {
        // 根据系统语言自动设置，默认为英文
        var systemLanguage = GetSystemLanguage();
        CurrentCulture = new CultureInfo(systemLanguage);
    }

    /// <summary>
    /// 获取系统语言，不支持的语言返回英文
    /// </summary>
    /// <returns>语言代码</returns>
    private static string GetSystemLanguage()
    {
        var systemCulture = CultureInfo.CurrentCulture;
        var languageCode = systemCulture.Name;

        // 检查是否为支持的语言
        return languageCode switch
        {
            "zh-CN" or "zh-Hans" or "zh-SG" => "zh-CN",  // 简体中文
            "zh-TW" or "zh-Hant" or "zh-HK" or "zh-MO" => "zh-CN",  // 繁体中文使用简体中文
            "en-US" or "en-GB" or "en-AU" or "en-CA" or "en-NZ" or "en-IE" or "en-ZA" or "en-JM" or "en-BZ" or "en-TT" => "en-US",  // 英语
            "ru-RU" or "ru-BY" or "ru-KZ" or "ru-UA" or "ru-MD" => "ru-RU",  // 俄语
            _ => "en-US"  // 其他语言默认使用英文
        };
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <returns>本地化字符串</returns>
    public string this[string key]
    {
        get
        {
            var resources = GetCurrentResources();
            if (resources.TryGetValue(key, out var value))
                return value;
            return $"[{key}]";
        }
    }

    /// <summary>
    /// 获取当前语言的资源字典
    /// </summary>
    private Dictionary<string, string> GetCurrentResources()
    {
        return CurrentCulture.Name switch
        {
            "en-US" => EnUsResources,
            "ru-RU" => RuRuResources,
            _ => ZhCnResources
        };
    }

    /// <summary>
    /// 获取格式化后的本地化字符串
    /// </summary>
    /// <param name="key">资源键</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化后的字符串</returns>
    public string GetString(string key, params object[] args)
    {
        var format = this[key];
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    /// <summary>
    /// 切换语言
    /// </summary>
    /// <param name="languageCode">语言代码 (zh-CN, en-US, ru-RU)</param>
    public void ChangeLanguage(string languageCode)
    {
        try
        {
            // 验证语言代码是否支持，不支持则使用英文
            var validatedLanguageCode = ValidateLanguageCode(languageCode);
            var culture = new CultureInfo(validatedLanguageCode);
            CurrentCulture = culture;
            
            // 同步设置 VideoStreamFetcher 库的语言
            VideoStreamFetcher.Localization.FetcherLocalization.SetLanguage(validatedLanguageCode);
            
            // 同步设置 Mp4Merger.Core 库的语言
            Mp4Merger.Core.Localization.MergerLocalization.SetLanguage(validatedLanguageCode);
        }
        catch (CultureNotFoundException)
        {
            // 如果语言不支持，使用默认英文
            CurrentCulture = new CultureInfo("en-US");
        }
    }

    /// <summary>
    /// 验证语言代码，返回支持的语言代码，不支持则返回英文
    /// </summary>
    /// <param name="languageCode">语言代码</param>
    /// <returns>验证后的语言代码</returns>
    private static string ValidateLanguageCode(string languageCode)
    {
        return languageCode switch
        {
            "zh-CN" => "zh-CN",
            "en-US" => "en-US",
            "ru-RU" => "ru-RU",
            _ => "en-US"  // 不支持的语言默认使用英文
        };
    }

    /// <summary>
    /// 根据语言代码获取语言选项
    /// </summary>
    /// <param name="languageCode">语言代码</param>
    /// <returns>语言选项</returns>
    public static LanguageOption? GetLanguageOption(string languageCode)
    {
        return SupportedLanguages.Find(l => l.Code == languageCode);
    }
}

/// <summary>
/// 语言选项
/// </summary>
public class LanguageOption
{
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 语言代码
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public LanguageOption(string displayName, string code)
    {
        DisplayName = displayName;
        Code = code;
    }

    /// <summary>
    /// 重写 ToString
    /// </summary>
    public override string ToString() => DisplayName;
}
