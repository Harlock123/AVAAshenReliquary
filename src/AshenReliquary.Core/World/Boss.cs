namespace AshenReliquary.Core.World;

/// <summary>
/// A Herald fight, from the creature's <c>boss</c> block: the phases it passes through as its health falls, the arena
/// round the spot it was placed (its home), the line it dies with and the flag its death sets.
/// </summary>
public sealed record BossDefinition(
    IReadOnlyList<BossPhase> Phases,
    double ArenaRadius = 10,
    string? DeathLine = null,
    string? DeathFlag = null,
    string? DeathValue = "slain",
    JournalEntryDefinition? Journal = null);

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
    IReadOnlyDictionary<string, string>? Look = null);

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
