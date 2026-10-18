using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Characters;

public enum FeatType
{
    Ancestry,
    Class,
    Skill,
    General,
    /// <summary>A side story's permanent reward, granted by a dialogue effect (never chosen on the level-up screen).</summary>
    Boon,
}

/// <summary>
/// What a feat does, read from <c>feats.json</c>. Each field is one kind of effect; a feat lists as many as it needs:
/// <list type="bullet">
/// <item><see cref="Statistic"/> + <see cref="Value"/>: an untyped bonus to a statistic (Will, Perception, ArmorClass ...).</item>
/// <item><see cref="Skill"/> + <see cref="Value"/>: a circumstance bonus to a skill's checks ("Lore" means any Lore,
/// "Untrained" any untrained skill).</item>
/// <item><see cref="HitPointsPerLevel"/>, <see cref="SpeedFeet"/>, <see cref="Resist"/> + <see cref="Value"/>.</item>
/// <item><see cref="Recovery"/>: out-of-combat recovery faster by this share (0.5 = half as fast again).</item>
/// <item><see cref="TaintRate"/>: Taint builds more slowly by this share.</item>
/// <item><see cref="CritDamage"/>: extra damage on a critical hit. <see cref="FuryPerHit"/>: extra Fury on each hit.</item>
/// <item><see cref="ResourceRegen"/>: Mana regenerates faster by this share.</item>
/// <item><see cref="Reach"/>: melee reach, in tiles. <see cref="SpellRange"/>: spell range, in tiles.</item>
/// <item><see cref="Ability"/>: a new ability on the bar. <see cref="ExtraMissiles"/> on <see cref="Ability"/>: one more missile.</item>
/// <item><see cref="IgnoreArmorSpeed"/>: body armour never slows the character.</item>
/// </list>
/// </summary>
public sealed record FeatEffect(
    string? Statistic = null,
    int Value = 0,
    string? Skill = null,
    int HitPointsPerLevel = 0,
    int SpeedFeet = 0,
    DamageType? Resist = null,
    double Recovery = 0,
    double TaintRate = 0,
    int CritDamage = 0,
    int FuryPerHit = 0,
    double ResourceRegen = 0,
    double Reach = 0,
    double SpellRange = 0,
    string? Ability = null,
    int ExtraMissiles = 0,
    bool IgnoreArmorSpeed = false);

/// <summary>
/// A feat: its type and the level it becomes available at, who may take it (an ancestry, a class, training in a
/// skill) and its effects. A background's skill feat is granted at level 1 by name.
/// </summary>
public sealed record FeatDefinition(string Id, string Name, FeatType Type, int Level, string Description, IReadOnlyList<FeatEffect> Effects,
    string? Ancestry = null, string? Class = null, string? Skill = null);

/// <summary>
/// What a character picked on reaching a level: the feats and skill increase the level grants (see
/// <see cref="Progression.LevelProgression"/>), and four attribute boosts at 5, 10, 15 and 20.
/// </summary>
public sealed record LevelChoice(
    AttributeKind[]? Boosts = null,
    string? ClassFeat = null,
    string? SkillFeat = null,
    string? GeneralFeat = null,
    string? AncestryFeat = null,
    string? SkillIncrease = null);
