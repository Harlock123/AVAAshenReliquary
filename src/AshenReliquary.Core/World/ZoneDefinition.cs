using System.Text.Json.Serialization;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;

namespace AshenReliquary.Core.World;

/// <summary>
/// What one map character means: a ground set, optional object tiles, and whether it blocks movement. A cell draws
/// one of <see cref="Objects"/> at random, or all of them layered when <see cref="Stack"/> is set (a gate's cross brace).
/// </summary>
public sealed record LegendEntry(string Ground, IReadOnlyList<int>? Objects = null, bool Blocks = false, bool BlocksSight = false, bool Stack = false);

/// <summary>One monster in a pack: a creature id and its rank.</summary>
public sealed record PackMember(string Creature, CreatureRank Rank = CreatureRank.Normal, int Count = 1);

/// <summary>A pack placed on the map. Members scatter within <see cref="Radius"/> tiles of <see cref="At"/>.</summary>
public sealed record SpawnDefinition(int[] At, IReadOnlyList<PackMember> Pack, double Radius = 2.0);

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

/// <summary>A monster type: its look, behaviour and defences. Level and rank come from where it is placed.</summary>
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
    string Voice = "zombie")
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
