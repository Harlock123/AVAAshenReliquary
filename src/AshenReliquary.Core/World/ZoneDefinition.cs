using System.Text.Json.Serialization;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;

namespace AshenReliquary.Core.World;

/// <summary>What one map character means: a ground set, optional object tiles, and whether it blocks movement.</summary>
public sealed record LegendEntry(string Ground, IReadOnlyList<int>? Objects = null, bool Blocks = false, bool BlocksSight = false);

/// <summary>One monster in a pack: a creature id and its rank.</summary>
public sealed record PackMember(string Creature, CreatureRank Rank = CreatureRank.Normal, int Count = 1);

/// <summary>A pack placed on the map. Members scatter within <see cref="Radius"/> tiles of <see cref="At"/>.</summary>
public sealed record SpawnDefinition(int[] At, IReadOnlyList<PackMember> Pack, double Radius = 2.0);

/// <summary>A shrine: checkpoint, respawn point, potion refill and Taint cleanse.</summary>
public sealed record ShrineDefinition(string Id, string Name, int[] At);

/// <summary>A zone exit; reaching it ends the zone (in the vertical slice: arriving at Fort Gallowmere).</summary>
public sealed record ExitDefinition(string Id, string Label, int[] At, double Radius = 2.0);

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
    IReadOnlyList<ExitDefinition> Exits)
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
    string? Tint = null)
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
