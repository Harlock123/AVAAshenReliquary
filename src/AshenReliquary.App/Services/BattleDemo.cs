using AshenReliquary.Core.World;

namespace AshenReliquary.App.Services;

/// <summary>
/// "--battle": the hero plays a zone's fights alone, for watching or recording (and for measuring the frame rate in a
/// real fight). They walk to the nearest pack, fight it as the balance bots do (<see cref="Tactics"/>), drink a potion
/// when hurt, and move on to the next. A demo hero cannot die: below half their health they are quietly topped up,
/// and no blow takes their last hit point (<see cref="Core.Characters.Character.CannotFall"/>).
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
        world.Hero.Character.CannotFall = true;
        // Checked every frame: a level-1 hero can lose a quarter of their health in one blow.
        var hero = world.Hero.Character;
        if (hero.HitPoints < hero.MaxHitPoints * 0.6 && world.Hero.HealRemaining <= 0)
            world.DrinkPotion();
        Tactics.Upkeep(world);
        if (hero.HitPoints < hero.MaxHitPoints / 2)
            hero.Heal(hero.MaxHitPoints);

        _thinkIn -= seconds;
        _replanIn -= seconds;
        if (_thinkIn > 0)
            return;
        _thinkIn = 0.2;

        var living = world.Monsters.Where(world.CanTarget).ToList();
        var target = living.Where(m => m.DistanceTo(world.Hero) < EngageDistance).MinBy(m => m.DistanceTo(world.Hero));
        if (target is not null)
        {
            _walkingTo = null;
            if (!world.Hero.IsSwinging)
                Tactics.Act(world, classId, target);
            return;
        }
        if (Tactics.Tidy(world) || Tactics.WarmUp(world))
        {
            _walkingTo = null;
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
}
