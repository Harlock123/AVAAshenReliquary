using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(AshenReliquary.App.Tests.TestAppBuilder))]
// One window at a time: the tests share the UI thread and the save folder override.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AshenReliquary.App.Tests;

public static class TestAppBuilder
{
    // Real Skia rendering (not the stub renderer) so tests can capture and inspect frames.
    public static AppBuilder BuildAvaloniaApp()
    {
        Views.MainWindow.RunGameLoop = false;
        // Never write into the player's real save folder from a test.
        Services.SaveStore.FolderOverride = Path.Combine(Path.GetTempPath(), "ashen-tests", Guid.NewGuid().ToString("N"));
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
