using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Content;

/// <summary>A playable ancestry (race). Speed is in feet: 25 ft is the reference run speed of 5 tiles a second.</summary>
public sealed record AncestryDefinition(
    string Id,
    string Name,
    int HitPoints,
    int SpeedFeet,
    IReadOnlyList<AttributeKind> Boosts,
    int FreeBoosts,
    IReadOnlyList<AttributeKind> Flaws,
    string Senses,
    string Trait,
    IReadOnlyList<HeritageDefinition>? Heritages = null,
    int FireResistancePerTwoLevels = 0);

/// <summary>
/// A heritage: the variety of an ancestry. The numeric fields are applied by the rules; the description also covers
/// effects that wait on later systems (skills, vision, extra cantrips).
/// </summary>
public sealed record HeritageDefinition(
    string Id,
    string Name,
    string Description,
    int HitPointsPerLevel = 0,
    int SpeedFeet = 0,
    int Will = 0,
    IReadOnlyDictionary<Combat.DamageType, int>? Resistances = null);

/// <summary>A background: one boost from <see cref="BoostChoices"/>, one free boost, a trained skill, a Lore and a skill feat.</summary>
public sealed record BackgroundDefinition(
    string Id,
    string Name,
    IReadOnlyList<AttributeKind> BoostChoices,
    string Skill,
    string Lore,
    string SkillFeat);

/// <summary>The statistics whose proficiency rank a class raises as it levels.</summary>
public enum ProficiencyTrack
{
    Attack,
    Spell,
    Armor,
    Fortitude,
    Reflex,
    Will,
    Perception,
    ClassDc,
}

/// <summary>
/// A class. <see cref="Proficiencies"/> maps each track to the levels where its rank changes, e.g.
/// Attack: { 1: Expert, 5: Master, 13: Legendary } for the Fighter.
/// </summary>
public sealed record ClassDefinition(
    string Id,
    string Name,
    int HitPointsPerLevel,
    IReadOnlyList<AttributeKind> KeyAttributes,
    string Resource,
    IReadOnlyList<string> Subclasses,
    IReadOnlyDictionary<ProficiencyTrack, IReadOnlyDictionary<int, ProficiencyRank>> Proficiencies,
    IReadOnlyList<ArmorCategory>? ArmorCategories = null,
    IReadOnlyList<string>? TrainedSkills = null,
    int SkillPicks = 0)
{
    /// <summary>
    /// Whether the class is trained in an armour category. Unarmoured defence always is; in armour it isn't trained
    /// in, the character gets no proficiency bonus to AC, as in Pathfinder. Null means every category.
    /// </summary>
    public bool IsTrainedIn(ArmorCategory category) =>
        category == ArmorCategory.Unarmored || ArmorCategories is null || ArmorCategories.Contains(category);

    /// <summary>The rank on <paramref name="track"/> at <paramref name="level"/>: the last change at or below that level.</summary>
    public ProficiencyRank RankAt(ProficiencyTrack track, int level)
    {
        if (!Proficiencies.TryGetValue(track, out var steps))
            return ProficiencyRank.Untrained;
        var rank = ProficiencyRank.Untrained;
        foreach (var (atLevel, stepRank) in steps.OrderBy(s => s.Key))
        {
            if (atLevel > level)
                break;
            rank = stepRank;
        }
        return rank;
    }
}
