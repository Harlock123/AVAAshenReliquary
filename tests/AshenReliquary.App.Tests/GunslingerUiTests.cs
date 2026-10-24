using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AshenReliquary.App.Views;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Items;

namespace AshenReliquary.App.Tests;

/// <summary>The Gunslinger on screen: at creation, its guns in hand, shots and the load on the HUD, R, and Ironhold's gunsmith.</summary>
public class GunslingerUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void The_gunslinger_can_be_made_at_creation_as_a_sniper()
    {
        var window = new MainWindow { Width = 1312, Height = 824 };
        var vm = window.ShowCharacterCreation();
        window.Show();
        var kit = new TestKitLite(window);
        kit.Pump();
        Assert.Contains(vm.Classes, c => c.Name == "Gunslinger" && c.Playable);
        vm.Choose("human", "ironhold-gunhand", "gunslinger", "sniper");
        vm.RecommendCommand.Execute(null);
        kit.Pump();
        Assert.Equal(["pistolero", "sniper"], vm.Subclasses.Select(s => s.Id));
        Assert.True(vm.CanBegin, vm.Problem);
        Assert.Contains(vm.Abilities, a => a.StartsWith("Sniper  Headshot"));
        kit.Capture(Shot("character-creation-gunslinger"));
        vm.BeginCommand.Execute(null);
        kit.Pump();
        var world = window.Session!.World;
        Assert.Equal(ResourceKind.Grit, world.Hero.Resource.Kind);
        Assert.Equal("arquebus", world.Hero.Equipment.In(EquipSlot.MainHand)?.Definition.Id);
        Assert.Equal("long_gun", window.Session.AppearanceLayers["main"]);
    }

    [AvaloniaFact]
    public void Quick_start_flags_name_the_gunslinger_and_twin_pistols_are_drawn_in_both_hands()
    {
        Assert.Equal(("gunslinger", "pistolero"), GameSession.QuickStart(["--gunslinger"]));
        Assert.Equal(("gunslinger", "sniper"), GameSession.QuickStart(["--sniper", "--mute"]));
        var session = GameSession.NewGame("gunslinger", 1, subclass: "pistolero");
        Assert.Equal("pistol", session.AppearanceLayers["main"]);
        Assert.Equal("pistol_offhand", session.AppearanceLayers["off"]);
    }

    [AvaloniaFact]
    public void A_pistolero_shoots_a_fen_zombie_and_r_loads_early()
    {
        var kit = TestKit.Open("gunslinger", zoneId: "rotwater_fens", subclass: "pistolero");
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 5, zombie.Y);
        world.Attack(zombie);
        var flew = false;
        for (var i = 0; i < 60 && !flew; i++)
        {
            kit.Play(1 / 30.0);
            flew = world.Projectiles.Any(p => p.Shot is { Bullet: true });
        }
        Assert.True(flew, "no shot flew");
        kit.Capture(Shot("gunslinger-shot"));
        world.Stop();
        kit.Play(1.0);
        if (world.Hero.Loaded < world.Capacity && world.ReloadProgress is null)
        {
            kit.Window.KeyPress(Key.R, RawInputModifiers.None, PhysicalKey.R, "r");
            kit.Window.KeyRelease(Key.R, RawInputModifiers.None, PhysicalKey.R, "r");
            kit.Play(0.05);
        }
        Assert.True(world.ReloadProgress is not null || world.Hero.Loaded == world.Capacity);
        kit.Capture(Shot("gunslinger-reload"));
    }

    [AvaloniaFact]
    public void Ironhold_s_gunsmith_sells_guns_rounds_and_materials()
    {
        var kit = TestKit.Open("gunslinger", zoneId: "ironhold", subclass: "sniper");
        var world = kit.World;
        var smith = world.Vendors.Single(v => v.Definition.Id == "ottilie");
        world.Hero.Equipment.AddGold(500);
        world.TalkTo(smith);
        for (var i = 0; i < 300 && world.OpenVendor is null; i++)
            kit.Play(1 / 30.0);
        Assert.Same(smith, world.OpenVendor);
        Assert.All(smith.Stock, i => Assert.True(i.Definition.IsFirearm));
        Assert.Contains(world.Supplies, s => s.Id == "black_powder");
        Assert.True(world.AtForge);
        Assert.True(world.BuySupply(world.Supplies.First(s => s.Id == "incendiary_round")));
        kit.Play(0.1);
        kit.Capture(Shot("gunslinger-gunsmith"));
    }
}
