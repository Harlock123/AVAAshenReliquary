using System.Text.Json.Serialization;
using AshenReliquary.Core.Combat;

namespace AshenReliquary.Core.Items;

/// <summary>Where an item is worn. Rings fill <see cref="Ring1"/> first, then <see cref="Ring2"/>.</summary>
public enum EquipSlot
{
    MainHand,
    OffHand,
    Head,
    Chest,
    Hands,
    Legs,
    Feet,
    Belt,
    Amulet,
    Ring1,
    Ring2,
}

public enum ItemCategory
{
    Weapon,
    Armor,
    Accessory,
    Consumable,
    /// <summary>Kept and carried, never sold, dropped or rolled as loot (the pilgrim's lantern).</summary>
    Key,
}

/// <summary>Pathfinder-style rarity tiers, shown in the spec's ARPG colours.</summary>
public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Unique,
    Set,
}

/// <summary>
/// A base item from items.json. <see cref="Icon"/> indexes Flare's icon sheet, <see cref="Gfx"/> names the paper-doll
/// layer drawn on the hero, <see cref="Loot"/> the sprite shown on the ground. Only weapons fill the weapon fields and
/// only body armour (the chest slot) the armour fields; other worn pieces are looks plus affixes. A shield (the
/// off-hand) gives <see cref="AcBonus"/> only while raised, and blocks <see cref="Hardness"/> of a blow. A consumable
/// names the <see cref="Ability"/> it is used as, stacks up to <see cref="Stack"/> in one bag cell and is worth
/// <see cref="Price"/> gold apiece. A weapon with the Versatile trait names its second damage type in
/// <see cref="Versatile"/>; a ranged weapon (a bow) shoots <see cref="Range"/> tiles.
/// </summary>
public sealed record ItemDefinition(
    string Id,
    string Name,
    ItemCategory Category,
    EquipSlot? Slot,
    int Icon,
    string Loot,
    string? Gfx = null,
    int MinLevel = 0,
    int Weight = 10,
    int DamageDie = 0,
    DamageType DamageType = DamageType.Bludgeoning,
    double AttacksPerSecond = 1.0,
    bool Ranged = false,
    IReadOnlyList<WeaponTrait>? Traits = null,
    int? DeadlyDie = null,
    int? FatalDie = null,
    ArmorCategory ArmorCategory = ArmorCategory.Unarmored,
    int AcBonus = 0,
    int DexCap = 99,
    int CheckPenalty = 0,
    int SpeedPenalty = 0,
    int Strength = 0,
    GearBonusesJson? Implicit = null,
    bool CasterFocus = false,
    double LightRadius = 0,
    string? Glyph = null,
    string? Description = null,
    bool Unique = false,
    int Hardness = 0,
    string? Ability = null,
    int Stack = 1,
    int Price = 0,
    DamageType? Versatile = null,
    double Range = 0)
{
    [JsonIgnore]
    public bool IsWeapon => Category == ItemCategory.Weapon;

    [JsonIgnore]
    public bool IsBodyArmor => Slot == EquipSlot.Chest;

    [JsonIgnore]
    public bool IsJewelry => Slot is EquipSlot.Amulet or EquipSlot.Ring1 or EquipSlot.Ring2;

    [JsonIgnore]
    public bool IsShield => Slot == EquipSlot.OffHand;

    /// <summary>A weapon held in both hands (a staff): no shield beside it.</summary>
    [JsonIgnore]
    public bool IsTwoHanded => IsWeapon && Traits?.Contains(WeaponTrait.TwoHand) == true;

    /// <summary>Used up from the belt (a bomb, an elixir).</summary>
    [JsonIgnore]
    public bool IsBeltItem => Category == ItemCategory.Consumable && Ability is not null;

    private WeaponDefinition? _weapon;
    private ArmorDefinition? _armor;

    public WeaponDefinition AsWeapon() => _weapon ??= new WeaponDefinition(Id, Name, DamageDie, DamageType, AttacksPerSecond, Ranged,
        new HashSet<WeaponTrait>(Traits ?? []), DeadlyDie, FatalDie) { VersatileType = Versatile, Range = Range };

    public ArmorDefinition AsArmor() => _armor ??= new ArmorDefinition(Id, Name, ArmorCategory, AcBonus, DexCap, CheckPenalty, SpeedPenalty, Strength);
}

/// <summary>The JSON shape of an implicit bonus on a base item (a wand's cast speed, a belt's hit points).</summary>
public sealed record GearBonusesJson(int MaxHitPoints = 0, double MoveSpeed = 0, double AttackSpeed = 0, double CastSpeed = 0, double ResourceRegen = 0);
