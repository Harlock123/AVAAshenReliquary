using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class DiceAndCheckTests
{
    [Theory]
    [InlineData("2d6+3", 2, 6, 3)]
    [InlineData("d8", 1, 8, 0)]
    [InlineData("4d10-2", 4, 10, -2)]
    public void Dice_parse(string text, int count, int sides, int bonus) =>
        Assert.Equal(new Dice(count, sides, bonus), Dice.Parse(text));

    [Fact]
    public void Dice_min_max_average_and_text()
    {
        var dice = new Dice(2, 6, 3);
        Assert.Equal(5, dice.Min);
        Assert.Equal(15, dice.Max);
        Assert.Equal(10, dice.Average);
        Assert.Equal("2d6+3", dice.ToString());
    }

    [Fact]
    public void Dice_roll_sums_each_die_plus_the_bonus() =>
        Assert.Equal(4 + 5 + 3, new Dice(2, 6, 3).Roll(new ScriptedRandom(4, 5)));

    [Fact]
    public void Seeded_random_repeats_for_the_same_seed()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);
        Assert.Equal(Enumerable.Range(0, 20).Select(_ => a.D20()), Enumerable.Range(0, 20).Select(_ => b.D20()));
    }

    [Theory]
    [InlineData(30, 20, 10, DegreeOfSuccess.CriticalSuccess)]
    [InlineData(29, 20, 10, DegreeOfSuccess.Success)]
    [InlineData(20, 20, 10, DegreeOfSuccess.Success)]
    [InlineData(19, 20, 10, DegreeOfSuccess.Failure)]
    [InlineData(10, 20, 5, DegreeOfSuccess.CriticalFailure)]
    [InlineData(11, 20, 5, DegreeOfSuccess.Failure)]
    public void Degree_follows_the_ten_point_bands(int total, int dc, int natural, DegreeOfSuccess expected) =>
        Assert.Equal(expected, Check.Degree(total, dc, natural));

    [Fact]
    public void Natural_twenty_raises_one_step_and_natural_one_lowers_one_step()
    {
        Assert.Equal(DegreeOfSuccess.Success, Check.Degree(25, 30, 20));        // failure -> success
        Assert.Equal(DegreeOfSuccess.CriticalSuccess, Check.Degree(30, 30, 20)); // success -> critical
        Assert.Equal(DegreeOfSuccess.Failure, Check.Degree(15, 10, 1));          // success -> failure
        Assert.Equal(DegreeOfSuccess.CriticalFailure, Check.Degree(5, 10, 1));   // failure -> critical failure
    }

    [Fact]
    public void Check_roll_uses_one_d20()
    {
        var result = Check.Roll(new ScriptedRandom(12), modifier: 7, dc: 18);
        Assert.Equal(19, result.Total);
        Assert.Equal(DegreeOfSuccess.Success, result.Degree);
    }

    [Fact]
    public void Chance_counts_the_faces_that_succeed()
    {
        // +5 vs DC 15: a 10 or better succeeds = 11 faces.
        Assert.Equal(0.55, Check.Chance(5, 15), 3);
        // Even an impossible DC succeeds on a natural 20 if it would otherwise only fail.
        Assert.Equal(0.05, Check.Chance(0, 21), 3);
    }

    [Fact]
    public void Flat_check_passes_on_the_dc_or_higher()
    {
        Assert.True(new ScriptedRandom(15).FlatCheck(15));
        Assert.False(new ScriptedRandom(14).FlatCheck(15));
    }
}
