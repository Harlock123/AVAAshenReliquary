using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AshenReliquary.App.Rendering;
using AshenReliquary.App.Views;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Items;

namespace AshenReliquary.App.Tests;

/// <summary>M3 step 4 on screen: the Champion at creation, its Devotion orb, and its auras drawn round it in a fight.</summary>
public class ChampionUiTests
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
    public void The_champion_can_be_made_at_creation_with_either_subclass()
    {
        var window = new MainWindow { Width = 1312, Height = 824 };
        var vm = window.ShowCharacterCreation();
        window.Show();
        var kit = new TestKitLite(window);
        kit.Pump();
        vm.Choose("human", "lantern-acolyte", "champion", "lanternbearer");
        vm.RecommendCommand.Execute(null);
        kit.Pump();
        Assert.Equal(["lanternbearer", "oathbreaker", "paladin"], vm.Subclasses.Select(s => s.Id));
        Assert.Contains(vm.Classes, c => c.Name == "Champion (paladin)");    // so a player looking for the paladin finds it
        Assert.True(vm.CanBegin, vm.Problem);
        Assert.Contains(vm.Preview, p => p == "Undead bane: +1 vitality");
        Assert.Contains(vm.Abilities, a => a.StartsWith("Lanternbearer  Lantern's Radiance"));
        kit.Capture(Shot("character-creation-champion"));
        vm.BeginCommand.Execute(null);
        kit.Pump();
        var world = window.Session!.World;
        Assert.Equal(ResourceKind.Devotion, world.Hero.Resource.Kind);
        Assert.Equal("wooden_shield", world.Hero.Equipment.In(EquipSlot.OffHand)?.Definition.Id);
        Assert.Equal(["champion_strike", "smite", "lay_on_hands", "shield_of_faith", "lanterns_radiance"], world.Bar.Values.Select(a => a.Id));
    }

    [AvaloniaFact]
    public void Quick_start_flags_name_the_champion_and_its_subclasses()
    {
        Assert.Equal(("champion", "lanternbearer"), GameSession.QuickStart(["--champion"]));
        Assert.Equal(("champion", "oathbreaker"), GameSession.QuickStart(["--oathbreaker", "--mute"]));
        Assert.Equal(("champion", "lanternbearer"), GameSession.QuickStart(["--lanternbearer"]));
        Assert.Equal(("champion", "paladin"), GameSession.QuickStart(["--paladin"]));
    }

    [AvaloniaFact]
    public void A_lanternbearer_raises_its_auras_by_key_beside_a_fen_zombie()
    {
        var kit = TestKit.Open("champion", zoneId: "rotwater_fens", subclass: "lanternbearer");
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        TestHooks.Place(world, zombie.X - 1.5, zombie.Y);
        world.Hero.Resource.Gain(100);
        Key(kit, Avalonia.Input.Key.D2, PhysicalKey.Digit2, "2");
        Key(kit, Avalonia.Input.Key.D3, PhysicalKey.Digit3, "3");
        Assert.Equal(["shield_of_faith", "lanterns_radiance"], world.Auras.Select(a => a.Ability.Id));
        Assert.NotNull(world.Hero.Ward);                                // Shield of Faith raised the shield
        var tip = Hud.AbilityTip(world, world.Bar["3"], "3").Select(l => l.Text).ToList();
        Assert.Contains(tip, t => t.Contains("vitality to the undead, fire to the living"));
        kit.Play(2.2);
        Assert.True(zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints, "the light burned it");
        kit.Capture(Shot("champion-auras"));
    }

    [AvaloniaFact]
    public void An_oathbreaker_spends_its_taint_on_dread()
    {
        var kit = TestKit.Open("champion", zoneId: "rotwater_fens", subclass: "oathbreaker");
        var world = kit.World;
        var c = world.Hero.Character;
        Assert.Equal("aura_of_dread", world.Bar["3"].Id);
        Key(kit, Avalonia.Input.Key.D3, PhysicalKey.Digit3, "3");
        Assert.Empty(world.Auras);
        Assert.Contains("Not enough Taint", world.LastMessage);
        c.Taint.Add(40 / c.Taint.RateMultiplier);
        Key(kit, Avalonia.Input.Key.D3, PhysicalKey.Digit3, "3");
        Assert.Single(world.Auras);
        Assert.InRange(c.Taint.Value, 19.5, 20.5);
        kit.Capture(Shot("champion-dread"));
    }
}
