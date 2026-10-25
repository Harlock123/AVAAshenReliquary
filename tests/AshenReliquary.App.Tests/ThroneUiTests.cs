using Avalonia.Headless.XUnit;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>
/// The Cinder Throne on screen (M5 step 1): the Echoes standing as grey figures of ash round the dais until Malachar
/// calls one, and the Order's knights, drawn from the avatar layers, fighting beside the hero when the beacon was lit.
/// </summary>
public class ThroneUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void The_court_stands_in_ash_until_malachar_calls_it_and_the_knights_come_in()
    {
        var kit = TestKit.Open("fighter", zoneId: "cinder_throne");
        var world = kit.World;
        world.TrainTo(20);
        world.Hero.Character.CannotFall = true;
        world.SetFlag("act4.beacon", "lit");
        var malachar = world.Monsters.Single(m => m.Definition.Id == "malachar");
        var echoes = world.Monsters.Where(m => m.IsEcho).ToList();
        Assert.Equal(4, echoes.Count);
        Assert.All(echoes, e => Assert.True(e.Dormant));
        foreach (var m in world.Monsters.Where(m => m != malachar && !m.IsEcho && m.Definition.Boss is null && m.Y > 30).ToList())
            TestHooks.Slay(world, m);
        TestHooks.Place(world, malachar.X, malachar.Y + 6);
        kit.Play(0.3);
        kit.Capture(Shot("throne-court"));

        kit.PlayUntil(() => world.Allies.Count(a => a.Definition.Id == "lantern_knight") == 2, 3);
        Assert.Equal(2, world.Allies.Count(a => a.Definition.Id == "lantern_knight"));
        kit.Play(0.5);
        kit.Capture(Shot("throne-knights"));

        kit.PlayUntil(() => echoes.Any(e => !e.Dormant), 20);
        Assert.Contains(echoes, e => !e.Dormant);
        kit.Play(1.0);
        kit.Capture(Shot("throne-echo-called"));
    }
}
