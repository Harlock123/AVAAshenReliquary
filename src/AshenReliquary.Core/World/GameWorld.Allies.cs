using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Allies (M3 plan, step 3): creatures fighting for the hero, starting with the Beastmaster's animal companion.
/// <list type="bullet">
/// <item>A companion's numbers follow the hero's level (<see cref="CompanionStats"/>) and its feats' growth.</item>
/// <item>It follows the hero; it falls on the hero's target, or else on the nearest monster fighting near the hero,
/// and bites (a creature Strike against the monster's AC). Its kills are the hero's.</item>
/// <item>Monsters fight whichever of the hero and their allies is nearer (the hero by a tile's preference).</item>
/// <item>Out of a fight it recovers as the hero does; shrines and the hero's respawn restore it. A fallen ally comes back
/// to the hero after <see cref="AllyReturnSeconds"/>; one left far behind runs (or, beyond
/// <see cref="AllyLeashTiles"/>, is simply) back at the hero's side.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    public const double AllyReturnSeconds = 20.0;
    public const double AllyLeashTiles = 16.0;
    public const double AllyFollowTiles = 2.5;
    public const double AllyEngageTiles = 7.0;
    public const double AllyCommandSpeed = 2.0;

    private readonly List<AllyActor> _allies = [];

    /// <summary>The hero's allies, living or fallen (the renderer draws the living).</summary>
    public IReadOnlyList<AllyActor> Allies => _allies;

    /// <summary>The Beastmaster's companion, or null.</summary>
    public AllyActor? Companion => _allies.FirstOrDefault();

    /// <summary>
    /// A companion's creature at a hero level: AC 14 + level, attack 6 + level, save DC 15 + level, (10 + 8 × level)
    /// HP and its dice + half the level, close to a monster of the hero's level but sturdier; each step of growth adds
    /// half its HP again, 1 to AC and attack, and a die.
    /// </summary>
    public static Creature CompanionStats(CreatureDefinition definition, int level, int growth)
    {
        var dice = Dice.Parse(definition.Damage ?? "1d6");
        var stats = new CreatureStats(level, CreatureRank.Normal, level,
            ArmorClass: 14 + level + growth,
            AttackBonus: 6 + level + growth,
            SaveDc: 15 + level,
            HitPoints: (int)Math.Round((10 + 8 * level) * (1 + 0.5 * growth)),
            DamageMultiplier: 1.0);
        return new Creature(definition.Name, stats, dice with { Count = dice.Count + growth, Bonus = dice.Bonus + level / 2 },
            definition.DamageType, definition.BuildDefenses(), definition.AttacksPerSecond);
    }

    /// <summary>
    /// The companion the hero's feats call for: summoned beside them when first gained, rebuilt (keeping its share of
    /// health) when their level or its growth changes, and sent away when no feat calls for it (a respec).
    /// </summary>
    private void RefreshCompanion()
    {
        var c = Hero.Character;
        if (c.Companion is not { } id || !_creatures.TryGetValue(id, out var definition))
        {
            _allies.Clear();
            return;
        }
        if (Companion is not { } ally || ally.Definition.Id != id)
        {
            _allies.Clear();
            _allies.Add(new AllyActor(definition, CompanionStats(definition, c.Level, c.CompanionGrowth), c.Level, c.CompanionGrowth));
            PlaceAllies();
            return;
        }
        if (ally.Level == c.Level && ally.Growth == c.CompanionGrowth)
            return;
        var share = (double)ally.Creature.HitPoints / ally.Creature.Stats.HitPoints;
        ally.Creature = CompanionStats(definition, c.Level, c.CompanionGrowth);
        ally.Creature.SetHitPoints(ally.IsDead && ally.ReturnIn > 0 ? 0 : (int)Math.Ceiling(share * ally.Creature.Stats.HitPoints));
        (ally.Level, ally.Growth) = (c.Level, c.CompanionGrowth);
    }

    /// <summary>Puts every living ally at the hero's side (a new zone, a respawn, a companion just come).</summary>
    private void PlaceAllies()
    {
        foreach (var ally in _allies)
        {
            var (x, y) = OpenNear(Hero.X, Hero.Y);
            (ally.X, ally.Y) = (x, y);
            ally.Path.Clear();
            ally.Target = null;
            ally.Command = null;
            ally.AttackElapsed = -1;
        }
    }

    /// <summary>The walkable spot nearest the hero's side, a tile off.</summary>
    private (double X, double Y) OpenNear(double x, double y)
    {
        foreach (var (dx, dy) in new[] { (1.0, 0.0), (0.0, 1.0), (-1.0, 0.0), (0.0, -1.0), (0.7, 0.7), (-0.7, 0.7), (0.7, -0.7), (-0.7, -0.7) })
        {
            if (Map.IsWalkable(x + dx, y + dy))
                return (x + dx, y + dy);
        }
        return (x, y);
    }

    /// <summary>A loaded game's companion health (0: fallen, back soon).</summary>
    internal void RestoreCompanion(int hitPoints)
    {
        RefreshCompanion();
        if (Companion is not { } ally)
            return;
        ally.Creature.SetHitPoints(hitPoints);
        if (ally.IsDead)
            ally.ReturnIn = AllyReturnSeconds;
        PlaceAllies();
    }

    /// <summary>Shrines and the hero's respawn: allies at full health, fallen ones back at once.</summary>
    private void RestAllies()
    {
        foreach (var ally in _allies)
        {
            ally.Creature.Restore();
            ally.ReturnIn = 0;
            ally.Play("stance", restart: true);
        }
    }

    /// <summary>
    /// Sic 'Em: the companion bounds at the target (faster than it runs) and its next bite is the ability's. Its cost
    /// and cooldown are spent only if there is a companion standing to send.
    /// </summary>
    private void SendCompanion(AbilityDefinition ability, MonsterActor target)
    {
        if (Companion is not { IsDead: false } ally)
        {
            Say(Companion is null ? "You have no companion to send." : $"Your {Companion.Name.ToLowerInvariant()} is down.");
            return;
        }
        if (Hero.DistanceTo(target) > ability.Range + target.Radius || !IsLit(target.X, target.Y))
        {
            Say($"{target.Name} is too far to send your {ally.Name.ToLowerInvariant()} at.");
            return;
        }
        if (!BeginSelf(ability))
            return;
        ally.Target = target;
        ally.Command = ability;
        ally.Path.Clear();
        Float(ally, "sic!", FloatingTextKind.Info, 0.3);
    }

    private void UpdateAllies(double dt)
    {
        RefreshCompanion();
        foreach (var ally in _allies)
            UpdateAlly(ally, dt);
    }

    private void UpdateAlly(AllyActor ally, double dt)
    {
        ally.Advance(dt);
        if (ally.HitFlinch > 0)
            ally.HitFlinch -= dt;
        if (ally.IsDead)
        {
            if (Hero.IsDead)
                return;
            ally.ReturnIn -= dt;
            if (ally.ReturnIn > 0)
                return;
            ally.Creature.Restore();
            ally.Creature.SetHitPoints(ally.Creature.Stats.HitPoints / 2);
            PlaceAllies();
            ally.Play("stance", restart: true);
            Float(ally, $"{ally.Name.ToLowerInvariant()} returns", FloatingTextKind.Heal, 0.5);
            return;
        }
        ally.Creature.Conditions.Update(dt);
        if (Hero.IsDead)
        {
            ally.Target = null;
            ally.Play("stance");
            return;
        }
        if (ally.DistanceTo(Hero) > AllyLeashTiles)
            PlaceAllies();
        // It catches its breath when the hero does.
        if (Time - _calmSince >= RegenDelaySeconds)
        {
            ally.RegenCarry += ally.Creature.Stats.HitPoints * RegenShare * dt;
            var whole = (int)ally.RegenCarry;
            ally.Creature.Heal(whole);
            ally.RegenCarry -= whole;
        }
        if (!ally.Creature.Conditions.CanAct)
            return;

        if (ally.Target is { } held && (held.IsDead || held.DistanceTo(Hero) > AllyLeashTiles || !IsLit(held.X, held.Y)))
        {
            ally.Target = null;
            ally.Command = null;
        }
        if (ally.Command is null)
            ally.Target = ChooseAllyTarget(ally);

        if (ally.AttackCooldown > 0)
            ally.AttackCooldown -= dt;
        if (ally.AttackElapsed >= 0)
        {
            var interval = 1.0 / ally.Creature.AttacksPerSecond;
            var windup = Math.Min(0.5, interval * 0.6);
            ally.AttackElapsed += dt;
            if (!ally.AttackLanded && ally.AttackElapsed >= windup * 0.6)
            {
                ally.AttackLanded = true;
                if (ally.Target is { IsDead: false } bitten && ally.DistanceTo(bitten) <= ally.Definition.AttackRange + bitten.Radius + 0.4)
                    AllyBites(ally, bitten);
            }
            if (ally.AttackElapsed >= windup)
            {
                ally.AttackElapsed = -1;
                ally.AttackCooldown = interval - windup;
            }
            return;
        }

        if (ally.Target is { } target)
        {
            var reach = ally.Definition.AttackRange + target.Radius;
            if (ally.DistanceTo(target) <= reach)
            {
                ally.Path.Clear();
                ally.Face(target.X, target.Y);
                if (ally.AttackCooldown <= 0)
                {
                    ally.AttackElapsed = 0;
                    ally.AttackLanded = false;
                    ally.Play("swing", 0.5 / Math.Min(0.5, 0.6 / ally.Creature.AttacksPerSecond), restart: true);
                }
                else if (ally.Animation != "swing" || ally.AnimationTime > 1)
                {
                    ally.Play("stance");
                }
                return;
            }
            Chase(ally, target.X, target.Y, ally.Command is not null ? AllyCommandSpeed : 1, dt);
            return;
        }

        if (ally.DistanceTo(Hero) > AllyFollowTiles)
            Chase(ally, Hero.X, Hero.Y, ally.DistanceTo(Hero) > 6 ? 1.5 : 1, dt);
        else
        {
            ally.Path.Clear();
            ally.Play("stance");
        }
    }

    /// <summary>An ally runs toward a point, planning a path now and then.</summary>
    private void Chase(AllyActor ally, double x, double y, double speedScale, double dt)
    {
        if (!ally.Creature.Conditions.CanMove)
        {
            ally.Play("stance");
            return;
        }
        ally.RepathIn -= dt;
        if (ally.Path.Count == 0 || ally.RepathIn <= 0)
        {
            PlanPath(ally, x, y);
            ally.RepathIn = 0.4;
        }
        if (FollowPath(ally, ally.Definition.MoveSpeed * speedScale, dt, null))
            ally.Play("run");
        else
            ally.Play("stance");
    }

    /// <summary>
    /// What an ally fights: the hero's own target if it is near the hero, else the nearest monster fighting within
    /// <see cref="AllyEngageTiles"/> of the hero (an ally doesn't wake sleeping packs on its own).
    /// </summary>
    private MonsterActor? ChooseAllyTarget(AllyActor ally)
    {
        if (Hero.CurrentTarget is { IsDead: false } theirs && theirs.DistanceTo(Hero) <= AllyEngageTiles + 3 && IsLit(theirs.X, theirs.Y)
            && theirs.State != MonsterState.Idle)
            return theirs;
        if (ally.Target is { IsDead: false } current && current.State != MonsterState.Idle && current.DistanceTo(Hero) <= AllyEngageTiles + 3)
            return current;
        bool Fighting(MonsterActor m) => m.State != MonsterState.Idle && m.DistanceTo(Hero) <= AllyEngageTiles && IsLit(m.X, m.Y);
        return _monsters.Where(m => !m.IsDead && (Fighting(m) || m.Victim == ally)).MinBy(m => m.DistanceTo(ally));
    }

    /// <summary>An ally's bite: a creature Strike against the monster's AC; a commanded bite adds its dice and condition.</summary>
    private void AllyBites(AllyActor ally, MonsterActor target)
    {
        var command = ally.Command;
        ally.Command = null;
        var result = Strike.Resolve(ally.Creature, target.Creature, ally.Cadence, Time, Random);
        var damage = result.Damage;
        if (damage > 0 && command is { ExtraDice: > 0 })
            damage += DamageCalculator.ApplyDefenses(new Dice(command.ExtraDice, ally.Creature.StrikeDamage.Sides).Roll(Random),
                ally.Creature.StrikeDamageType, target.Creature.Defenses);
        Aggro(target);
        if (damage <= 0)
        {
            Float(target, result.Label, FloatingTextKind.Miss);
            Sound(SoundCue.Miss, target);
            return;
        }
        var critical = result.Degree == DegreeOfSuccess.CriticalSuccess;
        damage = DamageMonster(target, damage);
        Float(target, critical ? $"{damage}!" : damage.ToString(), critical ? FloatingTextKind.Critical : FloatingTextKind.DamageDealt);
        target.HitFlinch = 0.15;
        Sound(critical ? SoundCue.Crit : SoundCue.Hit, target);
        // Whatever it bites turns on it.
        target.Victim = ally;
        target.VictimIn = 1.0;
        if (target.IsDead)
        {
            Kill(target, critical);
            return;
        }
        if (command?.OnHit is { } name && Enum.TryParse<ConditionKind>(name, out var kind))
        {
            target.Creature.Conditions.Apply(kind, command.OnHitValue, command.OnHitSeconds > 0 ? command.OnHitSeconds : null);
            Float(target, name.ToLowerInvariant(), FloatingTextKind.Info, 0.3);
        }
    }

    /// <summary>
    /// Whom a monster fights: the nearer of the hero and the living allies, the hero by a tile's preference; chosen
    /// again every second.
    /// </summary>
    private Actor VictimOf(MonsterActor m, double dt)
    {
        m.VictimIn -= dt;
        if (m.Victim is AllyActor { IsDead: true } || m.Victim is null || m.VictimIn <= 0)
        {
            m.VictimIn = 1.0;
            Actor best = Hero;
            var bestDistance = m.DistanceTo(Hero) - 1.0;
            foreach (var ally in _allies)
            {
                if (ally.IsDead)
                    continue;
                var d = m.DistanceTo(ally);
                if (d < bestDistance)
                    (best, bestDistance) = (ally, d);
            }
            m.Victim = best;
        }
        return m.Victim;
    }

    /// <summary>A monster's strike at an ally: its attack against the ally's AC; at 0 HP the ally falls and comes back later.</summary>
    private void MonsterBitesAlly(MonsterActor m, AllyActor ally)
    {
        var (attack, damage, _) = VengeanceBonus(m);
        var result = Strike.Resolve(m.Creature, ally.Creature, m.Cadence, Time, Random, attackBonus: attack, damageScale: damage);
        if (result.Damage <= 0)
        {
            Float(ally, "miss", FloatingTextKind.Miss);
            return;
        }
        ally.Creature.TakeDamage(result.Damage);
        ally.HitFlinch = 0.15;
        Float(ally, result.Damage.ToString(), FloatingTextKind.AllyHurt);
        if (!ally.IsDead)
            return;
        ally.ReturnIn = AllyReturnSeconds;
        ally.Target = null;
        ally.Command = null;
        ally.AttackElapsed = -1;
        ally.Path.Clear();
        ally.Play("die", restart: true);
        Sound(SoundCue.MonsterDeath, ally, ally.Definition.Voice);
        Say($"Your {ally.Name.ToLowerInvariant()} falls; it will find you again in {AllyReturnSeconds:0} s.");
        foreach (var other in _monsters.Where(o => o.Victim == ally))
            other.Victim = null;
    }
}
