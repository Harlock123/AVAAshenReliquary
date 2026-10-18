using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Monster affixes beyond Vengeful (see <see cref="MonsterAffix"/>): Hasted, Shielded and Tainted. Every blow, spell,
/// bomb and burn that lands on a monster goes through <see cref="DamageMonster"/>, so a Shielded ally's ward soaks it.
/// </summary>
public sealed partial class GameWorld
{
    public const double HasteAttackRate = 1.25;
    public const double HasteSpeed = 1.15;
    public const double PackWardEverySeconds = 10.0;
    public const double PackWardRadius = 6.0;
    public const double PackWardSeconds = 8.0;
    public const double TaintPoolRadius = 1.6;
    public const double TaintPoolSeconds = 8.0;

    /// <summary>How much the Taint meter fills for each point of Taint damage taken.</summary>
    public const double TaintPerDamage = 3.0;

    /// <summary>A Shielded monster's ward on its pack: soaks 4 + 2 × level.</summary>
    public static int PackWard(int level) => 4 + 2 * level;

    private void UpdateAffixes(MonsterActor m, double dt)
    {
        if (m.Ward > 0 && Time >= m.WardEndsAt)
            m.Ward = 0;
        if (!m.Has(MonsterAffix.Shielded) || m.State == MonsterState.Idle)
            return;
        m.WardPackIn -= dt;
        if (m.WardPackIn > 0)
            return;
        m.WardPackIn = PackWardEverySeconds;
        var amount = PackWard(m.Creature.Stats.Level);
        foreach (var ally in _monsters.Where(a => !a.IsDead && a.PackId == m.PackId && a.DistanceTo(m) <= PackWardRadius))
        {
            ally.Ward = amount;
            ally.WardEndsAt = Time + PackWardSeconds;
        }
        Float(m, "WARD", FloatingTextKind.Info, 0.8);
        Sound(SoundCue.Ward, m, "monster");
    }

    /// <summary>Damage landing on a monster: its ward (Shielded) soaks what it can, the rest is taken. Returns the rest.</summary>
    private int DamageMonster(MonsterActor m, int damage)
    {
        if (m.Ward > 0 && damage > 0)
        {
            var soaked = Math.Min(m.Ward, damage);
            m.Ward -= soaked;
            damage -= soaked;
            if (damage == 0)
                Float(m, $"absorbed {soaked}", FloatingTextKind.Miss);
        }
        m.Creature.TakeDamage(damage);
        return damage;
    }

    /// <summary>A Tainted monster dies in a pool of Taint: a warning ring, then 8 s of burning corruption.</summary>
    private void LeaveTaint(MonsterActor m)
    {
        var level = m.Creature.Stats.Level;
        AddHazard(new Hazard(m.X, m.Y, TaintPoolRadius, Time + 0.6, Time + 0.6 + TaintPoolSeconds,
            new Dice(1 + level / 3, 6), 14 + level, DamageType.Taint));
        Sound(SoundCue.GraveRise, m, "grave");
    }

    /// <summary>Taint damage taken fills the meter; at 100 the whispers turn the hero's feet for 2 s.</summary>
    private void Corrupt(int dealt)
    {
        if (dealt <= 0 || !Hero.Character.Taint.Add(dealt * TaintPerDamage))
            return;
        Say("The whispers take hold: your feet are not your own.");
        Float(Hero, "WHISPERS", FloatingTextKind.Critical, 1.0);
    }
}
