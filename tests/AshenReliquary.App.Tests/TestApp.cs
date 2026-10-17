using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(AshenReliquary.App.Tests.TestAppBuilder))]

namespace AshenReliquary.App.Tests;

public static class TestAppBuilder
{
    // Real Skia rendering (not the stub renderer) so tests can capture and inspect frames.
    public static AppBuilder BuildAvaloniaApp()
    {
        Views.MainWindow.RunGameLoop = false;
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
