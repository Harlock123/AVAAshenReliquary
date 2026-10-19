using System.Text.Json;
using System.Text.Json.Serialization;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Saving;

/// <summary>
/// One item in a save: its base, rarity, item level, runes, rolled affixes, (for rares) its name and (for a stack of
/// consumables) how many; older saves have no count, which reads as one.
/// </summary>
public sealed record SavedItem(string Base, Rarity Rarity, int ItemLevel, int Potency, int Striking, int Resilient,
    IReadOnlyList<SavedAffix> Affixes, string? Name, int Count = 1, ItemMark Mark = ItemMark.None);

public sealed record SavedAffix(string Id, int Value);

/// <summary>A trader's wares, so reloading doesn't reroll the table.</summary>
public sealed record SavedVendor(string Id, int StockedForLevel, List<SavedItem> Stock);

/// <summary>Loot left on the ground.</summary>
public sealed record SavedGroundItem(double X, double Y, SavedItem? Item, int Gold, bool Draught);

/// <summary>A zone visited and left: what it remembers (see <see cref="ZoneMemory"/>).</summary>
public sealed record SavedZone(string Id, int Revision, List<int> Slain, List<string> OpenedCaches, string? Explored,
    List<SavedGroundItem> Ground, bool Complete, List<SavedVendor> Vendors);

/// <summary>
/// A whole saved game, written as versioned JSON. The character is stored as the choices it was built from plus
/// each level-up's boosts, so loading rebuilds it through the rules (an edited save can't smuggle in an illegal
/// character); everything else is plain state.
/// </summary>
public sealed record SaveGame
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public string SaveId { get; init; } = "";
    public DateTimeOffset SavedAt { get; init; }
    public string? PortraitId { get; init; }

    /// <summary>
    /// A saved copy (a snapshot): its number among the character's copies (Elirk-2, Elirk-3: the character's own save is
    /// the first) and the save it was copied from. Null for a character's own save; a copy, once loaded, is a game of its
    /// own that saves over itself. <see cref="Label"/> is the older copies' "copy, 18 Oct 20:45".
    /// </summary>
    public int? CopyNumber { get; init; }
    public string? Label { get; init; }
    public string? CopyOf { get; init; }

    // The character.
    public required CharacterChoices Choices { get; init; }
    public Dictionary<int, AttributeKind[]> LevelUpBoosts { get; init; } = [];

    /// <summary>Each level's picks (feats, skill increase, boosts); missing in older saves, whose levels are left to pick again.</summary>
    public Dictionary<int, LevelChoice> LevelChoices { get; init; } = [];
    public int Level { get; init; } = 1;
    public int Xp { get; init; }
    public int HitPoints { get; init; }
    public double Taint { get; init; }
    public int HeroPoints { get; init; }
    public int PotionCharges { get; init; }
    public double Resource { get; init; }
    public Dictionary<EquipSlot, SavedItem> Worn { get; init; } = [];
    public List<SavedItem?> Bag { get; init; } = [];
    public int Gold { get; init; }

    // The world.
    public required string ZoneId { get; init; }
    public int Seed { get; init; }
    public double HeroX { get; init; }
    public double HeroY { get; init; }
    public double RespawnX { get; init; }
    public double RespawnY { get; init; }
    public List<string> Shrines { get; init; } = [];
    public List<int> Slain { get; init; } = [];
    public List<SavedGroundItem> Ground { get; init; } = [];
    public bool ZoneComplete { get; init; }

    /// <summary>The chests already opened; missing in older saves, where every chest is still shut.</summary>
    public List<string> OpenedCaches { get; init; } = [];

    /// <summary>The hub stash's slots; missing in older saves.</summary>
    public List<SavedItem?> Stash { get; init; } = [];

    /// <summary>The story's flags; missing in older saves.</summary>
    public Dictionary<string, string> Flags { get; init; } = [];

    /// <summary>The journal, oldest first; missing in older saves.</summary>
    public List<JournalEntry> Journal { get; init; } = [];

    /// <summary>The other zones visited, as they were left; missing in older saves (one zone only).</summary>
    public List<SavedZone> OtherZones { get; init; } = [];

    /// <summary>The zone layout's revision when saved; older saves have none and count as revision 1.</summary>
    public int ZoneRevision { get; init; } = 1;

    /// <summary>Each trader's stock; missing in older saves, whose traders roll fresh wares.</summary>
    public List<SavedVendor> Vendors { get; init; } = [];

    /// <summary>The explored map cells (base64 bitset); missing in older saves, which start unexplored.</summary>
    public string? Explored { get; init; }
    public double PlaySeconds { get; init; }

    /// <summary>What the load screen shows without rebuilding the game.</summary>
    [JsonIgnore]
    public string Summary => $"Level {Level} {Choices.AncestryId} {Choices.ClassId}";

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static SaveGame FromJson(string json)
    {
        var save = JsonSerializer.Deserialize<SaveGame>(json, Json) ?? throw new InvalidDataException("The save file is empty.");
        if (save.Version > CurrentVersion)
            throw new InvalidDataException($"This save was made by a newer version of the game (save version {save.Version}).");
        return save;
    }

    // ----- Capture ------------------------------------------------------------------------

    /// <summary>Records a game in progress.</summary>
    public static SaveGame Capture(GameWorld world, string saveId, int seed, string? portraitId, double playSeconds = 0,
        string? label = null, string? copyOf = null, int? copyNumber = null)
    {
        var hero = world.Hero;
        var c = hero.Character;
        var gear = hero.Equipment;
        return new SaveGame
        {
            SaveId = saveId,
            SavedAt = DateTimeOffset.Now,
            PortraitId = portraitId,
            Label = label,
            CopyOf = copyOf,
            CopyNumber = copyNumber,
            Choices = c.Choices ?? throw new InvalidOperationException("Only characters made by CharacterBuilder can be saved."),
            LevelUpBoosts = c.LevelUpBoosts.ToDictionary(kv => kv.Key, kv => kv.Value),
            LevelChoices = c.LevelChoices.ToDictionary(kv => kv.Key, kv => kv.Value),
            Level = c.Level,
            Xp = world.Experience.Xp,
            HitPoints = c.IsDead ? c.MaxHitPoints : c.HitPoints,
            Taint = c.Taint.Value,
            HeroPoints = c.HeroPoints.Count,
            PotionCharges = c.Potion.Charges,
            Resource = hero.Resource.Current,
            Worn = gear.Worn.ToDictionary(kv => kv.Key, kv => Save(kv.Value)),
            Bag = gear.Bag.Select(i => i is null ? null : Save(i)).ToList(),
            Gold = gear.Gold,
            ZoneId = world.Zone.Id,
            ZoneRevision = world.Zone.Revision,
            OpenedCaches = world.Caches.Where(c => c.Opened).Select(c => c.Definition.Id).ToList(),
            Stash = world.Stash.Slots.Select(i => i is null ? null : Save(i)).ToList(),
            Flags = world.Flags.ToDictionary(kv => kv.Key, kv => kv.Value),
            Journal = world.Journal.ToList(),
            OtherZones = world.Memories.Select(kv => new SavedZone(kv.Key, kv.Value.Revision, kv.Value.Slain.Order().ToList(),
                kv.Value.OpenedCaches.ToList(), kv.Value.Explored, kv.Value.Ground.Select(SaveGround).ToList(), kv.Value.Complete,
                kv.Value.Vendors.Select(v => new SavedVendor(v.Id, v.StockedForLevel, v.Stock.Select(Save).ToList())).ToList())).ToList(),
            Seed = seed,
            HeroX = c.IsDead ? world.RespawnPoint.X : hero.X,
            HeroY = c.IsDead ? world.RespawnPoint.Y : hero.Y,
            RespawnX = world.RespawnPoint.X,
            RespawnY = world.RespawnPoint.Y,
            Shrines = world.ActivatedShrines.ToList(),
            Slain = world.Slain.Order().ToList(),
            Ground = world.GroundItems.Select(SaveGround).ToList(),
            ZoneComplete = world.ZoneComplete,
            Vendors = world.Vendors.Select(v => new SavedVendor(v.Definition.Id, v.StockedForLevel, v.Stock.Select(Save).ToList())).ToList(),
            Explored = world.ExploredBits(),
            PlaySeconds = playSeconds,
        };
    }

    private static SavedGroundItem SaveGround(GroundItem g) =>
        new(g.X, g.Y, g.Drop.Item is { } item ? Save(item) : null, g.Drop.Gold, g.Drop.Draught);

    private static GroundItem LoadGround(ContentDatabase content, SavedGroundItem g) =>
        new(g.X, g.Y, new LootDrop(g.Item is { } item ? Load(content, item) : null, g.Gold, g.Draught), 0);

    private static SavedItem Save(Item item) => new(item.Definition.Id, item.Rarity, item.ItemLevel, item.Potency, item.Striking,
        item.Resilient, item.Affixes.Select(a => new SavedAffix(a.Definition.Id, a.Value)).ToList(),
        item.Rarity == Rarity.Rare ? item.Name : null, item.Count, item.Mark);

    // ----- Restore ------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds the game: the character through <see cref="CharacterBuilder"/> and each level-up, then their state
    /// and gear, then the world. <paramref name="seed"/> seeds the new session's dice (by default the saved seed
    /// plus the save time, so reloading doesn't replay the same rolls).
    /// </summary>
    public GameWorld Restore(ContentDatabase content, int? seed = null)
    {
        var character = CharacterBuilder.Create(content, Choices);
        for (var level = 2; level <= Level; level++)
        {
            if (LevelChoices.TryGetValue(level, out var choice))
            {
                character.LevelUp();
                character.Choose(level, choice);
            }
            else
            {
                // An older save: only the boosts were recorded; the rest of the level's picks are made again.
                character.LevelUp(LevelUpBoosts.TryGetValue(level, out var boosts) ? boosts : null);
            }
        }

        var zone = content.Zone(ZoneId);
        var world = new GameWorld(zone, content.Creatures, character, seed ?? unchecked(Seed + (int)SavedAt.ToUnixTimeSeconds()),
            content.AbilityBar(Choices.ClassId), content.Loot, content.Zones, content.Dialogues, content.Abilities);
        world.RestoreStory(Flags, Journal);
        world.Stash.Restore(Stash.Select(i => i is null ? null : Load(content, i)));
        foreach (var other in OtherZones)
        {
            world.RestoreMemory(other.Id, new ZoneMemory
            {
                Revision = other.Revision, Slain = other.Slain, OpenedCaches = other.OpenedCaches, Explored = other.Explored,
                Ground = other.Ground.Select(g => LoadGround(content, g)).ToList(), Complete = other.Complete,
                Vendors = other.Vendors.Select(v => new VendorMemory(v.Id, v.StockedForLevel, v.Stock.Select(i => Load(content, i)).ToList())).ToList(),
            });
        }
        var gear = world.Hero.Equipment;
        foreach (var (slot, item) in Worn)
            gear.Equip(Load(content, item), slot);
        foreach (var item in Bag)
        {
            if (item is not null)
                gear.TryAdd(Load(content, item));
        }
        gear.AddGold(Gold);

        character.RestoreHitPoints(HitPoints);
        character.Taint.Restore(Taint);
        character.HeroPoints.Restore(HeroPoints);
        character.Potion.Restore(PotionCharges);
        world.Hero.Resource.Restore(Resource);

        if (ZoneRevision != zone.Revision)
        {
            // The zone was remade since this save: places, kills and loot no longer line up, so the character
            // (level, XP, gear, gold) starts the new layout afresh at its start, rested.
            world.Restore(world.Hero.X, world.Hero.Y, world.RespawnPoint, [], [], [], false, Level, Xp);
            character.RestAtShrine();
            world.Announce($"{zone.Name} has changed since this save: you start again at its beginning, with all you carry.");
            return world;
        }
        var ground = Ground.Select(g => LoadGround(content, g));
        world.Restore(HeroX, HeroY, (RespawnX, RespawnY), Shrines, Slain, ground, ZoneComplete, Level, Xp, Explored, OpenedCaches);
        foreach (var vendor in Vendors)
            world.RestoreVendor(vendor.Id, vendor.StockedForLevel, vendor.Stock.Select(i => Load(content, i)));
        return world;
    }

    private static Item Load(ContentDatabase content, SavedItem saved)
    {
        var affixes = saved.Affixes
            .Select(a => content.Affixes.FirstOrDefault(d => d.Id == a.Id) is { } definition ? new AffixRoll(definition, a.Value) : null)
            .OfType<AffixRoll>()
            .ToList();
        return new Item(content.Item(saved.Base), saved.Rarity, saved.ItemLevel, affixes, saved.Potency, saved.Striking, saved.Resilient, saved.Name,
            Math.Max(1, saved.Count)) { Mark = saved.Mark };
    }
}
