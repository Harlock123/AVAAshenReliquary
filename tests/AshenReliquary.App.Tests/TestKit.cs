using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AshenReliquary.App.Views;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>Opens the real window on a fixed-seed session and plays it frame by frame.</summary>
internal sealed class TestKit
{
    public const double Frame = 1 / 30.0;

    private TestKit(MainWindow window) => Window = window;

    public MainWindow Window { get; }
    public GameWorld World => Window.Session!.World;

    public static TestKit Open(string classId = "fighter", int seed = 1337, int width = 1440, int height = 900)
    {
        var window = new MainWindow { Session = GameSession.NewGame(classId, seed), Width = width, Height = height };
        window.View.FixedTime = 0;
        window.Show();
        var kit = new TestKit(window);
        kit.Pump();
        return kit;
    }

    public TestKit Play(double seconds)
    {
        for (var t = 0.0; t < seconds; t += Frame)
            Window.Tick(TimeSpan.FromSeconds(Frame));
        Pump();
        return this;
    }

    public TestKit PlayUntil(Func<bool> done, double limitSeconds = 60)
    {
        for (var t = 0.0; t < limitSeconds && !done(); t += Frame)
            Window.Tick(TimeSpan.FromSeconds(Frame));
        Pump();
        return this;
    }

    public void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Renders the window and returns the frame; saves it when <paramref name="path"/> is given.</summary>
    public WriteableBitmap Capture(string? path = null)
    {
        Pump();
        var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        if (path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            frame.Save(path, PngBitmapEncoderOptions.Default);
        }
        return frame;
    }

    /// <summary>A left click at a screen point on the world view.</summary>
    public void Click(Point at)
    {
        Window.MouseDown(at, MouseButton.Left);
        Window.MouseUp(at, MouseButton.Left);
        Pump();
    }
}
