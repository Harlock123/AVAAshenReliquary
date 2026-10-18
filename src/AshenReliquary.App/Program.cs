using Avalonia;
using Avalonia.Skia;
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
        // Every sprite frame and tile is its own texture at the screen's full density, and a fight on a HiDPI screen
        // needs far more than Avalonia's default budget (about 28 MB): past it Skia evicts textures and uploads them
        // again every frame, which took fights down to single-digit frame rates.
        .With(new SkiaOptions { MaxGpuResourceSizeBytes = 512L * 1024 * 1024 })
#if DEBUG
        .WithDeveloperTools()
#endif
        .LogToTrace();
}
