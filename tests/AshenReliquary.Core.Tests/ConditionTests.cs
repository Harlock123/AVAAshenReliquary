using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class ConditionTests
{
    [Fact]
    public void Frightened_drops_by_one_every_two_seconds()
    {
        var set = new ConditionSet();
        set.Apply(ConditionKind.Frightened, 2);
        set.Update(1.9);
        Assert.Equal(2, set.Value(ConditionKind.Frightened));
        set.Update(0.1);
        Assert.Equal(1, set.Value(ConditionKind.Frightened));
        set.Update(2.0);
        Assert.False(set.Has(ConditionKind.Frightened));
    }

    [Fact]
    public void Reapplying_keeps_the_higher_value()
    {
        var set = new ConditionSet();
        set.Apply(ConditionKind.Clumsy, 2, 5);
        set.Apply(ConditionKind.Clumsy, 1, 5);
        Assert.Equal(2, set.Value(ConditionKind.Clumsy));
    }

    [Fact]
    public void Timed_conditions_expire()
    {
        var set = new ConditionSet();
        set.Apply(ConditionKind.OffGuard, durationSeconds: 1.0);
        set.Update(0.5);
        Assert.True(set.IsOffGuard);
        set.Update(0.6);
        Assert.False(set.IsOffGuard);
    }

    [Fact]
    public void Stun_is_clamped_to_half_a_second_to_two_seconds_and_leaves_three_seconds_of_immunity()
    {
        var set = new ConditionSet();
        Assert.Equal(2.0, set.Apply(ConditionKind.Stunned, durationSeconds: 5));
        Assert.False(set.CanAct);
        set.Update(2.0);
        Assert.True(set.CanAct);

        Assert.Equal(0, set.Apply(ConditionKind.Stunned, durationSeconds: 1));
        set.Update(3.0);
        Assert.Equal(0.5, set.Apply(ConditionKind.Stunned, durationSeconds: 0.1));
    }

    [Fact]
    public void Bosses_halve_each_control_effect_within_ten_seconds()
    {
        var boss = new ConditionSet(isBoss: true);
        Assert.Equal(4.0, boss.Apply(ConditionKind.Immobilized, durationSeconds: 4));
        boss.Update(5);
        Assert.Equal(2.0, boss.Apply(ConditionKind.Slowed, 1, 4));
        boss.Update(5);
        Assert.Equal(1.0, boss.Apply(ConditionKind.Prone, durationSeconds: 4));
        boss.Update(10.5);
        Assert.Equal(4.0, boss.Apply(ConditionKind.Prone, durationSeconds: 4));
    }

    [Fact]
    public void Non_bosses_get_full_control_durations()
    {
        var set = new ConditionSet();
        set.Apply(ConditionKind.Immobilized, durationSeconds: 4);
        Assert.Equal(4.0, set.Apply(ConditionKind.Slowed, 1, 4));
    }

    [Fact]
    public void Frightened_and_sickened_hit_every_check_and_dc_but_not_damage()
    {
        var set = new ConditionSet();
        set.Apply(ConditionKind.Frightened, 2);
        set.Apply(ConditionKind.Sickened, 1);
        Assert.Equal(-2, ModifierStack.Total(set.PenaltiesFor(StatisticKind.ClassDc, AttributeKind.Strength)));
        Assert.Equal(-2, ModifierStack.Total(set.PenaltiesFor(StatisticKind.Will, AttributeKind.Wisdom)));
        Assert.Equal(0, ModifierStack.Total(set.PenaltiesFor(StatisticKind.Damage, AttributeKind.Strength)));
    }

    [Fact]
    public void Clumsy_enfeebled_and_stupefied_only_hit_their_attributes()
    {
        var set = new ConditionSet();
        set.Apply(ConditionKind.Clumsy, 1, 10);
        set.Apply(ConditionKind.Enfeebled, 2, 10);
        set.Apply(ConditionKind.Stupefied, 3, 10);
        Assert.Equal(-1, ModifierStack.Total(set.PenaltiesFor(StatisticKind.Reflex, AttributeKind.Dexterity)));
        Assert.Equal(0, ModifierStack.Total(set.PenaltiesFor(StatisticKind.Fortitude, AttributeKind.Constitution)));
        Assert.Equal(-2, ModifierStack.Total(set.PenaltiesFor(StatisticKind.Damage, AttributeKind.Strength)));
        Assert.Equal(-3, ModifierStack.Total(set.PenaltiesFor(StatisticKind.SpellDc, AttributeKind.Intelligence)));
    }

    [Fact]
    public void Off_guard_is_a_two_point_circumstance_penalty_to_ac_and_prone_implies_it()
    {
        var set = new ConditionSet();
        set.Apply(ConditionKind.Prone, durationSeconds: 1);
        Assert.False(set.CanMove);
        var penalty = Assert.Single(set.PenaltiesFor(StatisticKind.ArmorClass, AttributeKind.Dexterity));
        Assert.Equal(new Modifier("Off-guard", -2, BonusType.Circumstance), penalty);
    }

    [Fact]
    public void Slowed_stretches_action_time_and_sickened_blocks_potions()
    {
        var set = new ConditionSet();
        set.Apply(ConditionKind.Slowed, 2, 5);
        Assert.Equal(1.5, set.ActionTimeMultiplier);
        set.Apply(ConditionKind.Sickened, 1);
        Assert.True(set.PotionsBlocked);
        set.Reduce(ConditionKind.Sickened);
        Assert.False(set.PotionsBlocked);
    }

    [Fact]
    public void Drained_lowers_max_hp_and_fortitude_until_a_shrine_rest()
    {
        var c = TestCharacters.Fighter();
        var maxHp = c.MaxHitPoints;
        var fort = c.Save(StatisticKind.Fortitude).Total;
        c.Conditions.Apply(ConditionKind.Drained, 2);
        Assert.Equal(maxHp - 2, c.MaxHitPoints);
        Assert.Equal(fort - 2, c.Save(StatisticKind.Fortitude).Total);
        c.RestAtShrine();
        Assert.Equal(maxHp, c.MaxHitPoints);
    }

    [Fact]
    public void Conditions_reach_the_characters_derived_stats()
    {
        var c = TestCharacters.Fighter();
        c.Conditions.Apply(ConditionKind.Frightened, 1);
        c.Conditions.Apply(ConditionKind.OffGuard, durationSeconds: 3);
        Assert.Equal(15 - 1 - 2, c.ArmorClass().Total);
        Assert.Equal(9 - 1, c.AttackModifier().Total);
    }
}
