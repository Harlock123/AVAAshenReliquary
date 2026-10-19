using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AshenReliquary.App.Rendering;
using AshenReliquary.App.Services;

namespace AshenReliquary.App.Tests;

/// <summary>The mouse wheel's zoom (stepped, gliding, kept in the settings) and the ring under the hero's target.</summary>
public class CameraTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static readonly Point Middle = new(720, 450);

    [AvaloniaFact]
    public void The_wheel_steps_the_zoom_in_and_out_and_the_settings_keep_it()
    {
        var kit = TestKit.Open();
        var view = kit.Window.View;
        try
        {
            Assert.Equal(WorldView.DefaultZoomLevel, view.ZoomLevel);
            kit.Window.MouseWheel(Middle, new Vector(0, 1));
            Assert.Equal(WorldView.DefaultZoomLevel + 1, view.ZoomLevel);
            Assert.Equal(WorldView.ZoomSteps[WorldView.DefaultZoomLevel + 1], view.Zoom); // tests fix the time: no glide
            Assert.Equal(view.ZoomLevel, Settings.Load().Zoom);

            for (var i = 0; i < 10; i++)
                kit.Window.MouseWheel(Middle, new Vector(0, -1));
            Assert.Equal(0, view.ZoomLevel);
            Assert.Equal(0, Settings.Load().Zoom);
            kit.Capture(Shot("zoomed-out"));

            // A touchpad's small amounts add up to a step.
            kit.Window.MouseWheel(Middle, new Vector(0, 0.4));
            kit.Window.MouseWheel(Middle, new Vector(0, 0.4));
            Assert.Equal(0, view.ZoomLevel);
            kit.Window.MouseWheel(Middle, new Vector(0, 0.4));
            Assert.Equal(1, view.ZoomLevel);
            Assert.Equal(1, new Views.MainWindow().View.ZoomLevel); // a new window starts where the wheel left it
        }
        finally
        {
            // The settings file is shared by the whole test run: put the zoom back for the other tests.
            view.ZoomLevel = WorldView.DefaultZoomLevel;
            (Settings.Load() with { Zoom = WorldView.DefaultZoomLevel }).Save();
        }
    }

    [AvaloniaFact]
    public void A_zoom_step_glides_there_and_settles_exactly_on_it()
    {
        var kit = TestKit.Open();
        var view = kit.Window.View;
        view.FixedTime = null;
        try
        {
            var from = view.Zoom;
            kit.Window.MouseWheel(Middle, new Vector(0, 1));
            Assert.True(view.Zooming);
            Assert.InRange(view.Zoom, from, view.ZoomTarget);
            kit.Capture(); // a frame mid-glide draws with the next step's frames stretched
            Thread.Sleep(TimeSpan.FromSeconds(WorldView.ZoomSeconds + 0.1));
            kit.Play(TestKit.Frame); // the game's tick asks for the next frame
            kit.Capture();
            Assert.False(view.Zooming);
            Assert.Equal(view.ZoomTarget, view.Zoom);
        }
        finally
        {
            view.ZoomLevel = WorldView.DefaultZoomLevel;
            (Settings.Load() with { Zoom = WorldView.DefaultZoomLevel }).Save();
        }
    }

    [AvaloniaFact]
    public void The_monster_being_attacked_has_a_red_ring_on_the_ground()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        var view = kit.Window.View;
        var pack = world.Monsters.OrderBy(m => m.DistanceTo(world.Hero)).First();
        TestHooks.Place(world, pack.X - 1.6, pack.Y);
        kit.Play(0.1);
        var target = world.Monsters.Where(m => !m.IsDead).MinBy(m => m.DistanceTo(world.Hero))!;
        Assert.Null(view.Targeted);
        var before = RedAround(kit.Capture(), view, target);

        world.Attack(target);
        kit.Play(0.2);
        Assert.Same(target, view.Targeted);
        var after = RedAround(kit.Capture(Shot("target-ring")), view, target);
        Assert.True(after > before + 40, $"ember-red pixels around the target's feet: {before} before, {after} after");

        TestHooks.Slay(world, target);
        kit.Play(0.1);
        Assert.Null(view.Targeted);
    }

    /// <summary>How many strongly red pixels lie in the band in front of a monster's feet, where the ring passes.</summary>
    private static int RedAround(WriteableBitmap frame, WorldView view, Core.World.MonsterActor m)
    {
        var foot = view.ToScreen(m.X, m.Y);
        var r = WorldView.TargetRingRadius * (float)view.Zoom * (float)m.Definition.Scale;
        using var buffer = frame.Lock();
        var row = new byte[buffer.RowBytes];
        var count = 0;
        for (var y = (int)(foot.Y + r * 0.3f); y <= (int)(foot.Y + r * 0.7f); y++)
        {
            Marshal.Copy(buffer.Address + y * buffer.RowBytes, row, 0, row.Length);
            for (var x = (int)(foot.X - r * 1.2f); x <= (int)(foot.X + r * 1.2f); x++)
            {
                // The ring's ember is far redder than any grass, road or shadow.
                var bgra = buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888;
                int red = row[x * 4 + (bgra ? 2 : 0)], g = row[x * 4 + 1], b = row[x * 4 + (bgra ? 0 : 2)];
                if (red > 140 && red > g * 1.6 && red > b * 1.6)
                    count++;
            }
        }
        return count;
    }
}
