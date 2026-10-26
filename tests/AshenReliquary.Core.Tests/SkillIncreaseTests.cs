using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>The level-up screen's hover over a skill increase: the checks before and after, where it is rolled, what it opens.</summary>
public class SkillIncreaseTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    [Fact]
    public void Untrained_to_trained_adds_level_plus_two_and_opens_the_skills_feats()
    {
        var fighter = TestCharacters.Fighter();
        var skill = Characters.Skills.Attribute.Keys.Order().First(s => fighter.SkillRank(s) == ProficiencyRank.Untrained
            && Content.Feats.Values.Any(f => f.Skill == s));
        var now = fighter.Skill(skill).Total;
        var trained = fighter.SkillAt(skill, ProficiencyRank.Trained).Total;
        Assert.Equal(now + fighter.Level + 2, trained);

        var lines = SkillIncrease.Preview(fighter, skill, Content.Dialogues.Values, Content.Feats.Values);
        Assert.Equal(($"{skill} checks", $"{now:+0;-0;+0} → {trained:+0;-0;+0}"), (lines[0].Text, lines[0].Right));
        var feat = Content.Feats.Values.First(f => f.Type == Characters.FeatType.Skill && f.Skill == skill);
        Assert.Contains(lines, l => l.Text.StartsWith("Opens skill feats:") && l.Text.Contains(feat.Name));

        // Raising a trained skill opens nothing new; Thievery trained is the trap-disarming line, not the "you can" one.
        var thievery = SkillIncrease.Preview(fighter, "Thievery", Content.Dialogues.Values, Content.Feats.Values);
        Assert.Contains(thievery, l => l.Text.Contains("Disarms traps"));
        Assert.DoesNotContain(thievery, l => l.Text.StartsWith("Opens skill feats:") || l.Text.Contains("you can disarm"));
    }

    [Fact]
    public void Each_conversation_dc_and_the_forge_show_the_odds_before_and_after()
    {
        var fighter = TestCharacters.Fighter();
        var diplomacy = SkillIncrease.Preview(fighter, "Diplomacy", Content.Dialogues.Values, Content.Feats.Values);
        Assert.Contains(diplomacy, l => l.Text.StartsWith("Rolled in ") && l.Text.Contains("conversations"));
        var dc = diplomacy.First(l => l.Text.StartsWith("  DC "));
        var (now, after) = (fighter.Skill("Diplomacy").Total, fighter.SkillAt("Diplomacy", fighter.SkillRank("Diplomacy") + 1).Total);
        var value = int.Parse(dc.Text[5..].Split(' ')[0]);
        Assert.Equal($"{Check.Chance(now, value):0%} → {Check.Chance(after, value):0%}", dc.Right);

        var crafting = SkillIncrease.Preview(fighter, "Crafting", Content.Dialogues.Values, Content.Feats.Values);
        Assert.Contains(crafting, l => l.Text == $"  An item of your level, DC {Crafting.Dc(fighter.Level)}");

        // A skill nothing rolls says so, rather than leaving the hover blank.
        var nature = SkillIncrease.Preview(fighter, "Nature", Content.Dialogues.Values, Content.Feats.Values);
        Assert.Contains(nature, l => l.Text.StartsWith("Nothing on the road rolls it yet"));
    }
}
