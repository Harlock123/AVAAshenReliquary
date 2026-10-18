using System.Text.Json.Serialization;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;

namespace AshenReliquary.Core.World;

/// <summary>
/// What one map character means: a ground set, optional object tiles, and whether it blocks movement. A cell draws
/// one of <see cref="Objects"/> at random, or all of them layered when <see cref="Stack"/> is set (a gate's cross brace).
/// </summary>
public sealed record LegendEntry(string Ground, IReadOnlyList<int>? Objects = null, bool Blocks = false, bool BlocksSight = false, bool Stack = false);

/// <summary>One monster in a pack: a creature id, its rank and any monster affixes ("vengeful").</summary>
public sealed record PackMember(string Creature, CreatureRank Rank = CreatureRank.Normal, int Count = 1, IReadOnlyList<string>? Affixes = null);

/// <summary>
/// A pack placed on the map. Members scatter within <see cref="Radius"/> tiles of <see cref="At"/>. <see cref="Level"/>
/// sets the pack's level where it differs from the zone's (the Fens run from 2 to 3).
/// </summary>
public sealed record SpawnDefinition(int[] At, IReadOnlyList<PackMember> Pack, double Radius = 2.0, int? Level = null);

/// <summary>A shrine: checkpoint, respawn point, potion refill and Taint cleanse.</summary>
public sealed record ShrineDefinition(string Id, string Name, int[] At);

/// <summary>
/// A way out of a zone. Walking within <see cref="Radius"/> of <see cref="At"/> completes the zone and, when
/// <see cref="To"/> names a zone, travels there, arriving at that zone's exit <see cref="ToExit"/>. A hero arriving
/// through an exit stands at its <see cref="Arrive"/> tile (outside its radius, so they don't bounce straight back),
/// or at <see cref="At"/> when it has none.
/// </summary>
public sealed record ExitDefinition(string Id, string Label, int[] At, double Radius = 2.0, string? To = null, string? ToExit = null,
    int[]? Arrive = null);

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
    IReadOnlyList<InteractableDefinition>? Interactables = null)
{
    public int Height => Rows.Count;
    public int Width => Rows.Max(r => r.Length);
}

/// <summary>
/// A monster type: its look, behaviour and defences. Level and rank come from where it is placed. <see cref="Look"/>
/// draws it from Flare's avatar layers (a cultist) instead of an enemy sprite sheet; <see cref="Caster"/> picks the
/// cast animation for a ranged attack; <see cref="Flanks"/> makes it circle to the far side of the hero from an ally
/// (Act 1's AI behaviour); <see cref="PoisonOnHit"/> gives its hits a <see cref="PoisonChance"/> of persistent poison.
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
    double PoisonChance = 0)
{
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
