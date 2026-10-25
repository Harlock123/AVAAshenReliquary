using System.Reflection;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>
/// M4 steps 5 and 6 on screen: Saint Oriel's Rest in the snow, a level-13 hero's abilities, a Mirrored monster's images;
/// the Frostbitten Pass's cold and blizzard, its minotaurs, ice ants and wraiths, the Hollow Woods' unseen and phantoms, the
/// Catacombs of Oriel and a turned wolf.
/// </summary>
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
            (int?)8, new[] { "mirrored", "hasted" }, null])!;
        var hurt = typeof(GameWorld).GetMethod("DamageMonster", BindingFlags.NonPublic | BindingFlags.Instance)!;
        hurt.Invoke(world, [champion, champion.Creature.Stats.HitPoints * 6 / 10]);
        kit.Play(0.6);
        Assert.Equal(2, world.Monsters.Count(m => m.IsMirrorImage));
        Assert.Equal("Mirrored Hasted Legionary Champion", champion.Name);
        kit.Capture(Shot("mirrored-images"));
    }

    private static MonsterActor Spawn(GameWorld world, string creature, double dx, double dy, int level)
    {
        var spawn = typeof(GameWorld).GetMethod("SpawnNear", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (MonsterActor)spawn.Invoke(world, [creature, Core.Creatures.CreatureRank.Normal, world.Hero.X + dx, world.Hero.Y + dy, 0.0, 999,
            (int?)level, Array.Empty<string>(), null])!;
    }

    private static void Clear(GameWorld world)
    {
        foreach (var m in world.Monsters.Where(m => m.DistanceTo(world.Hero) < 25).ToList())
            TestHooks.Slay(world, m);
    }

    [AvaloniaFact]
    public void The_frostbitten_pass_shows_the_cold_on_the_hud_and_a_blizzard_whitens_the_air()
    {
        var kit = TestKit.Open(zoneId: "frostbitten_pass");
        var world = kit.World;
        world.TrainTo(11);
        Assert.True(world.Zone.Cold);
        kit.Play(0.3);
        kit.Capture(Shot("pass-foot"));
        Clear(world);
        Spawn(world, "horned_guardian", 3, -2, 11);
        Spawn(world, "rime_crawler", 4, 0, 11);
        Spawn(world, "frost_wraith", 2, 2, 11);
        typeof(HeroActor).GetProperty(nameof(HeroActor.Cold))!.SetValue(world.Hero, 64.0);
        typeof(GameWorld).GetField("_blizzardAt", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(world, world.Time - 1);
        kit.Play(0.6);
        Assert.True(world.Blizzard);
        Assert.True(world.Hero.Cold > 64);
        kit.Capture(Shot("pass-blizzard"));
    }

    [AvaloniaFact]
    public void The_hollow_woods_hide_their_stalkers_and_phantoms_walk()
    {
        var kit = TestKit.Open(zoneId: "hollow_woods");
        var world = kit.World;
        world.TrainTo(12);
        kit.Play(0.3);
        kit.Capture(Shot("woods-edge"));
        Clear(world);
        var stalker = Spawn(world, "hollow_stalker", 4, -1, 12);
        Spawn(world, "phantom", 3, 2, 12);
        Spawn(world, "whisperer", 6, 1, 12);
        Assert.False(world.CanTarget(stalker));
        Assert.Null(world.MonsterAt(stalker.X, stalker.Y));
        typeof(MonsterActor).GetProperty("SpotIn", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(stalker, 99.0);
        kit.Play(0.2);
        kit.Capture(Shot("woods-unseen"));
    }

    [AvaloniaFact]
    public void The_catacombs_are_dark_and_a_dominated_wolf_turns_violet()
    {
        var kit = TestKit.Open("ranger", zoneId: "catacombs_of_oriel", subclass: "beastmaster");
        var world = kit.World;
        world.TrainTo(13);
        kit.Play(0.3);
        kit.Capture(Shot("catacombs-stair"));
        Clear(world);
        Spawn(world, "choir_acolyte", 4, 2, 13);
        Spawn(world, "catacomb_dead", 3, -1, 13);
        var wolf = world.Companion!;
        typeof(AllyActor).GetProperty(nameof(AllyActor.TurnedUntil))!.SetValue(wolf, world.Time + 5);
        kit.Play(0.4);
        Assert.True(wolf.IsTurned(world.Time));
        kit.Capture(Shot("catacombs-turned"));
    }

    [AvaloniaFact]
    public void Every_creature_s_sprite_loads_with_its_sheets()
    {
        var assets = Rendering.FlareAssets.Shared;
        foreach (var creature in Core.Content.ContentDatabase.Embedded.Creatures.Values.Where(c => c.Sprite != "avatar"))
        {
            var set = assets.Enemy(creature.Sprite);
            foreach (var name in new[] { "stance", "run", "swing", "die" })
                if (set[name] is { } anim)
                    Assert.NotNull(assets.Image(anim.Image ?? set.Image));
        }
        Assert.Equal("images/enemies/minotaur/minotaur_swing.png", assets.Enemy("minotaur")["swing"]!.Image);
    }

    [AvaloniaFact]
    public void In_a_narrow_window_the_target_frame_clears_a_long_zone_name_and_the_minimap()
    {
        var kit = TestKit.Open(zoneId: "catacombs_of_oriel", width: 731, height: 946);
        kit.Play(0.2);
        kit.Capture();
        var hud = typeof(Rendering.WorldView).GetField("_hud", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(kit.Window.View)!;
        var titleRight = (float)hud.GetType().GetField("_titleRight", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hud)!;
        Assert.True(titleRight > 731 / 2 - 170, "the name should be long enough to collide");
        var place = hud.GetType().GetMethod("TopFrame", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var (left, top, width) = ((float, float, float))place.Invoke(hud, [new SkiaSharp.SKSize(731, 946), 340f, 18f, 84f])!;
        var clearsName = left >= titleRight || top >= 84;
        Assert.True(clearsName);
        Assert.True(left + width <= 731 - Rendering.Minimap.Width - 14);
        // A wide window keeps it centred.
        var (wideLeft, wideTop, _) = ((float, float, float))place.Invoke(hud, [new SkiaSharp.SKSize(1440, 900), 340f, 18f, 84f])!;
        Assert.Equal((1440 / 2 - 170f, 18f), (wideLeft, wideTop));
    }
}
