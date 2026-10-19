using AshenReliquary.Core.Abilities;

namespace AshenReliquary.Core.World;

/// <summary>
/// The Ranger's rules (spec, section 7) and the hero's bow: Hunt Prey marks one monster, and every Strike against it
/// deals Hunt Prey's precision d8s; arrows that pierce fly on through the enemies behind their target.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>How far past its target, and how far off its line, a piercing arrow finds the next enemy.</summary>
    public const double PierceReach = 4.0;
    public const double PierceWidth = 0.9;

    /// <summary>Marks a monster as the hero's prey (it must be in range and in sight); it stays marked until it dies.</summary>
    private void Mark(AbilityDefinition ability, MonsterActor target)
    {
        if (Hero.DistanceTo(target) > ability.Range + target.Radius || !IsLit(target.X, target.Y))
        {
            Say($"{target.Name} is too far to hunt.");
            return;
        }
        if (!BeginSelf(ability))
            return;
        Hero.Prey = target;
        Hero.Face(target.X, target.Y);
        Float(target, "hunted", FloatingTextKind.Info, 0.3);
    }

    /// <summary>
    /// An arrow that pierces flies on from where it struck to the next living enemy beyond, near its line of flight,
    /// as a new Strike (no resource built, no ambush).
    /// </summary>
    private void PierceOn(Projectile p, ArrowShot shot)
    {
        if (shot.PierceLeft <= 0)
            return;
        var (fx, fy) = (p.Target.X - p.X, p.Target.Y - p.Y);
        var from = (X: Hero.X, Y: Hero.Y);
        var (dx, dy) = (p.Target.X - from.X, p.Target.Y - from.Y);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-6)
            (dx, dy, length) = (fx, fy, Math.Sqrt(fx * fx + fy * fy));
        if (length < 1e-6)
            return;
        (dx, dy) = (dx / length, dy / length);
        MonsterActor? next = null;
        var best = double.MaxValue;
        foreach (var m in _monsters)
        {
            if (m.IsDead || m == p.Target || !IsLit(m.X, m.Y))
                continue;
            var (ox, oy) = (m.X - p.Target.X, m.Y - p.Target.Y);
            var along = ox * dx + oy * dy;
            var off = Math.Abs(ox * dy - oy * dx);
            if (along <= 0 || along > PierceReach || off > PierceWidth + m.Radius || along >= best)
                continue;
            best = along;
            next = m;
        }
        if (next is null)
            return;
        _projectiles.Add(new Projectile(p.Ability, next, p.Rank, p.Target.X, p.Target.Y, Time, 0)
        {
            Shot = shot with { Builds = false, FromHiding = false, PierceLeft = shot.PierceLeft - 1 },
        });
    }
}
