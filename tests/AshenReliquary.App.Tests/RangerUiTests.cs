using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AshenReliquary.App.Views;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>M3 step 3 on screen: the Ranger at creation, its bow and arrows, Hunt Prey's mark and the Beastmaster's wolf.</summary>
public class RangerUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void The_ranger_can_be_made_at_creation_and_a_beastmaster_starts_with_a_wolf()
    {
        var window = new MainWindow { Width = 1312, Height = 824 };
        var vm = window.ShowCharacterCreation();
        window.Show();
        var kit = new TestKitLite(window);
        kit.Pump();
        vm.Choose("human", "hunter", "ranger", "beastmaster");
        vm.RecommendCommand.Execute(null);
        kit.Pump();
        Assert.Equal(["marksman", "beastmaster"], vm.Subclasses.Select(s => s.Id));
        Assert.True(vm.CanBegin, vm.Problem);
        Assert.Contains(vm.Preview, p => p == "Hunt Prey: 1d8");
        Assert.Contains(vm.Preview, p => p == "Companion: a wolf");
        Assert.Contains(vm.Abilities, a => a.StartsWith("Beastmaster  Sic 'Em"));
        kit.Capture(Shot("character-creation-ranger"));
        vm.BeginCommand.Execute(null);
        kit.Pump();
        var world = window.Session!.World;
        Assert.Equal(ResourceKind.Focus, world.Hero.Resource.Kind);
        Assert.Equal("shortbow", world.Hero.Equipment.In(EquipSlot.MainHand)?.Definition.Id);
        Assert.Equal("wolf", world.Companion?.Definition.Id);
        Assert.Equal("off", window.Session.AppearanceLayers.First(kv => kv.Value == "shortbow").Key); // drawn in the off hand
    }

    [AvaloniaFact]
    public void Quick_start_flags_name_the_ranger_and_its_subclasses()
    {
        Assert.Equal(("ranger", "marksman"), GameSession.QuickStart(["--ranger"]));
        Assert.Equal(("ranger", "beastmaster"), GameSession.QuickStart(["--beastmaster"]));
        Assert.Equal(("ranger", "marksman"), GameSession.QuickStart(["--marksman"]));
    }

    [AvaloniaFact]
    public void A_ranger_hunts_and_shoots_a_fen_zombie_from_range_with_its_wolf_beside_it()
    {
        var kit = TestKit.Open("ranger", zoneId: "rotwater_fens", subclass: "beastmaster");
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 6, zombie.Y);
        kit.Play(0.2);
        var foot = kit.Window.View.ToScreen(zombie.X, zombie.Y);
        kit.Window.MouseMove(new Point(foot.X, foot.Y - 60));
        kit.Window.KeyPress(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, "1");
        kit.Play(0.1);
        Assert.Equal(zombie, world.Hero.Prey);
        world.Attack(zombie);
        kit.PlayUntil(() => world.Projectiles.Any(p => p.IsArrow), 3);
        Assert.Contains(world.Projectiles, p => p.IsArrow);
        kit.Play(0.1);
        kit.Capture(Shot("ranger-arrow"));
        kit.Play(3);
        Assert.True(zombie.IsDead || zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints);
        kit.Capture(Shot("ranger-wolf"));
    }
}
