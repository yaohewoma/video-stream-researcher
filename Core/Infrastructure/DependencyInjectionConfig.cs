using Microsoft.Extensions.DependencyInjection;
using video_stream_researcher.Interfaces;
using video_stream_researcher.Services;
using video_stream_researcher.ViewModels;
using VideoStreamFetcher.Parsers;

namespace video_stream_researcher.Infrastructure;

/// <summary>
/// 依赖注入配置
/// </summary>
public static class DependencyInjectionConfig
{
    /// <summary>
    /// 配置服务
    /// </summary>
    /// <returns>服务提供程序</returns>
    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // 注册服务
        services.AddSingleton<IConfigManager, ConfigManager>();
        services.AddSingleton<IDownloadIndexService, DownloadIndexService>();
        services.AddTransient<IDownloadManager, DownloadManagerV2>();
        services.AddTransient<IVideoParser, VideoParserWrapper>();
        services.AddTransient<IDownloadFlowManager, DownloadFlowManager>();
        services.AddSingleton<IBilibiliLoginService, BilibiliLoginServiceWrapper>();

        // 注册视图模型 - 稍后手动创建并注入依赖
        services.AddTransient<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
