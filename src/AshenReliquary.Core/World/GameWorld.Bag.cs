using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>
/// Keeping the bag in order: judging items for the hero (<see cref="ItemJudge"/>), sorting the bag and the stash,
/// marking junk and dropping or selling it in one go, dropping worn gear, and moving items to a chosen bag or stash cell
/// (dragged there).
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>Whether the hero has a way to raise a shield (the fighter); without one a shield is a poor fit.</summary>
    public bool RaisesShields => _classBar.Values.Concat(_bar.Values).Any(a => a.NeedsShield);

    /// <summary>How an item suits the hero: its fit, and whether wearing it would be an upgrade.</summary>
    public ItemVerdict Judge(Item item) => ItemJudge.Judge(Hero.Character, Hero.Equipment, item, RaisesShields);

    /// <summary>The bag and stash order: upgrades, other usable gear, belt items, poor fits, keys; the most valuable first.</summary>
    private IComparer<Item> SortOrder(IEnumerable<Item> items)
    {
        var verdicts = items.Distinct().ToDictionary(i => i, Judge);
        return ItemJudge.SortOrder(i => verdicts.TryGetValue(i, out var v) ? v : Judge(i));
    }

    /// <summary>Sorts the bag for the hero.</summary>
    public void SortBag()
    {
        var equipment = Hero.Equipment;
        equipment.Sort(SortOrder(equipment.Bag.OfType<Item>()));
        Sound(SoundCue.Pickup, Hero, "cloth");
    }

    /// <summary>Sorts the stash the same way (while it is open).</summary>
    public void SortStash()
    {
        if (!StashOpen)
            return;
        Stash.Sort(SortOrder(Stash.Slots.OfType<Item>()));
        Sound(SoundCue.Pickup, Hero, "cloth");
    }

    /// <summary>The bag items marked as junk.</summary>
    public IReadOnlyList<Item> Junk => Hero.Equipment.Bag.OfType<Item>().Where(i => i.Mark == ItemMark.Junk).ToList();

    /// <summary>Drops every bag item marked as junk at the hero's feet; returns how many.</summary>
    public int DropJunk()
    {
        var count = 0;
        var equipment = Hero.Equipment;
        for (var i = 0; i < Equipment.BagSize; i++)
        {
            if (equipment.Bag[i] is not { Mark: ItemMark.Junk, Definition.Category: not ItemCategory.Key } || equipment.TakeFromBag(i) is not { } item)
                continue;
            Spill(Hero.X, Hero.Y, [new LootDrop(item)]);
            count++;
        }
        if (count > 0)
        {
            Sound(SoundCue.LootDrop, Hero);
            Say($"Dropped {count} junk item{(count == 1 ? "" : "s")}.");
        }
        return count;
    }

    /// <summary>Sells every bag item marked as junk to the open trader; returns how many and the gold they brought.</summary>
    public (int Count, int Gold) SellJunk()
    {
        if (OpenVendor is null)
            return (0, 0);
        int count = 0, gold = 0;
        for (var i = 0; i < Equipment.BagSize; i++)
        {
            if (Hero.Equipment.Bag[i] is not { Mark: ItemMark.Junk } item)
                continue;
            var price = Pricing.SellPrice(item);
            if (!Sell(i))
                continue;
            count++;
            gold += price;
        }
        if (count > 0)
            Say($"Sold {count} junk item{(count == 1 ? "" : "s")} for {gold} gold.");
        return (count, gold);
    }

    /// <summary>Takes off a worn item and drops it at the hero's feet (dragged out of the inventory).</summary>
    public bool DropWorn(EquipSlot slot)
    {
        var equipment = Hero.Equipment;
        if (equipment.In(slot) is not { } item)
            return false;
        if (item.Mark == ItemMark.Locked)
        {
            Say($"{item.Name} is locked. Ctrl-click it to unlock it.");
            return false;
        }
        var free = equipment.Bag.ToList().IndexOf(null);
        if (free < 0 || !equipment.UnequipTo(slot, free))
            return false;
        return DropFromBag(free);
    }

    /// <summary>Moves a bag item into stash slot <paramref name="stashIndex"/> (dragged there), if it is free.</summary>
    public bool StoreAt(int bagIndex, int stashIndex)
    {
        if (!StashOpen || Hero.Equipment.Bag.ElementAtOrDefault(bagIndex) is not { } item || !Stash.TryAddAt(item, stashIndex))
            return false;
        Hero.Equipment.TakeFromBag(bagIndex);
        return true;
    }

    /// <summary>Moves a stash item into bag cell <paramref name="bagIndex"/> (dragged there), if it is free.</summary>
    public bool RetrieveAt(int stashIndex, int bagIndex)
    {
        var equipment = Hero.Equipment;
        if (!StashOpen || Stash.Slots.ElementAtOrDefault(stashIndex) is not { } item || bagIndex < 0 || bagIndex >= Equipment.BagSize
            || equipment.Bag[bagIndex] is not null)
            return false;
        var taken = Stash.Take(stashIndex)!;
        if (!equipment.TryAdd(taken))
        {
            Stash.TryAddAt(taken, stashIndex);
            return false;
        }
        // TryAdd used the first free cell (or topped up a stack); put it where it was dropped.
        var landed = equipment.Bag.ToList().IndexOf(taken);
        if (landed >= 0 && landed != bagIndex)
            equipment.Move(landed, bagIndex);
        return true;
    }
}
