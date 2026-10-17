using System.Text.Json;
using System.Text.Json.Serialization;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Saving;

/// <summary>One item in a save: its base, rarity, item level, runes, rolled affixes and (for rares) its name.</summary>
public sealed record SavedItem(string Base, Rarity Rarity, int ItemLevel, int Potency, int Striking, int Resilient,
    IReadOnlyList<SavedAffix> Affixes, string? Name);

public sealed record SavedAffix(string Id, int Value);

/// <summary>Loot left on the ground.</summary>
public sealed record SavedGroundItem(double X, double Y, SavedItem? Item, int Gold, bool Draught);

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

    // The character.
    public required CharacterChoices Choices { get; init; }
    public Dictionary<int, AttributeKind[]> LevelUpBoosts { get; init; } = [];
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
    public static SaveGame Capture(GameWorld world, string saveId, int seed, string? portraitId, double playSeconds = 0)
    {
        var hero = world.Hero;
        var c = hero.Character;
        var gear = hero.Equipment;
        return new SaveGame
        {
            SaveId = saveId,
            SavedAt = DateTimeOffset.Now,
            PortraitId = portraitId,
            Choices = c.Choices ?? throw new InvalidOperationException("Only characters made by CharacterBuilder can be saved."),
            LevelUpBoosts = c.LevelUpBoosts.ToDictionary(kv => kv.Key, kv => kv.Value),
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
            Seed = seed,
            HeroX = c.IsDead ? world.RespawnPoint.X : hero.X,
            HeroY = c.IsDead ? world.RespawnPoint.Y : hero.Y,
            RespawnX = world.RespawnPoint.X,
            RespawnY = world.RespawnPoint.Y,
            Shrines = world.ActivatedShrines.ToList(),
            Slain = world.Slain.Order().ToList(),
            Ground = world.GroundItems.Select(g => new SavedGroundItem(g.X, g.Y, g.Drop.Item is { } item ? Save(item) : null, g.Drop.Gold, g.Drop.Draught)).ToList(),
            ZoneComplete = world.ZoneComplete,
            PlaySeconds = playSeconds,
        };
    }

    private static SavedItem Save(Item item) => new(item.Definition.Id, item.Rarity, item.ItemLevel, item.Potency, item.Striking,
        item.Resilient, item.Affixes.Select(a => new SavedAffix(a.Definition.Id, a.Value)).ToList(),
        item.Rarity == Rarity.Rare ? item.Name : null);

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
            character.LevelUp(LevelUpBoosts.TryGetValue(level, out var boosts) ? boosts : null);

        var zone = content.Zone(ZoneId);
        var world = new GameWorld(zone, content.Creatures, character, seed ?? unchecked(Seed + (int)SavedAt.ToUnixTimeSeconds()),
            content.AbilityBar(Choices.ClassId), content.Loot);
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

        var ground = Ground.Select(g => new GroundItem(g.X, g.Y,
            new LootDrop(g.Item is { } item ? Load(content, item) : null, g.Gold, g.Draught), 0));
        world.Restore(HeroX, HeroY, (RespawnX, RespawnY), Shrines, Slain, ground, ZoneComplete, Level, Xp);
        return world;
    }

    private static Item Load(ContentDatabase content, SavedItem saved)
    {
        var affixes = saved.Affixes
            .Select(a => content.Affixes.FirstOrDefault(d => d.Id == a.Id) is { } definition ? new AffixRoll(definition, a.Value) : null)
            .OfType<AffixRoll>()
            .ToList();
        return new Item(content.Item(saved.Base), saved.Rarity, saved.ItemLevel, affixes, saved.Potency, saved.Striking, saved.Resilient, saved.Name);
    }
}
