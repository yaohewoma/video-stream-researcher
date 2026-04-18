using System;
using System.IO;
using System.Text.Json;
using video_stream_researcher.Interfaces;

namespace video_stream_researcher.Services;

/// <summary>
/// 配置管理器
/// 负责保存和加载应用程序配置
/// </summary>
public class ConfigManager : IConfigManager
{
    private readonly string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
    private AppConfig _currentConfig;
    
    /// <summary>
    /// 初始化配置管理器
    /// </summary>
    public ConfigManager()
    {
        _currentConfig = LoadConfigInternal();
    }
    
    private AppConfig LoadConfigInternal()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = File.ReadAllText(_configFilePath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            // 配置加载失败，返回默认配置
            Console.WriteLine($"配置加载失败: {ex.Message}");
        }
        
        return new AppConfig();
    }
    
    /// <summary>
    /// 读取配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>配置值</returns>
    public T ReadConfig<T>(string key, T defaultValue = default!)
    {
        try
        {
            switch (key)
            {
                case "SavePath":
                    return (T)(object)_currentConfig.SavePath;
                case "IsDarkTheme":
                    return (T)(object)_currentConfig.IsDarkTheme;
                case "IsFFmpegEnabled":
                    return (T)(object)_currentConfig.IsFFmpegEnabled;
                case "MergeMode":
                    return (T)(object)_currentConfig.MergeMode;
                default:
                    return defaultValue;
            }
        }
        catch
        {
            return defaultValue;
        }
    }
    
    /// <summary>
    /// 保存配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="key">配置键</param>
    /// <param name="value">配置值</param>
    public void SaveConfig<T>(string key, T value)
    {
        try
        {
            switch (key)
            {
                case "SavePath":
                    if (value is string path)
                        _currentConfig.SavePath = path;
                    break;
                case "IsDarkTheme":
                    if (value is bool isDark)
                        _currentConfig.IsDarkTheme = isDark;
                    break;
                case "IsFFmpegEnabled":
                    if (value is bool isEnabled)
                        _currentConfig.IsFFmpegEnabled = isEnabled;
                    break;
                case "MergeMode":
                    if (value is string mode)
                        _currentConfig.MergeMode = mode;
                    break;
            }
            
            // 保存到文件
            string json = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            // 配置保存失败，不影响程序运行
            Console.WriteLine($"配置保存失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 重置配置
    /// </summary>
    public void ResetConfig()
    {
        _currentConfig = new AppConfig();
        SaveConfigInternal();
    }
    
    private void SaveConfigInternal()
    {
        try
        {
            string json = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            // 配置保存失败，不影响程序运行
            Console.WriteLine($"配置保存失败: {ex.Message}");
        }
    }
}

/// <summary>
/// 应用程序配置
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 保存路径
    /// </summary>
    public string SavePath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    
    /// <summary>
    /// 是否使用深色主题
    /// </summary>
    public bool IsDarkTheme { get; set; } = true;
    
    /// <summary>
    /// 是否启用FFmpeg
    /// </summary>
    public bool IsFFmpegEnabled { get; set; } = false;
    
    /// <summary>
    /// MP4合并模式
    /// </summary>
    public string MergeMode { get; set; } = "NonFragmented";
}