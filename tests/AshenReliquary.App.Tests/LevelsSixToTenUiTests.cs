using Avalonia.Headless.XUnit;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Items;

namespace AshenReliquary.App.Tests;

/// <summary>M4 step 1 on screen: a level-8 hero's new abilities in the Abilities panel and their tooltips, and Act 2's plate.</summary>
public class LevelsSixToTenUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    /// <summary>Takes the world's hero up to <paramref name="level"/>, making each level's choices as a player might.</summary>
    private static void LevelTo(Core.World.GameWorld world, int level)
    {
        var c = world.Hero.Character;
        while (c.Level < level)
        {
            TestHooks.GainXp(world, 1000);
            foreach (var at in c.PendingLevels.ToList())
            {
                var gains = Core.Progression.LevelProgression.GainsAt(at);
                string? Pick(Core.Progression.LevelGain gain, FeatType type) =>
                    (gains & gain) == 0 ? null : c.FeatOptions(type, at).Where(f => f.Dedication is null).Select(f => f.Id).First();
                world.ChooseLevel(at, new LevelChoice(
                    Boosts: (gains & Core.Progression.LevelGain.AttributeBoosts) != 0
                        ? new[] { c.KeyAttribute, Core.Rules.AttributeKind.Constitution, Core.Rules.AttributeKind.Dexterity, Core.Rules.AttributeKind.Wisdom, Core.Rules.AttributeKind.Strength }.Distinct().Take(4).ToArray() : null,
                    ClassFeat: Pick(Core.Progression.LevelGain.ClassFeat, FeatType.Class),
                    SkillFeat: Pick(Core.Progression.LevelGain.SkillFeat, FeatType.Skill),
                    GeneralFeat: Pick(Core.Progression.LevelGain.GeneralFeat, FeatType.General),
                    AncestryFeat: Pick(Core.Progression.LevelGain.AncestryFeat, FeatType.Ancestry),
                    SkillIncrease: (gains & Core.Progression.LevelGain.SkillIncrease) != 0
                        ? Skills.Attribute.Keys.Order().First(s => c.CanIncrease(s, at)) : null));
            }
        }
    }

    [AvaloniaFact]
    public void A_level_eight_vanguard_has_its_roar_and_whirlwind_and_wears_plate()
    {
        var kit = TestKit.Open("fighter", subclass: "vanguard");
        var world = kit.World;
        LevelTo(world, 8);
        Assert.Equal(8, world.Hero.Character.Level);
        Assert.Contains(world.KnownAbilities, a => a.Id == "intimidating_roar");
        Assert.Contains(world.KnownAbilities, a => a.Id == "whirlwind_strike");
        var whirl = world.KnownAbilities.Single(a => a.Id == "whirlwind_strike");
        var tip = Rendering.Hud.AbilityTip(world, whirl, "").Select(l => l.Text).ToList();
        Assert.Contains(tip, t => t.Contains("Vanguard, level 8"));
        var content = Core.Content.ContentDatabase.Embedded;
        foreach (var id in new[] { "full_plate", "plate_helm", "plate_gauntlets", "plate_greaves", "plate_boots" })
            world.Hero.Equipment.Equip(new Item(content.Item(id), Rarity.Common, 8));
        Assert.Equal("full_plate", world.Hero.Equipment.In(EquipSlot.Chest)?.Definition.Id);
        kit.Play(0.2);
        kit.Capture(Shot("level8-plate"));
        kit.Window.View.AbilitiesOpen = true;
        kit.Play(0.1);
        Assert.NotEqual(default, kit.Window.View.AbilityRow("whirlwind_strike"));
        kit.Capture(Shot("level8-abilities"));
    }

    [AvaloniaFact]
    public void Rain_of_arrows_and_call_of_the_pack_say_what_they_do()
    {
        var kit = TestKit.Open("ranger", subclass: "beastmaster");
        var world = kit.World;
        LevelTo(world, 8);
        var rain = Rendering.Hud.AbilityTip(world, world.KnownAbilities.Single(a => a.Id == "rain_of_arrows"), "").Select(l => l.Text).ToList();
        Assert.Contains(rain, t => t.Contains("Shortbow") && t.Contains("class DC"));
        var pack = Rendering.Hud.AbilityTip(world, world.KnownAbilities.Single(a => a.Id == "call_of_the_pack"), "").Select(l => l.Text).ToList();
        Assert.Contains(pack, t => t.Contains("2 for 20 s"));
    }

    [AvaloniaFact]
    public void The_level_ten_screen_offers_the_level_ten_feats_inside_a_narrow_window()
    {
        var kit = TestKit.Open("wizard", subclass: "pyromancer");
        kit.Window.MinWidth = 0;
        kit.Window.Width = 731;
        kit.Window.Height = 946;
        var world = kit.World;
        var character = world.Hero.Character;
        LevelTo(world, 9);
        TestHooks.GainXp(world, 1000);
        Assert.Equal([10], character.PendingLevels);
        kit.Window.View.LevelUpOpen = true;
        kit.Play(0.1);
        var panel = kit.Window.View.LevelUp;
        foreach (var title in new[] { "Quickened Casting", "Mage Armour" })
        {
            var (rect, _) = panel.Options.First(o => o.Title == title);
            Assert.True(rect.Left >= 0 && rect.Right <= 731 && rect.Bottom <= 946, $"{title} is drawn off the window: {rect}");
        }
        var (pick, _) = panel.Options.First(o => o.Title == "Quickened Casting");
        kit.Click(new Avalonia.Point(pick.MidX, pick.MidY));
        kit.Capture(Shot("level10-choices"));
        Assert.DoesNotContain(panel.Missing(character, 10), m => m.Contains("class feat"));
        Assert.NotEmpty(panel.Missing(character, 10));      // the skill feat and four boosts are still to choose
    }
}
