namespace AshenReliquary.Core.World;

/// <summary>Something the player should hear. The app picks the actual sound file (audio.json).</summary>
public enum SoundCue
{
    Swing,
    Hit,
    Crit,
    Miss,
    HeroHurt,
    MonsterAggro,
    MonsterDeath,
    Bow,
    WitchCast,
    GraveRise,
    Ignition,
    ForceBolt,
    Impact,
    LootDrop,
    Gold,
    Pickup,
    Potion,
    LevelUp,
    Shrine,
    Death,
    ZoneComplete,
    CacheOpen,
    /// <summary>A bomb bursting; its variant is the damage type ("fire", "cold").</summary>
    Bomb,
    /// <summary>A ward going up: "shield" raised, a "spell" cast, a Shielded "monster" warding its pack.</summary>
    Ward,
    /// <summary>Shield Block taking a blow.</summary>
    Block,
    /// <summary>A Unique or a Set piece dropping.</summary>
    RareDrop,
}

/// <summary>
/// A sound at a place on the map. <see cref="Variant"/> narrows it: a creature's voice ("zombie", "skeleton") or what
/// was picked up ("cloth", "metal", "jewel").
/// </summary>
public readonly record struct SoundEvent(SoundCue Cue, double X, double Y, string? Variant = null);
