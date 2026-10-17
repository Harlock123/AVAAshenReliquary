using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Items;

/// <summary>
/// The worn items and the 10x6 bag. Equipping updates the character at once: the main hand becomes the weapon,
/// the chest piece the armour, and every item's affixes and implicit bonuses are summed into its gear bonuses.
/// </summary>
public sealed class Equipment
{
    public const int BagColumns = 10;
    public const int BagRows = 6;
    public const int BagSize = BagColumns * BagRows;

    private readonly Character _character;
    private readonly Dictionary<EquipSlot, Item> _worn = [];
    private readonly Item?[] _bag = new Item?[BagSize];

    public Equipment(Character character) => _character = character;

    public IReadOnlyDictionary<EquipSlot, Item> Worn => _worn;
    public IReadOnlyList<Item?> Bag => _bag;
    public int Gold { get; private set; }

    /// <summary>Raised whenever what is worn changes (the paper doll redraws).</summary>
    public event Action? Changed;

    public Item? In(EquipSlot slot) => _worn.GetValueOrDefault(slot);

    public void AddGold(int amount) => Gold += Math.Max(0, amount);

    /// <summary>Puts an item in the first free bag cell. False when the bag is full.</summary>
    public bool TryAdd(Item item)
    {
        var free = Array.IndexOf(_bag, null);
        if (free < 0)
            return false;
        _bag[free] = item;
        return true;
    }

    public Item? TakeFromBag(int index)
    {
        var item = _bag[index];
        _bag[index] = null;
        return item;
    }

    /// <summary>The slot an item goes to: rings take the free hand (the first if both are full).</summary>
    public EquipSlot? SlotFor(Item item)
    {
        if (item.Definition.Slot is not { } slot)
            return null;
        if (slot is EquipSlot.Ring1 or EquipSlot.Ring2)
            return !_worn.ContainsKey(EquipSlot.Ring1) ? EquipSlot.Ring1 : !_worn.ContainsKey(EquipSlot.Ring2) ? EquipSlot.Ring2 : EquipSlot.Ring1;
        return slot;
    }

    /// <summary>Wears an item straight away (starting gear). Whatever was in its slot is returned.</summary>
    public Item? Equip(Item item, EquipSlot? slot = null)
    {
        var target = slot ?? SlotFor(item) ?? throw new RulesException($"{item.Name} can't be worn.");
        _worn.TryGetValue(target, out var previous);
        _worn[target] = item;
        Refresh();
        return previous;
    }

    /// <summary>Wears the item in bag cell <paramref name="index"/>, swapping what was worn into that cell.</summary>
    public bool EquipFromBag(int index)
    {
        if (_bag[index] is not { } item || SlotFor(item) is not { } slot)
            return false;
        _bag[index] = null;
        var previous = Equip(item, slot);
        _bag[index] = previous;
        return true;
    }

    /// <summary>Takes off the item in <paramref name="slot"/> into the bag. False when the bag is full.</summary>
    public bool Unequip(EquipSlot slot)
    {
        if (!_worn.TryGetValue(slot, out var item))
            return false;
        var free = Array.IndexOf(_bag, null);
        if (free < 0)
            return false;
        _worn.Remove(slot);
        _bag[free] = item;
        Refresh();
        return true;
    }

    /// <summary>
    /// What wearing <paramref name="item"/> would do: the character's statistics now and with it worn in the slot it
    /// would take. Nothing changes; the gear is put back before returning.
    /// </summary>
    public IReadOnlyList<StatChange> PreviewEquip(Item item) =>
        SlotFor(item) is { } slot ? Preview(() => _worn[slot] = item) : [];

    /// <summary>What taking off the item in <paramref name="slot"/> would do to the character's statistics.</summary>
    public IReadOnlyList<StatChange> PreviewUnequip(EquipSlot slot) => Preview(() => _worn.Remove(slot));

    private IReadOnlyList<StatChange> Preview(Action change)
    {
        var before = StatSnapshot.Take(_character);
        var saved = new Dictionary<EquipSlot, Item>(_worn);
        var hp = _character.HitPoints;
        change();
        Apply();
        var after = StatSnapshot.Take(_character);
        _worn.Clear();
        foreach (var (slot, item) in saved)
            _worn[slot] = item;
        Apply();
        _character.RestoreHitPoints(hp);
        return StatSnapshot.Compare(before, after);
    }

    /// <summary>Re-applies weapon, armour and gear bonuses to the character.</summary>
    public void Refresh()
    {
        Apply();
        Changed?.Invoke();
    }

    private void Apply()
    {
        _character.Weapon = _worn.TryGetValue(EquipSlot.MainHand, out var weapon) ? weapon.ToWeapon() : new Weapon(Character.Fist);
        _character.Armor = _worn.TryGetValue(EquipSlot.Chest, out var armor) ? armor.ToArmor() : Armor.None;
        _character.ApplyGear(Sum(_worn.Values));
    }

    /// <summary>The gear bonuses a set of items adds up to.</summary>
    public static GearBonuses Sum(IEnumerable<Item> items)
    {
        int hp = 0, damage = 0;
        double move = 0, attack = 0, cast = 0, regen = 0, fury = 0;
        var resist = new Dictionary<DamageType, int>();
        var saves = new Dictionary<StatisticKind, int>();
        foreach (var item in items)
        {
            if (item.Definition.Implicit is { } imp)
            {
                hp += imp.MaxHitPoints;
                move += imp.MoveSpeed;
                attack += imp.AttackSpeed;
                cast += imp.CastSpeed;
                regen += imp.ResourceRegen;
            }
            foreach (var affix in item.Affixes)
            {
                var v = affix.Value;
                switch (affix.Definition.Stat)
                {
                    case AffixStat.MaxHitPoints: hp += v; break;
                    case AffixStat.MoveSpeed: move += v / 100.0; break;
                    case AffixStat.AttackSpeed: attack += v / 100.0; break;
                    case AffixStat.CastSpeed: cast += v / 100.0; break;
                    case AffixStat.ResourceRegen: regen += v / 100.0; break;
                    case AffixStat.FuryPerHit: fury += v; break;
                    case AffixStat.WeaponDamage: damage += v; break;
                    case AffixStat.Resistance when affix.Definition.Resist is { } type:
                        resist[type] = resist.GetValueOrDefault(type) + v;
                        break;
                    case AffixStat.Fortitude: saves[StatisticKind.Fortitude] = saves.GetValueOrDefault(StatisticKind.Fortitude) + v; break;
                    case AffixStat.Reflex: saves[StatisticKind.Reflex] = saves.GetValueOrDefault(StatisticKind.Reflex) + v; break;
                    case AffixStat.Will: saves[StatisticKind.Will] = saves.GetValueOrDefault(StatisticKind.Will) + v; break;
                }
            }
        }
        return new GearBonuses
        {
            MaxHitPoints = hp, MoveSpeed = move, AttackSpeed = attack, CastSpeed = cast, ResourceRegen = regen,
            FuryPerHit = fury, WeaponDamage = damage, Resistances = resist, Saves = saves,
        };
    }
}
