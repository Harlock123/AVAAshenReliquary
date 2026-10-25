using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// The Burning God (M5 step 2), Malachar's second phase, on the boss framework's phase fields:
/// <list type="bullet">
/// <item>The Taint rises: in the hero, faster the longer the phase goes on; when it boils over, the whispers take the
/// hero's feet and the god's fire burns through them.</item>
/// <item>Lantern lights: pools of pale light drift over the arena; standing in one stops the rise and cleanses the
/// Taint.</item>
/// <item>Ash novas: rings that spread from the Throne to the arena's edge and beyond, to be Stepped through.</item>
/// <item>Malachar channels the god: he takes a share of every blow, and his strikes are fire and void in turn
/// (<see cref="BossPhase.DamageTakenScale"/> and <see cref="BossPhase.StrikeTypes"/>, applied where blows land).</item>
/// </list>
/// All of it ends when the hero falls (the fight starts again) or Malachar does.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>How far beyond the arena's edge a nova runs before it fades.</summary>
    public const double NovaOverrun = 4;

    private readonly List<LanternLight> _lanternLights = [];
    private readonly List<AshNova> _novas = [];

    /// <summary>The lantern lights drifting over the arena now.</summary>
    public IReadOnlyList<LanternLight> LanternLights => _lanternLights;

    /// <summary>The ash novas spreading now.</summary>
    public IReadOnlyList<AshNova> Novas => _novas;

    /// <summary>Whether the hero stands in a lantern light.</summary>
    public bool InLanternLight => _lanternLights.Any(l => l.Covers(Hero.X, Hero.Y));

    /// <summary>A phase begins: its lantern lights kindle round the arena, its first nova comes a little later.</summary>
    private void EnterBurning(MonsterActor m, BossPhase phase)
    {
        m.PhaseStartedAt = Time;
        m.NovaIn = phase.NovaEverySeconds / 2;
        _lanternLights.Clear();
        for (var i = 0; i < phase.Lights; i++)
        {
            var angle = 2 * Math.PI * i / phase.Lights + Math.PI / 4;
            var r = m.Definition.Boss!.ArenaRadius * 0.55;
            var (x, y) = (m.HomeX + Math.Cos(angle) * r, m.HomeY + Math.Sin(angle) * r);
            if (!Map.IsWalkable(x, y))
                (x, y) = OpenNear(x, y);
            var light = new LanternLight(x, y, phase.LightRadius);
            NextDrift(m, light);
            _lanternLights.Add(light);
        }
        if (phase.Lights > 0)
            Say("Pale lights kindle on the glass: the Order's lanterns, burning still.");
    }

    private void UpdateBurning(MonsterActor m, BossPhase phase, double dt)
    {
        foreach (var light in _lanternLights)
        {
            var (dx, dy) = (light.ToX - light.X, light.ToY - light.Y);
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.2)
            {
                NextDrift(m, light);
                continue;
            }
            var step = Math.Min(d, phase.LightSpeed * dt);
            (light.X, light.Y) = (light.X + dx / d * step, light.Y + dy / d * step);
        }
        if (phase.TaintRise > 0 && !Hero.IsDead)
        {
            if (InLanternLight)
                Hero.Character.Taint.Cleanse(phase.LightCleanse * dt);
            else
            {
                var rate = Math.Min(phase.TaintRiseMax, phase.TaintRise + phase.TaintRiseGrowth * (Time - m.PhaseStartedAt));
                if (AddTaint(rate * dt) && phase.WhispersDamage is { } burn)
                {
                    var dealt = DamageCalculator.ApplyDefenses(Dice.Parse(burn).Roll(Random), DamageType.Fire, Hero.Character.Defenses);
                    if (dealt > 0)
                    {
                        Float(Hero, $"the god burns {dealt}", FloatingTextKind.DamageTaken, 0.4);
                        HurtHero(dealt);
                    }
                }
            }
        }
        if (phase.NovaEverySeconds > 0)
        {
            m.NovaIn -= dt;
            if (m.NovaIn <= 0)
            {
                m.NovaIn = phase.NovaEverySeconds;
                _novas.Add(new AshNova(m.HomeX, m.HomeY, Time, phase.NovaSpeed, m.Definition.Boss!.ArenaRadius + NovaOverrun));
                Float(m, "ASH NOVA", FloatingTextKind.Critical, 0.8);
                Sound(SoundCue.Ignition, m.HomeX, m.HomeY);
            }
        }
        foreach (var nova in _novas.ToList())
        {
            var radius = nova.RadiusAt(Time);
            bool Crossed(Actor a)
            {
                var d = a.DistanceTo(nova.X, nova.Y);
                return d > nova.LastRadius - AshNova.Thickness / 2 && d <= radius + AshNova.Thickness / 2 && !nova.Struck.Contains(a);
            }
            if (!Hero.IsDead && Crossed(Hero))
            {
                nova.Struck.Add(Hero);
                if (Time < Hero.InvulnerableUntil || Hero.IsDodging)
                    Float(Hero, "stepped through", FloatingTextKind.Miss, 0.3);
                else
                {
                    var degree = Check.Roll(Random, Hero.Character.Save(StatisticKind.Reflex).Total, m.Creature.Stats.SaveDc).Degree;
                    var dealt = DamageCalculator.BasicSave(Dice.Parse(phase.NovaDamage).Roll(Random), degree, DamageType.Fire, Hero.Character.Defenses);
                    if (dealt > 0)
                    {
                        Float(Hero, $"ash {dealt}", FloatingTextKind.DamageTaken, 0.4);
                        HurtHero(dealt);
                    }
                }
            }
            foreach (var ally in _allies.Where(a => !a.IsDead && Crossed(a)).ToList())
            {
                nova.Struck.Add(ally);
                ally.Creature.TakeDamage(Dice.Parse(phase.NovaDamage).Roll(Random) / 2);
            }
            nova.LastRadius = radius;
            if (nova.Done(Time))
                _novas.Remove(nova);
        }
    }

    /// <summary>A light's next spot to drift to: somewhere open on the arena's floor.</summary>
    private void NextDrift(MonsterActor m, LanternLight light)
    {
        var reach = m.Definition.Boss!.ArenaRadius * 0.85;
        for (var tries = 0; tries < 12; tries++)
        {
            var angle = Random.Roll(0, 359) * Math.PI / 180;
            var r = 2 + Random.Roll(0, (int)(reach * 10)) / 10.0;
            var (x, y) = (m.HomeX + Math.Cos(angle) * r, m.HomeY + Math.Sin(angle) * r);
            if (Map.IsWalkable(x, y) && light.DistanceTo(x, y) >= 3)
            {
                (light.ToX, light.ToY) = (x, y);
                return;
            }
        }
        (light.ToX, light.ToY) = (m.HomeX, m.HomeY + 3);
    }

    /// <summary>The fight starts again (or is over): the lights and novas go out, his strikes are his own again.</summary>
    private void ResetBurning(MonsterActor m)
    {
        m.PhaseStartedAt = 0;
        m.NovaIn = 0;
        m.StrikesMade = 0;
        m.Creature.StrikeDamageType = m.Definition.DamageType;
        if (m.Definition.Boss!.Phases.Any(p => p.Lights > 0 || p.NovaEverySeconds > 0))
        {
            _lanternLights.Clear();
            _novas.Clear();
        }
    }
}
