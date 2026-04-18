using Avalonia;
using System;
using video_stream_researcher.UI;

namespace video_stream_researcher;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        #if ANDROID
            // Android 平台不需要 Main 方法，由 Avalonia.Android 处理
        #else
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        #endif
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
