using System.Text.Json.Serialization;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;

namespace AshenReliquary.Core.World;

/// <summary>
/// What one map character means: a ground set, optional object tiles, and whether it blocks movement. A cell draws
/// one of <see cref="Objects"/> at random, or all of them layered when <see cref="Stack"/> is set (a gate's cross brace).
/// <see cref="Tiles"/> names the ground tiles outright (another tileset's floors) while <see cref="Ground"/> still says
/// what kind of ground it is ("bog" for flooded flagstones). <see cref="Light"/> makes the cell a light (a torch, a
/// brazier, a shaft of moonlight) of that radius in tiles, in <see cref="LightColor"/>. <see cref="Cover"/>: a low
/// wall, a cart or a barricade, seen over but not shot through cleanly: a ranged attack across it at someone close behind
/// it is against cover (see <see cref="TileMap.InCover"/>).
/// </summary>
public sealed record LegendEntry(string Ground, IReadOnlyList<int>? Objects = null, bool Blocks = false, bool BlocksSight = false, bool Stack = false,
    IReadOnlyList<int>? Tiles = null, double Light = 0, string? LightColor = null, bool Cover = false);

/// <summary>One monster in a pack: a creature id, its rank and any monster affixes ("vengeful").</summary>
public sealed record PackMember(string Creature, CreatureRank Rank = CreatureRank.Normal, int Count = 1, IReadOnlyList<string>? Affixes = null);

/// <summary>
/// A pack placed on the map. Members scatter within <see cref="Radius"/> tiles of <see cref="At"/>. <see cref="Level"/>
/// sets the pack's level where it differs from the zone's (the Fens run from 2 to 3). When the whole pack has fallen,
/// world flag <see cref="Flag"/> is set to <see cref="Value"/> (a side story's "clear the camp").
/// </summary>
public sealed record SpawnDefinition(int[] At, IReadOnlyList<PackMember> Pack, double Radius = 2.0, int? Level = null,
    string? Flag = null, string? Value = null);

/// <summary>A shrine: checkpoint, respawn point, potion refill and Taint cleanse.</summary>
public sealed record ShrineDefinition(string Id, string Name, int[] At);

/// <summary>
/// A way out of a zone. Walking within <see cref="Radius"/> of <see cref="At"/> completes the zone and, when
/// <see cref="To"/> names a zone, travels there, arriving at that zone's exit <see cref="ToExit"/>. A hero arriving
/// through an exit stands at its <see cref="Arrive"/> tile (outside its radius, so they don't bounce straight back),
/// or at <see cref="At"/> when it has none. An exit with <see cref="Requires"/> (world flags, "" for unset) is a
/// Herald gate: until they hold it shows <see cref="SealedText"/> and goes nowhere; once open, an exit to no zone yet
/// shows <see cref="OpenText"/>.
/// </summary>
public sealed record ExitDefinition(string Id, string Label, int[] At, double Radius = 2.0, string? To = null, string? ToExit = null,
    int[]? Arrive = null, IReadOnlyDictionary<string, string>? Requires = null, string? SealedText = null, string? OpenText = null);

/// <summary>
/// A hand-authored zone. <see cref="Rows"/> is the map drawn in characters, row 0 at the top (north-west
/// edge on screen); <see cref="Legend"/> says what each character is.
/// </summary>
public sealed record ZoneDefinition(
    string Id,
    string Name,
    int Level,
    string Tileset,
    IReadOnlyList<string> Rows,
    IReadOnlyDictionary<string, LegendEntry> Legend,
    int[] PlayerStart,
    IReadOnlyList<SpawnDefinition> Spawns,
    IReadOnlyList<ShrineDefinition> Shrines,
    IReadOnlyList<ExitDefinition> Exits,
    IReadOnlyList<VendorDefinition>? Vendors = null,
    int Revision = 1,
    IReadOnlyList<CacheDefinition>? Caches = null,
    bool Town = false,
    string? Tagline = null,
    IReadOnlyList<NpcDefinition>? Npcs = null,
    IReadOnlyList<InteractableDefinition>? Interactables = null,
    double? Darkness = null,
    double HeroLight = 6.5,
    double XpScale = 1.0,
    int Act = 1)
{
    /// <summary>Under the open sky (Flare's grassland, or Act 2's grassland-and-ruins set): trees fringe the map, campfires glow.</summary>
    public bool Outdoors => Tileset is "tileset_grassland" or "tileset_ironhold";

    // XpScale: the share of monster XP the zone pays, Act 1's curve tuned so a hero leaves each zone about a level up.

    /// <summary>How dark the zone is, 0 to 1 (the night outdoors is 0.66); see <see cref="IsDark"/>.</summary>
    public double DarknessLevel => Darkness ?? 0.66;

    /// <summary>
    /// A truly dark place (a dungeon): what stands outside every light can't be seen, and the hero's strikes and spells
    /// against it must pass a DC 5 flat check (concealed).
    /// </summary>
    public bool IsDark => DarknessLevel >= 0.85;

    public int Height => Rows.Count;
    public int Width => Rows.Max(r => r.Length);
}

/// <summary>
/// A monster type: its look, behaviour and defences. Level and rank come from where it is placed. <see cref="Look"/>
/// draws it from Flare's avatar layers (a cultist) instead of an enemy sprite sheet; <see cref="Caster"/> picks the
/// cast animation for a ranged attack; <see cref="Flanks"/> makes it circle to the far side of the hero from an ally
/// (Act 1's AI behaviour); <see cref="PoisonOnHit"/> gives its hits a <see cref="PoisonChance"/> of persistent poison.
/// <see cref="Boss"/> makes it a Herald fight (phases, arena); <see cref="HitPointsScale"/> hand-tunes its HP.
/// <see cref="Named"/>: a named foe (the Drowned Prior) goes by its own name, without "Champion" or "the Elite";
/// <see cref="RareDrop"/>: it always drops a rare besides its usual loot.
/// </summary>
public sealed record CreatureDefinition(
    string Id,
    string Name,
    string Sprite,
    DamageType DamageType,
    double AttacksPerSecond = 1.0,
    double MoveSpeed = 2.5,
    double AggroRadius = 8.0,
    double AttackRange = 1.3,
    bool Ranged = false,
    string? Damage = null,
    double Scale = 1.0,
    IReadOnlyDictionary<DamageType, int>? Weaknesses = null,
    IReadOnlyDictionary<DamageType, int>? Resistances = null,
    IReadOnlyList<DamageType>? Immunities = null,
    string? Summons = null,
    double SummonEverySeconds = 0,
    int SummonCap = 0,
    bool Stationary = false,
    string? Tint = null,
    string Voice = "zombie",
    IReadOnlyDictionary<string, string>? Look = null,
    bool Caster = false,
    bool Flanks = false,
    string? PoisonOnHit = null,
    double PoisonChance = 0,
    BossDefinition? Boss = null,
    double HitPointsScale = 1.0,
    bool Named = false,
    bool RareDrop = false,
    bool Undead = false,
    bool Kites = false)
{
    // Undead: the walking dead (the Lanternbearer's undead bane and holy light burn them with vitality).
    // Kites: a ranged monster that keeps its distance, falling back when the hero closes (Act 2's AI behaviour).

    [JsonIgnore]
    public bool IsSpawner => Summons is not null;

    public DamageProfile BuildDefenses()
    {
        var profile = new DamageProfile();
        foreach (var (type, amount) in Weaknesses ?? new Dictionary<DamageType, int>())
            profile.Weak(type, amount);
        foreach (var (type, amount) in Resistances ?? new Dictionary<DamageType, int>())
            profile.Resist(type, amount);
        foreach (var type in Immunities ?? [])
            profile.Immune(type);
        return profile;
    }
}
