using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.App;

/// <summary>One play session: the character, their gear and the zone they are in.</summary>
public sealed class GameSession
{
    private GameSession(GameWorld world, string classId)
    {
        World = world;
        ClassId = classId;
    }

    public GameWorld World { get; }

    /// <summary>The portrait picked at character creation (Assets/Portraits id), if any.</summary>
    public string? PortraitId { get; set; }

    /// <summary>This character's save file name (Services/SaveStore).</summary>
    public string SaveId { get; private init; } = "";

    /// <summary>Set when this game was loaded from a saved copy: its number (or older label), kept on every save over it.</summary>
    public int? CopyNumber { get; private init; }
    public string? Label { get; private init; }
    public string? CopyOf { get; private init; }

    /// <summary>The seed the game started from (saved, so loot and spawns stay reproducible).</summary>
    public int Seed { get; private init; }

    /// <summary>Seconds played, across saves.</summary>
    public double PlaySeconds { get; private set; }

    public void AddPlayTime(double seconds) => PlaySeconds += seconds;

    /// <summary>Writes this game to its save file and says so on screen.</summary>
    public void Save(bool announce = true)
    {
        Services.SaveStore.Write(SaveGame.Capture(World, SaveId, Seed, PortraitId, PlaySeconds, Label, CopyOf, CopyNumber));
        if (announce)
            World.Announce("Game saved.");
    }

    /// <summary>
    /// Saves a copy of the game as it is now, as a save of its own (a snapshot to go back to), numbered after the
    /// character's copies so far: Elirk-2, then Elirk-3. This game carries on saving over its own file. Returns the
    /// copy's name.
    /// </summary>
    public string SaveCopy()
    {
        // A copy of a copy is numbered among the character's own copies, not a chain of copies.
        var original = CopyOf ?? SaveId;
        var number = Services.SaveStore.NextCopyNumber(original);
        Services.SaveStore.Write(SaveGame.Capture(World, Services.SaveStore.CopyId(original, number), Seed, PortraitId, PlaySeconds,
            copyOf: original, copyNumber: number));
        var name = $"{World.Hero.Character.Name}-{number}";
        World.Announce($"Saved a copy: {name}. Your game still saves to its own file.");
        return name;
    }

    /// <summary>A game rebuilt from a save.</summary>
    public static GameSession FromSave(SaveGame save)
    {
        var world = save.Restore(ContentDatabase.Embedded);
        var session = new GameSession(world, save.Choices.ClassId)
        {
            PortraitId = save.PortraitId,
            SaveId = save.SaveId,
            Seed = save.Seed,
            Label = save.Label,
            CopyOf = save.CopyOf,
            CopyNumber = save.CopyNumber,
        };
        session.PlaySeconds = save.PlaySeconds;
        session.HookAutosave();
        world.Announce($"Welcome back, {world.Hero.Character.Name}.");
        return session;
    }

    /// <summary>Autosave when a shrine wakes, on entering a zone and on leaving a trader (the spec's checkpoint moments).</summary>
    private void HookAutosave()
    {
        World.ShrineActivated += _ => Save(announce: false);
        World.ZoneEntered += _ => Save(announce: false);
        World.TradeClosed += _ => Save(announce: false);
    }
    public string ClassId { get; }

    /// <summary>The paper-doll layers to draw: each worn item's Flare layer, or the bare body where nothing is worn.</summary>
    public IReadOnlyDictionary<string, string> AppearanceLayers
    {
        get
        {
            var layers = new Dictionary<string, string>
            {
                ["head"] = "head_short", ["chest"] = "default_chest", ["legs"] = "default_legs",
                ["feet"] = "default_feet", ["hands"] = "default_hands",
            };
            foreach (var (slot, item) in World.Hero.Equipment.Worn)
            {
                if (item.Definition.Gfx is not { } gfx)
                    continue;
                var layer = slot switch
                {
                    EquipSlot.MainHand => "main", EquipSlot.OffHand => "off", EquipSlot.Head => "head", EquipSlot.Chest => "chest",
                    EquipSlot.Hands => "hands", EquipSlot.Legs => "legs", EquipSlot.Feet => "feet", _ => null,
                };
                if (layer is not null)
                    layers[layer] = gfx;
            }
            return layers;
        }
    }

    /// <summary>The classes the vertical slice can start as: the ones with abilities and a starting kit.</summary>
    public static bool IsPlayable(string classId) => classId is "fighter" or "wizard";

    /// <summary>Each playable class's starting gear, all common item level 1 (the fighter's sword and shield).</summary>
    public static IReadOnlyList<string> StartingKit(string classId) => classId == "wizard"
        ? ["staff", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves"]
        : ["longsword", "wooden_shield", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"];

    /// <summary>What every hero starts with on the belt: a few bombs and an antidote.</summary>
    public static IReadOnlyList<(string Id, int Count)> StartingBelt { get; } = [("alchemists_fire", 3), ("antidote", 1)];

    /// <summary>Puts a class's starting kit on a character, and its belt items in the bag (the game and the creation preview).</summary>
    public static Equipment Dress(Character hero, Equipment gear, string classId)
    {
        var content = ContentDatabase.Embedded;
        foreach (var id in StartingKit(classId))
            gear.Equip(new Item(content.Item(id), Rarity.Common, 1));
        foreach (var (id, count) in StartingBelt)
            gear.TryAdd(new Item(content.Item(id), Rarity.Common, 1, count: count));
        return gear;
    }

    /// <summary>A new game from the character creation screen's choices, on Gallows Road.</summary>
    public static GameSession NewGame(CharacterChoices choices, int seed, string zoneId = "gallows_road")
    {
        var content = ContentDatabase.Embedded;
        var hero = CharacterBuilder.Create(content, choices);
        var world = new GameWorld(content.Zone(zoneId), content.Creatures, hero, seed, content.AbilityBar(choices.ClassId), content.Loot, content.Zones, content.Dialogues, content.Abilities);
        Dress(hero, world.Hero.Equipment, choices.ClassId);
        hero.RestAtShrine();
        var session = new GameSession(world, choices.ClassId) { SaveId = Services.SaveStore.NewSaveId(choices.Name), Seed = seed };
        session.HookAutosave();
        return session;
    }

    /// <summary>A quick start (tests, "--wizard"): a Human with the recommended build for the class.</summary>
    public static GameSession NewGame(string classId, int seed, string zoneId = "gallows_road")
    {
        var session = NewGame(CharacterBuilder.Recommend(ContentDatabase.Embedded, "The Ashbound", "human",
            classId == "wizard" ? "hedge-scholar" : "deserter", classId), seed, zoneId);
        session.PortraitId = Services.Portraits.For("human").FirstOrDefault()?.Id;
        return session;
    }
}
