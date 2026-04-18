# Инструкции по Сборке

[English](./BUILD.md) | [简体中文](./BUILD.zh-CN.md) | **Русский**

---

## Требования к Окружению

- Windows 10/11 64-bit
- .NET 10.0 SDK или выше
- Visual Studio 2022 (рекомендуется) или VS Code

## Быстрый Старт

### Способ 1: Visual Studio 2022 (Рекомендуется)

1. Откройте файл проекта `video-stream-researcher.csproj`
2. Выберите конфигурацию `Release`
3. Сборка → Собрать Решение
4. Или используйте функцию публикации для создания однофайлового исполняемого файла

### Способ 2: Командная Строка

```bash
# Клонирование репозитория
git clone https://github.com/yaohewoma/video-stream-researcher.git
cd video-stream-researcher

# Восстановление зависимостей
dotnet restore

# Сборка проекта
dotnet build -c Release

# Публикация однофайловой версии
dotnet publish video-stream-researcher.csproj -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish
```

## Структура Проекта

```
video-stream-researcher/
├── video-stream-researcher.csproj    # Основной проект
├── Core/                             # Основные интерфейсы и модели
├── Logging/                          # Система логирования
├── Resources/                        # Многоязычные ресурсы
├── Services/                         # Бизнес-сервисы
├── UI/                               # Avalonia UI
├── Libraries/                        # Внешние библиотеки
│   ├── Mp4Merger.Core/
│   ├── VideoStreamFetcher/
│   ├── VideoPreviewer/
│   └── NativeVideoProcessor/
└── ...
```

## Конфигурация

Проект настроен на:
- ✅ Однофайловую публикацию
- ✅ Автономное развертывание
- ✅ Включенное сжатие
- ✅ Поддержку Windows 10/11 64-bit

## Результат Сборки

Скомпилированный исполняемый файл будет находиться по пути:
```
publish/video-stream-researcher.exe
```

## Технологический Стек

- .NET 10.0
- Avalonia UI 12.0
- C# 14.0
- Чистая реализация C#, без зависимости от FFmpeg
