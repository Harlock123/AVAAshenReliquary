using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;

namespace AshenReliquary.Core.World;

/// <summary>
/// The level-16 capstones (M4 step 8, spec section 9's "Capstone" column): auras with no reach that change the hero
/// while they last, as forms. Each is an <see cref="ActiveAura"/>, so it runs out, is listed by the HUD and ends with a
/// fall like any aura.
/// <list type="bullet">
/// <item>Unstoppable (the Fighter): blows <see cref="AbilityDefinition.AttackRate"/> times as fast, and
/// <see cref="AbilityDefinition.Steadfast"/>: stuns, holds, slows, fear, falls and the Choir's song all slide off.</item>
/// <item>Avatar of the Lantern (the Champion): a status bonus to AC and saves, holy light round the hero, and
/// <see cref="AbilityDefinition.RegenShare"/> of their HP back each pulse.</item>
/// <item>Death Mark (the Rogue): the next Strike that hits is a critical hit, and the mark is spent.</item>
/// <item>Hunter's Apex (the Ranger): every monster within its radius is hunted prey (Hunt Prey's d8s against each).</item>
/// <item>Lich Form (the Necromancer): every ally deals <see cref="AbilityDefinition.MinionDamage"/> more.</item>
/// <item>Smoke and Thunder (the Gunslinger): guns never empty, and fire faster.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    private IEnumerable<AbilityDefinition> Forms => _auras.Where(a => Time < a.EndsAt).Select(a => a.Ability);

    /// <summary>How much faster the hero's blows come in their form (1 without one).</summary>
    public double FormAttackRate => Forms.Aggregate(1.0, (rate, a) => rate * Math.Max(0.1, a.AttackRate));

    /// <summary>Nothing that takes control of the hero holds them (Unstoppable).</summary>
    public bool Steadfast => Forms.Any(a => a.Steadfast);

    /// <summary>What the hero's allies' blows are multiplied by (Lich Form doubles them).</summary>
    public double AllyDamageScale => 1 + Forms.Sum(a => a.MinionDamage);

    /// <summary>Whether a monster is the hero's prey: the one Hunt Prey marked, or any within Hunter's Apex.</summary>
    public bool IsPrey(MonsterActor m) =>
        m == Hero.Prey || Forms.Any(a => a.AllPrey && m.DistanceTo(Hero) <= a.Radius + m.Radius);

    /// <summary>The next Strike that hits is a critical hit (Death Mark).</summary>
    private bool SureCritical => Forms.Any(a => a.SureCritical);

    /// <summary>Death Mark is spent by the critical hit it gave.</summary>
    private void SpendSureCritical()
    {
        EndAura(_auras.FirstOrDefault(a => a.Ability.SureCritical));
        Float(Hero, "death mark", FloatingTextKind.Critical, 0.6);
    }

    /// <summary>The hero's guns need no loading (Smoke and Thunder).</summary>
    private bool FreeReloads => Forms.Any(a => a.FreeReloads);

    /// <summary>Steadfast: whatever has hold of the hero lets go (each frame, and as the form begins).</summary>
    private void ShakeOffControl()
    {
        if (!Steadfast)
            return;
        var conditions = Hero.Character.Conditions;
        foreach (var kind in conditions.Active.Select(a => a.Kind).Where(k => Combat.ConditionSet.IsControl(k) || k == ConditionKind.Frightened).ToList())
            conditions.Remove(kind);
        if (Hero.EnthralledUntil > Time)
            Hero.EnthralledUntil = Time;
    }

    /// <summary>A pulse of a form that mends (Avatar of the Lantern): its share of the hero's HP back.</summary>
    private void FormMends(AbilityDefinition ability)
    {
        var c = Hero.Character;
        if (ability.RegenShare <= 0 || c.HitPoints >= c.MaxHitPoints)
            return;
        var heal = Math.Max(1, (int)Math.Round(c.MaxHitPoints * ability.RegenShare));
        c.Heal(heal);
        Float(Hero, $"+{heal}", FloatingTextKind.Heal, 0.2);
    }
}
