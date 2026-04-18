using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Windows.Input;
using video_stream_researcher.Services;

namespace video_stream_researcher.UI.Converters;

/// <summary>
/// 本地化转换器 - 用于 XAML 绑定
/// </summary>
public class LocalizationConverter : IValueConverter
{
    /// <summary>
    /// 单例实例
    /// </summary>
    public static LocalizationConverter Instance { get; } = new();

    /// <summary>
    /// 私有构造函数
    /// </summary>
    private LocalizationConverter()
    {
    }

    /// <summary>
    /// 转换 - 根据资源键获取本地化字符串
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string key)
        {
            var result = LocalizationService.Instance[key];
            return result;
        }
        return value;
    }

    /// <summary>
    /// 反向转换 - 不支持
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("LocalizationConverter does not support ConvertBack");
    }
}
