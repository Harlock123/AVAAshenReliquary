namespace AshenReliquary.Core.Items;

/// <summary>What can be made at the forge (M3 plan, step 8; decision 5: these four, more with the acts).</summary>
public enum CraftRecipe
{
    /// <summary>A common piece of gear becomes uncommon, with an affix.</summary>
    Raise,

    /// <summary>An uncommon or rare piece gains an affix (up to its rarity's limit).</summary>
    AddAffix,

    /// <summary>One affix of an uncommon or rare piece is rolled again.</summary>
    Reroll,

    /// <summary>A belt item (a bomb or an antidote), two at a time.</summary>
    Brew,
}

/// <summary>
/// Salvage and crafting (spec, section 11): gear breaks down into Ashen Dust and Rune Fragments; recipes spend them,
/// and the smith's fee, on a Crafting check against a DC by item level. A critical success adds a bonus (another
/// affix, or a third belt item); a failure changes nothing and keeps the materials; a critical failure loses half of
/// them. The fee is paid whatever the result.
/// </summary>
public static class Crafting
{
    /// <summary>Most affixes a rarity holds.</summary>
    public static int MaxAffixes(Rarity rarity) => rarity switch { Rarity.Uncommon => 2, Rarity.Rare => 4, _ => 0 };

    /// <summary>
    /// Pathfinder's level-based DC (GM Core's table, levels 0-20): 14 at level 0, 15 at 1, 16 at 2, 18 at 3 ... 40 at 20.
    /// </summary>
    public static int Dc(int level)
    {
        int[] table = [14, 15, 16, 18, 19, 20, 22, 23, 24, 26, 27, 28, 30, 31, 32, 34, 35, 36, 38, 39, 40];
        return table[Math.Clamp(level, 0, table.Length - 1)];
    }

    /// <summary>What salvaging an item gives: Dust by rarity and item level, Fragments from its runes (and its rarity).</summary>
    public static (int Dust, int Fragments) Salvage(Item item)
    {
        var level = Math.Max(1, item.ItemLevel);
        var dust = item.Rarity switch
        {
            Rarity.Common => 1 + level / 3,
            Rarity.Uncommon => 2 + level / 2,
            Rarity.Rare => 4 + level,
            _ => 8 + 2 * level,
        };
        var fragments = 2 * (item.Potency + item.Striking + item.Resilient)
            + item.Rarity switch { Rarity.Rare => 1, Rarity.Unique or Rarity.Set => 3, _ => 0 };
        return (dust, fragments);
    }

    /// <summary>Why an item can't be salvaged, or null.</summary>
    public static string? CannotSalvage(Item item) =>
        item.Definition.Slot is null ? "Only gear can be salvaged."
        : item.Mark == ItemMark.Locked ? $"{item.Name} is locked."
        : null;

    /// <summary>A recipe's cost on an item (or, brewing, at the hero's level): Dust, Fragments and the smith's fee in gold.</summary>
    public static (int Dust, int Fragments, int Gold) Cost(CraftRecipe recipe, int itemLevel)
    {
        var level = Math.Max(1, itemLevel);
        return recipe switch
        {
            CraftRecipe.Raise => (3 + level, 0, 3 + 2 * level),
            CraftRecipe.AddAffix => (5 + level, 1, 3 + 2 * level),
            CraftRecipe.Reroll => (2 + level, 1, 3 + 2 * level),
            _ => (3, 0, 4),
        };
    }

    /// <summary>Why a recipe can't be worked on an item, or null (brewing takes no item).</summary>
    public static string? CannotCraft(CraftRecipe recipe, Item item) => item.Definition.Slot is null ? "Only gear can be worked."
        : item.Definition.IsSpecial ? $"{item.Name} is made as it is."
        : recipe switch
        {
            CraftRecipe.Raise when item.Rarity != Rarity.Common => "Only a common piece can be raised.",
            CraftRecipe.AddAffix when item.Rarity is not (Rarity.Uncommon or Rarity.Rare) => "Only uncommon and rare pieces take affixes.",
            CraftRecipe.AddAffix when item.Affixes.Count >= MaxAffixes(item.Rarity) => $"It holds all the affixes a{(item.Rarity == Rarity.Uncommon ? "n uncommon" : " rare")} piece can.",
            CraftRecipe.Reroll when item.Affixes.Count == 0 => "It has no affix to reroll.",
            _ => null,
        };
}
