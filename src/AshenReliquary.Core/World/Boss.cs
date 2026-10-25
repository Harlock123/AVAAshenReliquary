namespace AshenReliquary.Core.World;

/// <summary>
/// A Herald fight, from the creature's <c>boss</c> block: the phases it passes through as its health falls, the arena
/// round the spot it was placed (its home), the line it dies with and the flag its death sets. <see cref="Sisters"/>
/// (the Choir of Whispers) are creatures that fight beside it on its one health bar: a blow at any of them lands on
/// it. <see cref="BarName"/> names the HUD's boss bar when it isn't the boss's own name. <see cref="Allies"/>: when the
/// fight begins, <see cref="AlliesCount"/> of that creature come to fight for the hero, if the flags in
/// <see cref="AlliesIf"/> hold (the Order's knights at the Cinder Throne, the beacon lit). <see cref="Court"/>: its fall
/// brings down every monster still standing in the zone (Malachar's court).
/// </summary>
public sealed record BossDefinition(
    IReadOnlyList<BossPhase> Phases,
    double ArenaRadius = 10,
    string? DeathLine = null,
    string? DeathFlag = null,
    string? DeathValue = "slain",
    JournalEntryDefinition? Journal = null,
    IReadOnlyList<string>? Sisters = null,
    string? BarName = null,
    string? Allies = null,
    int AlliesCount = 0,
    IReadOnlyDictionary<string, string>? AlliesIf = null,
    bool Court = false);

/// <summary>
/// One phase, entered when health falls to <see cref="Below"/> of the maximum (the first phase is 1.0):
/// <list type="bullet">
/// <item>rot pools: every <see cref="PoolEverySeconds"/> a pool lands where the hero stands, after a warning ring of
/// <see cref="PoolWarningSeconds"/>; it lasts <see cref="PoolSeconds"/> and anyone in it makes a basic Fortitude save
/// (DC 14 + the boss's level) against <see cref="PoolDamage"/> poison each second;</item>
/// <item>summons: every <see cref="SummonEverySeconds"/>, <see cref="SummonCount"/> of <see cref="Summons"/> (minions)
/// rise round the boss, at most <see cref="SummonCap"/> standing at once;</item>
/// <item><see cref="Floods"/>: the arena floods from its rim inward with poison bog, leaving a dry heart of
/// <see cref="DryRadius"/> tiles;</item>
/// <item><see cref="Fuses"/>: the boss goes back to its home and roots there (Mother Sallow and her cauldron); after
/// <see cref="EnrageSeconds"/> it unleashes <see cref="EnrageDamage"/> on the hero every <see cref="EnrageEverySeconds"/>,
/// so the phase is a race;</item>
/// <item>its stance (Ser Hadric): <see cref="AcShift"/> to its AC (a shield wall up, armour cast off), its speed and attack
/// rate times <see cref="SpeedScale"/> and <see cref="AttackRateScale"/>, and its own <see cref="Look"/> (a paper-doll
/// boss in other gear);</item>
/// <item>a charge: every <see cref="ChargeEverySeconds"/> it marks a lane at the hero for <see cref="ChargeWindup"/>
/// seconds, then rushes down it; caught in it, the hero takes <see cref="ChargeDamage"/> against a basic Reflex save
/// and is knocked prone on a failure (a Step under way dodges it);</item>
/// <item><see cref="Banner"/>: it plants a standard (a creature that <see cref="CreatureDefinition.Rallies"/>) that
/// gives it and its legion +2 to hit and a fifth more damage until the standard is broken.</item>
/// <item>the Choir: its sisters stand beside it unless the phase has them <see cref="Withdraw"/>; it hides among
/// <see cref="Reflections"/> images of itself (<see cref="Reflection"/>), shuffled every <see cref="ReflectEverySeconds"/>,
/// that shatter at a touch and that a Perception check sees through; every <see cref="SongEverySeconds"/> its song
/// takes the hero's will (a Will save or enthralled for <see cref="SongSeconds"/>); and <see cref="Joins"/> comes to its
/// side when the flags in <see cref="JoinsIf"/> hold (Brother Caddoc, banished).</item>
/// <item>Vorthrax (M4 step 9): every <see cref="BreathEverySeconds"/> a cone of fire, <see cref="BreathArc"/> degrees and
/// <see cref="BreathRange"/> tiles toward the hero, marked on the ground for <see cref="BreathWindup"/> seconds first, then
/// <see cref="BreathDamage"/> against a basic Reflex save; <see cref="Flies"/>: aloft over its home, out of a blade's
/// reach, swooping down beside the hero every <see cref="SwoopEverySeconds"/> for <see cref="SwoopSeconds"/>; pools may
/// land <see cref="PoolCount"/> at a time and be of <see cref="PoolType"/> (a firestorm); every
/// <see cref="CrumbleEverySeconds"/> one of the arena's platforms (legend <c>crumbles</c>) cracks into embers and, after
/// <see cref="CrumbleWarning"/> seconds, sinks into the lava, <see cref="CrumbleDamage"/> to anyone left on it.</item>
/// <item>Malachar's court (M5): every <see cref="CallsEchoEverySeconds"/>, once the last has fallen, it calls the next
/// dormant Echo of the zone into the fight, one at a time.</item>
/// <item>The Burning God (M5 step 2): the Taint rises in the hero by <see cref="TaintRise"/> a second, growing by
/// <see cref="TaintRiseGrowth"/> each second of the phase to at most <see cref="TaintRiseMax"/>; when it boils over (the
/// whispers) it also burns for <see cref="WhispersDamage"/>. <see cref="Lights"/> pale lantern lights
/// (<see cref="LanternLight"/>) of <see cref="LightRadius"/> drift over the arena at <see cref="LightSpeed"/>; standing in
/// one stops the rise and cleanses <see cref="LightCleanse"/> a second. Every <see cref="NovaEverySeconds"/> a ring of ash
/// (<see cref="AshNova"/>) spreads from the boss's home at <see cref="NovaSpeed"/> to the arena's edge and beyond, burning
/// whoever it crosses for <see cref="NovaDamage"/> against a basic Reflex save (a Step under way passes through it). The
/// boss takes <see cref="DamageTakenScale"/> of every blow, and its strikes take each of <see cref="StrikeTypes"/> in
/// turn.</item>
/// </list>
/// </summary>
public sealed record BossPhase(
    double Below,
    string Name,
    double PoolEverySeconds = 0,
    double PoolWarningSeconds = 1.2,
    double PoolSeconds = 10,
    double PoolRadius = 1.6,
    string PoolDamage = "2d6",
    double SummonEverySeconds = 0,
    string? Summons = null,
    int SummonCount = 0,
    int SummonCap = 0,
    bool Floods = false,
    double DryRadius = 4,
    bool Fuses = false,
    double EnrageSeconds = 0,
    double EnrageEverySeconds = 2,
    string EnrageDamage = "4d6",
    int AcShift = 0,
    double SpeedScale = 1.0,
    double AttackRateScale = 1.0,
    double ChargeEverySeconds = 0,
    double ChargeWindup = 1.0,
    string ChargeDamage = "3d8",
    string? Banner = null,
    IReadOnlyDictionary<string, string>? Look = null,
    bool Withdraw = false,
    int Reflections = 0,
    double ReflectEverySeconds = 12,
    string? Reflection = null,
    double SongEverySeconds = 0,
    double SongSeconds = 3,
    string? Joins = null,
    IReadOnlyDictionary<string, string>? JoinsIf = null,
    double BreathEverySeconds = 0,
    double BreathArc = 60,
    double BreathRange = 7,
    double BreathWindup = 1.2,
    string BreathDamage = "8d6",
    bool Flies = false,
    double SwoopEverySeconds = 9,
    double SwoopSeconds = 4,
    int PoolCount = 1,
    Combat.DamageType? PoolType = null,
    double CrumbleEverySeconds = 0,
    double CrumbleWarning = 2.5,
    string CrumbleDamage = "6d6",
    double CallsEchoEverySeconds = 0,
    double TaintRise = 0,
    double TaintRiseGrowth = 0,
    double TaintRiseMax = 10,
    string? WhispersDamage = null,
    int Lights = 0,
    double LightRadius = 1.6,
    double LightSpeed = 0.8,
    double LightCleanse = 30,
    double NovaEverySeconds = 0,
    double NovaSpeed = 4,
    string NovaDamage = "6d6",
    double DamageTakenScale = 1.0,
    IReadOnlyList<Combat.DamageType>? StrikeTypes = null);

/// <summary>A pale lantern light drifting over the Burning God's arena toward (<see cref="ToX"/>, <see cref="ToY"/>).</summary>
public sealed class LanternLight(double x, double y, double radius)
{
    public double X { get; internal set; } = x;
    public double Y { get; internal set; } = y;
    public double Radius { get; } = radius;
    internal double ToX { get; set; } = x;
    internal double ToY { get; set; } = y;

    public bool Covers(double px, double py) => (px - X) * (px - X) + (py - Y) * (py - Y) <= Radius * Radius;

    public double DistanceTo(double x, double y) => Math.Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
}

/// <summary>A ring of ash spreading from (<see cref="X"/>, <see cref="Y"/>) since <see cref="StartedAt"/>.</summary>
public sealed class AshNova(double x, double y, double startedAt, double speed, double reach)
{
    /// <summary>How thick the ring is drawn, in tiles.</summary>
    public const double Thickness = 0.7;

    public double X { get; } = x;
    public double Y { get; } = y;
    public double StartedAt { get; } = startedAt;
    public double Speed { get; } = speed;
    public double Reach { get; } = reach;
    internal double LastRadius { get; set; }
    internal HashSet<object> Struck { get; } = [];

    public double RadiusAt(double now) => Math.Max(0, now - StartedAt) * Speed;

    public bool Done(double now) => RadiusAt(now) > Reach;
}

/// <summary>A boss's breath: the cone it marked toward the hero, burning from <see cref="BurnAt"/>.</summary>
public sealed class BossBreath(double x, double y, double direction, double arc, double range, double burnAt)
{
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Direction { get; } = direction;
    public double Arc { get; } = arc;
    public double Range { get; } = range;
    public double BurnAt { get; } = burnAt;
    internal bool Burned { get; set; }

    /// <summary>Whether a point lies in the cone.</summary>
    public bool Covers(double px, double py)
    {
        var (dx, dy) = (px - X, py - Y);
        var d = Math.Sqrt(dx * dx + dy * dy);
        if (d > Range)
            return false;
        if (d < 0.6)
            return true;
        var off = Math.Abs(Math.IEEERemainder(Math.Atan2(dy, dx) - Direction, 2 * Math.PI));
        return off <= Arc * Math.PI / 360;
    }
}

/// <summary>A platform of the arena cracking (embers) until <see cref="SinksAt"/>, when it goes into the lava.</summary>
public sealed class Crumbling(string group, IReadOnlyList<(int X, int Y)> cells, double sinksAt)
{
    public string Group { get; } = group;
    public IReadOnlyList<(int X, int Y)> Cells { get; } = cells;
    public double SinksAt { get; } = sinksAt;
}

/// <summary>A boss's charge: the lane it marked (from its spot to beyond the hero), live from <see cref="RushAt"/>.</summary>
public sealed class BossCharge(double fromX, double fromY, double toX, double toY, double rushAt)
{
    public double FromX { get; } = fromX;
    public double FromY { get; } = fromY;
    public double ToX { get; } = toX;
    public double ToY { get; } = toY;
    public double RushAt { get; } = rushAt;
    internal bool Struck { get; set; }
}

/// <summary>
/// A pool on the ground: a warning ring until <see cref="ActiveAt"/>, then a hazard until <see cref="EndsAt"/>. Mother
/// Sallow's rot is poison; a Tainted monster's death leaves <see cref="Combat.DamageType.Taint"/>.
/// </summary>
public sealed class Hazard(double x, double y, double radius, double activeAt, double endsAt, Rules.Dice damage, int dc,
    Combat.DamageType type = Combat.DamageType.Poison)
{
    public Combat.DamageType Type { get; } = type;
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Radius { get; } = radius;
    public double ActiveAt { get; } = activeAt;
    public double EndsAt { get; } = endsAt;
    public Rules.Dice Damage { get; } = damage;
    public int Dc { get; } = dc;
    internal double TickIn { get; set; }

    public bool IsActive(double now) => now >= ActiveAt && now < EndsAt;

    public bool Covers(double x, double y) => (x - X) * (x - X) + (y - Y) * (y - Y) <= Radius * Radius;
}
