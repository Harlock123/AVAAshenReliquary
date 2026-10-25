using System.Reflection;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>
/// Act 4 on screen (M4 step 8): the Ember Gate camp, an ash storm in the wastes, lava, bridges and embers, the egg in the
/// Wyrm's Nest, a leader and its pack, and the Reliquary's wards and Echoes.
/// </summary>
public class ActFourUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static void SetTime(GameWorld world, double time) =>
        typeof(GameWorld).GetProperty(nameof(GameWorld.Time))!.SetValue(world, time);

    [AvaloniaFact]
    public void The_ember_gate_camp_shows_its_people_its_fires_and_the_beacon()
    {
        var kit = TestKit.Open(zoneId: "ember_gate");
        var world = kit.World;
        var beacon = world.Interactives.Single(i => i.Id == "beacon");
        TestHooks.Place(world, beacon.X - 3, beacon.Y + 4);
        kit.Play(0.5);
        Assert.Equal("ember_gate", world.Zone.Id);
        Assert.Contains(world.Interactives, i => i.Id == "corwen");
        kit.Capture(Shot("ember-gate"));
        world.SetFlag("act4.beacon", "lit");
        kit.Play(0.3);
        kit.Capture(Shot("ember-gate-beacon"));
    }

    [AvaloniaFact]
    public void The_wastes_close_in_under_an_ash_storm_and_a_leader_wears_its_mark()
    {
        var kit = TestKit.Open(zoneId: "ash_wastes");
        var world = kit.World;
        world.TrainTo(16);
        world.Hero.Character.CannotFall = true;
        var captain = world.Monsters.First(m => m.IsLeader && m.Definition.Id == "inquisitor_captain");
        TestHooks.Place(world, captain.X - 4, captain.Y + 3);
        kit.Play(1.0);
        Assert.True(captain.IsLeader);
        kit.Capture(Shot("wastes-leader"));
        SetTime(world, GameWorld.AshStormEverySeconds + 2);
        kit.Play(0.5);
        Assert.True(world.AshStorm);
        kit.Capture(Shot("wastes-storm"));
    }

    [AvaloniaFact]
    public void The_lava_rivers_glow_and_their_bridge_carries_the_road()
    {
        var kit = TestKit.Open(zoneId: "lava_rivers");
        var world = kit.World;
        world.TrainTo(17);
        foreach (var m in world.Monsters.ToList())
            TestHooks.Slay(world, m);
        var bridge = Enumerable.Range(0, world.Map.Height).SelectMany(y => Enumerable.Range(0, world.Map.Width).Select(x => (x, y)))
            .Where(c => world.Map.IsLava(c.x, c.y) && !world.Map.IsBlocked(c.x, c.y)).MaxBy(c => c.y);
        TestHooks.Place(world, bridge.x + 0.5, bridge.y + 4.5);
        kit.Play(0.5);
        kit.Capture(Shot("lava-bridge"));
        var ford = Enumerable.Range(0, world.Map.Height).SelectMany(y => Enumerable.Range(0, world.Map.Width).Select(x => (x, y)))
            .Where(c => world.Map.IsEmbers(c.x, c.y)).OrderBy(c => c.y).Skip(30).First();
        TestHooks.Place(world, ford.x + 0.5, ford.y + 0.5);
        kit.Play(1.5);
        Assert.True(world.Hero.Character.HitPoints < world.Hero.Character.MaxHitPoints);
        kit.Capture(Shot("lava-ford"));
    }

    [AvaloniaFact]
    public void The_nest_keeps_the_wyrm_s_egg_in_its_embers()
    {
        var kit = TestKit.Open(zoneId: "wyrms_nest");
        var world = kit.World;
        world.TrainTo(18);
        foreach (var m in world.Monsters.ToList())
            TestHooks.Slay(world, m);
        var egg = world.Interactives.Single(i => i.Id == "egg");
        TestHooks.Place(world, egg.X - 2, egg.Y + 3);
        kit.Play(0.5);
        kit.Capture(Shot("nest-egg"));
    }

    [AvaloniaFact]
    public void The_reliquary_s_wards_burn_until_the_echo_before_them_falls()
    {
        var kit = TestKit.Open("wizard", zoneId: "reliquary");
        var world = kit.World;
        world.TrainTo(19);
        world.Hero.Character.CannotFall = true;
        var sallow = world.Monsters.Single(m => m.IsEcho && m.Definition.Id == "mother_sallow");
        TestHooks.Place(world, sallow.X, sallow.Y + 6);
        kit.Play(1.5);
        Assert.Same(sallow, world.Boss);
        kit.Capture(Shot("reliquary-echo"));
        var gate = world.Zone.Gates!.Single(g => g.Id == "vault_of_rot");
        Assert.False(world.GateOpen(gate));
        TestHooks.Slay(world, sallow);
        TestHooks.Place(world, gate.Cells[1][0] + 0.5, gate.Cells[1][1] - 3.5);
        kit.Play(0.5);
        Assert.True(world.GateOpen(gate));
        var iron = world.Zone.Gates!.Single(g => g.Id == "vault_of_iron");
        TestHooks.Place(world, iron.Cells[1][0] + 0.5, iron.Cells[1][1] - 3.5);
        kit.Play(0.3);
        kit.Capture(Shot("reliquary-ward"));
    }
}
