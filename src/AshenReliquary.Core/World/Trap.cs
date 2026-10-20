using AshenReliquary.Core.Combat;

namespace AshenReliquary.Core.World;

/// <summary>
/// A trap placed in a zone (M4 step 3, the Old Barracks): a pressure rune under the flagstones. <see cref="Kind"/> is
/// "spikes" (piercing), "flame" (fire) or "poison" (poison, and persistent poison on a failed save); <see cref="Dc"/>
/// overrides the zone level's DC for spotting, disarming and the save.
/// </summary>
public sealed record TrapDefinition(string Id, int[] At, string Kind = "spikes", int? Dc = null)
{
    public DamageType DamageType => Kind switch
    {
        "flame" => DamageType.Fire,
        "poison" => DamageType.Poison,
        _ => DamageType.Piercing,
    };

    public string Name => Kind switch
    {
        "flame" => "Flame rune",
        "poison" => "Poison-needle rune",
        _ => "Spike rune",
    };
}

/// <summary>Where a trap stands: unseen, spotted, made safe, or spent.</summary>
public enum TrapState
{
    Hidden,
    Found,
    Disarmed,
    Sprung,
}

/// <summary>
/// A trap in play. The hero gets one Perception check to spot it on coming within <see cref="GameWorld.TrapNoticeTiles"/>;
/// stepping on one (unseen or seen, not while dodging) springs it: a basic Reflex save against its DC. A spotted trap can
/// be disarmed with Thievery (trained), by a click that walks the hero over. Its state is kept as a world flag.
/// </summary>
public sealed class Trap
{
    internal Trap(TrapDefinition definition)
    {
        Definition = definition;
        X = definition.At[0] + 0.5;
        Y = definition.At[1] + 0.5;
    }

    public TrapDefinition Definition { get; }
    public double X { get; }
    public double Y { get; }
    public TrapState State { get; internal set; }

    /// <summary>Tries at disarming it so far (the bots give up after a few).</summary>
    internal int Attempts { get; set; }

    /// <summary>The hero has had their check to spot it.</summary>
    internal bool Checked { get; set; }

    /// <summary>When it was sprung (game time), for the flash.</summary>
    public double SprungAt { get; internal set; } = double.NegativeInfinity;

    /// <summary>Still dangerous: unseen or spotted, not disarmed or spent.</summary>
    public bool Armed => State is TrapState.Hidden or TrapState.Found;

    public double DistanceTo(double x, double y) => Math.Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
}
