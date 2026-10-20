using System.Reflection;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>M4 step 5 on screen: Saint Oriel's Rest in the snow, a level-13 hero's abilities, and a Mirrored monster's images.</summary>
public class ActThreeTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void Saint_oriels_rest_draws_its_cloister_in_the_snow()
    {
        var kit = TestKit.Open(zoneId: "saint_oriels_rest");
        var world = kit.World;
        Assert.Equal(3, world.Zone.Act);
        kit.Play(0.3);
        kit.Capture(Shot("oriel-gate"));
        TestHooks.Place(world, 18.5, 14.5);
        kit.Play(0.3);
        kit.Capture(Shot("oriel-cloister"));
        TestHooks.Place(world, 28.5, 12.5);
        kit.Play(0.3);
        kit.Capture(Shot("oriel-tower"));
        Assert.Equal(["halvard", "mora"], world.Vendors.Select(v => v.Definition.Id).Order());
    }

    [AvaloniaFact]
    public void A_level_thirteen_pyromancer_has_cone_of_cold_and_meteor_with_tooltips()
    {
        var kit = TestKit.Open("wizard", subclass: "pyromancer");
        var world = kit.World;
        world.TrainTo(13);
        var meteor = world.KnownAbilities.Single(a => a.Id == "meteor");
        var tip = Rendering.Hud.AbilityTip(world, meteor, "").Select(l => l.Text).ToList();
        Assert.Contains(tip, t => t.Contains("Pyromancer, level 13"));
        Assert.Contains(world.KnownAbilities, a => a.Id == "cone_of_cold");
        kit.Window.View.AbilitiesOpen = true;
        kit.Play(0.2);
        kit.Capture(Shot("level13-abilities"));
    }

    [AvaloniaFact]
    public void A_mirrored_monster_splits_into_glassy_images()
    {
        var kit = TestKit.Open(zoneId: "iron_march");
        var world = kit.World;
        world.TrainTo(8);
        foreach (var m in world.Monsters.Where(m => m.DistanceTo(world.Hero) < 25).ToList())
            TestHooks.Slay(world, m);
        var spawn = typeof(GameWorld).GetMethod("SpawnNear", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var champion = (MonsterActor)spawn.Invoke(world, ["legionary", Core.Creatures.CreatureRank.Champion, world.Hero.X + 4, world.Hero.Y - 2, 0.0, 999,
            (int?)8, new[] { "mirrored", "hasted" }])!;
        var hurt = typeof(GameWorld).GetMethod("DamageMonster", BindingFlags.NonPublic | BindingFlags.Instance)!;
        hurt.Invoke(world, [champion, champion.Creature.Stats.HitPoints * 6 / 10]);
        kit.Play(0.6);
        Assert.Equal(2, world.Monsters.Count(m => m.IsMirrorImage));
        Assert.Equal("Mirrored Hasted Legionary Champion", champion.Name);
        kit.Capture(Shot("mirrored-images"));
    }
}
