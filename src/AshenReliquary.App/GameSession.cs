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

    /// <summary>The seed the game started from (saved, so loot and spawns stay reproducible).</summary>
    public int Seed { get; private init; }

    /// <summary>Seconds played, across saves.</summary>
    public double PlaySeconds { get; private set; }

    public void AddPlayTime(double seconds) => PlaySeconds += seconds;

    /// <summary>Writes this game to its save file and says so on screen.</summary>
    public void Save(bool announce = true)
    {
        Services.SaveStore.Write(SaveGame.Capture(World, SaveId, Seed, PortraitId, PlaySeconds));
        if (announce)
            World.Announce("Game saved.");
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
        };
        session.PlaySeconds = save.PlaySeconds;
        session.HookAutosave();
        world.Announce($"Welcome back, {world.Hero.Character.Name}.");
        return session;
    }

    /// <summary>Autosave whenever a shrine is touched (the spec's checkpoint moments).</summary>
    private void HookAutosave()
    {
        World.ShrineActivated += _ => Save(announce: false);
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

    /// <summary>Each playable class's starting gear, all common item level 1.</summary>
    public static IReadOnlyList<string> StartingKit(string classId) => classId == "wizard"
        ? ["staff", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves"]
        : ["longsword", "leather_chest", "leather_pants", "leather_boots", "leather_gloves"];

    /// <summary>Puts a class's starting kit on a character (used by the game and by the creation preview).</summary>
    public static Equipment Dress(Character hero, Equipment gear, string classId)
    {
        var content = ContentDatabase.Embedded;
        foreach (var id in StartingKit(classId))
            gear.Equip(new Item(content.Item(id), Rarity.Common, 1));
        return gear;
    }

    /// <summary>A new game from the character creation screen's choices, on Gallows Road.</summary>
    public static GameSession NewGame(CharacterChoices choices, int seed, string zoneId = "gallows_road")
    {
        var content = ContentDatabase.Embedded;
        var hero = CharacterBuilder.Create(content, choices);
        var world = new GameWorld(content.Zone(zoneId), content.Creatures, hero, seed, content.AbilityBar(choices.ClassId), content.Loot);
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
