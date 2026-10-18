using AshenReliquary.Core.World;

namespace AshenReliquary.App.Services;

/// <summary>
/// "--battle": the hero plays a zone's fights alone, for watching or recording (and for measuring the frame rate in a
/// real fight). They walk to the nearest pack, fight it with the abilities on their bar, drink a potion when hurt, and
/// move on to the next. A demo hero cannot die: below half their health they are quietly topped up.
/// </summary>
public sealed class BattleDemo(GameWorld world, string classId)
{
    /// <summary>How close a living monster must be before the hero turns to fight it rather than walk on.</summary>
    public const double EngageDistance = 9;

    private double _thinkIn;
    private MonsterActor? _walkingTo;
    private double _replanIn;

    public void Update(double seconds)
    {
        if (world.Hero.IsDead)
            return;
        // Checked every frame: a level-1 hero can lose a quarter of their health in one blow.
        var hero = world.Hero.Character;
        if (hero.HitPoints < hero.MaxHitPoints * 0.6 && world.Hero.HealRemaining <= 0)
            world.DrinkPotion();
        if (hero.HitPoints < hero.MaxHitPoints / 2)
            hero.Heal(hero.MaxHitPoints);

        _thinkIn -= seconds;
        _replanIn -= seconds;
        if (_thinkIn > 0)
            return;
        _thinkIn = 0.2;

        var living = world.Monsters.Where(m => !m.IsDead).ToList();
        var target = living.Where(m => m.DistanceTo(world.Hero) < EngageDistance).MinBy(m => m.DistanceTo(world.Hero));
        if (target is not null)
        {
            _walkingTo = null;
            if (!world.Hero.IsSwinging)
                Fight(target);
            return;
        }
        // Nothing near: walk to the closest pack still standing (planning again now and then, as monsters wander).
        if (living.MinBy(m => m.DistanceTo(world.Hero)) is { } next && (next != _walkingTo || _replanIn <= 0))
        {
            _walkingTo = next;
            _replanIn = 3;
            world.MoveTo(next.X, next.Y);
        }
    }

    private void Fight(MonsterActor target)
    {
        var resource = world.Hero.Resource;
        bool Ready(string slot) => world.Bar.TryGetValue(slot, out var a) && resource.CanSpend(a.Cost) && world.CooldownRemaining(a) <= 0;
        if (classId == "wizard")
        {
            if (Ready("rmb"))
                world.UseSlot("rmb", target);
            else if (world.Hero.CurrentTarget != target)
                world.Attack(target);
            return;
        }
        if (Ready("1") && target.DistanceTo(world.Hero) > 2.5)
            world.UseSlot("1", target);
        else if (Ready("rmb") && target.DistanceTo(world.Hero) < 1.6)
            world.UseSlot("rmb", target);
        else if (world.Hero.CurrentTarget != target)
            world.Attack(target);
    }
}
