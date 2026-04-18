using System.Globalization;

namespace Mp4Merger.Core.Localization;

/// <summary>
/// MP4合并库本地化服务
/// </summary>
public static class MergerLocalization
{
    private static string _currentLanguage = "zh-CN";

    /// <summary>
    /// 当前语言代码
    /// </summary>
    public static string CurrentLanguage => _currentLanguage;

    /// <summary>
    /// 设置语言
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        _currentLanguage = languageCode;
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    public static string GetString(string key, params object[] args)
    {
        var resources = GetResourcesForLanguage(_currentLanguage);
        if (resources.TryGetValue(key, out string? value))
        {
            return args.Length > 0 ? string.Format(value, args) : value;
        }
        return key;
    }

    private static Dictionary<string, string> GetResourcesForLanguage(string languageCode)
    {
        return languageCode switch
        {
            "en-US" => EnUsResources,
            "ru-RU" => RuRuResources,
            _ => ZhCnResources
        };
    }

    // 中文资源
    private static readonly Dictionary<string, string> ZhCnResources = new()
    {
        // MP4Merger
        ["Merge.StartTask"] = "📋 开始合并任务：{0:yyyy-MM-dd HH:mm:ss}",
        ["Merge.InputFiles"] = "📁 输入文件：视频={0}，音频={1}",
        ["Merge.OutputFile"] = "📁 输出文件：{0}",
        ["Merge.CreatingFile"] = "   创建合并后的MP4文件...",
        ["Merge.Completed"] = "✅ MP4音视频合并完成！耗时：{0:0.00} 秒",
        ["Merge.Failed"] = "❌ 合并失败：{0}",
        ["Merge.ExceptionType"] = "❌ 异常类型：{0}",
        ["Merge.ExceptionDetails"] = "❌ 异常详情：{0}",
        ["Merge.CleanupFile"] = "   清理不完整的输出文件",
        ["Merge.ConvertingToNonFragmented"] = "   转换为非分片MP4格式...",
        ["Merge.NonFragmentedComplete"] = "   非分片MP4转换完成",
        ["Merge.NonFragmentedFailed"] = "   非分片MP4转换失败：{0}",
        ["Merge.UsingOriginalFile"] = "   使用原始输出文件",
        ["Merge.OutputValidationFailed"] = "输出文件验证失败",

        // MediaProcessor
        ["Processor.ReadingVideo"] = "   读取视频文件数据...",
        ["Processor.ReadingAudio"] = "   读取音频文件数据...",
        ["Processor.AudioFormat"] = "   音频格式：{0}",
        ["Processor.ExtractingVideoInfo"] = "   提取视频信息...",
        ["Processor.ExtractingAudioInfo"] = "   提取音频信息...",
        ["Processor.DashDetected"] = "   检测到DASH分段视频：{0}个分段",
        ["Processor.RebuildingSampleTable"] = "   重建样本表...",
        ["Processor.VideoInfo"] = "   视频信息：{0}x{1}, 时长: {2:mm\\:ss}",
        ["Processor.AudioInfo"] = "   音频信息：{0}声道, {1}Hz, 时长: {2:mm\\:ss}",
        ["Processor.ExtractingVideoData"] = "   提取视频媒体数据...",
        ["Processor.ExtractingAudioData"] = "   提取音频媒体数据...",
        ["Processor.FMP4AudioDetected"] = "   检测到fMP4音频，解析出 {0} 个样本",

        // MP4Validator
        ["Validator.VideoNotFound"] = "视频文件不存在",
        ["Validator.AudioNotFound"] = "音频文件不存在",
        ["Validator.VideoSize"] = "📊 视频大小：{0:0.00} MB",
        ["Validator.AudioSize"] = "📊 音频大小：{0:0.00} MB",
        ["Validator.VideoTooSmall"] = "⚠️ 警告：视频文件可能过小，可能导致合并失败",
        ["Validator.AudioTooSmall"] = "⚠️ 警告：音频文件可能过小，可能导致合并失败",
        ["Validator.CreatingOutputDir"] = "📁 创建输出目录：{0}",
        ["Validator.OutputNotCreated"] = "❌ 错误：输出文件未创建",
        ["Validator.OutputSize"] = "📊 输出文件大小：{0:0.00} MB",
        ["Validator.OutputTooSmall"] = "⚠️ 警告：输出文件可能过小，可能合并不完整",

        // MP4Writer
        ["Writer.WritingFtyp"] = "   写入ftyp盒子...",
        ["Writer.ProcessingDash"] = "   处理DASH格式视频...",
        ["Writer.EstimatedMoovSize"] = "   估算的moov大小: {0}, 实际的moov大小: {1}",
        ["Writer.ActualMdatStart"] = "   实际的mdat起始位置: {0}",
        ["Writer.WritingMoov"] = "   写入moov盒子...",
        ["Writer.MdatStartPosition"] = "   mdat盒子起始位置: {0}",
        ["Writer.WritingMdat"] = "   写入mdat盒子...",
        ["Writer.FileCreated"] = "   文件创建完成",

        // MediaExtractor
        ["Extractor.FMP4DetectedVideo"] = "   检测到fMP4格式，使用fMP4解析器",
        ["Extractor.FMP4DetectedAudio"] = "   检测到fMP4格式，使用fMP4解析器提取音频",
        ["Extractor.MP3Size"] = "   MP3音频数据大小：{0:0.00} MB",
        ["Extractor.MP3ProcessingError"] = "   处理MP3音频数据时出错：{0}",
        ["Extractor.MdatNotFound"] = "   未找到{0}mdat盒子，尝试提取{1}数据",
        ["Extractor.FoundStartCode"] = "   找到{0}起始码，提取剩余数据：{1:0.00} MB",
        ["Extractor.NoDataFound"] = "   未找到{0}数据，返回所有数据",
        ["Extractor.ExtractionError"] = "   提取{0}媒体数据时出错：{1}",
        ["Extractor.ExtractingMdat"] = "   提取{0}mdat盒子 #{1}，大小：{2} bytes ({3:0.00} KB)",
        ["Extractor.ExtractingRemaining"] = "   ... 提取剩余{0}mdat盒子中...",
        ["Extractor.SkippingInvalid"] = "   跳过{0}mdat盒子 #{1}，数据无效",
        ["Extractor.SkippingEmpty"] = "   跳过{0}mdat盒子 #{1}，数据为空或全零",
        ["Extractor.TotalExtracted"] = "   共提取{0}个{1}mdat盒子，总大小：{2:0.00} KB",

        // FMP4Parser
        ["Parser.ParsingFragment"] = "   解析fMP4片段 #{0}，样本数: {1}",
        ["Parser.ParsingRemaining"] = "   ... 继续解析剩余fMP4片段 ...",
        ["Parser.TotalFragments"] = "   共解析{0}个fMP4片段，提取{1}个样本",

        // MP4TrackReconstructor
        ["Reconstructor.ExtractingH264Config"] = "   从视频样本中提取H.264配置信息...",
        ["Reconstructor.ConfigFoundInSample"] = "   ✅ 在样本 #{0} 中找到配置信息",
        ["Reconstructor.SPSPPSFound"] = "   找到 {0} 个SPS, {1} 个PPS",
        ["Reconstructor.CreatingAvcC"] = "   创建avcC盒子，大小: {0}",
        ["Reconstructor.UpdatingStsd"] = "   更新stsd，原大小: {0}, 新大小: {1}",
        ["Reconstructor.ConfigNotFound"] = "   ⚠️ 警告: 未能从前10个样本中提取H.264配置信息",
        ["Reconstructor.UsingExistingAvcC"] = "   ℹ️ 使用原始trak盒子中的avcC (大小: {0})",
        ["Reconstructor.ConfigError"] = "   ❌ 错误: 无法获取有效的H.264配置，生成的文件可能无法播放",
        ["Reconstructor.RebuildingWithFMP4Samples"] = "   使用fMP4样本信息重建样本表{0}，样本数: {1}",
        ["Reconstructor.SecondPass"] = "（第二次）",
        ["Reconstructor.ParsingFrames"] = "   解析视频帧...",
        ["Reconstructor.FramesFound"] = "   找到 {0} 个视频帧",
        ["Reconstructor.UpdatingVideoDuration"] = "   更新视频duration: {0}",
        ["Reconstructor.VideoTrakSizeBefore"] = "   重建前视频trak大小: {0}",
        ["Reconstructor.VideoTrakSizeAfter"] = "   重建后视频trak大小: {0}",
        ["Reconstructor.AudioTrakSizeBefore"] = "   重建前音频trak大小: {0}",
        ["Reconstructor.AudioTrakSizeAfter"] = "   重建后音频trak大小: {0}",
        ["Reconstructor.RebuildingAudioWithFMP4"] = "   使用fMP4样本信息重建音频样本表{0}，样本数: {1}",
        ["Reconstructor.UpdatingAudioDuration"] = "   更新音频duration: {0}",

        // Mp4MergeService
        ["Service.StartingTask"] = "\n📋 开始任务 {0}/{1}: {2}",
        ["Service.TaskCompleted"] = "📋 任务 {0}/{1} 完成: {2}",
        ["Service.Success"] = "成功",
        ["Service.Failed"] = "失败",

        // Common
        ["Common.AAC"] = "AAC",
        ["Common.MP3"] = "MP3",
        ["Common.Other"] = "其他",
        ["Common.Video"] = "视频",
        ["Common.Audio"] = "音频",
        ["Common.H264"] = "H.264",
    };

    // English resources
    private static readonly Dictionary<string, string> EnUsResources = new()
    {
        // MP4Merger
        ["Merge.StartTask"] = "📋 Starting merge task: {0:yyyy-MM-dd HH:mm:ss}",
        ["Merge.InputFiles"] = "📁 Input files: Video={0}, Audio={1}",
        ["Merge.OutputFile"] = "📁 Output file: {0}",
        ["Merge.CreatingFile"] = "   Creating merged MP4 file...",
        ["Merge.Completed"] = "✅ MP4 merge completed! Time elapsed: {0:0.00} seconds",
        ["Merge.Failed"] = "❌ Merge failed: {0}",
        ["Merge.ExceptionType"] = "❌ Exception type: {0}",
        ["Merge.ExceptionDetails"] = "❌ Exception details: {0}",
        ["Merge.CleanupFile"] = "   Cleaning up incomplete output file",
        ["Merge.ConvertingToNonFragmented"] = "   Converting to non-fragmented MP4 format...",
        ["Merge.NonFragmentedComplete"] = "   Non-fragmented MP4 conversion completed",
        ["Merge.NonFragmentedFailed"] = "   Non-fragmented MP4 conversion failed: {0}",
        ["Merge.UsingOriginalFile"] = "   Using original output file",
        ["Merge.OutputValidationFailed"] = "Output file validation failed",

        // MediaProcessor
        ["Processor.ReadingVideo"] = "   Reading video file data...",
        ["Processor.ReadingAudio"] = "   Reading audio file data...",
        ["Processor.AudioFormat"] = "   Audio format: {0}",
        ["Processor.ExtractingVideoInfo"] = "   Extracting video info...",
        ["Processor.ExtractingAudioInfo"] = "   Extracting audio info...",
        ["Processor.DashDetected"] = "   DASH segmented video detected: {0} segments",
        ["Processor.RebuildingSampleTable"] = "   Rebuilding sample table...",
        ["Processor.VideoInfo"] = "   Video info: {0}x{1}, Duration: {2:mm\\:ss}",
        ["Processor.AudioInfo"] = "   Audio info: {0} channels, {1}Hz, Duration: {2:mm\\:ss}",
        ["Processor.ExtractingVideoData"] = "   Extracting video media data...",
        ["Processor.ExtractingAudioData"] = "   Extracting audio media data...",
        ["Processor.FMP4AudioDetected"] = "   fMP4 audio detected, parsed {0} samples",

        // MP4Validator
        ["Validator.VideoNotFound"] = "Video file does not exist",
        ["Validator.AudioNotFound"] = "Audio file does not exist",
        ["Validator.VideoSize"] = "📊 Video size: {0:0.00} MB",
        ["Validator.AudioSize"] = "📊 Audio size: {0:0.00} MB",
        ["Validator.VideoTooSmall"] = "⚠️ Warning: Video file may be too small, merge may fail",
        ["Validator.AudioTooSmall"] = "⚠️ Warning: Audio file may be too small, merge may fail",
        ["Validator.CreatingOutputDir"] = "📁 Creating output directory: {0}",
        ["Validator.OutputNotCreated"] = "❌ Error: Output file was not created",
        ["Validator.OutputSize"] = "📊 Output file size: {0:0.00} MB",
        ["Validator.OutputTooSmall"] = "⚠️ Warning: Output file may be too small, merge may be incomplete",

        // MP4Writer
        ["Writer.WritingFtyp"] = "   Writing ftyp box...",
        ["Writer.ProcessingDash"] = "   Processing DASH format video...",
        ["Writer.EstimatedMoovSize"] = "   Estimated moov size: {0}, Actual moov size: {1}",
        ["Writer.ActualMdatStart"] = "   Actual mdat start position: {0}",
        ["Writer.WritingMoov"] = "   Writing moov box...",
        ["Writer.MdatStartPosition"] = "   mdat box start position: {0}",
        ["Writer.WritingMdat"] = "   Writing mdat box...",
        ["Writer.FileCreated"] = "   File creation completed",

        // MediaExtractor
        ["Extractor.FMP4DetectedVideo"] = "   fMP4 format detected, using fMP4 parser",
        ["Extractor.FMP4DetectedAudio"] = "   fMP4 format detected, using fMP4 parser for audio",
        ["Extractor.MP3Size"] = "   MP3 audio data size: {0:0.00} MB",
        ["Extractor.MP3ProcessingError"] = "   Error processing MP3 audio data: {0}",
        ["Extractor.MdatNotFound"] = "   {0} mdat box not found, trying to extract {1} data",
        ["Extractor.FoundStartCode"] = "   {0} start code found, extracting remaining data: {1:0.00} MB",
        ["Extractor.NoDataFound"] = "   No {0} data found, returning all data",
        ["Extractor.ExtractionError"] = "   Error extracting {0} media data: {1}",
        ["Extractor.ExtractingMdat"] = "   Extracting {0} mdat box #{1}, size: {2} bytes ({3:0.00} KB)",
        ["Extractor.ExtractingRemaining"] = "   ... Extracting remaining {0} mdat boxes...",
        ["Extractor.SkippingInvalid"] = "   Skipping {0} mdat box #{1}, invalid data",
        ["Extractor.SkippingEmpty"] = "   Skipping {0} mdat box #{1}, empty or all-zero data",
        ["Extractor.TotalExtracted"] = "   Extracted {0} {1} mdat boxes total, size: {2:0.00} KB",

        // FMP4Parser
        ["Parser.ParsingFragment"] = "   Parsing fMP4 fragment #{0}, samples: {1}",
        ["Parser.ParsingRemaining"] = "   ... Continuing to parse remaining fMP4 fragments ...",
        ["Parser.TotalFragments"] = "   Parsed {0} fMP4 fragments total, extracted {1} samples",

        // MP4TrackReconstructor
        ["Reconstructor.ExtractingH264Config"] = "   Extracting H.264 configuration from video samples...",
        ["Reconstructor.ConfigFoundInSample"] = "   ✅ Configuration found in sample #{0}",
        ["Reconstructor.SPSPPSFound"] = "   Found {0} SPS, {1} PPS",
        ["Reconstructor.CreatingAvcC"] = "   Creating avcC box, size: {0}",
        ["Reconstructor.UpdatingStsd"] = "   Updating stsd, original size: {0}, new size: {1}",
        ["Reconstructor.ConfigNotFound"] = "   ⚠️ Warning: Could not extract H.264 configuration from first 10 samples",
        ["Reconstructor.UsingExistingAvcC"] = "   ℹ️ Using avcC from original trak box (size: {0})",
        ["Reconstructor.ConfigError"] = "   ❌ Error: Cannot obtain valid H.264 configuration, generated file may not play",
        ["Reconstructor.RebuildingWithFMP4Samples"] = "   Rebuilding sample table with fMP4 sample info{0}, samples: {1}",
        ["Reconstructor.SecondPass"] = " (second pass)",
        ["Reconstructor.ParsingFrames"] = "   Parsing video frames...",
        ["Reconstructor.FramesFound"] = "   Found {0} video frames",
        ["Reconstructor.UpdatingVideoDuration"] = "   Updating video duration: {0}",
        ["Reconstructor.VideoTrakSizeBefore"] = "   Video trak size before rebuild: {0}",
        ["Reconstructor.VideoTrakSizeAfter"] = "   Video trak size after rebuild: {0}",
        ["Reconstructor.AudioTrakSizeBefore"] = "   Audio trak size before rebuild: {0}",
        ["Reconstructor.AudioTrakSizeAfter"] = "   Audio trak size after rebuild: {0}",
        ["Reconstructor.RebuildingAudioWithFMP4"] = "   Rebuilding audio sample table with fMP4 sample info{0}, samples: {1}",
        ["Reconstructor.UpdatingAudioDuration"] = "   Updating audio duration: {0}",

        // Mp4MergeService
        ["Service.StartingTask"] = "\n📋 Starting task {0}/{1}: {2}",
        ["Service.TaskCompleted"] = "📋 Task {0}/{1} completed: {2}",
        ["Service.Success"] = "Success",
        ["Service.Failed"] = "Failed",

        // Common
        ["Common.AAC"] = "AAC",
        ["Common.MP3"] = "MP3",
        ["Common.Other"] = "Other",
        ["Common.Video"] = "Video",
        ["Common.Audio"] = "Audio",
        ["Common.H264"] = "H.264",
    };

    // Russian resources
    private static readonly Dictionary<string, string> RuRuResources = new()
    {
        // MP4Merger
        ["Merge.StartTask"] = "📋 Начало задачи слияния: {0:yyyy-MM-dd HH:mm:ss}",
        ["Merge.InputFiles"] = "📁 Входные файлы: Видео={0}, Аудио={1}",
        ["Merge.OutputFile"] = "📁 Выходной файл: {0}",
        ["Merge.CreatingFile"] = "   Создание объединенного MP4 файла...",
        ["Merge.Completed"] = "✅ Слияние MP4 завершено! Затраченное время: {0:0.00} секунд",
        ["Merge.Failed"] = "❌ Ошибка слияния: {0}",
        ["Merge.ExceptionType"] = "❌ Тип исключения: {0}",
        ["Merge.ExceptionDetails"] = "❌ Подробности исключения: {0}",
        ["Merge.CleanupFile"] = "   Очистка неполного выходного файла",
        ["Merge.ConvertingToNonFragmented"] = "   Конвертация в нефрагментированный MP4 формат...",
        ["Merge.NonFragmentedComplete"] = "   Конвертация в нефрагментированный MP4 завершена",
        ["Merge.NonFragmentedFailed"] = "   Ошибка конвертации в нефрагментированный MP4: {0}",
        ["Merge.UsingOriginalFile"] = "   Использование исходного выходного файла",
        ["Merge.OutputValidationFailed"] = "Ошибка проверки выходного файла",

        // MediaProcessor
        ["Processor.ReadingVideo"] = "   Чтение данных видео файла...",
        ["Processor.ReadingAudio"] = "   Чтение данных аудио файла...",
        ["Processor.AudioFormat"] = "   Формат аудио: {0}",
        ["Processor.ExtractingVideoInfo"] = "   Извлечение информации о видео...",
        ["Processor.ExtractingAudioInfo"] = "   Извлечение информации об аудио...",
        ["Processor.DashDetected"] = "   Обнаружено DASH сегментированное видео: {0} сегментов",
        ["Processor.RebuildingSampleTable"] = "   Перестроение таблицы сэмплов...",
        ["Processor.VideoInfo"] = "   Информация о видео: {0}x{1}, Длительность: {2:mm\\:ss}",
        ["Processor.AudioInfo"] = "   Информация об аудио: {0} каналов, {1}Гц, Длительность: {2:mm\\:ss}",
        ["Processor.ExtractingVideoData"] = "   Извлечение видео данных...",
        ["Processor.ExtractingAudioData"] = "   Извлечение аудио данных...",
        ["Processor.FMP4AudioDetected"] = "   Обнаружен fMP4 аудио, разобрано {0} сэмплов",

        // MP4Validator
        ["Validator.VideoNotFound"] = "Видео файл не существует",
        ["Validator.AudioNotFound"] = "Аудио файл не существует",
        ["Validator.VideoSize"] = "📊 Размер видео: {0:0.00} МБ",
        ["Validator.AudioSize"] = "📊 Размер аудио: {0:0.00} МБ",
        ["Validator.VideoTooSmall"] = "⚠️ Предупреждение: Видео файл может быть слишком мал, слияние может не удаться",
        ["Validator.AudioTooSmall"] = "⚠️ Предупреждение: Аудио файл может быть слишком мал, слияние может не удаться",
        ["Validator.CreatingOutputDir"] = "📁 Создание выходной директории: {0}",
        ["Validator.OutputNotCreated"] = "❌ Ошибка: Выходной файл не был создан",
        ["Validator.OutputSize"] = "📊 Размер выходного файла: {0:0.00} МБ",
        ["Validator.OutputTooSmall"] = "⚠️ Предупреждение: Выходной файл может быть слишком мал, слияние может быть неполным",

        // MP4Writer
        ["Writer.WritingFtyp"] = "   Запись ftyp блока...",
        ["Writer.ProcessingDash"] = "   Обработка DASH формата видео...",
        ["Writer.EstimatedMoovSize"] = "   Оценочный размер moov: {0}, Фактический размер moov: {1}",
        ["Writer.ActualMdatStart"] = "   Фактическая начальная позиция mdat: {0}",
        ["Writer.WritingMoov"] = "   Запись moov блока...",
        ["Writer.MdatStartPosition"] = "   Начальная позиция mdat блока: {0}",
        ["Writer.WritingMdat"] = "   Запись mdat блока...",
        ["Writer.FileCreated"] = "   Создание файла завершено",

        // MediaExtractor
        ["Extractor.FMP4DetectedVideo"] = "   Обнаружен fMP4 формат, используется fMP4 парсер",
        ["Extractor.FMP4DetectedAudio"] = "   Обнаружен fMP4 формат, используется fMP4 парсер для аудио",
        ["Extractor.MP3Size"] = "   Размер MP3 аудио данных: {0:0.00} МБ",
        ["Extractor.MP3ProcessingError"] = "   Ошибка обработки MP3 аудио данных: {0}",
        ["Extractor.MdatNotFound"] = "   {0} mdat блок не найден, попытка извлечь {1} данные",
        ["Extractor.FoundStartCode"] = "   Найден {0} стартовый код, извлечение оставшихся данных: {1:0.00} МБ",
        ["Extractor.NoDataFound"] = "   {0} данные не найдены, возврат всех данных",
        ["Extractor.ExtractionError"] = "   Ошибка извлечения {0} медиа данных: {1}",
        ["Extractor.ExtractingMdat"] = "   Извлечение {0} mdat блока #{1}, размер: {2} байт ({3:0.00} КБ)",
        ["Extractor.ExtractingRemaining"] = "   ... Извлечение оставшихся {0} mdat блоков...",
        ["Extractor.SkippingInvalid"] = "   Пропуск {0} mdat блока #{1}, неверные данные",
        ["Extractor.SkippingEmpty"] = "   Пропуск {0} mdat блока #{1}, пустые или нулевые данные",
        ["Extractor.TotalExtracted"] = "   Всего извлечено {0} {1} mdat блоков, общий размер: {2:0.00} КБ",

        // FMP4Parser
        ["Parser.ParsingFragment"] = "   Разбор fMP4 фрагмента #{0}, сэмплов: {1}",
        ["Parser.ParsingRemaining"] = "   ... Продолжение разбора оставшихся fMP4 фрагментов ...",
        ["Parser.TotalFragments"] = "   Всего разобрано {0} fMP4 фрагментов, извлечено {1} сэмплов",

        // MP4TrackReconstructor
        ["Reconstructor.ExtractingH264Config"] = "   Извлечение H.264 конфигурации из видео сэмплов...",
        ["Reconstructor.ConfigFoundInSample"] = "   ✅ Конфигурация найдена в сэмпле #{0}",
        ["Reconstructor.SPSPPSFound"] = "   Найдено {0} SPS, {1} PPS",
        ["Reconstructor.CreatingAvcC"] = "   Создание avcC блока, размер: {0}",
        ["Reconstructor.UpdatingStsd"] = "   Обновление stsd, исходный размер: {0}, новый размер: {1}",
        ["Reconstructor.ConfigNotFound"] = "   ⚠️ Предупреждение: Не удалось извлечь H.264 конфигурацию из первых 10 сэмплов",
        ["Reconstructor.UsingExistingAvcC"] = "   ℹ️ Использование avcC из исходного trak блока (размер: {0})",
        ["Reconstructor.ConfigError"] = "   ❌ Ошибка: Невозможно получить действительную H.264 конфигурацию, созданный файл может не воспроизводиться",
        ["Reconstructor.RebuildingWithFMP4Samples"] = "   Перестроение таблицы сэмплов с информацией fMP4{0}, сэмплов: {1}",
        ["Reconstructor.SecondPass"] = " (второй проход)",
        ["Reconstructor.ParsingFrames"] = "   Разбор видео кадров...",
        ["Reconstructor.FramesFound"] = "   Найдено {0} видео кадров",
        ["Reconstructor.UpdatingVideoDuration"] = "   Обновление длительности видео: {0}",
        ["Reconstructor.VideoTrakSizeBefore"] = "   Размер видео trak до перестроения: {0}",
        ["Reconstructor.VideoTrakSizeAfter"] = "   Размер видео trak после перестроения: {0}",
        ["Reconstructor.AudioTrakSizeBefore"] = "   Размер аудио trak до перестроения: {0}",
        ["Reconstructor.AudioTrakSizeAfter"] = "   Размер аудио trak после перестроения: {0}",
        ["Reconstructor.RebuildingAudioWithFMP4"] = "   Перестроение аудио таблицы сэмплов с информацией fMP4{0}, сэмплов: {1}",
        ["Reconstructor.UpdatingAudioDuration"] = "   Обновление длительности аудио: {0}",

        // Mp4MergeService
        ["Service.StartingTask"] = "\n📋 Начало задачи {0}/{1}: {2}",
        ["Service.TaskCompleted"] = "📋 Задача {0}/{1} завершена: {2}",
        ["Service.Success"] = "Успешно",
        ["Service.Failed"] = "Ошибка",

        // Common
        ["Common.AAC"] = "AAC",
        ["Common.MP3"] = "MP3",
        ["Common.Other"] = "Другой",
        ["Common.Video"] = "Видео",
        ["Common.Audio"] = "Аудио",
        ["Common.H264"] = "H.264",
    };
}
