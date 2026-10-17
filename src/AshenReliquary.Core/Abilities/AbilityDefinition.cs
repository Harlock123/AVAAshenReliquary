using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Abilities;

/// <summary>How an ability does its work.</summary>
public enum AbilityKind
{
    /// <summary>A weapon Strike in melee reach (the basic attack, Power Attack).</summary>
    WeaponStrike,

    /// <summary>Dash up to <see cref="AbilityDefinition.Range"/> tiles to the target, then Strike (Sudden Charge).</summary>
    ChargeStrike,

    /// <summary>A projectile that makes a spell attack roll on arrival (Ignition).</summary>
    SpellBolt,

    /// <summary>Projectiles that always hit (Force Bolt).</summary>
    Missiles,
}

/// <summary>
/// An active ability, read from abilities.json. Costs are in the class resource (Fury, Mana). <see cref="Actions"/>
/// is the Pathfinder action cost, which becomes the wind-up time (0.5 s per action, never faster than a weapon swing
/// for weapon abilities).
/// </summary>
public sealed record AbilityDefinition(
    string Id,
    string Name,
    string Class,
    string Slot,
    AbilityKind Kind,
    int Actions = 1,
    double Range = 0,
    double Cost = 0,
    double Cooldown = 0,
    bool Repeat = false,
    int ExtraDice = 0,
    int CadenceAttacks = 1,
    string? Dice = null,
    string? HeightenDice = null,
    string? CritPersistent = null,
    int Missiles = 1,
    DamageType DamageType = DamageType.Bludgeoning,
    IReadOnlyList<string>? Traits = null,
    string Description = "")
{
    public bool Has(string trait) => Traits?.Contains(trait, StringComparer.OrdinalIgnoreCase) == true;

    public bool IsFlourish => Has("Flourish");

    public bool IsSpell => Kind is AbilityKind.SpellBolt or AbilityKind.Missiles;

    public bool UsesWeapon => Kind is AbilityKind.WeaponStrike or AbilityKind.ChargeStrike;

    /// <summary>A Pathfinder spell rank for a caster of <paramref name="level"/>: half the level, rounded up.</summary>
    public static int SpellRank(int level) => Math.Max(1, (level + 1) / 2);

    /// <summary>The damage dice at a spell rank: the base dice plus <see cref="HeightenDice"/> per rank above 1.</summary>
    public Dice DamageAt(int rank)
    {
        var dice = Dice is { } text ? Rules.Dice.Parse(text) : new Rules.Dice(0, 1);
        if (HeightenDice is { } extra && rank > 1)
        {
            var step = Rules.Dice.Parse(extra);
            dice = dice with { Count = dice.Count + step.Count * (rank - 1), Bonus = dice.Bonus + step.Bonus * (rank - 1) };
        }
        return dice;
    }

    /// <summary>Weapon abilities' extra dice grow at levels 10 and 18, like Power Attack.</summary>
    public int ExtraDiceAt(int level) => ExtraDice == 0 ? 0 : ExtraDice + (level >= 10 ? 1 : 0) + (level >= 18 ? 1 : 0);

    /// <summary>Force Bolt-style missile count: one more every two spell ranks.</summary>
    public int MissilesAt(int rank) => Missiles + (rank - 1) / 2;
}
