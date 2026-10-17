using Avalonia.Headless.XUnit;

namespace AshenReliquary.App.Tests;

public class PerformanceTests(ITestOutputHelper output)
{
    /// <summary>Renders 60 frames of a fight at 1440x900 and reports where the time goes.</summary>
    [AvaloniaTheory]
    [InlineData(1440, 900, null)]
    [InlineData(2624, 1648, null)]   // the VM's full screen in device pixels, at full resolution
    [InlineData(2624, 1648, 0.5f)]   // the same screen with the world drawn at half (1x logical) resolution
    public void Frame_time_breakdown(int width, int height, float? worldScale)
    {
        var kit = TestKit.Open(width: width, height: height);
        kit.Window.View.WorldRenderScale = worldScale;
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        world.Attack(zombie);
        kit.Play(0.5);
        kit.Capture(); // warm-up: decodes every image once
        kit.Window.View.Stats.Reset();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 59; i++) // the F3 overlay resets the averages every 60 frames
        {
            kit.Play(1 / 30.0);
            kit.Capture();
        }
        output.WriteLine($"{width}x{height} world scale {kit.Window.View.LastWorldScale}:");
        output.WriteLine($"wall: {clock.Elapsed.TotalMilliseconds / 59:0.0} ms per captured frame");
        output.WriteLine(kit.Window.View.Stats.ToString());
        Assert.Equal(59, kit.Window.View.Stats.Frames);
    }
}
