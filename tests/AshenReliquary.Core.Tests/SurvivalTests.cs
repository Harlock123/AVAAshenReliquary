using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class SurvivalTests
{
    [Fact]
    public void Hero_points_are_one_per_zone_up_to_three()
    {
        var points = new HeroPoints();
        for (var i = 0; i < 5; i++)
            points.GainForZone();
        Assert.Equal(3, points.Count);
        Assert.True(points.TrySpend());
        Assert.Equal(2, points.Count);
    }

    [Fact]
    public void Reaching_zero_hp_spends_a_hero_point_to_revive_at_half()
    {
        var c = TestCharacters.Fighter();
        c.HeroPoints.GainForZone();
        Assert.Equal(DeathOutcome.RevivedByHeroPoint, c.TakeDamage(999));
        Assert.Equal(c.MaxHitPoints / 2, c.HitPoints);
        Assert.Equal(0, c.HeroPoints.Count);
    }

    [Fact]
    public void Without_a_hero_point_the_character_dies_until_a_shrine()
    {
        var c = TestCharacters.Fighter();
        Assert.Equal(DeathOutcome.Alive, c.TakeDamage(5));
        Assert.Equal(DeathOutcome.Died, c.TakeDamage(999));
        Assert.True(c.IsDead);
        c.Heal(10);
        Assert.Equal(0, c.HitPoints);
        c.RestAtShrine();
        Assert.False(c.IsDead);
        Assert.Equal(c.MaxHitPoints, c.HitPoints);
    }

    [Fact]
    public void Potions_have_three_charges_an_eight_second_cooldown_and_heal_d8_plus_twice_level()
    {
        var potion = new HealingPotion();
        Assert.Equal(5 + 2 * 3, potion.Drink(0, level: 3, new ScriptedRandom(5)));
        Assert.Null(potion.Drink(7.9, 3, new ScriptedRandom(5)));
        Assert.NotNull(potion.Drink(8.0, 3, new ScriptedRandom(1)));
        Assert.NotNull(potion.Drink(16.0, 3, new ScriptedRandom(1)));
        Assert.Equal(0, potion.Charges);
        Assert.Null(potion.Drink(24.0, 3, new ScriptedRandom(1)));
        potion.Refill();
        Assert.Equal(HealingPotion.MaxCharges, potion.Charges);
    }

    [Fact]
    public void Sickened_characters_cannot_drink()
    {
        var conditions = new ConditionSet();
        conditions.Apply(ConditionKind.Sickened, 1);
        Assert.Null(new HealingPotion().Drink(0, 1, new ScriptedRandom(4), conditions));
    }

    [Fact]
    public void Taint_gives_minus_one_to_saves_from_fifty_and_whispers_at_a_hundred()
    {
        var taint = new TaintMeter();
        taint.Add(49);
        Assert.Null(taint.SavePenalty);
        taint.Add(1);
        Assert.Equal(-1, taint.SavePenalty?.Value);
        Assert.True(taint.Add(60));
        Assert.Equal(TaintMeter.WhispersSeconds, taint.ReversedMovementRemaining);
        Assert.Equal(TaintMeter.PenaltyThreshold, taint.Value);
        taint.Update(2.0);
        Assert.Equal(0, taint.ReversedMovementRemaining);
    }

    [Fact]
    public void Ashborn_and_constitution_slow_taint_down()
    {
        Assert.Equal(0.75, TaintMeter.RateFor(0.75, 0), 6);
        Assert.Equal(0.9, TaintMeter.RateFor(1.0, 2), 6);
        Assert.Equal(0.5, TaintMeter.RateFor(1.0, 20), 6);
    }

    [Fact]
    public void Taint_penalty_reaches_the_characters_saves()
    {
        var c = TestCharacters.Fighter();
        var will = c.Save(StatisticKind.Will).Total;
        c.Taint.Add(100);
        Assert.Equal(will - 1, c.Save(StatisticKind.Will).Total);
    }
}
