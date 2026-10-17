using AshenReliquary.Core.Progression;

namespace AshenReliquary.Core.Tests;

public class ProgressionTests
{
    [Fact]
    public void Level_one_grants_the_starting_package() =>
        Assert.Equal(LevelGain.AncestryFeat | LevelGain.SkillFeat | LevelGain.ClassFeature | LevelGain.Subclass,
            LevelProgression.GainsAt(1));

    [Fact]
    public void Even_levels_grant_class_and_skill_feats()
    {
        foreach (var level in new[] { 2, 4, 6, 8, 12, 14, 16, 18 })
            Assert.Equal(LevelGain.ClassFeat | LevelGain.SkillFeat, LevelProgression.GainsAt(level));
    }

    [Fact]
    public void Level_five_matches_the_spec_table() =>
        Assert.Equal(LevelGain.AttributeBoosts | LevelGain.AncestryFeat | LevelGain.SkillIncrease | LevelGain.ClassFeature,
            LevelProgression.GainsAt(5));

    [Theory]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(19)]
    public void General_feat_levels(int level) =>
        Assert.True(LevelProgression.GainsAt(level).HasFlag(LevelGain.GeneralFeat | LevelGain.SkillIncrease | LevelGain.ClassFeature));

    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    [InlineData(17)]
    public void Ancestry_feat_levels(int level) =>
        Assert.Equal(LevelGain.AncestryFeat | LevelGain.SkillIncrease | LevelGain.ClassFeature, LevelProgression.GainsAt(level));

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void Boost_levels_also_grant_feats(int level) =>
        Assert.Equal(LevelGain.ClassFeat | LevelGain.SkillFeat | LevelGain.AttributeBoosts, LevelProgression.GainsAt(level));

    [Fact]
    public void A_thousand_xp_fills_each_level_and_twenty_is_the_cap()
    {
        var xp = new Experience();
        Assert.Equal(0, xp.Add(999));
        Assert.Equal(1, xp.Add(1));
        Assert.Equal(2, xp.Level);
        Assert.Equal(18, xp.Add(50_000));
        Assert.Equal(20, xp.Level);
        Assert.Equal(0, xp.Xp);
        Assert.Equal(0, xp.Add(1000));
    }

    [Fact]
    public void Death_costs_a_tenth_of_a_level_but_never_a_level()
    {
        var xp = new Experience();
        xp.Add(1250);
        xp.ApplyDeathPenalty();
        Assert.Equal((2, 150), (xp.Level, xp.Xp));
        xp.ApplyDeathPenalty();
        xp.ApplyDeathPenalty();
        Assert.Equal((2, 0), (xp.Level, xp.Xp));
    }

    [Fact]
    public void Difficulty_modes_match_the_spec()
    {
        Assert.Equal(new DifficultySettings(DifficultyMode.Story, 0.6, 0.6, 0, 1.0, 1.0), DifficultySettings.For(DifficultyMode.Story));
        Assert.Equal(new DifficultySettings(DifficultyMode.Veteran, 1.0, 1.0, 0, 1.0, 1.0), DifficultySettings.For(DifficultyMode.Veteran));
        Assert.Equal(new DifficultySettings(DifficultyMode.Elite, 1.6, 1.4, 2, 1.5, 2.0), DifficultySettings.For(DifficultyMode.Elite));
        Assert.Equal(1.1, DifficultySettings.For(DifficultyMode.Veteran, hardcore: true).XpMultiplier, 6);
    }
}
