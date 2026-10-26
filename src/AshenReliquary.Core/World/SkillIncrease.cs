using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>One line of a skill increase's preview: its text, and whether it is a change for the better.</summary>
public sealed record SkillPreviewLine(string Text, bool Better = false, string? Right = null);

/// <summary>
/// What raising a skill a rank does for the hero, for the level-up screen's hover: the check's total before and after,
/// where the game rolls that skill (traps, the forge, hiding, the road's conversations, with the chance of success
/// before and after) and, from untrained to trained, the skill feats it opens.
/// </summary>
public static class SkillIncrease
{
    /// <summary>The skills the game rolls outside conversation, and what for.</summary>
    public static readonly IReadOnlyDictionary<string, string> InPlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Thievery"] = "Disarms traps in barrows and ruins (only when trained), against the trap's DC.",
        ["Crafting"] = "Rolled at the forge to raise, add and reroll runes and to brew, against a DC by the item's level.",
        ["Stealth"] = "Hide in Shadows: each monster hunting you rolls its Perception against your Stealth.",
    };

    public static IReadOnlyList<SkillPreviewLine> Preview(Character c, string skill, IEnumerable<DialogueDefinition> dialogues,
        IEnumerable<FeatDefinition> feats)
    {
        var rank = c.SkillRank(skill);
        var next = rank + 1;
        var now = c.Skill(skill).Total;
        var after = c.SkillAt(skill, next).Total;
        var lines = new List<SkillPreviewLine>
        {
            new($"{skill} checks", true, $"{now:+0;-0;+0} → {after:+0;-0;+0}"),
            new($"{Skills.AttributeFor(skill)} skill. Trained adds your level + 2; each rank after, 2 more."),
        };
        if (InPlay.TryGetValue(skill, out var use))
            lines.Add(new(use));
        if (skill.Equals("Crafting", StringComparison.OrdinalIgnoreCase))
        {
            var dc = Crafting.Dc(c.Level);
            lines.Add(new($"  An item of your level, DC {dc}", true, Odds(now, after, dc)));
        }
        if (skill.Equals("Thievery", StringComparison.OrdinalIgnoreCase) && rank == ProficiencyRank.Untrained)
            lines.Add(new("  Trained, you can disarm traps.", true));

        // The road's conversations that roll this skill, by DC.
        var checks = dialogues.SelectMany(d => d.Nodes.Values).SelectMany(n => n.Choices ?? [])
            .Select(ch => ch.Check).OfType<SkillCheckDefinition>()
            .Where(ch => ch.Skill.Equals(skill, StringComparison.OrdinalIgnoreCase))
            .GroupBy(ch => ch.Dc).OrderBy(g => g.Key).ToList();
        if (checks.Count > 0)
        {
            var count = checks.Sum(g => g.Count());
            lines.Add(new($"Rolled in {count} conversation{(count == 1 ? "" : "s")} on the road:"));
            foreach (var g in checks)
                lines.Add(new($"  DC {g.Key}{(g.Count() > 1 ? $" (×{g.Count()})" : "")}", true, Odds(now, after, g.Key)));
        }
        if (!InPlay.ContainsKey(skill) && checks.Count == 0)
            lines.Add(new("Nothing on the road rolls it yet: it counts for feats and their bonuses."));

        // Untrained to trained opens the skill's feats.
        if (rank == ProficiencyRank.Untrained)
        {
            var opened = feats.Where(f => f.Type == FeatType.Skill && f.Skill is { } s && s.Equals(skill, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Level).ThenBy(f => f.Name).Select(f => f.Level > 1 ? $"{f.Name} ({f.Level})" : f.Name).ToList();
            if (opened.Count > 0)
                lines.Add(new($"Opens skill feats: {string.Join(", ", opened)}.", true));
        }
        return lines;
    }

    /// <summary>The chance of success at a DC, now and after: "55% → 65%".</summary>
    private static string Odds(int now, int after, int dc) =>
        $"{Check.Chance(now, dc):0%} → {Check.Chance(after, dc):0%}";
}
