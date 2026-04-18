using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using video_stream_researcher.Interfaces;

namespace video_stream_researcher.Services;

public sealed class DownloadIndexService : IDownloadIndexService
{
    private static readonly object Gate = new object();
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public DownloadIndexHit? TryFindExisting(string url, string savePath, string? variant = null)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(savePath))
        {
            return null;
        }

        if (!TryGetKey(url, variant, out var key, out var platform, out var contentId))
        {
            return null;
        }

        try
        {
            lock (Gate)
            {
                var index = LoadIndex(savePath, allowRepair: true);
                if (index.Entries.TryGetValue(key, out var entry))
                {
                    var filePath = Path.Combine(savePath, entry.FolderName, entry.FileName);
                    if (!File.Exists(filePath))
                    {
                        AppendError(savePath, $"索引命中但文件不存在: key={key} path={filePath}");
                        index.Entries.Remove(key);
                        SaveIndex(savePath, index);
                        return null;
                    }

                    var info = new FileInfo(filePath);
                    var size = info.Length;
                    if (entry.Size > 0 && size != entry.Size)
                    {
                        AppendError(savePath, $"索引命中但大小不匹配: key={key} index={entry.Size} actual={size}");
                        return null;
                    }

                    if (!string.IsNullOrWhiteSpace(entry.Sha256))
                    {
                        if (entry.LastWriteTimeUtcTicks != info.LastWriteTimeUtc.Ticks)
                        {
                            var sha = ComputeSha256Hex(filePath);
                            if (!string.Equals(sha, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                            {
                                AppendError(savePath, $"索引命中但哈希不匹配: key={key}");
                                return null;
                            }

                            entry.LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks;
                            entry.Size = size;
                            index.Entries[key] = entry;
                            SaveIndex(savePath, index);
                            WriteMeta(Path.Combine(savePath, entry.FolderName), entry);
                        }
                    }

                    return new DownloadIndexHit
                    {
                        OutputDirectory = Path.Combine(savePath, entry.FolderName),
                        OutputPath = filePath,
                        Size = size
                    };
                }

                var plan = CreatePlan(platform, contentId, variant);
                if (plan.OutputFolderName == null || plan.OutputFileBaseName == null)
                {
                    return null;
                }

                var folder = Path.Combine(savePath, plan.OutputFolderName);
                var ext = GetExpectedExtension(variant);
                var file = Path.Combine(folder, $"{plan.OutputFileBaseName}.{ext}");
                if (!File.Exists(file))
                {
                    return null;
                }

                var discoveredInfo = new FileInfo(file);
                var discoveredSize = discoveredInfo.Length;
                var discovered = new IndexEntry
                {
                    Url = url,
                    Platform = platform,
                    ContentId = contentId,
                    Variant = variant,
                    FolderName = plan.OutputFolderName,
                    FileName = $"{plan.OutputFileBaseName}.{ext}",
                    Size = discoveredSize,
                    Sha256 = ComputeSha256Hex(file),
                    LastWriteTimeUtcTicks = discoveredInfo.LastWriteTimeUtc.Ticks,
                    Title = null,
                    UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };
                index.Entries[key] = discovered;
                SaveIndex(savePath, index);
                WriteMeta(folder, discovered);

                return new DownloadIndexHit
                {
                    OutputDirectory = folder,
                    OutputPath = file,
                    Size = discoveredSize
                };
            }
        }
        catch (Exception ex)
        {
            AppendError(savePath, $"TryFindExisting 异常: {ex.Message}");
            return null;
        }
    }

    public DownloadIndexPlan PrepareForDownload(string url, string savePath, string? variant = null)
    {
        if (!TryGetKey(url, variant, out var key, out var platform, out var contentId))
        {
            return new DownloadIndexPlan();
        }

        var plan = CreatePlan(platform, contentId, variant);
        if (plan.OutputFolderName == null || plan.OutputFileBaseName == null)
        {
            return plan;
        }

        try
        {
            lock (Gate)
            {
                var folderPath = Path.Combine(savePath, plan.OutputFolderName);
                if (Directory.Exists(folderPath))
                {
                    var meta = ReadMeta(folderPath);
                    if (meta != null && string.Equals(meta.Url, url, StringComparison.OrdinalIgnoreCase))
                    {
                        return plan;
                    }

                    var candidateFile = Path.Combine(folderPath, $"{plan.OutputFileBaseName}.mp4");
                    if (File.Exists(candidateFile))
                    {
                        try
                        {
                            var size = new FileInfo(candidateFile).Length;
                            var sha = ComputeSha256Hex(candidateFile);
                            AppendError(savePath, $"检测到命名冲突或缓存损坏: folder={folderPath} size={size} sha256={sha}");
                        }
                        catch
                        {
                        }
                    }

                    var quarantineRoot = Path.Combine(savePath, "_quarantine");
                    Directory.CreateDirectory(quarantineRoot);
                    var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    var quarantinePath = Path.Combine(quarantineRoot, $"{plan.OutputFolderName}_{timestamp}");
                    Directory.Move(folderPath, quarantinePath);
                    AppendError(savePath, $"检测到命名冲突或缓存损坏，已转移到隔离区: {quarantinePath}");
                }

                var index = LoadIndex(savePath, allowRepair: true);
                if (index.Entries.TryGetValue(key, out var existing))
                {
                    var expectedFolder = plan.OutputFolderName;
                    var expectedFile = $"{plan.OutputFileBaseName}.mp4";
                    if (!string.Equals(existing.FolderName, expectedFolder, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(existing.FileName, expectedFile, StringComparison.OrdinalIgnoreCase))
                    {
                        index.Entries.Remove(key);
                        SaveIndex(savePath, index);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppendError(savePath, $"PrepareForDownload 异常: {ex.Message}");
        }

        return plan;
    }

    public void RecordCompleted(string url, string savePath, string outputDirectory, string outputPath, long bytesWritten, string? title, string? variant = null)
    {
        if (!TryGetKey(url, variant, out var key, out var platform, out var contentId))
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                var folderName = Path.GetFileName(outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var fileName = Path.GetFileName(outputPath);

                var size = bytesWritten;
                try
                {
                    if (File.Exists(outputPath))
                    {
                        size = new FileInfo(outputPath).Length;
                    }
                }
                catch
                {
                }

                var sha = File.Exists(outputPath) ? ComputeSha256Hex(outputPath) : null;
                var entry = new IndexEntry
                {
                    Url = url,
                    Platform = platform,
                    ContentId = contentId,
                    Variant = variant,
                    FolderName = folderName,
                    FileName = fileName,
                    Size = size,
                    Sha256 = sha,
                    LastWriteTimeUtcTicks = File.Exists(outputPath) ? new FileInfo(outputPath).LastWriteTimeUtc.Ticks : 0,
                    Title = title,
                    UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };

                var index = LoadIndex(savePath, allowRepair: true);
                index.Entries[key] = entry;
                SaveIndex(savePath, index);
                WriteMeta(outputDirectory, entry);
            }
        }
        catch (Exception ex)
        {
            AppendError(savePath, $"RecordCompleted 异常: {ex.Message}");
        }
    }

    private static bool TryGetKey(string url, string? variant, out string key, out string platform, out string contentId)
    {
        key = string.Empty;
        platform = string.Empty;
        contentId = string.Empty;

        if (TryGetKuaishouPlaybackId(url, out var id))
        {
            platform = "kuaishou";
            contentId = id;
            key = string.IsNullOrWhiteSpace(variant) ? $"kuaishou_playback:{id}" : $"kuaishou_playback:{id}:{variant}";
            return true;
        }

        return false;
    }

    private static bool TryGetKuaishouPlaybackId(string url, out string id)
    {
        id = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Contains("kuaishou.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && string.Equals(segments[0], "playback", StringComparison.OrdinalIgnoreCase))
        {
            id = segments[1];
            return !string.IsNullOrWhiteSpace(id);
        }

        return false;
    }

    private static DownloadIndexPlan CreatePlan(string platform, string contentId, string? variant)
    {
        if (platform == "kuaishou" && !string.IsNullOrWhiteSpace(contentId))
        {
            var baseName = $"kuaishou_playback_{contentId}";
            if (!string.IsNullOrWhiteSpace(variant))
            {
                baseName = $"{baseName}_{variant}";
            }
            return new DownloadIndexPlan
            {
                OutputFolderName = baseName,
                OutputFileBaseName = baseName
            };
        }

        return new DownloadIndexPlan();
    }

    private static string GetExpectedExtension(string? variant)
    {
        if (string.IsNullOrWhiteSpace(variant))
        {
            return "mp4";
        }

        if (variant.StartsWith("preview_", StringComparison.OrdinalIgnoreCase))
        {
            return "mp4";
        }

        return "mp4";
    }

    private static string GetIndexDirectory(string savePath) => Path.Combine(savePath, ".vsr_index");
    private static string GetIndexPath(string savePath) => Path.Combine(GetIndexDirectory(savePath), "downloads.json");
    private static string GetErrorLogPath(string savePath) => Path.Combine(GetIndexDirectory(savePath), "errors.log");
    private static string GetMetaPath(string outputDirectory) => Path.Combine(outputDirectory, ".vsr_meta.json");

    private static DownloadIndex LoadIndex(string savePath, bool allowRepair)
    {
        Directory.CreateDirectory(GetIndexDirectory(savePath));
        var path = GetIndexPath(savePath);
        if (!File.Exists(path))
        {
            return new DownloadIndex();
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var index = JsonSerializer.Deserialize<DownloadIndex>(json, JsonOptions) ?? new DownloadIndex();
            index.Entries ??= new Dictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase);
            return index;
        }
        catch (Exception ex)
        {
            AppendError(savePath, $"索引读取失败，尝试重建: {ex.Message}");
            if (!allowRepair)
            {
                return new DownloadIndex();
            }

            return RebuildIndexFromDisk(savePath);
        }
    }

    private static void SaveIndex(string savePath, DownloadIndex index)
    {
        Directory.CreateDirectory(GetIndexDirectory(savePath));
        index.Version = 1;
        index.Entries ??= new Dictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase);
        var json = JsonSerializer.Serialize(index, JsonOptions);
        File.WriteAllText(GetIndexPath(savePath), json, Encoding.UTF8);
    }

    private static DownloadIndex RebuildIndexFromDisk(string savePath)
    {
        var index = new DownloadIndex();
        index.Entries = new Dictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!Directory.Exists(savePath))
            {
                return index;
            }

            foreach (var dir in Directory.EnumerateDirectories(savePath))
            {
                var meta = ReadMeta(dir);
                if (meta == null)
                {
                    continue;
                }

                if (!TryGetKey(meta.Url, meta.Variant, out var key, out _, out _))
                {
                    continue;
                }

                var filePath = Path.Combine(dir, meta.FileName);
                if (!File.Exists(filePath))
                {
                    continue;
                }

                try
                {
                    var info = new FileInfo(filePath);
                    meta.Size = info.Length;
                    meta.LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks;
                }
                catch
                {
                }

                index.Entries[key] = meta;
            }

            SaveIndex(savePath, index);
        }
        catch (Exception ex)
        {
            AppendError(savePath, $"索引重建失败: {ex.Message}");
        }

        return index;
    }

    private static IndexEntry? ReadMeta(string outputDirectory)
    {
        var path = GetMetaPath(outputDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<IndexEntry>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteMeta(string outputDirectory, IndexEntry entry)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            File.WriteAllText(GetMetaPath(outputDirectory), json, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static void AppendError(string savePath, string message)
    {
        try
        {
            Directory.CreateDirectory(GetIndexDirectory(savePath));
            var line = $"{DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}";
            File.AppendAllText(GetErrorLogPath(savePath), line, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static string ComputeSha256Hex(string path)
    {
        using var sha = SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
        var hash = sha.ComputeHash(stream);
        var sb = new StringBuilder(hash.Length * 2);
        for (var i = 0; i < hash.Length; i++)
        {
            sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private sealed class DownloadIndex
    {
        public int Version { get; set; }
        public Dictionary<string, IndexEntry> Entries { get; set; } = new Dictionary<string, IndexEntry>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class IndexEntry
    {
        public string Url { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ContentId { get; set; } = string.Empty;
        public string? Variant { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long Size { get; set; }
        public string? Sha256 { get; set; }
        public long LastWriteTimeUtcTicks { get; set; }
        public string? Title { get; set; }
        public string? UpdatedAtUtc { get; set; }
    }
}
