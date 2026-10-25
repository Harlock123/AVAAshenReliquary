using AshenReliquary.Core.Items;

namespace AshenReliquary.Core.World;

/// <summary>
/// Where an act begins for a hero started there (M4 step 10, for the exit test's play of Acts 2-4): the zone its road
/// starts in, the level a hero reaches it at in the campaign, the story flags of the acts before (their Heralds dead),
/// the hub shrines already woken for the Rift, and gold to spend at the act's smith.
/// </summary>
public sealed record ActStart(int Act, string Zone, int Level, IReadOnlyDictionary<string, string> Flags, IReadOnlyList<string> Shrines, int Gold);

public sealed partial class GameWorld
{
    /// <summary>
    /// The acts a hero can start at: Act 1 on the gallows road; Act 2 on the Iron March past Sallow's Mire (level 6);
    /// Act 3 at the foot of the Frostbitten Pass (level 11); Act 4 at the Ember Gate camp (level 15).
    /// </summary>
    public static readonly IReadOnlyList<ActStart> ActStarts =
    [
        new(1, "gallows_road", 1, new Dictionary<string, string>(), [], 0),
        new(2, "iron_march", 6, new Dictionary<string, string> { ["act1.sallow"] = "slain" }, ["fort_shrine"], 900),
        new(3, "frostbitten_pass", 11, new Dictionary<string, string>
        {
            ["act1.sallow"] = "slain", ["act2.varro"] = "slain", ["act2.hadric"] = "slain",
        }, ["fort_shrine", "ward_shrine"], 3500),
        new(4, "ember_gate", 15, new Dictionary<string, string>
        {
            ["act1.sallow"] = "slain", ["act2.varro"] = "slain", ["act2.hadric"] = "slain", ["act3.bell_tower"] = "open", ["act3.choir"] = "slain",
        }, ["fort_shrine", "ward_shrine", "cloister_shrine"], 9000),
    ];

    /// <summary>
    /// Sets a new hero up at an act's start: the acts before it done (their Herald flags), their hubs' shrines woken, the
    /// hero raised to the act's level (the level-up choices left for the player), the worn kit made again at that item
    /// level with every fundamental rune it can hold (as a player's smith would have etched them), gold for the smith, and
    /// the hero rested. The world must already be in the act's zone.
    /// </summary>
    public void BeginAct(ActStart start)
    {
        foreach (var (flag, value) in start.Flags)
            _flags[flag] = value;
        foreach (var shrine in start.Shrines)
            _activatedShrines.Add(shrine);
        ApplyBoons();
        TrainTo(start.Level);
        var gear = Hero.Equipment;
        foreach (var (slot, worn) in gear.Worn.ToList())
        {
            var item = new Item(worn.Definition, worn.Rarity, Math.Max(worn.ItemLevel, start.Level));
            foreach (var kind in Runework.Kinds(item.Definition))
                item.SetRune(kind, Runework.Ceiling(item, kind));
            gear.Equip(item, slot);
        }
        gear.Refresh();
        if (start.Gold > 0)
            gear.AddGold(start.Gold);
        // A few more bombs for the harder road.
        if (start.Act > 1 && _loot?.Items.TryGetValue("alchemists_fire", out var fire) == true)
            gear.TryAdd(new Item(fire!, Rarity.Common, 1, count: 2));
        Hero.Character.RestAtShrine();
        PopulateInteractives(Zone);
        SettleFlaggedPacks();
    }
}
