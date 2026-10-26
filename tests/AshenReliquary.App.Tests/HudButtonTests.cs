using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

namespace AshenReliquary.App.Tests;

/// <summary>A click on the HUD's buttons uses them as their keys would: the bar's slots, the belt and the flasks.</summary>
public class HudButtonTests
{
    [AvaloniaFact]
    public void A_click_on_a_bar_slot_uses_its_ability()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        kit.Play(0.1);
        kit.Click(kit.Window.View.BarSlot("3"));                        // the fighter's Raise a Shield
        kit.Play(0.3);
        Assert.NotNull(world.Hero.Ward);
    }

    [AvaloniaFact]
    public void A_click_on_the_bar_or_the_belt_aims_at_the_nearest_monster_in_sight()
    {
        var kit = TestKit.Open(zoneId: "rotwater_fens");
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 3, zombie.Y);
        kit.Play(0.1);
        var view = kit.Window.View;
        kit.Click(view.BeltSlot(0));                                    // alchemist's fire
        kit.Play(1.0);
        Assert.Equal(2, world.BeltCount(0));
        // By now the pack is on the hero: the nearest of it is struck.
        var nearest = world.NearestFoe(Rendering.WorldView.AimTiles);
        Assert.NotNull(nearest);
        kit.Click(view.BarSlot("lmb"));
        Assert.Same(nearest, world.Hero.CurrentTarget);
    }

    [AvaloniaFact]
    public void A_click_on_the_flasks_drinks_one()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        var c = world.Hero.Character;
        c.TakeDamage(c.MaxHitPoints / 2);
        kit.Play(0.1);
        var before = c.Potion.Charges;
        kit.Click(kit.Window.View.Flasks);
        kit.Play(0.1);
        Assert.Equal(before - 1, c.Potion.Charges);
    }
}
