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
}

/// <summary>
/// A sound at a place on the map. <see cref="Variant"/> narrows it: a creature's voice ("zombie", "skeleton") or what
/// was picked up ("cloth", "metal", "jewel").
/// </summary>
public readonly record struct SoundEvent(SoundCue Cue, double X, double Y, string? Variant = null);
