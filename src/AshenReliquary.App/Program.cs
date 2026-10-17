using Avalonia;
using AshenReliquary.App.Services;

namespace AshenReliquary.App;

// A classic Program class (not top-level statements) so the Avalonia previewer
// can discover BuildAvaloniaApp and Windows gets an STA main thread.
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        DisplayScale.Apply();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
#if DEBUG
        .WithDeveloperTools()
#endif
        .LogToTrace();
}
