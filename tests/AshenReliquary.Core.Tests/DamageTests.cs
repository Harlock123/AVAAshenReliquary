using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class DamageTests
{
    [Theory]
    [InlineData(DegreeOfSuccess.CriticalSuccess, GrazeEffect.None, 2.0)]
    [InlineData(DegreeOfSuccess.Success, GrazeEffect.None, 1.0)]
    [InlineData(DegreeOfSuccess.Failure, GrazeEffect.None, 0.0)]
    [InlineData(DegreeOfSuccess.Failure, GrazeEffect.Half, 0.5)]
    [InlineData(DegreeOfSuccess.CriticalFailure, GrazeEffect.Half, 0.0)]
    public void Degree_multiplier(DegreeOfSuccess degree, GrazeEffect graze, double expected) =>
        Assert.Equal(expected, DamageCalculator.DegreeMultiplier(degree, graze));

    [Fact]
    public void Damage_is_dice_plus_bonus_times_multiplier_minus_resistance_plus_weakness()
    {
        var target = new DamageProfile().Resist(DamageType.Fire, 3).Weak(DamageType.Cold, 5);
        Assert.Equal(9, DamageCalculator.Compute(5, 4, DegreeOfSuccess.Success, DamageType.Slashing, target));
        Assert.Equal(18, DamageCalculator.Compute(5, 4, DegreeOfSuccess.CriticalSuccess, DamageType.Slashing, target));
        Assert.Equal(6, DamageCalculator.Compute(5, 4, DegreeOfSuccess.Success, DamageType.Fire, target));
        Assert.Equal(14, DamageCalculator.Compute(5, 4, DegreeOfSuccess.Success, DamageType.Cold, target));
        Assert.Equal(4, DamageCalculator.Compute(5, 4, DegreeOfSuccess.Failure, DamageType.Slashing, target, GrazeEffect.Half));
    }

    [Fact]
    public void Extra_critical_dice_are_added_after_doubling() =>
        Assert.Equal(18 + 7, DamageCalculator.Compute(5, 4, DegreeOfSuccess.CriticalSuccess, DamageType.Piercing, null, extraCriticalDamage: 7));

    [Fact]
    public void A_hit_deals_at_least_one_before_defenses_and_resistance_never_heals()
    {
        Assert.Equal(1, DamageCalculator.Compute(1, -5, DegreeOfSuccess.Success, DamageType.Slashing, null));
        var tough = new DamageProfile().Resist(DamageType.Slashing, 10);
        Assert.Equal(0, DamageCalculator.Compute(3, 0, DegreeOfSuccess.Success, DamageType.Slashing, tough));
    }

    [Fact]
    public void Immunity_stops_everything_including_weakness()
    {
        var undead = new DamageProfile().Immune(DamageType.Poison).Weak(DamageType.Poison, 5);
        Assert.Equal(0, DamageCalculator.ApplyDefenses(12, DamageType.Poison, undead));
    }

    [Fact]
    public void Profiles_keep_the_highest_resistance_of_a_type()
    {
        var profile = new DamageProfile().Resist(DamageType.Fire, 5).Resist(DamageType.Fire, 3);
        Assert.Equal(5, profile.Resistances[DamageType.Fire]);
    }
}
