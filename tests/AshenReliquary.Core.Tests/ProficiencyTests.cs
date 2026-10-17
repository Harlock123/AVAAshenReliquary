using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class ProficiencyTests
{
    [Theory]
    [InlineData(ProficiencyRank.Untrained, 5, 0)]
    [InlineData(ProficiencyRank.Trained, 1, 3)]
    [InlineData(ProficiencyRank.Expert, 1, 5)]
    [InlineData(ProficiencyRank.Master, 10, 16)]
    [InlineData(ProficiencyRank.Legendary, 20, 28)]
    public void Bonus_is_two_per_rank_plus_level(ProficiencyRank rank, int level, int expected) =>
        Assert.Equal(expected, Proficiency.Bonus(rank, level));

    [Theory]
    [InlineData(ProficiencyRank.Expert, 3)]
    [InlineData(ProficiencyRank.Master, 7)]
    [InlineData(ProficiencyRank.Legendary, 15)]
    public void Skill_ranks_unlock_at_pathfinder_levels(ProficiencyRank rank, int level) =>
        Assert.Equal(level, Proficiency.MinimumLevelForSkillRank(rank));
}
