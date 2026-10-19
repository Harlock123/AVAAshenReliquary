using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AshenReliquary.App.Views;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Items;

namespace AshenReliquary.App.Tests;

/// <summary>M3 step 2 on screen: the Rogue at creation and in play, and the Abilities panel (K) that chooses the bar.</summary>
public class RogueUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static void Key(TestKit kit, Key key, PhysicalKey physical, string text)
    {
        kit.Window.KeyPress(key, RawInputModifiers.None, physical, text);
        kit.Window.KeyRelease(key, RawInputModifiers.None, physical, text);
        kit.Play(0.05);
    }

    [AvaloniaFact]
    public void The_rogue_can_be_made_at_creation_with_either_subclass()
    {
        var window = new MainWindow { Width = 1312, Height = 824 };
        var vm = window.ShowCharacterCreation();
        window.Show();
        var kit = new TestKitLite(window);
        kit.Pump();
        vm.Choose("human", "street-urchin", "rogue", "trickster");
        vm.RecommendCommand.Execute(null);
        kit.Pump();
        Assert.Equal(["cutthroat", "trickster"], vm.Subclasses.Select(s => s.Id));
        Assert.True(vm.CanBegin, vm.Problem);
        Assert.Contains(vm.Abilities, a => a.StartsWith("Trickster  Snare"));
        Assert.Contains(vm.Preview, p => p == "Sneak attack: 1d6");
        kit.Capture(Shot("character-creation-rogue"));
        vm.BeginCommand.Execute(null);
        kit.Pump();
        var world = window.Session!.World;
        Assert.Equal("trickster", world.Hero.Character.Subclass?.Id);
        Assert.Equal(ResourceKind.Momentum, world.Hero.Resource.Kind);
        Assert.Equal("shortsword", world.Hero.Equipment.In(EquipSlot.MainHand)?.Definition.Id);
        Assert.Equal(["rogue_strike", "sneak_strike", "tumble", "hide", "snare"], world.Bar.Values.Select(a => a.Id));
    }

    [AvaloniaFact]
    public void Quick_start_flags_name_the_rogue_and_its_subclasses()
    {
        Assert.Equal(("rogue", "cutthroat"), GameSession.QuickStart(["--rogue"]));
        Assert.Equal(("rogue", "trickster"), GameSession.QuickStart(["--trickster", "--mute"]));
        Assert.Equal(("rogue", "cutthroat"), GameSession.QuickStart(["--cutthroat"]));
        Assert.Equal(("fighter", "bulwark"), GameSession.QuickStart(["--fighter"]));
    }

    [AvaloniaFact]
    public void The_abilities_panel_moves_an_ability_by_click_by_key_and_by_drag_and_empties_a_slot()
    {
        var kit = TestKit.Open("rogue", subclass: "trickster");
        var world = kit.World;
        var view = kit.Window.View;
        Assert.Equal("snare", world.Bar["3"].Id);
        Key(kit, Avalonia.Input.Key.K, PhysicalKey.K, "k");
        Assert.True(view.AbilitiesOpen);
        kit.Play(0.05);

        // Click Snare's row, then slot 6.
        kit.Click(view.AbilityRow("snare"));
        Assert.Equal("snare", view.Abilities.Held);
        kit.Click(view.BarSlot("6"));
        Assert.Equal("snare", world.Bar["6"].Id);
        Assert.False(world.Bar.ContainsKey("3"));
        Assert.Null(view.Abilities.Held);
        kit.Capture(Shot("abilities-panel"));

        // A right click on a slot empties it: Snare waits in the list.
        kit.Window.MouseDown(view.BarSlot("6"), MouseButton.Right);
        kit.Window.MouseUp(view.BarSlot("6"), MouseButton.Right);
        kit.Play(0.05);
        Assert.Null(world.SlotOf("snare"));

        // Hover its row and press 5.
        kit.Window.MouseMove(view.AbilityRow("snare"));
        Key(kit, Avalonia.Input.Key.D5, PhysicalKey.Digit5, "5");
        Assert.Equal("snare", world.Bar["5"].Id);

        // Drag Hide's row onto slot 1: Tumble goes back to Hide's 2.
        kit.Window.MouseDown(view.AbilityRow("hide"), MouseButton.Left);
        kit.Window.MouseMove(view.BarSlot("1"));
        kit.Window.MouseUp(view.BarSlot("1"), MouseButton.Left);
        kit.Play(0.05);
        Assert.Equal("hide", world.Bar["1"].Id);
        Assert.Equal("tumble", world.Bar["2"].Id);

        Key(kit, Avalonia.Input.Key.Escape, PhysicalKey.Escape, "");
        Assert.False(view.AbilitiesOpen);
        // Closed, the keys use the bar again: 1 is now Hide.
        Key(kit, Avalonia.Input.Key.D1, PhysicalKey.Digit1, "1");
        Assert.True(world.Hero.IsHidden);
    }

    [AvaloniaFact]
    public void A_hidden_rogue_is_drawn_dim_and_tumbles_toward_the_pointer()
    {
        var kit = TestKit.Open("rogue");
        var world = kit.World;
        var view = kit.Window.View;
        Key(kit, Avalonia.Input.Key.D2, PhysicalKey.Digit2, "2");
        Assert.True(world.Hero.IsHidden);
        kit.Capture(Shot("rogue-hidden"));
        var (x, y) = (world.Hero.X, world.Hero.Y);
        var aim = view.ToScreen(x, y + 3);
        kit.Window.MouseMove(new Point(aim.X, aim.Y));
        Key(kit, Avalonia.Input.Key.D1, PhysicalKey.Digit1, "1");
        kit.Play(0.5);
        Assert.True(world.Hero.Y - y > 2.5, $"moved {world.Hero.Y - y:0.00}");
        Assert.InRange(world.Hero.X - x, -0.3, 0.3);
    }
}
