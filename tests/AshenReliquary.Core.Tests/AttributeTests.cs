using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class AttributeTests
{
    [Fact]
    public void Attributes_start_at_zero()
    {
        var set = new AttributeSet();
        Assert.All(Enum.GetValues<AttributeKind>(), k => Assert.Equal(0, set[k]));
    }

    [Fact]
    public void Boost_adds_one_and_flaw_subtracts_one()
    {
        var set = new AttributeSet();
        set.Boost(AttributeKind.Strength);
        set.Flaw(AttributeKind.Charisma);
        Assert.Equal(1, set[AttributeKind.Strength]);
        Assert.Equal(-1, set[AttributeKind.Charisma]);
    }

    [Fact]
    public void From_plus_four_two_partial_boosts_make_one()
    {
        var set = new AttributeSet();
        for (var i = 0; i < 4; i++)
            set.Boost(AttributeKind.Strength);
        Assert.Equal(4, set[AttributeKind.Strength]);

        set.Boost(AttributeKind.Strength);
        Assert.Equal(4, set[AttributeKind.Strength]);
        Assert.True(set.HasPartialBoost(AttributeKind.Strength));

        set.Boost(AttributeKind.Strength);
        Assert.Equal(5, set[AttributeKind.Strength]);
        Assert.False(set.HasPartialBoost(AttributeKind.Strength));
    }

    [Fact]
    public void Starting_at_plus_four_and_boosting_every_level_up_reaches_plus_six()
    {
        // 4 level-ups x 1 boost each above +4 = 4 partial boosts = +2.
        var set = new AttributeSet();
        for (var i = 0; i < 4; i++)
            set.Boost(AttributeKind.Strength);
        foreach (var level in AttributeSet.BoostLevels)
            set.ApplyLevelUpBoosts(level, [AttributeKind.Strength, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom]);
        Assert.Equal(6, set[AttributeKind.Strength]);
    }

    [Fact]
    public void Level_up_boosts_must_be_four_different_attributes_at_a_boost_level()
    {
        var set = new AttributeSet();
        Assert.Throws<RulesException>(() => set.ApplyLevelUpBoosts(4,
            [AttributeKind.Strength, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom]));
        Assert.Throws<RulesException>(() => set.ApplyLevelUpBoosts(5,
            [AttributeKind.Strength, AttributeKind.Strength, AttributeKind.Constitution, AttributeKind.Wisdom]));
        Assert.Throws<RulesException>(() => set.ApplyLevelUpBoosts(5, [AttributeKind.Strength]));
    }
}
