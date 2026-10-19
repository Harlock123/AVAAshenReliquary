using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>The arsenal on screen: the belt and its keys, the off hand, the trader's supplies, hit-stop, the ossuary.</summary>
public class ArsenalUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void A_new_fighter_carries_a_shield_and_a_belt_of_bombs()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        Assert.Equal("wooden_shield", world.Hero.Equipment.In(EquipSlot.OffHand)?.Definition.Id);
        kit.Play(0.2);
        Assert.Equal(["alchemists_fire", "antidote", null, null], world.Belt.Select(i => i?.Id));
        Assert.Equal(3, world.BeltCount(0));
        // A quick start is a Bulwark, whose Challenge joins the class's bar.
        Assert.Equal(["challenge", "cleave", "power_attack", "raise_shield", "strike", "sudden_charge"], world.Bar.Values.Select(a => a.Id).Order());
        kit.Capture(Shot("belt"));
    }

    [AvaloniaFact]
    public void Key_seven_throws_the_first_belt_bomb_at_the_monster_under_the_pointer()
    {
        var kit = TestKit.Open(zoneId: "rotwater_fens");
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 3, zombie.Y);
        kit.Play(0.1);
        var foot = kit.Window.View.ToScreen(zombie.X, zombie.Y);
        kit.Window.MouseMove(new Point(foot.X, foot.Y - 60));
        kit.Window.KeyPress(Key.D7, RawInputModifiers.None, PhysicalKey.Digit7, "7");
        kit.Play(1.0);
        Assert.Equal(2, world.BeltCount(0));
        kit.Capture(Shot("bomb"));
    }

    [AvaloniaFact]
    public void Number_three_raises_the_shield_and_the_hero_holds_it_up()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        var ac = world.Hero.Character.ArmorClass().Total;
        kit.Window.KeyPress(Key.D3, RawInputModifiers.None, PhysicalKey.Digit3, "3");
        kit.Play(0.3);
        Assert.NotNull(world.Hero.Ward);
        Assert.Equal(ac + 2, world.Hero.Character.ArmorClass().Total);
        Assert.Equal("block", world.Hero.Animation);
        kit.Capture(Shot("shield-raised"));
    }

    [AvaloniaFact]
    public void The_inventory_shows_the_shield_in_the_off_hand_and_stacks_by_count()
    {
        var kit = TestKit.Open(width: 1440, height: 900);
        kit.Window.View.InventoryOpen = true;
        kit.Play(0.1);
        kit.Capture(Shot("inventory-arsenal"));
        var gear = kit.World.Hero.Equipment;
        var shield = gear.In(EquipSlot.OffHand)!;
        Assert.True(gear.Unequip(EquipSlot.OffHand));
        Assert.Contains(shield, gear.Bag);
        Assert.Equal(3, gear.Bag[0]!.Count);
    }

    [AvaloniaFact]
    public void Wennas_supplies_are_bought_with_a_click()
    {
        var kit = TestKit.Open(seed: 21, width: 1312, height: 900, zoneId: "fort_gallowmere");
        var world = kit.World;
        var wenna = world.Vendors.Single(v => v.Definition.Id == "wenna");
        TestHooks.Place(world, wenna.X + 1, wenna.Y + 1);
        world.TalkTo(wenna);
        kit.PlayUntil(() => world.OpenVendor is not null, 6);
        kit.Play(0.1);
        world.Hero.Equipment.AddGold(200);
        var frost = world.Supplies.Single(s => s.Id == "frost_vial");
        var at = kit.Window.View.SupplyCell(world.Supplies.ToList().IndexOf(frost));
        kit.Window.MouseMove(at);
        kit.Play(0.1);
        kit.Capture(Shot("supplies"));
        kit.Click(at);
        Assert.Equal(1, world.Hero.Equipment.CountOf(frost));
        kit.Play(0.1);
        Assert.Contains(world.Belt, i => i == frost);
    }

    [AvaloniaFact]
    public void A_critical_hit_stops_the_world_for_a_moment()
    {
        var kit = TestKit.Open(zoneId: "rotwater_fens");
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 1, zombie.Y);
        var crits = world.HeroCrits;
        for (var i = 0; i < 30 * 40 && world.HeroCrits == crits; i++)
        {
            world.Hero.Character.Heal(999);
            if (world.Hero.CurrentTarget is null || world.Hero.CurrentTarget.IsDead)
                world.Attack(world.Monsters.Where(m => !m.IsDead).MinBy(m => m.DistanceTo(world.Hero))!);
            kit.Window.Tick(TimeSpan.FromSeconds(1 / 30.0));
        }
        Assert.True(world.HeroCrits > crits, "forty seconds of blows and never a critical hit");
        var time = world.Time;
        kit.Window.Tick(TimeSpan.FromSeconds(1 / 60.0)); // the tick that notices the crit holds the world still
        Assert.Equal(time, world.Time);
        kit.Window.Tick(TimeSpan.FromSeconds(MainWindowHitStop));
        kit.Window.Tick(TimeSpan.FromSeconds(1 / 30.0));
        Assert.True(world.Time > time);
    }

    private const double MainWindowHitStop = Views.MainWindow.HitStopSeconds;

    [AvaloniaFact]
    public void The_ossuary_renders_with_prior_aldous_in_his_tomb()
    {
        var kit = TestKit.Open(zoneId: "prior_ossuary");
        var world = kit.World;
        var prior = world.Monsters.Single(m => m.Definition.Id == "drowned_prior");
        kit.Play(0.3);
        kit.Capture(Shot("ossuary-stair"));
        TestHooks.Place(world, prior.X, prior.Y - 5);
        world.Hero.Character.Heal(999);
        kit.Play(1.5);
        kit.Capture(Shot("ossuary-prior"));
        Assert.Contains(world.Monsters, m => m.Ward > 0);
    }

    [AvaloniaFact]
    public void A_stormcallers_thunderclap_spreads_a_ring_on_the_ground()
    {
        var kit = TestKit.Open("wizard", zoneId: "rotwater_fens", subclass: "stormcaller");
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 1.5, zombie.Y);
        kit.Play(0.1);
        world.UseSlot(world.Bar.Single(kv => kv.Value.Id == "thunderclap").Key, null);
        kit.Window.Tick(TimeSpan.FromSeconds(0.1));
        Assert.Single(world.Bursts);
        kit.Capture(Shot("thunderclap"));
    }
}
