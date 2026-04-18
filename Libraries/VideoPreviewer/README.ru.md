# VideoPreviewer

## Введение

VideoPreviewer — это библиотека предпросмотра видео на основе Media Foundation, разработанная для .NET 10.0-windows. Обеспечивает высокопроизводительное декодирование видео, извлечение кадров и конвертацию форматов.

## Возможности

- 🎬 **Декодирование видео** — на основе Media Foundation API
- 🖼️ **Извлечение кадров** — эффективное извлечение кадров видео
- 🎨 **Конвертация форматов** — аппаратное ускорение NV12 в RGB32
- 🔊 **Воспроизведение аудио** — интегрированный аудиовыход NAudio
- ⚡ **Производительность** — управление памятью ArrayPool, параллельная обработка
- 🖥️ **Аппаратное ускорение** — поддержка аппаратного декодирования DXVA

## Установка

```bash
dotnet add package VideoPreviewer
```

## Быстрый старт

```csharp
using VideoPreviewer.MediaFoundation;

// Создание видео-ридера
using var reader = new MfVideoReader();

// Открытие видео
reader.Open("video.mp4");

// Чтение кадра
var frame = reader.ReadFrame();
if (frame != null)
{
    // Обработка данных кадра
    byte[] data = frame.Data;
    int width = frame.Width;
    int height = frame.Height;
}

// Позиционирование на определенное время
reader.Seek(TimeSpan.FromSeconds(10));
```

## Структура проекта

```
VideoPreviewer/
├── MediaFoundation/   # Обертки Media Foundation
│   ├── MfVideoReader.cs
│   └── MfAudioReader.cs
├── Rendering/         # Связанное с рендерингом
├── Utils/             # Утилиты
└── VideoPreviewer.cs  # Основной класс
```

## Технические детали

### Конвертация цветового пространства
```csharp
// Конвертация NV12 в RGB32
// Малое разрешение: однопоточная обработка
// Большое разрешение: оптимизация параллельной обработки
```

### Управление памятью
```csharp
// Использование ArrayPool для снижения нагрузки на GC
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Использование буфера
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

## Зависимости

- .NET 10.0-windows
- NAudio 2.2.1
- Windows Media Foundation

## Требования к платформе

- Windows 10/11 64-bit
- Поддержка Media Foundation

## Лицензия

MIT License — только для технических исследований и образовательных целей

---

**Разработчик**: yaohewoma  
**Версия**: v2.0  
**Последнее обновление**: 2026-04-18
