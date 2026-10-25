using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Vorthrax the Cinderwyrm (M4 step 9), on the boss framework's phase fields:
/// <list type="bullet">
/// <item>His breath: a cone of fire marked on the ground toward the hero (<see cref="BossBreath"/>), then a basic Reflex
/// save against its damage for anyone in it; a Step under way passes through it.</item>
/// <item>Flight: aloft over his home, nothing but a bow, a gun, a spell or a bomb reaches him; every so often he swoops down
/// beside the hero, and for a few seconds he is on the ground to be struck. His firestorm drops pools of fire round the
/// hero, several at once.</item>
/// <item>The burning rock: in his last phase he lands, and every so often one of the arena's platforms cracks into embers
/// and sinks into the lava, the one under the hero first; anyone still on it is burned and thrown to firm ground.</item>
/// </list>
/// The arena comes back whole when the hero falls.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>How long a cracked platform burns before it sinks, by default; how far a swoop lands from the hero.</summary>
    public const double SwoopLandTiles = 1.6;

    private readonly List<Crumbling> _crumbling = [];

    /// <summary>The platforms cracking now (the renderer marks them).</summary>
    public IReadOnlyList<Crumbling> Crumbling => _crumbling;

    /// <summary>
    /// Vorthrax's turn, from <see cref="UpdateBoss"/>: flight and swoops, the breath, the crumbling rock. True while he is
    /// busy (aloft, or drawing breath), so he makes no ordinary attack.
    /// </summary>
    private bool UpdateWyrm(MonsterActor m, BossPhase phase, double dt)
    {
        if (phase.CrumbleEverySeconds > 0)
            UpdateCrumbling(m, phase, dt);
        if (phase.Flies)
        {
            if (Time < m.LandedUntil)
            {
                m.Aloft = false;
            }
            else
            {
                if (!m.Aloft)
                    Rise(m);
                m.SwoopIn -= dt;
                if (m.SwoopIn <= 0 && !Hero.IsDead)
                    Swoop(m, phase);
                else
                {
                    m.Play("run");
                    m.Face(Hero.X, Hero.Y);
                    return true;
                }
            }
        }
        else if (m.Aloft)
        {
            Land(m, m.HomeX, m.HomeY);
        }
        if (phase.BreathEverySeconds > 0 && !m.Aloft)
            return Breathe(m, phase, dt);
        return false;
    }

    private void Rise(MonsterActor m)
    {
        m.Aloft = true;
        m.Path.Clear();
        m.AttackElapsed = -1;
        m.Breath = null;
        (m.X, m.Y) = (m.HomeX, m.HomeY);
        Float(m, "takes to the air", FloatingTextKind.Info, 1.0);
        Sound(SoundCue.MonsterAggro, m, m.Definition.Voice);
        if (Hero.Order?.Target == m && !Shoots)
            Hero.Order = null;
    }

    /// <summary>Down out of the sky beside the hero, on firm ground, and on the ground for a while.</summary>
    private void Swoop(MonsterActor m, BossPhase phase)
    {
        m.SwoopIn = phase.SwoopEverySeconds;
        m.LandedUntil = Time + phase.SwoopSeconds;
        var (dx, dy) = (m.X - Hero.X, m.Y - Hero.Y);
        var d = Math.Max(0.01, Math.Sqrt(dx * dx + dy * dy));
        var (x, y) = (Hero.X + dx / d * SwoopLandTiles, Hero.Y + dy / d * SwoopLandTiles);
        if (!Map.IsWalkable(x, y))
            (x, y) = OpenNear(Hero.X, Hero.Y);
        Land(m, x, y);
        Float(m, "SWOOPS", FloatingTextKind.Critical, 1.0);
        AddImpact(x, y, DamageType.Fire);
        Sound(SoundCue.Crit, m);
    }

    private void Land(MonsterActor m, double x, double y)
    {
        m.Aloft = false;
        (m.X, m.Y) = (x, y);
        m.Path.Clear();
        m.Face(Hero.X, Hero.Y);
    }

    /// <summary>A melee blow can't reach a flying boss in the air.</summary>
    public bool OutOfReach(MonsterActor m) => m.Aloft;

    /// <summary>The breath: a cone marked toward the hero, then fire. True while it is drawing breath.</summary>
    private bool Breathe(MonsterActor m, BossPhase phase, double dt)
    {
        if (m.Breath is { } breath)
        {
            if (Time < breath.BurnAt)
                return true;
            if (!breath.Burned)
            {
                breath.Burned = true;
                Sound(SoundCue.Ignition, m);
                for (var r = 1.0; r <= breath.Range; r += 1.5)
                    AddImpact(breath.X + Math.Cos(breath.Direction) * r, breath.Y + Math.Sin(breath.Direction) * r, DamageType.Fire);
                if (!Hero.IsDead && breath.Covers(Hero.X, Hero.Y))
                {
                    if (Time < Hero.InvulnerableUntil || Hero.IsDodging)
                        Float(Hero, "dodged", FloatingTextKind.Miss, 0.3);
                    else
                    {
                        var degree = Check.Roll(Random, Hero.Character.Save(StatisticKind.Reflex).Total, m.Creature.Stats.SaveDc).Degree;
                        var dealt = DamageCalculator.BasicSave(Dice.Parse(phase.BreathDamage).Roll(Random), degree, DamageType.Fire, Hero.Character.Defenses);
                        if (dealt > 0)
                        {
                            Float(Hero, $"burned {dealt}", FloatingTextKind.DamageTaken, 0.4);
                            HurtHero(dealt);
                        }
                    }
                }
                foreach (var ally in _allies.Where(a => !a.IsDead && breath.Covers(a.X, a.Y)))
                    ally.Creature.TakeDamage(Dice.Parse(phase.BreathDamage).Roll(Random) / 2);
            }
            if (Time < breath.BurnAt + 0.4)
                return true;
            m.Breath = null;
            m.BreathIn = phase.BreathEverySeconds;
            return false;
        }
        m.BreathIn -= dt;
        var dist = m.DistanceTo(Hero);
        if (m.BreathIn > 0 || m.AttackElapsed >= 0 || Hero.IsDead || dist > phase.BreathRange || !Map.HasLineOfSight(m.X, m.Y, Hero.X, Hero.Y))
            return false;
        m.Breath = new BossBreath(m.X, m.Y, Math.Atan2(Hero.Y - m.Y, Hero.X - m.X), phase.BreathArc, phase.BreathRange, Time + phase.BreathWindup);
        m.Path.Clear();
        m.Face(Hero.X, Hero.Y);
        m.Play("cast", 0.8, restart: true);
        Float(m, "draws breath", FloatingTextKind.Critical, 0.9);
        Sound(SoundCue.MonsterAggro, m, m.Definition.Voice);
        return true;
    }

    /// <summary>The burning rock: platforms crack, then sink; the one under the hero first.</summary>
    private void UpdateCrumbling(MonsterActor m, BossPhase phase, double dt)
    {
        foreach (var c in _crumbling.Where(c => Time >= c.SinksAt).ToList())
        {
            _crumbling.Remove(c);
            foreach (var (x, y) in c.Cells)
                Map.Sink(x, y);
            Say("The rock gives way beneath you into the fire.");
            Sound(SoundCue.Crit, c.Cells[0].X + 0.5, c.Cells[0].Y + 0.5);
            ThrowClear(m, phase, c);
        }
        m.CrumbleIn -= dt;
        if (m.CrumbleIn > 0)
            return;
        m.CrumbleIn = phase.CrumbleEverySeconds;
        var standing = Map.Platforms.Where(p => !p.Value.Any(cell => Map.IsLava(cell.X, cell.Y)) && _crumbling.All(c => c.Group != p.Key)).ToList();
        if (standing.Count == 0)
            return;
        bool Under((int X, int Y) cell) => (int)Math.Floor(Hero.X) == cell.X && (int)Math.Floor(Hero.Y) == cell.Y;
        var pick = standing.FirstOrDefault(p => p.Value.Any(Under));
        if (pick.Key is null)
            pick = standing.MinBy(p => p.Value.Min(cell => Hero.DistanceTo(cell.X + 0.5, cell.Y + 0.5)));
        foreach (var (x, y) in pick.Value)
            Map.Crack(x, y);
        _crumbling.Add(new Crumbling(pick.Key, pick.Value, Time + phase.CrumbleWarning));
        Float(m, "THE ROCK CRACKS", FloatingTextKind.Critical, 1.0);
    }

    /// <summary>Whoever is on a platform as it sinks: burned (a basic Reflex save) and thrown to firm ground.</summary>
    private void ThrowClear(MonsterActor boss, BossPhase phase, Crumbling c)
    {
        bool On(Actor a) => c.Cells.Any(cell => (int)Math.Floor(a.X) == cell.X && (int)Math.Floor(a.Y) == cell.Y);
        if (!Hero.IsDead && On(Hero))
        {
            var degree = Check.Roll(Random, Hero.Character.Save(StatisticKind.Reflex).Total, boss.Creature.Stats.SaveDc).Degree;
            var dealt = DamageCalculator.BasicSave(Dice.Parse(phase.CrumbleDamage).Roll(Random), degree, DamageType.Fire, Hero.Character.Defenses);
            (Hero.X, Hero.Y) = FirmGround(Hero.X, Hero.Y);
            Hero.Path.Clear();
            Hero.Goal = null;
            if (dealt > 0)
            {
                Float(Hero, $"into the fire {dealt}", FloatingTextKind.DamageTaken, 0.4);
                HurtHero(dealt);
            }
        }
        foreach (var a in _monsters.Where(x => !x.IsDead && On(x)).Cast<Actor>().Concat(_allies.Where(x => !x.IsDead && On(x))))
        {
            (a.X, a.Y) = FirmGround(a.X, a.Y);
            a.Path.Clear();
        }
    }

    /// <summary>The nearest walkable spot to a point, searched outward.</summary>
    private (double X, double Y) FirmGround(double x, double y)
    {
        for (var r = 1; r <= 12; r++)
        {
            (double X, double Y)? best = null;
            for (var dy = -r; dy <= r; dy++)
            for (var dx = -r; dx <= r; dx++)
            {
                var (cx, cy) = ((int)Math.Floor(x) + dx, (int)Math.Floor(y) + dy);
                if (Map.IsBlocked(cx, cy) || Map.IsCrumbling(cx, cy))
                    continue;
                var p = (cx + 0.5, cy + 0.5);
                if (best is not { } b || (p.Item1 - x) * (p.Item1 - x) + (p.Item2 - y) * (p.Item2 - y) < (b.X - x) * (b.X - x) + (b.Y - y) * (b.Y - y))
                    best = p;
            }
            if (best is { } found)
                return found;
        }
        return (x, y);
    }

    /// <summary>The fight starts again: down out of the sky, no breath held, the rock whole.</summary>
    private void ResetWyrm(MonsterActor m)
    {
        m.Aloft = false;
        m.Breath = null;
        var first = m.Definition.Boss!.Phases[0];
        m.BreathIn = first.BreathEverySeconds / 2;
        m.SwoopIn = 0;
        m.LandedUntil = 0;
        m.CrumbleIn = 0;
        _crumbling.Clear();
        Map.RaisePlatforms();
    }
}
