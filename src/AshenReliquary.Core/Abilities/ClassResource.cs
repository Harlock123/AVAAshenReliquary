using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Abilities;

/// <summary>
/// The class resource abilities spend. Fury (Fighter) starts empty, builds when you hit or are hit and drains
/// after a few seconds out of combat. Mana (casters) starts full and regenerates with INT and level. Momentum (Rogue)
/// starts empty and builds as the hero moves (only up to a running start out of a fight) and on hits, crits most; it
/// drains once the hero stands still out of a fight.
/// </summary>
public sealed class ClassResource
{
    public const double FuryPerHit = 12;
    public const double FuryPerCrit = 20;
    public const double FuryWhenHit = 6;
    public const double FuryDecayDelay = 4;
    public const double FuryDecayPerSecond = 8;
    public const double MomentumPerTile = 4;
    public const double MomentumPerHit = 4;
    public const double MomentumPerCrit = 15;

    /// <summary>Out of a fight, moving builds Momentum only this far: a running start.</summary>
    public const double MomentumRunningStart = 30;
    public const double MomentumDecayDelay = 3;
    public const double MomentumDecayPerSecond = 10;

    /// <summary>Seconds after the last blow struck or taken that the hero still counts as in a fight.</summary>
    public const double CombatSeconds = 5;

    private readonly Character _character;
    private double _sinceCombat = double.PositiveInfinity;
    private double _sinceMoved = double.PositiveInfinity;

    public ClassResource(Character character)
    {
        _character = character;
        Kind = character.Class.Resource.StartsWith("Fury", StringComparison.OrdinalIgnoreCase) ? ResourceKind.Fury
            : character.Class.Resource.StartsWith("Mana", StringComparison.OrdinalIgnoreCase) ? ResourceKind.Mana
            : character.Class.Resource.StartsWith("Momentum", StringComparison.OrdinalIgnoreCase) ? ResourceKind.Momentum
            : ResourceKind.Focus;
        Current = StartsEmpty ? 0 : Max;
    }

    public ResourceKind Kind { get; }
    public string Name => Kind.ToString();
    public double Current { get; private set; }

    /// <summary>Fury and Momentum start empty and are built in a fight; Mana and Focus start full and regenerate.</summary>
    public bool StartsEmpty => Kind is ResourceKind.Fury or ResourceKind.Momentum;

    /// <summary>Whether a blow was struck or taken in the last <see cref="CombatSeconds"/>.</summary>
    public bool InCombat => _sinceCombat < CombatSeconds;

    public double Max => Kind switch
    {
        ResourceKind.Fury or ResourceKind.Momentum => 100,
        _ => 20 + 5 * _character.Level + 4 * Math.Max(0, _character.Attributes[_character.KeyAttribute]),
    };

    /// <summary>Mana or Focus per second: 1 + a quarter per point of the key attribute + a tenth per level.</summary>
    public double RegenPerSecond => StartsEmpty ? 0
        : (1 + 0.25 * Math.Max(0, _character.Attributes[_character.KeyAttribute]) + 0.1 * _character.Level) * (1 + _character.Gear.ResourceRegen + _character.ResourceRegenBonus);

    public bool CanSpend(double amount) => Current + 1e-9 >= amount;

    public bool TrySpend(double amount)
    {
        if (!CanSpend(amount))
            return false;
        Current -= amount;
        return true;
    }

    /// <summary>
    /// A hit landed (or, with <paramref name="taken"/>, a hit received): Fury builds, and Momentum on the hero's own
    /// hits (most on a critical). A graze builds half as much; with <paramref name="builds"/> false (a sweep's other
    /// targets) the fight is only kept going. Gear's and feats' "per hit" adds to either.
    /// </summary>
    public void OnCombat(bool critical = false, bool taken = false, bool graze = false, bool builds = true)
    {
        _sinceCombat = 0;
        if (!builds || Kind is not (ResourceKind.Fury or ResourceKind.Momentum) || (taken && Kind == ResourceKind.Momentum))
            return;
        var perHit = _character.Gear.FuryPerHit + _character.FuryPerHitBonus;
        var gain = Kind == ResourceKind.Momentum ? (critical ? MomentumPerCrit : MomentumPerHit) + perHit
            : taken ? FuryWhenHit : (critical ? FuryPerCrit : FuryPerHit) + perHit;
        Current = Math.Min(Max, Current + (graze ? gain / 2 : gain));
    }

    /// <summary>
    /// The hero moved this many tiles: Momentum builds, in a fight up to the full bar and out of one only up to
    /// <see cref="MomentumRunningStart"/>.
    /// </summary>
    public void OnMoved(double tiles)
    {
        if (Kind != ResourceKind.Momentum || tiles <= 0)
            return;
        _sinceMoved = 0;
        var cap = InCombat ? Max : Math.Max(Current, MomentumRunningStart);
        Current = Math.Min(cap, Current + tiles * MomentumPerTile);
    }

    /// <summary>Spends everything there is (Finishing Blow); returns how much that was.</summary>
    public double SpendAll()
    {
        var spent = Current;
        Current = 0;
        return spent;
    }

    public void Update(double dt)
    {
        _sinceCombat += dt;
        _sinceMoved += dt;
        if (Kind == ResourceKind.Fury)
        {
            if (_sinceCombat > FuryDecayDelay)
                Current = Math.Max(0, Current - FuryDecayPerSecond * dt);
        }
        else if (Kind == ResourceKind.Momentum)
        {
            if (_sinceCombat > MomentumDecayDelay && _sinceMoved > MomentumDecayDelay)
                Current = Math.Max(0, Current - MomentumDecayPerSecond * dt);
        }
        else
        {
            Current = Math.Min(Max, Current + RegenPerSecond * dt);
        }
    }

    internal void Restore(double value) => Current = Math.Clamp(value, 0, Max);

    /// <summary>Back to the starting value: empty Fury and Momentum, full Mana (respawning at a shrine).</summary>
    public void Reset() => Current = StartsEmpty ? 0 : Max;
}

public enum ResourceKind
{
    Fury,
    Mana,
    Focus,
    Momentum,
}
