using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class CreatureAndEncounterTests
{
    [Fact]
    public void Creature_stats_follow_the_approximate_formulas_with_solo_scaling()
    {
        var normal = CreatureStats.For(1, CreatureRank.Normal);
        Assert.Equal(16, normal.ArmorClass);
        Assert.Equal(8 - 2, normal.AttackBonus);
        Assert.Equal(17, normal.SaveDc);
        Assert.Equal(5, normal.HitPoints); // 20 x 0.25
        Assert.Equal(0.4, normal.DamageMultiplier);

        var elite = CreatureStats.For(10, CreatureRank.Elite); // effective level 12
        Assert.Equal(12, elite.EffectiveLevel);
        Assert.Equal(27, elite.ArmorClass);
        Assert.Equal(21 - 2, elite.AttackBonus);
        Assert.Equal(29, elite.SaveDc);
        Assert.Equal(20 + 17 * 11, elite.HitPoints);
    }

    [Theory]
    [InlineData(CreatureRank.Minion, 0.12, -2, -3, 0.35)]
    [InlineData(CreatureRank.Normal, 0.25, 0, -2, 0.4)]
    [InlineData(CreatureRank.Champion, 0.6, 1, -2, 0.5)]
    [InlineData(CreatureRank.Elite, 1.0, 2, -2, 0.6)]
    [InlineData(CreatureRank.Herald, 20.0, 2, 0, 1.0)]
    [InlineData(CreatureRank.Prophet, 35.0, 3, 0, 1.0)]
    public void Rank_scaling_matches_the_spec(CreatureRank rank, double hp, int level, int attack, double damage) =>
        Assert.Equal(new RankScaling(hp, level, attack, damage), CreatureStats.ScalingFor(rank));

    [Theory]
    [InlineData(-1, 1, 4, 0)]
    [InlineData(1, 1, 8, 1)]
    [InlineData(5, 2, 8, 3)]
    [InlineData(10, 3, 8, 6)]
    [InlineData(20, 5, 8, 12)]
    public void Default_strike_damage_by_level(int level, int count, int sides, int bonus) =>
        Assert.Equal(new Dice(count, sides, bonus), CreatureStats.DefaultStrikeDamageFor(level));

    [Fact]
    public void Default_strike_damage_averages_about_four_plus_one_and_a_half_per_level()
    {
        for (var level = 0; level <= 23; level++)
            Assert.InRange(CreatureStats.DefaultStrikeDamageFor(level).Average, 4 + 1.5 * level - 1, 4 + 1.5 * level + 1);
    }

    [Fact]
    public void Heralds_and_the_prophet_get_boss_diminishing_returns()
    {
        var herald = new Creature("Mother Sallow", CreatureStats.For(5, CreatureRank.Herald), new Dice(2, 8, 5), DamageType.Poison);
        Assert.True(herald.Conditions.IsBoss);
        var champion = new Creature("Rot champion", CreatureStats.For(5, CreatureRank.Champion), new Dice(1, 8, 4), DamageType.Slashing);
        Assert.False(champion.Conditions.IsBoss);
    }

    [Fact]
    public void Creatures_take_damage_down_to_zero()
    {
        var zombie = new Creature("Fen zombie", CreatureStats.For(1, CreatureRank.Normal), new Dice(1, 6), DamageType.Bludgeoning);
        zombie.TakeDamage(3);
        Assert.Equal(2, zombie.HitPoints);
        zombie.TakeDamage(50);
        Assert.True(zombie.IsDead);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(-4, 10)]
    [InlineData(-3, 15)]
    [InlineData(-2, 20)]
    [InlineData(-1, 30)]
    [InlineData(0, 40)]
    [InlineData(1, 60)]
    [InlineData(2, 80)]
    [InlineData(3, 120)]
    [InlineData(4, 160)]
    [InlineData(6, 160)]
    public void Creature_xp_by_level_difference(int difference, int xp) =>
        Assert.Equal(xp, Encounter.CreatureXp(5 + difference, 5));

    [Fact]
    public void Solo_pack_cost_weighs_each_creature_by_its_rank()
    {
        var level = 5;
        CreatureStats Of(CreatureRank rank) => CreatureStats.For(level, rank);
        Assert.Equal(60, Encounter.PackCost([Of(CreatureRank.Normal), Of(CreatureRank.Normal), Of(CreatureRank.Normal)], level));
        Assert.Equal(80, Encounter.PackCost([Of(CreatureRank.Champion), Of(CreatureRank.Normal), Of(CreatureRank.Normal)], level));
        Assert.Equal(120, Encounter.PackCost([Of(CreatureRank.Elite), Of(CreatureRank.Normal), Of(CreatureRank.Normal)], level));
        Assert.Equal(80, Encounter.PackCost(Enumerable.Repeat(Of(CreatureRank.Minion), 8), level));
        Assert.Throws<ArgumentOutOfRangeException>(() => Encounter.RankWeight(CreatureRank.Herald));
    }

    [Fact]
    public void Budgets_and_ratings()
    {
        Assert.Equal(80, Encounter.Budget(ThreatLevel.Moderate));
        Assert.Equal(ThreatLevel.Low, Encounter.Rate(60));
        Assert.Equal(ThreatLevel.Severe, Encounter.Rate(140));
        Assert.Equal(ThreatLevel.Trivial, Encounter.Rate(45));
        Assert.Equal(ThreatLevel.Extreme, Encounter.Rate(200));
    }
}
