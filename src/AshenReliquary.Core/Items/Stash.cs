namespace AshenReliquary.Core.Items;

/// <summary>The hub's stash: 60 slots of storage that stay put while the hero travels. Saved with the character.</summary>
public sealed class Stash
{
    public const int Columns = 10;
    public const int Rows = 6;
    public const int Size = Columns * Rows;

    private readonly Item?[] _slots = new Item?[Size];

    public IReadOnlyList<Item?> Slots => _slots;

    /// <summary>Puts an item in the first free slot; false when the stash is full.</summary>
    public bool TryAdd(Item item)
    {
        var free = Array.IndexOf(_slots, null);
        if (free < 0)
            return false;
        _slots[free] = item;
        return true;
    }

    /// <summary>Takes the item out of a slot, or null if it is empty.</summary>
    public Item? Take(int index)
    {
        if (index < 0 || index >= Size || _slots[index] is not { } item)
            return null;
        _slots[index] = null;
        return item;
    }

    internal void Restore(IEnumerable<Item?> items)
    {
        var i = 0;
        foreach (var item in items.Take(Size))
            _slots[i++] = item;
    }
}
