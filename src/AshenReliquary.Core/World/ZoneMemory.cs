using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>A trader's table as it was left: the level it was stocked for and what was on it.</summary>
public sealed record VendorMemory(string Id, int StockedForLevel, IReadOnlyList<Item> Stock);

/// <summary>
/// What a zone keeps while the hero is elsewhere: the placed monsters killed, the chests opened, the explored map,
/// the loot left lying, whether it was completed and its traders' tables. Kept for the layout <see cref="Revision"/>
/// it was made on; a zone remade since starts afresh.
/// </summary>
public sealed record ZoneMemory
{
    public int Revision { get; init; } = 1;
    public IReadOnlyCollection<int> Slain { get; init; } = [];
    public IReadOnlyCollection<string> OpenedCaches { get; init; } = [];
    public string? Explored { get; init; }
    public IReadOnlyList<GroundItem> Ground { get; init; } = [];
    public bool Complete { get; init; }
    public IReadOnlyList<VendorMemory> Vendors { get; init; } = [];
}
