using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace AshenReliquary.App.Tests;

/// <summary>Ctrl held (asked for after M3): a click attacks the monster nearest the pointer and never walks the hero there.</summary>
public class TargetingTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void A_ctrl_click_beside_a_monster_attacks_it_and_on_bare_ground_stays_put()
    {
        var kit = TestKit.Open("necromancer", zoneId: "rotwater_fens");
        var world = kit.World;
        var view = kit.Window.View;
        world.Hero.Character.CannotFall = true;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 6, zombie.Y);
        kit.Play(0.2);

        // Two tiles beside the zombie, on open ground: a plain click would walk there.
        var beside = view.ToScreen(zombie.X, zombie.Y + 2);
        kit.Window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft, "");
        kit.Window.MouseMove(new Point(beside.X, beside.Y), RawInputModifiers.Control);
        kit.Play(0.05);
        Assert.True(view.AttackOnly);
        Assert.NotNull(view.AttackTarget(new Point(beside.X, beside.Y)));            // the pack's nearest to the pointer
        kit.Capture(Shot("ctrl-target-ring"));
        kit.Window.MouseDown(new Point(beside.X, beside.Y), MouseButton.Left, RawInputModifiers.Control);
        kit.Window.MouseUp(new Point(beside.X, beside.Y), MouseButton.Left, RawInputModifiers.Control);
        kit.Play(0.1);
        var pointed = view.ToMap(new Point(beside.X, beside.Y));
        Assert.NotNull(world.Hero.CurrentTarget);
        Assert.InRange(world.Hero.CurrentTarget!.DistanceTo(pointed.X, pointed.Y), 0, Rendering.WorldView.AttackPickTiles + 1);

        // Far from every monster: the hero neither walks nor attacks.
        world.Stop();
        var (x, y) = (world.Hero.X, world.Hero.Y);
        var empty = view.ToScreen(x - 3, y - 3);
        if (view.AttackTarget(new Point(empty.X, empty.Y)) is null)
        {
            kit.Window.MouseDown(new Point(empty.X, empty.Y), MouseButton.Left, RawInputModifiers.Control);
            kit.Window.MouseUp(new Point(empty.X, empty.Y), MouseButton.Left, RawInputModifiers.Control);
            kit.Play(0.5);
            Assert.InRange(world.Hero.DistanceTo(x, y), 0, 0.2);
        }
        kit.Window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.ControlLeft, "");
        kit.Play(0.05);
        Assert.False(view.AttackOnly);
    }

    [AvaloniaFact]
    public void Holding_ctrl_and_the_button_moves_on_to_the_next_monster_when_one_falls()
    {
        var kit = TestKit.Open("necromancer", zoneId: "rotwater_fens");
        var world = kit.World;
        var view = kit.Window.View;
        world.Hero.Character.CannotFall = true;
        var zombies = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").OrderBy(m => m.DistanceTo(world.Hero)).Take(2).ToList();
        TestHooks.Place(world, zombies[0].X - 5, zombies[0].Y);
        kit.Play(0.2);
        var at = view.ToScreen(zombies[0].X, zombies[0].Y);
        kit.Window.KeyPress(Key.LeftCtrl, RawInputModifiers.Control, PhysicalKey.ControlLeft, "");
        kit.Window.MouseMove(new Point(at.X, at.Y), RawInputModifiers.Control);
        kit.Window.MouseDown(new Point(at.X, at.Y), MouseButton.Left, RawInputModifiers.Control);
        kit.Play(0.1);
        var first = world.Hero.CurrentTarget!;
        TestHooks.Slay(world, first);
        kit.Play(0.3);
        // Still held: the next monster nearest the pointer (if any is within reach of it) is attacked.
        var next = view.AttackTarget(new Point(at.X, at.Y));
        Assert.Equal(next, world.Hero.CurrentTarget);
        kit.Window.MouseUp(new Point(at.X, at.Y), MouseButton.Left, RawInputModifiers.Control);
        kit.Window.KeyRelease(Key.LeftCtrl, RawInputModifiers.None, PhysicalKey.ControlLeft, "");
    }
}
