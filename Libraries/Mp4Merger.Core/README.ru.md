# Mp4Merger.Core

## Введение

Mp4Merger.Core — это высокопроизводительная библиотека для объединения аудио/видео MP4, разработанная для .NET 10.0. Поддерживает DASH, fMP4 и другие форматы с асинхронной обработкой и оптимизацией памяти.

## Возможности

- 🎬 **Объединение аудио/видео MP4** — поддержка объединения отдельных аудио и видео потоков
- 📦 **Поддержка формата DASH** — автоматическая обработка сегментированного видео DASH
- 🔄 **Парсинг fMP4** — поддержка формата fragmented MP4
- ⚡ **Асинхронная обработка** — полностью асинхронные I/O операции
- 🛡️ **Безопасность типов** — полная поддержка дженериков
- 🌍 **Многоязычность** — поддержка китайского/английского/русского

## Установка

```bash
dotnet add package Mp4Merger.Core
```

## Быстрый старт

```csharp
using Mp4Merger.Core.Core;
using Mp4Merger.Core.Services;

// Использование MP4Merger
using var merger = new MP4Merger();
var result = await merger.MergeVideoAudioAsync(
    "video.mp4", 
    "audio.mp3", 
    "output.mp4",
    status => Console.WriteLine(status)
);

// Использование Mp4MergeService
var service = new Mp4MergeService();
var result = await service.MergeAsync(
    "video.mp4",
    "audio.mp3", 
    "output.mp4",
    status => Console.WriteLine(status),
    convertToNonFragmented: true
);
```

## Структура проекта

```
Mp4Merger.Core/
├── Boxes/          # Определения MP4 box
├── Builders/       # Построители треков
├── Core/           # Основные классы обработки
├── Extensions/     # Методы расширения
├── Localization/   # Поддержка многоязычности
├── Media/          # Извлечение медиа
├── Models/         # Модели данных
└── Services/       # Публичные сервисы
```

## Зависимости

- .NET 10.0
- System.Memory

## Лицензия

MIT License — только для технических исследований и образовательных целей

---

**Разработчик**: yaohewoma  
**Версия**: v2.0  
**Последнее обновление**: 2026-04-18
