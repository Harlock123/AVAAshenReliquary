using Avalonia.Headless.XUnit;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>
/// Vorthrax on screen (M4 step 9): his breath marked on the ground, the wyrm aloft over his firestorm, and the platforms
/// of his cavern cracking before they sink.
/// </summary>
public class VorthraxUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void Vorthrax_breathes_flies_and_breaks_his_floor()
    {
        var kit = TestKit.Open("wizard", zoneId: "wyrms_nest");
        var world = kit.World;
        world.TrainTo(19);
        world.Hero.Character.CannotFall = true;
        var wyrm = world.Monsters.Single(m => m.Definition.Id == "vorthrax");
        foreach (var m in world.Monsters.Where(m => m != wyrm).ToList())
            TestHooks.Slay(world, m);
        TestHooks.Place(world, wyrm.X, wyrm.Y + 4);
        kit.PlayUntil(() => wyrm.Breath is { } b && world.Time > b.BurnAt - 0.6, 15);
        Assert.NotNull(wyrm.Breath);
        kit.Capture(Shot("vorthrax-breath"));

        wyrm.Creature.SetHitPoints(wyrm.Creature.Stats.HitPoints / 2);
        kit.PlayUntil(() => wyrm.Aloft && world.Hazards.Count >= 3, 10);
        Assert.True(wyrm.Aloft);
        kit.Capture(Shot("vorthrax-aloft"));

        wyrm.Creature.SetHitPoints(wyrm.Creature.Stats.HitPoints / 5);
        kit.PlayUntil(() => world.Crumbling.Count > 0, 10);
        Assert.False(wyrm.Aloft);
        Assert.NotEmpty(world.Crumbling);
        kit.Capture(Shot("vorthrax-crumbling"));
        kit.PlayUntil(() => world.Crumbling.Count == 0, 6);
        kit.Play(0.3);
        kit.Capture(Shot("vorthrax-sunk"));
    }
}
