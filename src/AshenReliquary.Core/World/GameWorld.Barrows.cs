using AshenReliquary.Core.Combat;

namespace AshenReliquary.Core.World;

/// <summary>
/// The Barrow Fields' dead (M4 step 3): a creature with <see cref="CreatureDefinition.Revives"/> leaves a corpse that
/// rises again that many seconds after it falls, at half its health, unless its body is destroyed first: smashed (a
/// click on it walks the hero over to break it), burned, hallowed or crushed (fire, vitality, bludgeoning or force from
/// a burst, a bomb or an aura), blown apart by Corpse Explosion, or felled by a critical hit. A risen corpse gives no XP
/// or loot the second time; after <see cref="MaxRises"/> it stays down.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>How many times one body can rise.</summary>
    public const int MaxRises = 3;

    /// <summary>How close the hero must be to smash a rising corpse.</summary>
    public const double SmashReach = 1.6;

    /// <summary>The damage that destroys a rising corpse when an area attack catches it: fire burns, vitality hallows, a blow or force crushes.</summary>
    public static bool Scours(DamageType type) => type is DamageType.Fire or DamageType.Vitality or DamageType.Bludgeoning or DamageType.Force;

    /// <summary>Corpses that will rise again, for the renderer and the bots.</summary>
    public IEnumerable<MonsterActor> RisingCorpses => _monsters.Where(m => m.IsRising);

    /// <summary>A fallen reviver begins to stir, unless the killing blow was a critical hit (the body is broken outright).</summary>
    private void LayToRise(MonsterActor m, bool critical)
    {
        if (m.Definition.Revives <= 0 || m.Rises >= MaxRises)
            return;
        if (critical)
        {
            Float(m, "shattered", FloatingTextKind.Info, 0.4);
            return;
        }
        m.RisesIn = m.Definition.Revives;
    }

    /// <summary>A rising corpse's clock; at zero it stands again.</summary>
    private void UpdateRising(MonsterActor m, double dt)
    {
        m.RisesIn -= dt;
        if (m.RisesIn > 0)
            return;
        m.RisesIn = 0;
        m.Rises++;
        m.Creature.Conditions.Clear();
        m.Creature.Persistent.Clear();
        m.Creature.Heal(m.Creature.Stats.HitPoints / 2 - m.Creature.HitPoints);
        m.DeadFor = 0;
        m.HitFlinch = 0;
        m.State = MonsterState.Idle;
        m.AttackElapsed = -1;
        m.Path.Clear();
        m.Play("spawn", restart: true);
        Sound(SoundCue.GraveRise, m, "grave");
        Float(m, "rises again", FloatingTextKind.Info, 0.5);
        Aggro(m);
    }

    /// <summary>Breaks a rising corpse for good.</summary>
    private void DestroyCorpse(MonsterActor m, string how)
    {
        if (!m.IsRising)
            return;
        m.RisesIn = 0;
        m.Rises = MaxRises;
        Float(m, how, FloatingTextKind.Info, 0.4);
        AddImpact(m.X, m.Y, how == "burned" ? DamageType.Fire : how == "hallowed" ? DamageType.Vitality : DamageType.Bludgeoning);
        Sound(SoundCue.Hit, m);
    }

    /// <summary>An area attack of a scouring damage type destroys every rising corpse it covers.</summary>
    internal void ScourCorpses(double x, double y, double radius, DamageType type)
    {
        if (!Scours(type))
            return;
        var how = type switch
        {
            DamageType.Fire => "burned",
            DamageType.Vitality => "hallowed",
            _ => "crushed",
        };
        foreach (var m in _monsters.Where(m => m.IsRising && m.DistanceTo(x, y) <= radius + m.Radius).ToList())
            DestroyCorpse(m, how);
    }

    /// <summary>Walk to a rising corpse (a click on it) and smash it before it stands.</summary>
    public void SmashCorpse(MonsterActor corpse)
    {
        if (Hero.IsDead || !corpse.IsRising)
            return;
        Hero.Order = null;
        Hero.PickupTarget = null;
        Hero.TalkTarget = null;
        Hero.CacheTarget = null;
        Hero.TrapTarget = null;
        if (corpse.DistanceTo(Hero) <= SmashReach)
        {
            Smash(corpse);
            return;
        }
        Hero.CorpseTarget = corpse;
        Hero.Goal = (corpse.X, corpse.Y);
        PlanPath(Hero, corpse.X, corpse.Y);
    }

    private void Smash(MonsterActor corpse)
    {
        Hero.CorpseTarget = null;
        Hero.Goal = null;
        Hero.Path.Clear();
        Hero.Face(corpse.X, corpse.Y);
        Hero.Play("swing", 2, restart: true);
        DestroyCorpse(corpse, "smashed");
    }

    /// <summary>The hero's walk to a corpse: smash it on arrival; give up if it rose first.</summary>
    private void UpdateCorpseTarget()
    {
        if (Hero.CorpseTarget is not { } corpse)
            return;
        if (!corpse.IsRising)
        {
            Hero.CorpseTarget = null;
            return;
        }
        if (corpse.DistanceTo(Hero) <= SmashReach)
            Smash(corpse);
    }
}
