using Avalonia.Headless.XUnit;
using AshenReliquary.App.Views;
using AshenReliquary.Core.Items;

namespace AshenReliquary.App.Tests;

/// <summary>
/// M4 step 10: beginning at a later act, from the creation screen's "Begin at" button or "--act N" (for the exit test's
/// play of Acts 2-4).
/// </summary>
public class ActStartUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void A_hero_made_at_creation_can_begin_at_act_three()
    {
        var window = new MainWindow { Width = 1312, Height = 824 };
        var vm = window.ShowCharacterCreation();
        window.Show();
        var kit = new TestKitLite(window);
        kit.Pump();
        vm.Choose("human", "ironhold-gunhand", "gunslinger", "pistolero");
        vm.RecommendCommand.Execute(null);
        Assert.Equal(1, vm.StartAct);
        vm.NextStartActCommand.Execute(null);
        vm.NextStartActCommand.Execute(null);
        Assert.Equal(3, vm.StartAct);
        Assert.Contains("Act 3", vm.StartActLabel);
        kit.Pump();
        kit.Capture(Shot("character-creation-act-three"));
        vm.BeginCommand.Execute(null);
        kit.Pump();
        var world = window.Session!.World;
        Assert.Equal("frostbitten_pass", world.Zone.Id);
        Assert.Equal(11, world.Hero.Character.Level);
        Assert.Equal("slain", world.Flag("act2.hadric"));
        Assert.Equal(11, world.Hero.Equipment.In(EquipSlot.MainHand)!.ItemLevel);
        vm.NextStartActCommand.Execute(null);
        vm.NextStartActCommand.Execute(null);
        Assert.Equal(1, vm.StartAct);                                              // round again
    }

    [AvaloniaFact]
    public void The_act_flag_starts_a_quick_start_at_that_act()
    {
        var session = GameSession.NewGame("ranger", 1, subclass: "marksman", act: 4);
        Assert.Equal("ember_gate", session.World.Zone.Id);
        Assert.Equal(15, session.World.Hero.Character.Level);
        Assert.Contains("cloister_shrine", session.World.ActivatedShrines);
    }
}
