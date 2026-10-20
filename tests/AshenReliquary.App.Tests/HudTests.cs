using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.App.Tests;

public class HudTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void Hud_shows_conditions_buffs_and_an_ability_tooltip()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        var c = kit.World.Hero.Character;
        c.Conditions.Apply(ConditionKind.Frightened, 2);
        c.Conditions.Apply(ConditionKind.Slowed, 1, 6);
        c.TakeDamage(8);
        kit.World.DrinkPotion();
        c.Taint.Add(70);
        kit.World.Hero.Resource.OnCombat();
        kit.Play(0.3);
        // RMB slot (Power Attack): the bar is centred, 8 slots of 54 px with 6 px gaps, 98 px above the bottom.
        var barLeft = 1288 / 2f - (8 * 54 + 7 * 6) / 2f;
        kit.Window.MouseMove(new Point(barLeft + 60 + 27, 770 - 98 + 27));
        kit.Play(0.05);
        kit.Capture(Shot("hud-tooltip"));
        Assert.True(c.Conditions.Has(ConditionKind.Frightened));
    }

    [AvaloniaFact]
    public void Character_sheet_explains_its_numbers()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        kit.Window.View.CharacterSheetOpen = true;
        kit.World.Hero.Character.AddModifier(StatisticKind.ArmorClass, new Modifier("Shield of Faith", 1, BonusType.Status));
        kit.World.Hero.Character.AddModifier(StatisticKind.ArmorClass, new Modifier("Bless", 1, BonusType.Status));
        kit.Play(0.2);
        // The AC line: the sheet starts at y=70; title, subtitle, attributes, then Defence: HP, AC.
        kit.Window.MouseMove(new Point(120, 70 + 26 + 17 + 22 + 48 + 18 + 17 - 4));
        kit.Play(0.05);
        kit.Capture(Shot("character-sheet"));
        Assert.True(kit.Window.View.CharacterSheetOpen);
    }

    [AvaloniaFact]
    public void Minimap_reveals_what_has_been_explored()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        var world = kit.World;
        var before = world.ExploredVersion;
        // Walk up the road a little: aim for the first shrine.
        var shrine = world.Zone.Shrines[0];
        world.MoveTo(shrine.At[0] + 0.5, shrine.At[1] + 1.5);
        kit.Play(8);
        Assert.True(world.ExploredVersion > before);
        kit.Capture(Shot("minimap"));
    }

    [AvaloniaTheory]
    [InlineData("gallows_road")]
    [InlineData("fort_gallowmere")]
    [InlineData("rotwater_fens")]
    [InlineData("drowned_chapel")]
    [InlineData("sallows_mire")]
    [InlineData("prior_ossuary")]
    public void Every_shrine_answers_to_a_click_anywhere_on_its_stone_circle(string zone)
    {
        var kit = TestKit.Open(zoneId: zone);
        var world = kit.World;
        var view = kit.Window.View;
        foreach (var shrine in world.Zone.Shrines)
        {
            TestHooks.Place(world, shrine.At[0] - 1.5, shrine.At[1] + 0.5);
            kit.Play(0.05);
            var area = view.ShrineArea(world, shrine);
            Assert.True(area.Width > 100 * view.Zoom, $"{shrine.Id}'s circle is only {area.Width} px across");
            foreach (var (fx, fy) in new[] { (0f, 0f), (0.4f, 0f), (-0.4f, 0f), (0f, 0.4f), (0f, -0.4f) })
            {
                var p = new Point(area.MidX + fx * area.Width, area.MidY + fy * area.Height);
                Assert.Same(shrine, view.ShrineUnder(p));
            }
        }
    }

    [AvaloniaFact]
    public void Clicking_the_east_side_of_a_woken_shrine_opens_the_rift()
    {
        // The grassland shrine's art lies east of its cell: the click area once sat west of the glowing ring and a
        // click on the ring's east half walked the hero there instead.
        var kit = TestKit.Open();
        var world = kit.World;
        var view = kit.Window.View;
        var shrine = world.Zone.Shrines[0];
        TestHooks.Place(world, shrine.At[0] + 0.5, shrine.At[1] + 1.2);
        kit.PlayUntil(() => world.ActivatedShrines.Contains(shrine.Id), 5);
        var area = view.ShrineArea(world, shrine);
        var anchor = view.ToScreen(shrine.At[0] + 0.5, shrine.At[1] + 0.5);
        var click = new Point(area.MidX + area.Width * 0.38, area.MidY);
        Assert.True(click.X > anchor.X + 60 * view.Zoom, "the probe should be well east of the shrine's cell");
        kit.Click(click);
        kit.Play(0.1);
        Assert.Same(shrine, world.OpenShrine);
        kit.Capture(Shot("rift-east-click"));
    }

    [AvaloniaFact]
    public void Target_frame_shows_a_burning_monster()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        zombie.Creature.Persistent.Add(DamageType.Fire, new Dice(1, 4));
        zombie.Creature.Conditions.Apply(ConditionKind.Frightened, 1);
        world.Attack(zombie);
        kit.Play(0.2);
        kit.Window.MouseMove(new Point(1288 / 2f, 26));
        kit.Play(0.05);
        kit.Capture(Shot("target-frame"));
        Assert.Contains(DamageType.Fire, zombie.Creature.Persistent.Types);
    }

    [Fact]
    public void The_town_banner_fades_in_holds_and_fades_out()
    {
        Assert.Equal(0, AshenReliquary.App.Rendering.TownBanner.Alpha(0));
        Assert.Equal(0.5, AshenReliquary.App.Rendering.TownBanner.Alpha(0.5), 6);
        Assert.Equal(1, AshenReliquary.App.Rendering.TownBanner.Alpha(3));
        Assert.InRange(AshenReliquary.App.Rendering.TownBanner.Alpha(5.75), 0.4, 0.6);
        Assert.Equal(0, AshenReliquary.App.Rendering.TownBanner.Alpha(7));
    }

    [AvaloniaFact]
    public void A_cooling_ability_shows_a_clock_sweep_and_flashes_as_it_comes_ready()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        var world = kit.World;
        var view = kit.Window.View;
        world.Hero.Character.TakeDamage(8);
        Assert.True(world.DrinkPotion());
        Assert.True(world.Dodge(world.Hero.X + 3, world.Hero.Y));
        TestHooks.StartCooldown(world, "sudden_charge", 8);
        kit.Play(3);                                            // 5 s left on the charge: the dark pie covers 5/8
        kit.Capture(Shot("hud-cooldown-sweep"));
        Assert.Equal("5", Rendering.Hud.SweepText(world.CooldownRemaining(world.Bar["1"]) + 0.01));
        Assert.Equal("2.4", Rendering.Hud.SweepText(2.4));
        Assert.Equal(0, view.ReadyFlash("1"));

        kit.Play(5.05);                                         // ready: the frame flares, then fades within a quarter second
        Assert.Equal(0, world.CooldownRemaining(world.Bar["1"]));
        Assert.True(view.ReadyFlash("1") > 0, "the slot should flash as it comes ready");
        kit.Capture(Shot("hud-cooldown-ready"));
        kit.Play(0.4);
        Assert.Equal(0, view.ReadyFlash("1"));
    }

    [AvaloniaFact]
    public void In_a_narrow_window_the_potions_and_step_sit_above_the_orb_clear_of_the_bar()
    {
        var kit = TestKit.Open();
        kit.Window.MinWidth = 0;
        kit.Window.Width = 731;
        kit.Window.Height = 946;
        var world = kit.World;
        Assert.True(world.Dodge(world.Hero.X + 3, world.Hero.Y));
        kit.Play(1);
        kit.Capture(Shot("hud-narrow-step"));
        var barTop = kit.Window.View.BarSlot("lmb").Y - 27;
        Assert.True(kit.Window.View.HudRowTop + 68 <= barTop - 40, $"the row ({kit.Window.View.HudRowTop}) should end above the bar ({barTop})");

        var wide = TestKit.Open(width: 1288, height: 770);
        wide.Play(0.1);
        Assert.Equal(770 - 68, wide.Window.View.HudRowTop);   // a wide window keeps them beside the orb
    }
}
