using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace AshenReliquary.App.Tests;

/// <summary>The bar's right-click menu: right-click any slot and choose what it does, even an ability already on another key.</summary>
public class BindMenuTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static void RightClick(TestKit kit, Avalonia.Point at)
    {
        kit.Window.MouseDown(at, MouseButton.Right);
        kit.Window.MouseUp(at, MouseButton.Right);
        kit.Play(0.05);
    }

    [AvaloniaFact]
    public void Right_clicking_a_slot_lists_every_ability_and_binds_the_choice_there_too()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        var world = kit.World;
        var view = kit.Window.View;
        kit.Play(0.2);
        var onOne = world.Bar["1"].Id;
        var before = world.Hero.X;

        RightClick(kit, view.BarSlot("rmb"));
        Assert.Equal("rmb", view.BindMenuSlot);
        Assert.Equal(before, world.Hero.X);                                    // no Power Attack, no walk
        kit.Window.MouseMove(view.BindMenuRow(onOne));
        kit.Play(0.05);
        kit.Capture(Shot("bind-menu"));

        kit.Click(view.BindMenuRow(onOne));
        Assert.Null(view.BindMenuSlot);
        Assert.Equal(onOne, world.Bar["rmb"].Id);
        Assert.Equal(onOne, world.Bar["1"].Id);                                // still on key 1 as well

        // Escape closes it without a change; "Empty this slot" clears the slot.
        RightClick(kit, view.BarSlot("3"));
        Assert.Equal("3", view.BindMenuSlot);
        kit.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
        kit.Play(0.05);
        Assert.Null(view.BindMenuSlot);
        Assert.False(kit.Window.GameMenu.IsVisible);

        RightClick(kit, view.BarSlot("3"));
        kit.Click(view.BindMenuRow(""));
        Assert.False(world.Bar.ContainsKey("3"));

        // A right click on another slot moves the menu there.
        RightClick(kit, view.BarSlot("lmb"));
        RightClick(kit, view.BarSlot("2"));
        Assert.Equal("2", view.BindMenuSlot);
    }
}
