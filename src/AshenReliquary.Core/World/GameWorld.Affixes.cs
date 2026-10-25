using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Monster affixes beyond Vengeful (see <see cref="MonsterAffix"/>): Hasted, Shielded, Tainted and Mirrored. Every blow,
/// spell, bomb and burn that lands on a monster goes through <see cref="DamageMonster"/>, so a Shielded ally's ward soaks
/// it and a Mirrored monster splits there. A monster may carry several affixes at once (Act 3's champions stack two).
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

    /// <summary>How many images a Mirrored monster splits into.</summary>
    public const int MirrorImages = 2;

    /// <summary>
    /// A Mirrored monster at half its health splits: <see cref="MirrorImages"/> images of it (minions, so weaker, and
    /// worth no XP or loot) step out beside it and fight; they shatter when the original falls.
    /// </summary>
    private void Split(MonsterActor m)
    {
        m.HasSplit = true;
        for (var i = 0; i < MirrorImages; i++)
        {
            var image = SpawnNear(m.Definition.Id, Creatures.CreatureRank.Minion, m.X, m.Y, 1.5, m.PackId, m.Creature.Stats.Level);
            image.SummonedBy = m;
            image.IsMirrorImage = true;
            image.Play("spawn", restart: true);
            Aggro(image);
        }
        Float(m, "MIRRORED", FloatingTextKind.Critical, 0.8);
        Sound(SoundCue.Ward, m, "monster");
    }

    /// <summary>The original fell: its images shatter.</summary>
    private void ShatterImages(MonsterActor original)
    {
        foreach (var image in _monsters.Where(x => x.IsMirrorImage && x.SummonedBy == original && !x.IsDead).ToList())
        {
            image.Creature.TakeDamage(image.Creature.HitPoints);
            image.State = MonsterState.Dead;
            image.Play("die", restart: true);
            Float(image, "shatters", FloatingTextKind.Info, 0.4);
        }
    }

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
        // A dormant Echo is ash, not yet a foe.
        if (m.Dormant)
            return 0;
        // The Choir: a reflection shatters at a touch; a blow at a sister lands on the one health bar they share.
        if (m.IsReflection)
        {
            if (damage > 0)
                m.Creature.TakeDamage(m.Creature.HitPoints);
            return 0;
        }
        if (m.SharesWith is { IsDead: false } pool)
        {
            var landed = DamageMonster(pool, damage);
            SyncChoir(pool);
            // A sister struck as she withdrew (no longer in the field to follow the bar) still falls with it.
            if (pool.IsDead)
                m.Creature.TakeDamage(m.Creature.HitPoints);
            return landed;
        }
        if (m.Definition.Boss is not null && damage > 0)
            m.Threat[_dealer ?? Hero] = m.Threat.GetValueOrDefault(_dealer ?? Hero) + damage;
        if (m.Ward > 0 && damage > 0)
        {
            var soaked = Math.Min(m.Ward, damage);
            m.Ward -= soaked;
            damage -= soaked;
            if (damage == 0)
                Float(m, $"absorbed {soaked}", FloatingTextKind.Miss);
        }
        m.Creature.TakeDamage(damage);
        // A figment struck dissolves (Kill sees to it, worth nothing).
        if (m.Definition.Illusion && damage > 0)
            m.Creature.TakeDamage(m.Creature.HitPoints);
        if (m.Has(MonsterAffix.Mirrored) && !m.HasSplit && !m.IsDead && m.Creature.HitPoints * 2 <= m.Creature.Stats.HitPoints)
            Split(m);
        if (m.Definition.Boss is { Sisters.Count: > 0 })
            SyncChoir(m);
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
        if (dealt > 0)
            AddTaint(dealt * TaintPerDamage);
    }
}
