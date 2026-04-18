# NativeVideoProcessor

## Введение

NativeVideoProcessor — это библиотека нативной обработки видео, построенная на Vortice.MediaFoundation и .NET 10.0. Обеспечивает аппаратно-ускоренное транскодирование и обработку видео.

## Возможности

- 🎬 **Транскодирование видео** — поддержка транскодирования в несколько форматов
- 🚀 **Аппаратное ускорение** — поддержка аппаратного ускорения DXVA
- 🎨 **Конвертация форматов** — конвертация между различными видеоформатами
- ⚡ **Высокая производительность** — нативная оптимизация производительности
- 🔧 **Расширяемость** — архитектура на основе плагинов

## Установка

```bash
dotnet add package NativeVideoProcessor
```

## Быстрый старт

```csharp
using NativeVideoProcessor;
using NativeVideoProcessor.Engine;

// Создание движка транскодирования
using var engine = new MfMediaEngine();

// Транскодирование видео
await engine.TranscodeAsync(
    "input.mp4",
    "output.mp4",
    new TranscodeOptions
    {
        TargetWidth = 1920,
        TargetHeight = 1080,
        VideoBitrate = 5000000
    },
    progress => Console.WriteLine($"Прогресс: {progress:P}")
);
```

## Структура проекта

```
NativeVideoProcessor/
├── Engine/            # Движки транскодирования
│   └── MfMediaEngine.cs
├── Interfaces/        # Определения интерфейсов
│   └── IMediaEngine.cs
├── Models/            # Модели данных
│   ├── MediaInfo.cs
│   └── TranscodeOptions.cs
└── NativeVideoProcessor.cs
```

## Зависимости

- .NET 10.0
- Vortice.MediaFoundation
- Windows Media Foundation

## Требования к платформе

- Windows 10/11 64-bit
- Поддержка Media Foundation
- Опционально: GPU с поддержкой DXVA

## Лицензия

MIT License — только для технических исследований и образовательных целей

---

**Разработчик**: yaohewoma  
**Версия**: v2.0  
**Последнее обновление**: 2026-04-18
