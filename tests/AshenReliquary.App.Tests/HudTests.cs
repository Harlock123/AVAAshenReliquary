using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.App.Tests;

public class HudTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void Hud_shows_conditions_buffs_and_an_ability_tooltip()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        var c = kit.World.Hero.Character;
        c.Conditions.Apply(ConditionKind.Frightened, 2);
        c.Conditions.Apply(ConditionKind.Slowed, 1, 6);
        c.TakeDamage(8);
        kit.World.DrinkPotion();
        c.Taint.Add(70);
        kit.World.Hero.Resource.OnCombat();
        kit.Play(0.3);
        // RMB slot (Power Attack): the bar is centred, 8 slots of 54 px with 6 px gaps, 98 px above the bottom.
        var barLeft = 1288 / 2f - (8 * 54 + 7 * 6) / 2f;
        kit.Window.MouseMove(new Point(barLeft + 60 + 27, 770 - 98 + 27));
        kit.Play(0.05);
        kit.Capture(Shot("hud-tooltip"));
        Assert.True(c.Conditions.Has(ConditionKind.Frightened));
    }

    [AvaloniaFact]
    public void Character_sheet_explains_its_numbers()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        kit.Window.View.CharacterSheetOpen = true;
        kit.World.Hero.Character.AddModifier(StatisticKind.ArmorClass, new Modifier("Shield of Faith", 1, BonusType.Status));
        kit.World.Hero.Character.AddModifier(StatisticKind.ArmorClass, new Modifier("Bless", 1, BonusType.Status));
        kit.Play(0.2);
        // The AC line: the sheet starts at y=70; title, subtitle, attributes, then Defence: HP, AC.
        kit.Window.MouseMove(new Point(120, 70 + 26 + 17 + 22 + 48 + 18 + 17 - 4));
        kit.Play(0.05);
        kit.Capture(Shot("character-sheet"));
        Assert.True(kit.Window.View.CharacterSheetOpen);
    }

    [AvaloniaFact]
    public void Minimap_reveals_what_has_been_explored()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        var world = kit.World;
        var before = world.ExploredVersion;
        // Walk up the road a little: aim for the first shrine.
        var shrine = world.Zone.Shrines[0];
        world.MoveTo(shrine.At[0] + 0.5, shrine.At[1] + 1.5);
        kit.Play(8);
        Assert.True(world.ExploredVersion > before);
        kit.Capture(Shot("minimap"));
    }

    [AvaloniaFact]
    public void Target_frame_shows_a_burning_monster()
    {
        var kit = TestKit.Open(width: 1288, height: 770);
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        zombie.Creature.Persistent.Add(DamageType.Fire, new Dice(1, 4));
        zombie.Creature.Conditions.Apply(ConditionKind.Frightened, 1);
        world.Attack(zombie);
        kit.Play(0.2);
        kit.Window.MouseMove(new Point(1288 / 2f, 26));
        kit.Play(0.05);
        kit.Capture(Shot("target-frame"));
        Assert.Contains(DamageType.Fire, zombie.Creature.Persistent.Types);
    }
}
