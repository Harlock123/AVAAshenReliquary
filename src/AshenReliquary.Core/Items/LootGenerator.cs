using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Items;

/// <summary>A single thing dropped: an item, gold, or a healing draught (a potion charge).</summary>
public sealed record LootDrop(Item? Item, int Gold = 0, bool Draught = false);

/// <summary>
/// Rolls drops from the spec's Act 1 tables. Rarity: 70% common, 25% uncommon, 5% rare (uniques and sets come
/// later). Item level is the monster's effective level; runes are gated by item level (weapon potency from 2,
/// striking from 4, armour potency from 5, resilient from 8). Uncommon items get 1 to 2 affixes, rares 3 to 4.
/// </summary>
public sealed class LootGenerator(IReadOnlyDictionary<string, ItemDefinition> items, IReadOnlyList<AffixDefinition> affixes)
{
    public IReadOnlyDictionary<string, ItemDefinition> Items { get; } = items;

    public IReadOnlyList<LootDrop> Roll(CreatureRank rank, int level, IRandomSource random, bool spawner = false)
    {
        var drops = new List<LootDrop>();
        var (itemChance, items, goldChance, goldMultiplier) = rank switch
        {
            CreatureRank.Minion => (4, 1, 15, 0.5),
            CreatureRank.Normal => (14, 1, 35, 1.0),
            CreatureRank.Champion => (100, 2, 100, 3.0),
            CreatureRank.Elite => (100, 3, 100, 5.0),
            _ => (100, 4, 100, 10.0),
        };
        if (spawner)
            (itemChance, items, goldChance, goldMultiplier) = (100, 1, 100, 2.0);

        for (var i = 0; i < items; i++)
        {
            // The first roll uses the rank's chance; extra rolls on bigger monsters are 50/50.
            if (random.Roll(1, 100) <= (i == 0 ? itemChance : 50))
                drops.Add(new LootDrop(Generate(Math.Max(1, level), RollRarity(rank, random), random)));
        }
        if (random.Roll(1, 100) <= goldChance)
            drops.Add(new LootDrop(null, Gold: (int)Math.Max(1, random.Roll(2, 6) * (1 + level) * goldMultiplier)));
        if (rank != CreatureRank.Minion && random.Roll(1, 100) <= 5)
            drops.Add(new LootDrop(null, Draught: true));
        return drops;
    }

    /// <summary>
    /// A chest's contents at <paramref name="level"/> (the hero's): a plain cache holds one item (45% common, 45%
    /// uncommon, 10% rare), 3 to 18 x (1 + level) gold and a 30% chance of a healing draught; a reliquary holds two items,
    /// the first at least uncommon (25% rare), triple the gold and a 60% chance of a draught.
    /// </summary>
    public IReadOnlyList<LootDrop> RollCache(World.CacheKind kind, int level, IRandomSource random)
    {
        level = Math.Max(1, level);
        var drops = new List<LootDrop>();
        if (kind == World.CacheKind.Reliquary)
        {
            drops.Add(new LootDrop(Generate(level, random.Roll(1, 100) <= 25 ? Rarity.Rare : Rarity.Uncommon, random)));
            drops.Add(new LootDrop(Generate(level, RollRarity(CreatureRank.Champion, random), random)));
        }
        else
        {
            var roll = random.Roll(1, 100);
            drops.Add(new LootDrop(Generate(level, roll <= 45 ? Rarity.Common : roll <= 90 ? Rarity.Uncommon : Rarity.Rare, random)));
        }
        var gold = random.Roll(3, 18) * (1 + level) * (kind == World.CacheKind.Reliquary ? 3 : 1);
        drops.Add(new LootDrop(null, Gold: gold));
        if (random.Roll(1, 100) <= (kind == World.CacheKind.Reliquary ? 60 : 30))
            drops.Add(new LootDrop(null, Draught: true));
        return drops;
    }

    public static Rarity RollRarity(CreatureRank rank, IRandomSource random)
    {
        var roll = random.Roll(1, 1000);
        var rarity = roll <= 700 ? Rarity.Common : roll <= 950 ? Rarity.Uncommon : Rarity.Rare;
        // Champions and better have a coin-flip chance to lift a common drop to uncommon.
        if (rarity == Rarity.Common && rank >= CreatureRank.Champion && random.Roll(1, 2) == 2)
            rarity = Rarity.Uncommon;
        return rarity;
    }

    /// <param name="only">Narrows the pool (a smith's stock); null allows any equippable item.</param>
    public Item Generate(int itemLevel, Rarity rarity, IRandomSource random, Func<ItemDefinition, bool>? only = null)
    {
        var pool = Items.Values.Where(i => i.Slot is not null && i.MinLevel <= itemLevel && (!i.IsJewelry || rarity != Rarity.Common)
            && (only is null || only(i))).ToList();
        var definition = Pick(pool, random);

        int potency = 0, striking = 0, resilient = 0;
        var runeBoost = rarity == Rarity.Common ? 0 : 20;
        if (definition.IsWeapon)
        {
            if (Runework.Ceiling(definition, itemLevel, RuneKind.Potency) > 0 && random.Roll(1, 100) <= 30 + runeBoost)
                potency = 1;
            if (Runework.Ceiling(definition, itemLevel, RuneKind.Striking) > 0 && random.Roll(1, 100) <= 20 + runeBoost)
                striking = 1;
        }
        else if (definition.IsBodyArmor)
        {
            if (Runework.Ceiling(definition, itemLevel, RuneKind.Potency) > 0 && random.Roll(1, 100) <= 30 + runeBoost)
                potency = 1;
            if (Runework.Ceiling(definition, itemLevel, RuneKind.Resilient) > 0 && random.Roll(1, 100) <= 20 + runeBoost)
                resilient = 1;
        }

        var count = rarity switch
        {
            Rarity.Uncommon => random.Roll(1, 2),
            Rarity.Rare => random.Roll(3, 4),
            _ => 0,
        };
        var rolled = new List<AffixRoll>();
        var candidates = affixes.Where(a => a.Fits(definition, itemLevel)).ToList();
        for (var i = 0; i < count && candidates.Count > 0; i++)
        {
            var prefixes = rolled.Count(r => r.Definition.Prefix);
            var suffixes = rolled.Count - prefixes;
            var max = rarity == Rarity.Uncommon ? 1 : 2;
            var allowed = candidates.Where(a => (a.Prefix ? prefixes : suffixes) < max).ToList();
            if (allowed.Count == 0)
                break;
            var affix = allowed[random.Roll(0, allowed.Count - 1)];
            candidates.RemoveAll(a => a.Stat == affix.Stat && a.Resist == affix.Resist);
            var low = (int)Math.Round(affix.Min + affix.PerLevel * itemLevel);
            var high = (int)Math.Round(affix.Max + affix.PerLevel * itemLevel);
            rolled.Add(new AffixRoll(affix, random.Roll(Math.Max(1, low), Math.Max(1, Math.Max(low, high)))));
        }

        var name = rarity == Rarity.Rare ? RareName(definition, random) : null;
        return new Item(definition, rarity, itemLevel, rolled, potency, striking, resilient, name);
    }

    private static ItemDefinition Pick(List<ItemDefinition> pool, IRandomSource random)
    {
        var total = pool.Sum(i => i.Weight);
        var roll = random.Roll(1, total);
        foreach (var item in pool)
        {
            roll -= item.Weight;
            if (roll <= 0)
                return item;
        }
        return pool[^1];
    }

    private static readonly string[] RareFirst = ["Ash", "Grave", "Gallows", "Mire", "Ember", "Rot", "Bone", "Hollow", "Cinder", "Dusk", "Sallow", "Lantern"];
    private static readonly string[] RareSecond = ["bite", "song", "ward", "mark", "fang", "veil", "shroud", "call", "thorn", "grip", "oath", "keeper"];

    private static string RareName(ItemDefinition definition, IRandomSource random) =>
        $"{RareFirst[random.Roll(0, RareFirst.Length - 1)]}{RareSecond[random.Roll(0, RareSecond.Length - 1)]} {definition.Name}";
}
