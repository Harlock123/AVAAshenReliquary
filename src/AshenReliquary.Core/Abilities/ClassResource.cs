using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Abilities;

/// <summary>
/// The class resource abilities spend. Fury (Fighter) starts empty, builds when you hit or are hit and drains
/// after a few seconds out of combat. Mana (casters) starts full and regenerates with INT and level.
/// </summary>
public sealed class ClassResource
{
    public const double FuryPerHit = 12;
    public const double FuryPerCrit = 20;
    public const double FuryWhenHit = 6;
    public const double FuryDecayDelay = 4;
    public const double FuryDecayPerSecond = 8;

    private readonly Character _character;
    private double _sinceCombat = double.PositiveInfinity;

    public ClassResource(Character character)
    {
        _character = character;
        Kind = character.Class.Resource.StartsWith("Fury", StringComparison.OrdinalIgnoreCase) ? ResourceKind.Fury
            : character.Class.Resource.StartsWith("Mana", StringComparison.OrdinalIgnoreCase) ? ResourceKind.Mana
            : ResourceKind.Focus;
        Current = Kind == ResourceKind.Fury ? 0 : Max;
    }

    public ResourceKind Kind { get; }
    public string Name => Kind.ToString();
    public double Current { get; private set; }

    public double Max => Kind switch
    {
        ResourceKind.Fury => 100,
        _ => 20 + 5 * _character.Level + 4 * Math.Max(0, _character.Attributes[_character.KeyAttribute]),
    };

    /// <summary>Mana or Focus per second: 1 + a quarter per point of the key attribute + a tenth per level.</summary>
    public double RegenPerSecond => Kind == ResourceKind.Fury ? 0
        : (1 + 0.25 * Math.Max(0, _character.Attributes[_character.KeyAttribute]) + 0.1 * _character.Level) * (1 + _character.Gear.ResourceRegen);

    public bool CanSpend(double amount) => Current + 1e-9 >= amount;

    public bool TrySpend(double amount)
    {
        if (!CanSpend(amount))
            return false;
        Current -= amount;
        return true;
    }

    /// <summary>A hit landed (or, with <paramref name="taken"/>, a hit received): Fury builds.</summary>
    public void OnCombat(bool critical = false, bool taken = false)
    {
        _sinceCombat = 0;
        if (Kind == ResourceKind.Fury)
            Current = Math.Min(Max, Current + (taken ? FuryWhenHit : (critical ? FuryPerCrit : FuryPerHit) + _character.Gear.FuryPerHit));
    }

    public void Update(double dt)
    {
        _sinceCombat += dt;
        if (Kind == ResourceKind.Fury)
        {
            if (_sinceCombat > FuryDecayDelay)
                Current = Math.Max(0, Current - FuryDecayPerSecond * dt);
        }
        else
        {
            Current = Math.Min(Max, Current + RegenPerSecond * dt);
        }
    }

    internal void Restore(double value) => Current = Math.Clamp(value, 0, Max);

    /// <summary>Back to the starting value: empty Fury, full Mana (respawning at a shrine).</summary>
    public void Reset() => Current = Kind == ResourceKind.Fury ? 0 : Max;
}

public enum ResourceKind
{
    Fury,
    Mana,
    Focus,
}
