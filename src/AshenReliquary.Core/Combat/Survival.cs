using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Combat;

/// <summary>Hero Points: one per zone, at most three. Spent to reroll a check, or automatically to survive death.</summary>
public sealed class HeroPoints
{
    public const int Max = 3;

    public int Count { get; private set; }

    public void GainForZone() => Count = Math.Min(Max, Count + 1);

    public bool TrySpend()
    {
        if (Count == 0)
            return false;
        Count--;
        return true;
    }
}

/// <summary>
/// Healing potions run on charges rather than a stack: 3 charges refilled at shrines, an 8 s cooldown,
/// and each drink heals 1d8 + 2 x level over 2 s. Sickened creatures can't drink.
/// </summary>
public sealed class HealingPotion
{
    public const int MaxCharges = 3;
    public const double CooldownSeconds = 8.0;
    public const double HealDurationSeconds = 2.0;

    private double _readyAt = double.NegativeInfinity;

    public int Charges { get; private set; } = MaxCharges;

    public bool CanDrink(double now, ConditionSet? conditions = null) =>
        Charges > 0 && now >= _readyAt && conditions?.PotionsBlocked != true;

    /// <summary>Drinks a charge and returns the total healing, spread over <see cref="HealDurationSeconds"/>; null when it can't be drunk.</summary>
    public int? Drink(double now, int level, IRandomSource random, ConditionSet? conditions = null)
    {
        if (!CanDrink(now, conditions))
            return null;
        Charges--;
        _readyAt = now + CooldownSeconds;
        return random.Roll(1, 8) + 2 * level;
    }

    public void Refill() => Charges = MaxCharges;

    /// <summary>A healing draught picked up: one more charge, up to the maximum. False when already full.</summary>
    public bool AddCharge()
    {
        if (Charges >= MaxCharges)
            return false;
        Charges++;
        return true;
    }
}

/// <summary>
/// The Taint corruption meter, 0 to 100. At 50 or more the bearer takes -1 (status) to saves. Reaching 100
/// sets off the whispers: movement is reversed for 2 s and the meter falls back to 50.
/// </summary>
public sealed class TaintMeter(double rateMultiplier = 1.0)
{
    public const double Max = 100;
    public const double PenaltyThreshold = 50;
    public const double WhispersSeconds = 2.0;

    public double RateMultiplier { get; } = rateMultiplier;
    public double Value { get; private set; }
    public double ReversedMovementRemaining { get; private set; }

    /// <summary>The rate for an ancestry multiplier (Ashborn 0.75) and CON: 5% slower per point, never below half rate.</summary>
    public static double RateFor(double ancestryMultiplier, int constitution) =>
        ancestryMultiplier * Math.Max(0.5, 1.0 - 0.05 * constitution);

    /// <summary>Adds corruption. Returns true when it set off the whispers.</summary>
    public bool Add(double amount)
    {
        Value = Math.Clamp(Value + amount * RateMultiplier, 0, Max);
        if (Value < Max)
            return false;
        ReversedMovementRemaining = WhispersSeconds;
        Value = PenaltyThreshold;
        return true;
    }

    public void Cleanse(double amount = Max) => Value = Math.Max(0, Value - amount);

    public void Update(double deltaSeconds) =>
        ReversedMovementRemaining = Math.Max(0, ReversedMovementRemaining - deltaSeconds);

    public Modifier? SavePenalty => Value >= PenaltyThreshold ? new Modifier("Tainted", -1, BonusType.Status) : null;
}
