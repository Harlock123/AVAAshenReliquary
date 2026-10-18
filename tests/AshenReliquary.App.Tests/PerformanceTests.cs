using Avalonia.Headless.XUnit;
using AshenReliquary.App.Rendering;

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

    /// <summary>
    /// A creature drawn larger than the rest (the plague crawler at 1.15x) keeps its own frames: it used to empty the
    /// whole cache on every change of scale, so each frame with a crawler in view resampled every tile and sprite again.
    /// </summary>
    [AvaloniaFact]
    public void Sprites_at_two_scales_are_kept_side_by_side()
    {
        var cache = new SpriteCache(FlareAssets.Shared);
        var plain = cache.Get(FlareAssets.IconSheet, 0, 0, FlareAssets.IconSize, FlareAssets.IconSize, 2f);
        var larger = cache.Get(FlareAssets.IconSheet, 0, 0, FlareAssets.IconSize, FlareAssets.IconSize, 2.3f);
        Assert.Same(plain, cache.Get(FlareAssets.IconSheet, 0, 0, FlareAssets.IconSize, FlareAssets.IconSize, 2f));
        Assert.Same(larger, cache.Get(FlareAssets.IconSheet, 0, 0, FlareAssets.IconSize, FlareAssets.IconSize, 2.3f));
        Assert.Equal(2, cache.Count);
    }

    /// <summary>Zooming through many scales keeps only the most recently used ones.</summary>
    [AvaloniaFact]
    public void Old_scales_are_dropped_least_recently_used_first()
    {
        var cache = new SpriteCache(FlareAssets.Shared);
        var first = cache.Get(FlareAssets.IconSheet, 0, 0, FlareAssets.IconSize, FlareAssets.IconSize, 1f);
        for (var i = 1; i <= SpriteCache.MaxScales; i++)
        {
            cache.Get(FlareAssets.IconSheet, 0, 0, FlareAssets.IconSize, FlareAssets.IconSize, 1f + i * 0.1f);
            if (i < SpriteCache.MaxScales)
                Assert.Same(first, cache.Get(FlareAssets.IconSheet, 0, 0, FlareAssets.IconSize, FlareAssets.IconSize, 1f));
        }
        Assert.Equal(SpriteCache.MaxScales, cache.Scales);
        // 1x was used just before the last new scale arrived, so 1.1x was the stalest and went instead.
        Assert.Same(first, cache.Get(FlareAssets.IconSheet, 0, 0, FlareAssets.IconSize, FlareAssets.IconSize, 1f));
    }
}
