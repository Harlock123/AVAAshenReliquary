using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>What a crafting attempt came to: the check and what it did.</summary>
public sealed record CraftOutcome(CheckResult Check, string Message);

/// <summary>
/// Crafting at the smith's forge (M3 plan, step 8; see <see cref="Crafting"/>): salvage gear into materials, and work
/// the four recipes on a Crafting check. Only at a smith (a trader with Runework), as rune work is.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>Whether a smith's forge is open (Garrow).</summary>
    public bool AtForge => OpenVendor is { Definition.Runework: true };

    /// <summary>The belt items the forge can brew at the hero's level.</summary>
    public IReadOnlyList<ItemDefinition> Brewable =>
        _loot?.Items.Values.Where(i => i.IsBeltItem && i.MinLevel <= Hero.Character.Level).ToList() ?? [];

    /// <summary>Breaks a bag item down into Ashen Dust and Rune Fragments. False (with a word) when it can't be.</summary>
    public bool Salvage(Item item)
    {
        var gear = Hero.Equipment;
        if (!AtForge || !gear.Bag.Contains(item))
        {
            if (gear.Worn.Values.Contains(item))
                Say($"Take {item.Name} off before you break it down.");
            return false;
        }
        if (Crafting.CannotSalvage(item) is { } why)
        {
            Say(why);
            return false;
        }
        var (dust, fragments) = Crafting.Salvage(item);
        gear.TakeFromBag(gear.Bag.ToList().IndexOf(item));
        gear.AddMaterials(dust, fragments);
        Sound(SoundCue.Ward, Hero, "shield");
        Say($"You break down {item.Name}: {dust} Ashen Dust{(fragments > 0 ? $", {fragments} Rune Fragment{(fragments == 1 ? "" : "s")}" : "")}.");
        return true;
    }

    /// <summary>
    /// Works a recipe on an item (Raise, Add an affix, Reroll the affix at <paramref name="affix"/>) or brews a belt
    /// item: pays the smith's fee, makes the Crafting check against the item level's DC, and on a success does the work.
    /// Null (with a word) when it can't be tried.
    /// </summary>
    public CraftOutcome? Craft(CraftRecipe recipe, Item? item = null, int affix = 0, ItemDefinition? brew = null)
    {
        var gear = Hero.Equipment;
        if (!AtForge || _loot is null)
            return null;
        int level;
        if (recipe == CraftRecipe.Brew)
        {
            if (brew is not { IsBeltItem: true } || !Brewable.Contains(brew))
                return null;
            level = Math.Max(1, brew.MinLevel);
        }
        else
        {
            if (item is null || !Owns(item))
                return null;
            if (Crafting.CannotCraft(recipe, item) is { } why)
            {
                Say(why);
                return null;
            }
            level = item.ItemLevel;
        }
        var (dust, fragments, fee) = Crafting.Cost(recipe, level);
        if (gear.Dust < dust || gear.Fragments < fragments)
        {
            Say($"You need {dust} Ashen Dust{(fragments > 0 ? $" and {fragments} Rune Fragment{(fragments == 1 ? "" : "s")}" : "")}.");
            return null;
        }
        if (!Pay(fee))
        {
            Say($"The forge costs {fee} gold.");
            return null;
        }
        var dc = Crafting.Dc(level);
        var check = Check.Roll(Random, Hero.Character.Skill("Crafting").Total, dc);
        var rolled = $"Crafting {check.Total} against DC {dc}";
        if (check.Degree <= DegreeOfSuccess.Failure)
        {
            var lost = check.Degree == DegreeOfSuccess.CriticalFailure;
            if (lost)
                gear.TrySpendMaterials(dust / 2, fragments / 2);
            Sound(SoundCue.Miss, Hero);
            return Done(check, lost ? $"{rolled}: a critical failure. The work is spoiled and half the materials with it."
                : $"{rolled}: a failure. Nothing comes of it; you keep the materials.");
        }
        gear.TrySpendMaterials(dust, fragments);
        var critical = check.Degree == DegreeOfSuccess.CriticalSuccess;
        Sound(SoundCue.Ward, Hero, "shield");
        if (recipe == CraftRecipe.Brew)
        {
            var made = new Item(brew!, Rarity.Common, Hero.Character.Level, count: critical ? 3 : 2);
            var count = made.Count;
            if (!gear.TryAdd(made))
                Spill(Hero.X, Hero.Y, [new LootDrop(made)]);
            return Done(check, $"{rolled}: {(critical ? "a critical success" : "a success")}. You brew {count} × {brew!.Name}.");
        }
        var affixes = item!.Affixes.ToList();
        var rarity = item.Rarity;
        switch (recipe)
        {
            case CraftRecipe.Raise:
                rarity = Rarity.Uncommon;
                AddAffix(item, rarity, affixes);
                break;
            case CraftRecipe.AddAffix:
                AddAffix(item, rarity, affixes);
                break;
            default:
                var at = Math.Clamp(affix, 0, affixes.Count - 1);
                var old = affixes[at];
                affixes.RemoveAt(at);
                if (_loot.RollAffix(item.Definition, item.ItemLevel, rarity, affixes, Random) is { } fresh)
                    affixes.Insert(at, fresh);
                else
                    affixes.Insert(at, old);
                break;
        }
        // A critical success: a bonus affix, if the piece has room for one.
        var bonus = critical && affixes.Count < Crafting.MaxAffixes(rarity) && AddAffix(item, rarity, affixes);
        item.Reforge(rarity, affixes);
        gear.Refresh();
        return Done(check, $"{rolled}: {(critical ? "a critical success" : "a success")}. {item.Name} now has {string.Join(", ", affixes.Select(a => a.Describe()))}"
            + (bonus ? " (a bonus affix)." : "."));

        CraftOutcome Done(CheckResult result, string message)
        {
            Say(message);
            return new CraftOutcome(result, message);
        }
    }

    private bool AddAffix(Item item, Rarity rarity, List<AffixRoll> affixes)
    {
        if (_loot!.RollAffix(item.Definition, item.ItemLevel, rarity, affixes, Random) is not { } fresh)
            return false;
        affixes.Add(fresh);
        return true;
    }
}
