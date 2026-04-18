# VideoStreamFetcher

## Введение

VideoStreamFetcher — это мощная библиотека для парсинга и загрузки видеопотоков, поддерживающая множество видеоплатформ. Разработана для .NET 10.0 с асинхронной загрузкой, поддержкой HLS и проверкой безопасности.

## Возможности

- 🔍 **Поддержка нескольких платформ** — Bilibili, Kuaishou, Miyoushe и др.
- 📥 **Загрузка видео** — поддержка форматов MP4, FLV, HLS/M3U8
- 🎵 **Извлечение аудио** — отдельная загрузка аудиопотоков
- 🔄 **Загрузка HLS** — поддержка загрузки и объединения сегментированного видео
- 🛡️ **Проверка безопасности** — защита от path traversal и очистка имен файлов
- ⚡ **Асинхронная обработка** — полностью асинхронный I/O с callback прогресса
- 🌍 **Многоязычность** — поддержка китайского/английского/русского

## Установка

```bash
dotnet add package VideoStreamFetcher
```

## Быстрый старт

```csharp
using VideoStreamFetcher;
using VideoStreamFetcher.Downloads;
using VideoStreamFetcher.Parsers;

// Создание клиента
var client = new VideoStreamClient();

// Парсинг видео
var videoInfo = await client.ParseAsync(
    "https://www.bilibili.com/video/BV...",
    status => Console.WriteLine(status)
);

// Загрузка видео
using var downloader = new VideoDownloader();
var result = await downloader.DownloadAsync(
    videoInfo,
    @"C:\Downloads",
    progress => Console.WriteLine($"Прогресс: {progress:F2}%"),
    status => Console.WriteLine(status),
    speed => Console.WriteLine($"Скорость: {speed} bytes/s")
);
```

## Структура проекта

```
VideoStreamFetcher/
├── Auth/              # Управление аутентификацией
├── Downloads/         # Функционал загрузки
│   ├── VideoDownloader.cs
│   ├── StreamDownloader.cs
│   ├── HlsDownloader.cs
│   ├── RemuxService.cs
│   └── PathSecurityValidator.cs
├── Localization/      # Поддержка многоязычности
├── Parsers/           # Парсеры видео
│   ├── PlatformParsers/
│   │   ├── IPlatformParser.cs
│   │   ├── BilibiliParser.cs
│   │   └── VideoParserFactory.cs
│   └── VideoParser.cs
└── Remux/             # Функционал remux
```

## Поддерживаемые платформы

| Платформа | Статус | Примечания |
|-----------|--------|------------|
| Bilibili | ✅ | Поддержка форматов DASH и DURL |
| Kuaishou | ✅ | Поддержка live и VOD |
| Miyoushe | ✅ | Поддержка Genshin, Honkai и др. |
| Generic | ✅ | Поддержка стандартных видеоссылок |

## Функции безопасности

```csharp
// Проверка безопасности пути
PathSecurityValidator.ValidatePathOrThrow(outputPath, baseDirectory);

// Очистка имени файла
var safeName = PathSecurityValidator.SanitizeFileName(fileName);
```

## Зависимости

- .NET 10.0
- Mp4Merger.Core
- Newtonsoft.Json 13.0.4
- QRCoder 1.6.0
- Selenium.WebDriver 4.20.0

## Лицензия

MIT License — только для технических исследований и образовательных целей

---

**Разработчик**: yaohewoma  
**Версия**: v2.0  
**Последнее обновление**: 2026-04-18
