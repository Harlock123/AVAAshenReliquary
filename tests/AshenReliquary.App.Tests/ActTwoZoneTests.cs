using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

namespace AshenReliquary.App.Tests;

/// <summary>M4 step 2 on screen: Ironhold and the Iron March, drawn from the grassland-and-ruins tileset.</summary>
public class ActTwoZoneTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void Ironhold_draws_its_ward_people_and_traders()
    {
        var kit = TestKit.Open(zoneId: "ironhold");
        var world = kit.World;
        Assert.Equal(2, world.Zone.Act);
        Assert.True(world.Zone.Outdoors);
        kit.Play(0.3);
        kit.Capture(Shot("ironhold-gate"));
        TestHooks.Place(world, 23.5, 17.5);
        kit.Play(0.3);
        kit.Capture(Shot("ironhold-market"));
        TestHooks.Place(world, 14.5, 15.5);
        kit.Play(0.3);
        kit.Capture(Shot("ironhold-statues"));
        Assert.Equal(["hilde", "sable"], world.Vendors.Select(v => v.Definition.Id).Order());
    }

    [AvaloniaFact]
    public void The_iron_march_draws_its_walls_cover_and_monsters()
    {
        var kit = TestKit.Open(zoneId: "iron_march");
        var world = kit.World;
        kit.Play(0.3);
        kit.Capture(Shot("march-causeway"));
        foreach (var (name, y) in new[] { ("march-battlefield", 163), ("march-watchtower", 127), ("march-camp", 96), ("march-gate", 12) })
        {
            var x = Enumerable.Range(0, world.Map.Width).First(x => world.Zone.Rows[y][x] == '=') + 1.5;
            TestHooks.Place(world, x, y + 0.5);
            kit.Play(0.3);
            kit.Capture(Shot(name));
        }
        var lookout = world.Zone.Caches!.Single(c => c.Id == "lookout").At;
        TestHooks.Place(world, lookout[0] + 2.5, lookout[1] + 0.5);
        kit.Play(0.6);
        kit.Capture(Shot("march-lookout"));
        var varro = world.Monsters.Single(m => m.Definition.Id == "centurion_varro");
        TestHooks.Place(world, varro.X + 2.5, varro.Y + 4.5);
        kit.Play(0.6);
        kit.Capture(Shot("march-varro"));
        Assert.Equal("Centurion Varro", varro.Name);
    }

    [AvaloniaFact]
    public void The_barrow_fields_rising_dead_glow_and_a_click_smashes_one()
    {
        var kit = TestKit.Open(zoneId: "barrow_fields");
        var world = kit.World;
        var corpse = world.Monsters.First(m => m.Definition.Revives > 0 && m.DistanceTo(world.Hero) < 20);
        TestHooks.Place(world, corpse.X - 2, corpse.Y + 1);
        foreach (var m in world.Monsters.Where(m => m != corpse && m.DistanceTo(world.Hero) < 12).ToList())
            TestHooks.Slay(world, m);
        TestHooks.Slay(world, corpse);
        kit.Play(1.5);
        Assert.True(corpse.IsRising);
        var at = kit.Window.View.ScreenOf(corpse.X, corpse.Y);
        kit.Window.MouseMove(at);
        kit.Play(0.1);
        Assert.Equal(corpse, kit.Window.View.CorpseUnder(at));
        kit.Capture(Shot("barrow-rising"));
        kit.Click(at);
        kit.Play(2);
        Assert.False(corpse.IsRising);
        kit.Capture(Shot("barrow-smashed"));
    }

    [AvaloniaFact]
    public void The_old_barracks_show_a_spotted_trap_with_how_to_disarm_it()
    {
        var kit = TestKit.Open("rogue", zoneId: "old_barracks");
        var world = kit.World;
        var trap = world.ZoneTraps.First(t => t.Definition.Id == "stair_passage");
        TestHooks.Place(world, trap.X, trap.Y - 2.5);
        foreach (var m in world.Monsters.ToList())
            TestHooks.Slay(world, m);
        for (var i = 0; i < 20 && trap.State == Core.World.TrapState.Hidden; i++)
        {
            TestHooks.Reveal(world, trap);
            kit.Play(0.1);
        }
        Assert.Equal(Core.World.TrapState.Found, trap.State);
        var at = kit.Window.View.ScreenOf(trap.X, trap.Y);
        kit.Window.MouseMove(at);
        kit.Play(0.3);
        Assert.Equal(trap, kit.Window.View.TrapUnder(at));
        kit.Capture(Shot("barracks-trap"));
    }
}
