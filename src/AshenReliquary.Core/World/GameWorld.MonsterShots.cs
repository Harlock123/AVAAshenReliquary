using AshenReliquary.Core.Combat;

namespace AshenReliquary.Core.World;

/// <summary>
/// A ranged monster's shot in flight (an archer's arrow, a caster's bolt): it follows its victim and its blow is rolled
/// when it arrives; a Step or a Tumble under way while it is within <see cref="GameWorld.ShotDodgeTiles"/> dodges it.
/// </summary>
public sealed class MonsterShot(MonsterActor shooter, Actor victim, double x, double y)
{
    public MonsterActor Shooter { get; } = shooter;
    public Actor Victim { get; } = victim;
    public double X { get; internal set; } = x;
    public double Y { get; internal set; } = y;

    /// <summary>A caster's bolt (drawn as a glow in its damage type's colour) rather than an arrow.</summary>
    public bool IsSpell => Shooter.Definition.Caster || Shooter.Definition.Sprite.Contains("mage");

    public DamageType DamageType => Shooter.Creature.StrikeDamageType;

    /// <summary>The hero dodged it on its last stretch: it flies on harmlessly.</summary>
    public bool Dodged { get; internal set; }
}

public sealed partial class GameWorld
{
    /// <summary>How fast a monster's shot flies: a little slower than the hero's, so it can be seen coming.</summary>
    public const double MonsterShotSpeed = 9.0;

    /// <summary>How close a shot must be for a Step or Tumble to dodge it.</summary>
    public const double ShotDodgeTiles = 2.5;

    private readonly List<MonsterShot> _monsterShots = [];

    /// <summary>Monsters' arrows and bolts in flight (the renderer draws them).</summary>
    public IReadOnlyList<MonsterShot> MonsterShots => _monsterShots;

    /// <summary>A ranged monster looses at its victim: the shot leaves from it, at chest height.</summary>
    private void Loose(MonsterActor m, Actor victim) => _monsterShots.Add(new MonsterShot(m, victim, m.X, m.Y));

    /// <summary>Shots fly at their victims (they follow them) and strike on arrival.</summary>
    private void UpdateMonsterShots(double dt)
    {
        foreach (var shot in _monsterShots.ToList())
        {
            var victim = shot.Victim;
            if (victim.IsDead || Hero.IsDead)
            {
                _monsterShots.Remove(shot);
                continue;
            }
            var d = Math.Sqrt((victim.X - shot.X) * (victim.X - shot.X) + (victim.Y - shot.Y) * (victim.Y - shot.Y));
            if (victim == Hero && d <= ShotDodgeTiles && Time < Hero.InvulnerableUntil)
                shot.Dodged = true;
            var step = MonsterShotSpeed * dt;
            if (d > step + 0.25)
            {
                shot.X += (victim.X - shot.X) / d * step;
                shot.Y += (victim.Y - shot.Y) / d * step;
                continue;
            }
            _monsterShots.Remove(shot);
            if (shot.Dodged)
                Float(Hero, "dodged", FloatingTextKind.Miss, 0.3);
            else if (victim is AllyActor ally)
                MonsterBitesAlly(shot.Shooter, ally);
            else
                MonsterStrike(shot.Shooter);
        }
    }
}
