using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class CharacterBuilderTests
{
    [Fact]
    public void Fighter_attributes_follow_ancestry_background_class_and_free_boosts()
    {
        var c = TestCharacters.Fighter();
        Assert.Equal(4, c.Attributes[AttributeKind.Strength]);
        Assert.Equal(2, c.Attributes[AttributeKind.Dexterity]);
        Assert.Equal(2, c.Attributes[AttributeKind.Constitution]);
        Assert.Equal(0, c.Attributes[AttributeKind.Intelligence]);
        Assert.Equal(1, c.Attributes[AttributeKind.Wisdom]);
        Assert.Equal(0, c.Attributes[AttributeKind.Charisma]);
    }

    [Fact]
    public void Ancestry_flaws_apply()
    {
        var c = TestCharacters.Wizard();
        Assert.Equal(1, c.Attributes[AttributeKind.Constitution]); // elf flaw -1, then +1 background, +1 free
        Assert.Equal(4, c.Attributes[AttributeKind.Intelligence]);
    }

    [Fact]
    public void Boosts_within_one_group_must_differ() =>
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(TestCharacters.Content, TestCharacters.FighterChoices with
        {
            FreeBoosts = [AttributeKind.Strength, AttributeKind.Strength, AttributeKind.Constitution, AttributeKind.Wisdom],
        }));

    [Fact]
    public void Background_boost_must_be_one_of_its_two() =>
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(TestCharacters.Content, TestCharacters.FighterChoices with
        {
            BackgroundBoost = AttributeKind.Charisma,
        }));

    [Fact]
    public void Key_attribute_must_belong_to_the_class() =>
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(TestCharacters.Content, TestCharacters.FighterChoices with
        {
            KeyAttribute = AttributeKind.Intelligence,
        }));

    [Fact]
    public void Ancestry_free_boost_count_must_match() =>
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(TestCharacters.Content, TestCharacters.FighterChoices with
        {
            AncestryFreeBoosts = [AttributeKind.Strength],
        }));
}

public class CharacterStatsTests
{
    [Fact]
    public void Max_hp_is_ancestry_hp_plus_class_hp_and_con_per_level()
    {
        var c = TestCharacters.Fighter();
        Assert.Equal(8 + (10 + 2) * 1, c.MaxHitPoints);
        Assert.Equal(c.MaxHitPoints, c.HitPoints);

        TestCharacters.AtLevel(c, 4);
        Assert.Equal(8 + (10 + 2) * 4, c.MaxHitPoints);
        Assert.Equal(c.MaxHitPoints, c.HitPoints);
    }

    [Fact]
    public void Level_five_boost_to_con_raises_hp_for_every_level()
    {
        var c = TestCharacters.AtLevel(TestCharacters.Fighter(), 5);
        Assert.Equal(3, c.Attributes[AttributeKind.Constitution]);
        Assert.Equal(8 + (10 + 3) * 5, c.MaxHitPoints);
    }

    [Fact]
    public void Ac_is_ten_plus_capped_dex_plus_proficiency_plus_item()
    {
        var c = TestCharacters.Fighter();
        Assert.Equal(10 + 2 + 3, c.ArmorClass().Total); // unarmored, trained

        c.Armor = new Armor(new ArmorDefinition("chain", "Chain shirt", ArmorCategory.Light, 2, 3), Potency: 1);
        Assert.Equal(10 + 2 + 3 + 3, c.ArmorClass().Total);

        c.Armor = new Armor(new ArmorDefinition("plate", "Full plate", ArmorCategory.Heavy, 6, 0, 3, 10, 4));
        Assert.Equal(10 + 0 + 3 + 6, c.ArmorClass().Total); // DEX capped at +0
    }

    [Fact]
    public void Ac_breakdown_names_every_part_for_the_tooltip()
    {
        var c = TestCharacters.Fighter();
        c.Armor = new Armor(new ArmorDefinition("chain", "Chain shirt", ArmorCategory.Light, 2, 3));
        var sources = c.ArmorClass().Applied.Select(m => m.ToString()).ToList();
        Assert.Equal(["Base +10", "DEX +2", "Trained +3", "Chain shirt +2 item"], sources);

        c.Weapon = new Weapon(Character.Fist, Potency: 1);
        Assert.Contains("Potency rune +1 item", c.AttackModifier().Applied.Select(m => m.ToString()));
    }

    [Fact]
    public void Attack_uses_str_for_melee_dex_for_ranged_and_the_better_for_finesse()
    {
        var c = TestCharacters.Fighter();
        var sword = new WeaponDefinition("longsword", "Longsword", 8, DamageType.Slashing, 1.2);
        var bow = new WeaponDefinition("shortbow", "Shortbow", 6, DamageType.Piercing, 1.3, IsRanged: true);

        c.Weapon = new Weapon(sword, Potency: 1);
        Assert.Equal(4 + 5 + 1, c.AttackModifier().Total); // STR + expert (fighter L1) + potency
        Assert.Equal(4, c.DamageBonus().Total);

        c.Weapon = new Weapon(bow);
        Assert.Equal(2 + 5, c.AttackModifier().Total);
        Assert.Equal(0, c.DamageBonus().Total);

        Assert.Equal(AttributeKind.Strength, c.AttackAttribute(new Weapon(Character.Fist)));
    }

    [Fact]
    public void Saves_perception_and_class_dc()
    {
        var c = TestCharacters.Fighter();
        c.Armor = new Armor(ArmorDefinition.None, Resilient: 1);
        Assert.Equal(2 + 5 + 1, c.Save(StatisticKind.Fortitude).Total);
        Assert.Equal(2 + 5 + 1, c.Save(StatisticKind.Reflex).Total);
        Assert.Equal(1 + 3 + 1, c.Save(StatisticKind.Will).Total);
        Assert.Equal(1 + 5, c.Perception().Total);
        Assert.Equal(10 + 4 + 3, c.ClassDc().Total);
        Assert.Equal(4.0 + 0.25 * 6, c.DetectionRangeTiles);
    }

    [Fact]
    public void Spell_attack_and_dc_use_the_key_attribute()
    {
        var w = TestCharacters.Wizard();
        Assert.Equal(4 + 3, w.SpellAttack().Total);
        Assert.Equal(10 + 4 + 3, w.SpellDc().Total);
        TestCharacters.AtLevel(w, 7);
        Assert.Equal(4 + 4 + 7, w.SpellAttack().Total); // expert at 7
    }

    [Fact]
    public void Proficiency_rises_with_level()
    {
        var c = TestCharacters.AtLevel(TestCharacters.Fighter(), 5);
        Assert.Equal(ProficiencyRank.Master, c.Rank(ProficiencyTrack.Attack));
        Assert.Equal(4 + (6 + 5), c.AttackModifier().Total); // the level-5 STR boost is only partial at +4
        Assert.True(c.Attributes.HasPartialBoost(AttributeKind.Strength));
    }

    [Fact]
    public void Gear_and_buff_modifiers_follow_the_stacking_rule()
    {
        var c = TestCharacters.Fighter();
        c.AddModifier(StatisticKind.ArmorClass, new Modifier("Shield of Faith", 1, BonusType.Status));
        c.AddModifier(StatisticKind.ArmorClass, new Modifier("Bless", 1, BonusType.Status));
        Assert.Equal(15 + 1, c.ArmorClass().Total);
        c.RemoveModifiers("Shield of Faith");
        c.RemoveModifiers("Bless");
        Assert.Equal(15, c.ArmorClass().Total);
    }

    [Fact]
    public void Heavy_armor_slows_unless_strength_is_met()
    {
        var c = TestCharacters.Fighter(); // STR +4
        c.Armor = new Armor(new ArmorDefinition("plate", "Full plate", ArmorCategory.Heavy, 6, 0, 3, 10, 5));
        Assert.Equal(25 - 10, c.SpeedFeet);
        c.Armor = new Armor(new ArmorDefinition("plate", "Full plate", ArmorCategory.Heavy, 6, 0, 3, 10, 4));
        Assert.Equal(25 - 5, c.SpeedFeet);
        Assert.Equal(4.0, c.MoveSpeedTilesPerSecond());
    }

    [Fact]
    public void Level_cap_is_twenty_and_a_boost_level_waits_for_its_boosts()
    {
        var c = TestCharacters.AtLevel(TestCharacters.Fighter(), 4);
        c.LevelUp();                                         // level 5's boosts can wait for the level-up screen
        Assert.Equal(5, c.Level);
        Assert.Contains(5, c.PendingLevels);
        Assert.Throws<RulesException>(() => c.LevelUp([AttributeKind.Strength]));
        TestCharacters.AtLevel(c, 20);
        Assert.Throws<RulesException>(() => c.LevelUp());
    }
}
