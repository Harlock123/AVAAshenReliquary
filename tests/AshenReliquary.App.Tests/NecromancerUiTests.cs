using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AshenReliquary.App.Rendering;
using AshenReliquary.App.Views;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Items;

namespace AshenReliquary.App.Tests;

/// <summary>M3 step 5 on screen: the Necromancer at creation, its Grave Charges and skeletons in a fight.</summary>
public class NecromancerUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void The_necromancer_can_be_made_at_creation_with_either_subclass()
    {
        var window = new MainWindow { Width = 1312, Height = 824 };
        var vm = window.ShowCharacterCreation();
        window.Show();
        var kit = new TestKitLite(window);
        kit.Pump();
        vm.Choose("human", "gravedigger", "necromancer", "plaguebringer");
        vm.RecommendCommand.Execute(null);
        kit.Pump();
        Assert.Equal(["bonelord", "plaguebringer"], vm.Subclasses.Select(s => s.Id));
        Assert.True(vm.CanBegin, vm.Problem);
        Assert.Contains(vm.Preview, p => p == "Grave Charges: up to 5");
        Assert.Contains(vm.Preview, p => p == "Skeletons: 2 at once");
        Assert.Contains(vm.Abilities, a => a.StartsWith("Plaguebringer  Corpse Explosion"));
        kit.Capture(Shot("character-creation-necromancer"));
        vm.BeginCommand.Execute(null);
        kit.Pump();
        var world = window.Session!.World;
        Assert.Equal(ResourceKind.Mana, world.Hero.Resource.Kind);
        Assert.Equal("staff", world.Hero.Equipment.In(EquipSlot.MainHand)?.Definition.Id);
        Assert.Equal(["grave_bolt", "raise_bones", "siphon_life", "corpse_explosion"], world.Bar.Values.Select(a => a.Id));
    }

    [AvaloniaFact]
    public void Quick_start_flags_name_the_necromancer_and_its_subclasses()
    {
        Assert.Equal(("necromancer", "bonelord"), GameSession.QuickStart(["--necromancer"]));
        Assert.Equal(("necromancer", "plaguebringer"), GameSession.QuickStart(["--plaguebringer", "--mute"]));
        Assert.Equal(("necromancer", "bonelord"), GameSession.QuickStart(["--bonelord"]));
    }

    [AvaloniaFact]
    public void A_bonelord_raises_skeletons_with_a_right_click_and_they_fight_a_fen_zombie()
    {
        var kit = TestKit.Open("necromancer", zoneId: "rotwater_fens", subclass: "bonelord");
        var world = kit.World;
        world.Hero.Character.CannotFall = true;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 5, zombie.Y);
        kit.Play(0.2);
        var view = kit.Window.View;
        for (var i = 0; i < 3; i++)
        {
            var me = view.ToScreen(world.Hero.X, world.Hero.Y + 2);
            kit.Window.MouseDown(new Avalonia.Point(me.X, me.Y), MouseButton.Right);
            kit.Window.MouseUp(new Avalonia.Point(me.X, me.Y), MouseButton.Right);
            kit.Play(1.2);
        }
        Assert.True(world.Minions.Count(m => !m.IsDead) >= 2, "skeletons stand (the Fens may already have struck one down)");
        var tip = Hud.AbilityTip(world, world.Bar["rmb"], "RMB").Select(l => l.Text).ToList();
        Assert.Contains(tip, t => t.Contains("of 3 skeletons"));
        world.Attack(zombie);
        kit.Play(1.5);
        kit.Capture(Shot("necromancer-skeletons"));
        kit.PlayUntil(() => zombie.IsDead, 20);
        Assert.True(zombie.IsDead);
        Assert.True(world.GraveCharges >= 1);
        kit.Play(0.3);
        kit.Capture(Shot("necromancer-charges"));
    }
}
