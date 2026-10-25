using System.Reflection;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>The Bell Tower on screen: the three candles in the chapel, and the Choir in the belfry with her reflections.</summary>
public class ChoirUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static void Invoke(GameWorld world, string method, params object[] args) =>
        typeof(GameWorld).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, args);

    [AvaloniaFact]
    public void The_candles_burn_in_their_colours_and_white_once_set_right()
    {
        var kit = TestKit.Open(zoneId: "bell_tower");
        var world = kit.World;
        world.TrainTo(14);
        foreach (var m in world.Monsters.ToList())
            TestHooks.Slay(world, m);
        var stand = world.Interactives.Single(i => i.Id == "candle_brigid");
        TestHooks.Place(world, stand.X, stand.Y + 3);
        kit.Play(0.3);
        Assert.Equal("blue", world.CandleColour(stand));
        kit.Capture(Shot("tower-candles"));
        foreach (var (a, b) in new[] { ("agnete", "brigid"), ("cille", "brigid") })
        {
            Invoke(world, "TouchCandle", world.Interactives.Single(i => i.Id == "candle_" + a));
            Invoke(world, "TouchCandle", world.Interactives.Single(i => i.Id == "candle_" + b));
        }
        Assert.Equal("rekindled", world.Flag("act3.candles"));
        kit.Play(0.3);
        kit.Capture(Shot("tower-candles-white"));
    }

    [AvaloniaFact]
    public void The_choir_sings_on_one_bar_and_hides_among_her_reflections()
    {
        var kit = TestKit.Open("wizard", zoneId: "bell_tower");
        var world = kit.World;
        world.TrainTo(14);
        world.Hero.Character.CannotFall = true;
        var agnete = world.Monsters.Single(m => m.Definition.Id == "choir_agnete");
        foreach (var m in world.Monsters.Where(m => m != agnete).ToList())
            TestHooks.Slay(world, m);
        TestHooks.Place(world, agnete.X, agnete.Y + 5);
        kit.Play(1.5);
        Assert.Same(agnete, world.Boss);
        Assert.Equal(2, world.Monsters.Count(m => m.SharesWith == agnete && !m.IsDead));
        kit.Capture(Shot("belfry-choir"));
        agnete.Creature.SetHitPoints(agnete.Creature.Stats.HitPoints / 2);
        kit.Play(0.6);
        Assert.Equal(4, world.Monsters.Count(m => m.IsReflection && !m.IsDead));
        typeof(MonsterActor).GetProperty(nameof(MonsterActor.TrueVoiceUntil))!.SetValue(agnete, world.Time + 5);
        kit.Play(0.2);
        kit.Capture(Shot("belfry-reflections"));
    }
}
