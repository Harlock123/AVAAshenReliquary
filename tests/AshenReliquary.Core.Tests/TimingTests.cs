using AshenReliquary.Core.Combat;

namespace AshenReliquary.Core.Tests;

public class TimingTests
{
    [Fact]
    public void Attacks_at_the_weapons_own_rhythm_take_no_penalty()
    {
        var cadence = new AttackCadence();
        foreach (var t in new[] { 0.0, 1.0, 2.0, 3.0, 4.0 })
            Assert.Equal(0, cadence.Attack(t, swingSeconds: 1.0, agile: false));
    }

    [Fact]
    public void Early_attacks_chain_to_minus_two_then_minus_four_and_a_full_swing_resets()
    {
        var cadence = new AttackCadence();
        Assert.Equal(0, cadence.Attack(0.0, 1.0, false));
        Assert.Equal(-2, cadence.Attack(0.3, 1.0, false));
        Assert.Equal(-4, cadence.Attack(0.6, 1.0, false));
        Assert.Equal(-4, cadence.Attack(0.9, 1.0, false));
        Assert.Equal(0, cadence.Attack(1.9, 1.0, false));  // a full swing later: reset
        Assert.Equal(-2, cadence.Attack(2.2, 1.0, false)); // the chain starts again
    }

    [Fact]
    public void Early_means_inside_ninety_percent_of_a_swing()
    {
        var cadence = new AttackCadence();
        cadence.Attack(0, 1.0, false);
        Assert.Equal(-2, cadence.PenaltyAt(0.89, 1.0, false));
        Assert.Equal(0, cadence.PenaltyAt(0.90, 1.0, false));
    }

    [Fact]
    public void Agile_weapons_take_minus_one_then_minus_two()
    {
        var cadence = new AttackCadence();
        cadence.Attack(0, 0.6, agile: true);
        Assert.Equal(-1, cadence.Attack(0.2, 0.6, agile: true));
        Assert.Equal(-2, cadence.Attack(0.4, 0.6, agile: true));
    }

    [Fact]
    public void Penalty_preview_does_not_record_an_attack()
    {
        var cadence = new AttackCadence();
        cadence.Attack(0, 1.0, false);
        Assert.Equal(-2, cadence.PenaltyAt(0.5, 1.0, false));
        Assert.Equal(-2, cadence.PenaltyAt(0.5, 1.0, false));
    }

    [Theory]
    [InlineData(1, 0.5)]
    [InlineData(2, 1.0)]
    [InlineData(3, 1.5)]
    public void One_action_is_half_a_second(int actions, double seconds) =>
        Assert.Equal(seconds, ActionTiming.CastTime(actions));

    [Fact]
    public void Slowed_and_cast_speed_change_cast_time_and_cast_speed_is_capped()
    {
        Assert.Equal(1.25, ActionTiming.CastTime(2, slowedMultiplier: 1.25));
        Assert.Equal(1.0 / 1.5, ActionTiming.CastTime(2, castSpeedBonus: 0.9), 6);
    }

    [Fact]
    public void Attack_interval_follows_weapon_speed_with_a_sixty_percent_cap()
    {
        Assert.Equal(1 / 1.2, ActionTiming.AttackInterval(1.2), 6);
        Assert.Equal(1 / (1.2 * 1.6), ActionTiming.AttackInterval(1.2, attackSpeedBonus: 2.0), 6);
    }

    [Theory]
    [InlineData(25, 0.0, 5.0)]
    [InlineData(30, 0.0, 6.0)]
    [InlineData(20, 0.0, 4.0)]
    [InlineData(25, 1.0, 7.0)] // +40% cap
    public void Move_speed_is_five_tiles_a_second_at_twenty_five_feet(int feet, double bonus, double tiles) =>
        Assert.Equal(tiles, ActionTiming.MoveSpeedTilesPerSecond(feet, bonus), 6);

    [Fact]
    public void Cooldown_reduction_is_capped_at_thirty_percent() =>
        Assert.Equal(7.0, ActionTiming.Cooldown(10, 0.5), 6);
}
