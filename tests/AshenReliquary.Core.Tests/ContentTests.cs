using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class ContentTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    [Fact]
    public void Embedded_content_has_the_specs_six_ancestries_nine_backgrounds_and_seven_classes()
    {
        Assert.Equal(["ashborn", "dwarf", "elf", "halfling", "human", "orc"], Content.Ancestries.Keys.Order());
        Assert.Equal(9, Content.Backgrounds.Count);
        Assert.Equal(["champion", "fighter", "gunslinger", "necromancer", "ranger", "rogue", "wizard"], Content.Classes.Keys.Order());
    }

    [Theory]
    [InlineData("human", 8, 25)]
    [InlineData("dwarf", 10, 20)]
    [InlineData("elf", 6, 30)]
    [InlineData("halfling", 6, 25)]
    [InlineData("orc", 10, 25)]
    [InlineData("ashborn", 8, 25)]
    public void Ancestry_hit_points_and_speed_match_the_spec(string id, int hp, int speed)
    {
        var ancestry = Content.Ancestry(id);
        Assert.Equal(hp, ancestry.HitPoints);
        Assert.Equal(speed, ancestry.SpeedFeet);
    }

    [Theory]
    [InlineData("fighter", 10)]
    [InlineData("champion", 10)]
    [InlineData("rogue", 8)]
    [InlineData("ranger", 10)]
    [InlineData("wizard", 6)]
    [InlineData("necromancer", 6)]
    public void Class_hit_points_per_level_match_the_spec(string id, int hp) =>
        Assert.Equal(hp, Content.Class(id).HitPointsPerLevel);

    [Fact]
    public void Every_class_has_two_subclasses_and_the_champion_three() =>
        Assert.All(Content.Classes.Values, c => Assert.Equal(c.Id == "champion" ? 3 : 2, c.Subclasses.Count));

    [Theory]
    [InlineData("fighter", ProficiencyTrack.Attack, 1, 5, 13)]
    [InlineData("champion", ProficiencyTrack.Attack, 5, 13, 0)]
    [InlineData("ranger", ProficiencyTrack.Attack, 5, 13, 0)]
    [InlineData("rogue", ProficiencyTrack.Attack, 5, 13, 0)]
    [InlineData("wizard", ProficiencyTrack.Spell, 7, 15, 19)]
    [InlineData("necromancer", ProficiencyTrack.Spell, 7, 15, 19)]
    public void Signature_proficiency_milestones_match_the_spec(string id, ProficiencyTrack track, int expert, int master, int legendary)
    {
        var c = Content.Class(id);
        Assert.Equal(ProficiencyRank.Expert, c.RankAt(track, expert));
        Assert.True(c.RankAt(track, expert - 1) < ProficiencyRank.Expert || expert == 1);
        Assert.Equal(ProficiencyRank.Master, c.RankAt(track, master));
        Assert.Equal(ProficiencyRank.Expert, c.RankAt(track, master - 1));
        if (legendary > 0)
        {
            Assert.Equal(ProficiencyRank.Legendary, c.RankAt(track, legendary));
            Assert.Equal(ProficiencyRank.Master, c.RankAt(track, legendary - 1));
        }
        else
        {
            Assert.Equal(ProficiencyRank.Master, c.RankAt(track, 20));
        }
    }

    [Fact]
    public void Unknown_ids_are_rules_errors() =>
        Assert.Throws<RulesException>(() => Content.Class("bard"));
}
