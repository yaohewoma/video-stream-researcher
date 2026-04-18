# Инструкции по Сборке

[English](./BUILD.md) | [简体中文](./BUILD.zh-CN.md) | **Русский**

---

## Требования к Окружению

- Windows 10/11 64-bit
- .NET 10.0 SDK или выше
- Visual Studio 2022 или VS Code

## Структура Проекта

```
video-stream-researcher/
├── video-stream-researcher.csproj    # Основной файл проекта
├── Program.cs                        # Точка входа
├── Core/                             # Основные интерфейсы и модели
├── Logging/                          # Система логирования
├── Resources/                        # Файлы многоязычных ресурсов
├── Services/                         # Бизнес-сервисы
├── UI/                               # Avalonia UI
├── Libraries/                        # Внешние библиотеки
│   ├── Mp4Merger.Core/              # Библиотека объединения MP4
│   ├── VideoStreamFetcher/          # Библиотека загрузки видео
│   ├── VideoPreviewer/              # Библиотека предпросмотра видео
│   └── NativeVideoProcessor/        # Нативная обработка видео
└── ...
```

## Шаги Сборки

### 1. Клонирование Репозитория

```bash
git clone https://github.com/yaohewoma/video-stream-researcher.git
cd video-stream-researcher
```

### 2. Восстановление Зависимостей

```bash
dotnet restore
```

### 3. Сборка Версии Debug

```bash
dotnet build
```

### 4. Публикация Однофайловой Версии

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish
```

### 5. Результат Сборки

Скомпилированный однофайловый исполняемый файл будет находиться по пути:
```
publish/video-stream-researcher.exe
```

## Конфигурация

Проект настроен на:
- ✅ Однофайловую публикацию (все зависимости упакованы в один exe)
- ✅ Автономность (не требуется .NET runtime на целевой машине)
- ✅ Включенное сжатие (уменьшает размер файла)
- ✅ Поддержку Windows 10/11 64-bit

## Примечания

1. При первой сборке может потребоваться загрузка пакетов NuGet, убедитесь в наличии сетевого подключения
2. При возникновении ошибок конфликта типов очистите и пересоберите:
   ```bash
   dotnet clean
   dotnet build
   ```
3. Релизная версия автоматически включает все необходимые зависимости

## Технологический Стек

- .NET 10.0
- Avalonia UI 12.0
- C# 14.0
- Чистая реализация C#, без зависимости от FFmpeg
