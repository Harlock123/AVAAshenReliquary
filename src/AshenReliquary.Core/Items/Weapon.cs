using AshenReliquary.Core.Combat;

namespace AshenReliquary.Core.Items;

public enum WeaponTrait
{
    Agile,
    Finesse,
    Reach,
    Sweep,
    Forceful,
    Deadly,
    Fatal,
    TwoHand,
    Thrown,
    Backstabber,
    Trip,
    Shove,
    Parry,
    Volley,
    Versatile,
}

/// <summary>
/// A base weapon. <see cref="DamageDie"/> is the die size (8 for a d8). <see cref="DeadlyDie"/> and
/// <see cref="FatalDie"/> are only set when the weapon has that trait.
/// </summary>
public sealed record WeaponDefinition(
    string Id,
    string Name,
    int DamageDie,
    DamageType DamageType,
    double AttacksPerSecond,
    bool IsRanged = false,
    IReadOnlySet<WeaponTrait>? Traits = null,
    int? DeadlyDie = null,
    int? FatalDie = null)
{
    public IReadOnlySet<WeaponTrait> Traits { get; init; } = Traits ?? new HashSet<WeaponTrait>();

    public bool Has(WeaponTrait trait) => Traits.Contains(trait);
}

/// <summary>
/// A weapon with its fundamental runes. Potency (+1 to +3) is an item bonus to attack;
/// striking (0 to 3: none, striking, greater, major) adds damage dice.
/// </summary>
public sealed record Weapon(WeaponDefinition Definition, int Potency = 0, int Striking = 0)
{
    public int DiceCount => 1 + Striking;

    /// <summary>A weapon holds property runes up to its potency value, as in Pathfinder.</summary>
    public int PropertyRuneSlots => Potency;
}
