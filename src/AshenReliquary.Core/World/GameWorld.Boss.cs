using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Herald fights (spec, section 12): a boss moves through the phases of its <see cref="BossDefinition"/> as its health
/// falls (rot pools, summoned adds, a flooding arena, a last fused stand against a deluge timer), never gives up the
/// chase, and starts again from full when the hero falls. Its death sets the act's flag and says its last line.
/// </summary>
public sealed partial class GameWorld
{
    private readonly List<Hazard> _hazards = [];
    private readonly List<(int X, int Y)> _flooded = [];

    /// <summary>Rot pools on the ground, warning rings first.</summary>
    public IReadOnlyList<Hazard> Hazards => _hazards;

    internal void AddHazard(Hazard hazard) => _hazards.Add(hazard);

    /// <summary>The boss the hero is fighting (awake and alive), for the HUD's boss bar and the music.</summary>
    public MonsterActor? Boss => _monsters.FirstOrDefault(m => m.Definition.Boss is not null && !m.IsDead && m.State != MonsterState.Idle);

    /// <summary>Raised when a boss falls (its death line goes up as a banner).</summary>
    public event Action<MonsterActor>? BossSlain;

    /// <summary>How fast a boss rushes down its charge lane, in tiles a second.</summary>
    public const double ChargeSpeed = 12;

    /// <summary>How close to the lane's runner the hero must be to be caught in a charge.</summary>
    public const double ChargeReach = 1.2;

    /// <summary>How far a standard's rally reaches.</summary>
    public const double RallyRadius = 14;

    /// <summary>
    /// A boss's own turn before its ordinary attack: phase changes, pools, summons, flooding, the deluge, its charge.
    /// True while it is busy charging (no ordinary attack this tick).
    /// </summary>
    private bool UpdateBoss(MonsterActor m, double dt)
    {
        var boss = m.Definition.Boss!;
        var share = (double)m.Creature.HitPoints / m.Creature.Stats.HitPoints;
        while (m.BossPhase + 1 < boss.Phases.Count && share <= boss.Phases[m.BossPhase + 1].Below)
        {
            m.BossPhase++;
            EnterPhase(m, boss.Phases[m.BossPhase]);
        }
        var phase = boss.Phases[m.BossPhase];
        m.Creature.ArmorClassShift = phase.AcShift;

        if (phase.PoolEverySeconds > 0)
        {
            m.PoolIn -= dt;
            if (m.PoolIn <= 0)
            {
                m.PoolIn = phase.PoolEverySeconds;
                var dice = Dice.Parse(phase.PoolDamage);
                _hazards.Add(new Hazard(Hero.X, Hero.Y, phase.PoolRadius, Time + phase.PoolWarningSeconds,
                    Time + phase.PoolWarningSeconds + phase.PoolSeconds, dice, PoolDc(m)));
                Sound(SoundCue.WitchCast, m);
            }
        }
        if (phase.SummonEverySeconds > 0 && phase.Summons is { } summons)
        {
            m.SummonIn -= dt;
            if (m.SummonIn <= 0)
            {
                m.SummonIn = phase.SummonEverySeconds;
                var standing = _monsters.Count(x => x.SummonedBy == m && !x.IsDead);
                for (var i = 0; i < phase.SummonCount && standing < phase.SummonCap; i++, standing++)
                {
                    var child = SpawnNear(summons, CreatureRank.Minion, m.X, m.Y, 3.0, m.PackId, m.Creature.Stats.Level);
                    child.SummonedBy = m;
                    child.Play("spawn", restart: true);
                    Aggro(child);
                }
                Sound(SoundCue.GraveRise, m, "grave");
            }
        }
        if (phase.Floods)
            Flood(m, boss, phase, dt);
        if (phase.Fuses && phase.EnrageSeconds > 0)
        {
            m.EnrageIn -= dt;
            if (m.EnrageIn <= 0)
            {
                m.DelugeIn -= dt;
                if (m.DelugeIn <= 0)
                {
                    m.DelugeIn = phase.EnrageEverySeconds;
                    Float(Hero, "DELUGE", FloatingTextKind.Critical, 1.0);
                    Sound(SoundCue.HeroHurt, Hero);
                    HurtHero(DamageCalculator.ApplyDefenses(Dice.Parse(phase.EnrageDamage).Roll(Random), DamageType.Poison, Hero.Character.Defenses));
                }
            }
        }
        if (phase.ChargeEverySeconds > 0 && !m.Fused && m.Creature.Conditions.CanMove)
        {
            if (m.Charge is { } charge)
                return Rush(m, phase, charge, dt);
            m.ChargeIn -= dt;
            var d = m.DistanceTo(Hero);
            if (m.ChargeIn <= 0 && m.AttackElapsed < 0 && d >= 2.5 && d <= 12 && Map.HasLineOfSight(m.X, m.Y, Hero.X, Hero.Y))
            {
                // Mark the lane: from where it stands, through the hero, on as far as the ground runs (2.5 tiles beyond).
                var (dx, dy) = ((Hero.X - m.X) / d, (Hero.Y - m.Y) / d);
                var reach = 0.0;
                while (reach < d + 2.5 && Map.IsWalkable(m.X + dx * (reach + 0.25), m.Y + dy * (reach + 0.25)))
                    reach += 0.25;
                m.Charge = new BossCharge(m.X, m.Y, m.X + dx * reach, m.Y + dy * reach, Time + phase.ChargeWindup);
                m.Path.Clear();
                m.Face(Hero.X, Hero.Y);
                m.Play("stance");
                Float(m, "CHARGE!", FloatingTextKind.Critical, 0.8);
                Sound(SoundCue.MonsterAggro, m, m.Definition.Voice);
                return true;
            }
        }
        return false;
    }

    /// <summary>A charge: braced while the lane shows, then a rush down it; the hero caught in it is trampled once.</summary>
    private bool Rush(MonsterActor m, BossPhase phase, BossCharge charge, double dt)
    {
        if (Time < charge.RushAt)
            return true;
        var (tx, ty) = (charge.ToX, charge.ToY);
        var d = m.DistanceTo(tx, ty);
        var step = ChargeSpeed * dt;
        var done = d <= step;
        if (done)
            (m.X, m.Y) = (tx, ty);
        else
        {
            m.X += (tx - m.X) / d * step;
            m.Y += (ty - m.Y) / d * step;
            m.Face(tx, ty);
            m.Play("run");
        }
        if (!charge.Struck && !Hero.IsDead && m.DistanceTo(Hero) <= ChargeReach)
        {
            charge.Struck = true;
            if (Time < Hero.InvulnerableUntil)
                Float(Hero, "dodged", FloatingTextKind.Miss, 0.3);
            else
            {
                var degree = Check.Roll(Random, Hero.Character.Save(StatisticKind.Reflex).Total, m.Creature.Stats.SaveDc).Degree;
                var dealt = DamageCalculator.BasicSave(Dice.Parse(phase.ChargeDamage).Roll(Random), degree, m.Creature.StrikeDamageType,
                    Hero.Character.Defenses);
                Sound(SoundCue.Crit, Hero);
                if (dealt > 0 && HurtHero(dealt) && degree <= DegreeOfSuccess.Failure)
                {
                    Hero.Character.Conditions.Apply(ConditionKind.Prone, 1, 1.0);
                    Float(Hero, "knocked down", FloatingTextKind.Critical, 0.6);
                }
            }
        }
        if (done)
        {
            m.Charge = null;
            m.ChargeIn = phase.ChargeEverySeconds;
        }
        return true;
    }

    /// <summary>A monster fights under a standing standard of its pack (Hadric's banner): +2 to hit and a fifth more damage.</summary>
    internal bool Rallied(MonsterActor m) =>
        !m.Definition.Rallies && _monsters.Any(s => s.Definition.Rallies && !s.IsDead && s.PackId == m.PackId && s.DistanceTo(m) <= RallyRadius);

    /// <summary>A boss's attack rate in its phase (Hadric strikes faster with his armour off).</summary>
    private static double BossAttackRate(MonsterActor m) => m.Definition.Boss is { } b ? b.Phases[m.BossPhase].AttackRateScale : 1;

    /// <summary>
    /// A rot pool's Fortitude DC: 14 + the boss's level (19 for Mother Sallow), the hazard's own number rather than her
    /// spell DC, which at her effective level would be out of a level-5 hero's reach.
    /// </summary>
    public static int PoolDc(MonsterActor m) => 14 + m.Creature.Stats.Level;

    private void EnterPhase(MonsterActor m, BossPhase phase)
    {
        Say($"{m.Definition.Name}: {phase.Name}");
        Float(m, phase.Name.ToUpperInvariant(), FloatingTextKind.Critical, 1.2);
        m.PoolIn = Math.Min(m.PoolIn, 2.0);
        m.SummonIn = 1.0;
        m.FloodRadius = m.Definition.Boss!.ArenaRadius;
        m.Creature.ArmorClassShift = phase.AcShift;
        m.Charge = null;
        m.ChargeIn = phase.ChargeEverySeconds / 2;
        if (phase.Banner is { } banner)
        {
            // The standard goes up beside the boss's home; it rallies the legion until it is broken.
            var standard = SpawnNear(banner, CreatureRank.Normal, m.HomeX + 2, m.HomeY - 2, 1.0, m.PackId, m.Creature.Stats.Level);
            standard.SummonedBy = m;
            Aggro(standard);
            Float(standard, "RALLY!", FloatingTextKind.Critical, 1.0);
        }
        if (phase.Fuses)
        {
            // She goes back to her cauldron and roots there; the race begins.
            m.Fused = true;
            m.X = m.HomeX;
            m.Y = m.HomeY;
            m.Path.Clear();
            m.EnrageIn = phase.EnrageSeconds;
            m.DelugeIn = 0;
            if (phase.EnrageSeconds > 0)
                Say($"Kill her before the cauldron boils over: {phase.EnrageSeconds:0} seconds.");
        }
    }

    /// <summary>The arena floods from its rim inward, half a tile a second, down to its dry heart.</summary>
    private void Flood(MonsterActor m, BossDefinition boss, BossPhase phase, double dt)
    {
        if (m.FloodRadius <= phase.DryRadius)
            return;
        m.FloodRadius = Math.Max(phase.DryRadius, m.FloodRadius - 0.5 * dt);
        var r = boss.ArenaRadius;
        for (var y = (int)(m.HomeY - r); y <= (int)(m.HomeY + r); y++)
        for (var x = (int)(m.HomeX - r); x <= (int)(m.HomeX + r); x++)
        {
            var d = Math.Sqrt((x + 0.5 - m.HomeX) * (x + 0.5 - m.HomeX) + (y + 0.5 - m.HomeY) * (y + 0.5 - m.HomeY));
            if (d > r || d < m.FloodRadius || Map.IsBlocked(x, y) || Map.IsBog(x, y) || Map.IsWater(x, y))
                continue;
            Map.SetBog(x, y, true);
            _flooded.Add((x, y));
        }
    }

    /// <summary>Rot and Taint pools: the hero in an active one makes a basic Fortitude save against its damage each second.</summary>
    private void UpdateHazards(double dt)
    {
        _hazards.RemoveAll(h => Time >= h.EndsAt);
        if (Hero.IsDead || Hero.DodgeRemaining > 0)
            return;
        foreach (var h in _hazards)
        {
            if (!h.IsActive(Time) || !h.Covers(Hero.X, Hero.Y))
                continue;
            h.TickIn -= dt;
            if (h.TickIn > 0)
                continue;
            h.TickIn = 1.0;
            var save = Check.Roll(Random, Hero.Character.Save(StatisticKind.Fortitude).Total + PoisonSaveBonus(h.Type), h.Dc);
            var rolled = h.Damage.Roll(Random);
            var amount = save.Degree switch
            {
                DegreeOfSuccess.CriticalSuccess => 0,
                DegreeOfSuccess.Success => rolled / 2,
                DegreeOfSuccess.Failure => rolled,
                _ => rolled * 2,
            };
            var dealt = DamageCalculator.ApplyDefenses(amount, h.Type, Hero.Character.Defenses);
            if (h.Type == DamageType.Taint)
                Corrupt(dealt);
            if (dealt > 0 && !HurtHero(dealt))
                return;
        }
    }

    /// <summary>The hero fell: every boss starts again from full, at home, its adds, pools and flood gone.</summary>
    private void ResetBosses()
    {
        foreach (var m in _monsters.Where(m => m.Definition.Boss is not null && !m.IsDead).ToList())
        {
            m.Creature.Restore();
            (m.X, m.Y) = (m.HomeX, m.HomeY);
            m.BossPhase = 0;
            m.Fused = false;
            m.PoolIn = m.Definition.Boss!.Phases[0].PoolEverySeconds;
            m.SummonIn = 0;
            m.EnrageIn = 0;
            m.DelugeIn = 0;
            m.FloodRadius = m.Definition.Boss.ArenaRadius;
            m.Creature.ArmorClassShift = 0;
            m.Charge = null;
            m.ChargeIn = m.Definition.Boss.Phases[0].ChargeEverySeconds;
            _monsters.RemoveAll(x => x.SummonedBy == m);
        }
        ClearBossAftermath();
    }

    private void ClearBossAftermath()
    {
        _hazards.Clear();
        foreach (var (x, y) in _flooded)
            Map.SetBog(x, y, false);
        _flooded.Clear();
    }

    /// <summary>A boss's death: its adds crumble, the water drains, its flag and journal entry are written.</summary>
    private void BossDied(MonsterActor m)
    {
        var boss = m.Definition.Boss!;
        foreach (var add in _monsters.Where(x => x.SummonedBy == m && !x.IsDead).ToList())
        {
            add.Creature.TakeDamage(add.Creature.HitPoints);
            add.State = MonsterState.Dead;
            add.Play("die", restart: true);
        }
        ClearBossAftermath();
        if (boss.DeathFlag is { } flag)
            SetFlag(flag, boss.DeathValue ?? "slain");
        if (boss.Journal is { } entry)
            WriteJournal(entry);
        Arrival = new Banner($"{m.Definition.Name} is dead", boss.DeathLine, Time);
        if (boss.DeathLine is { } line)
            Say(line);
        BossSlain?.Invoke(m);
    }
}
