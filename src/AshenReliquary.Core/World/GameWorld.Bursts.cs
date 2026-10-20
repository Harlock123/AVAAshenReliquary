using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>A burst going off, for the renderer: a ring (or a cone toward <see cref="Direction"/>) spreading out.</summary>
public sealed record BurstEffect(double X, double Y, double Radius, double Arc, double Direction, DamageType Type, double At);

/// <summary>
/// Area abilities (the subclasses' Thunderclap, Burning Hands, Earthshaker and Challenge): every enemy in the area makes
/// a basic save against the hero's spell DC or class DC. Damage follows the save (none, half, full, double); a failed
/// save also brings the ability's condition and knockback. A full burst goes off at once with no target, like a ward; a
/// cone is aimed at a monster and winds up like any other ability.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>How long a burst's ring stays on screen.</summary>
    public const double BurstSeconds = 0.45;

    private readonly List<BurstEffect> _bursts = [];

    /// <summary>Bursts still spreading (the renderer draws them).</summary>
    public IReadOnlyList<BurstEffect> Bursts => _bursts;

    /// <summary>Whether the hero holds a two-handed melee weapon (Vanguard abilities need one; a bow will not do).</summary>
    public bool WieldsTwoHanded => Hero.Equipment.In(EquipSlot.MainHand) is { Definition.IsTwoHanded: true, Definition.Ranged: false };

    /// <summary>A full burst round the hero: it goes off at once (its cost and cooldown as any ability).</summary>
    private void SelfBurst(AbilityDefinition ability)
    {
        if (WhyNot(ability) is { } problem)
        {
            Say(problem);
            return;
        }
        Pay(ability);
        if (ability.Cooldown > 0)
            Hero.ReadyAt[ability.Id] = Time + ability.Cooldown;
        if (ability.IsFlourish)
            Hero.FlourishReadyAt = Time + FlourishLockoutSeconds;
        if (!Hero.IsSwinging)
            Hero.Play(ability.IsSpellBurst ? "cast" : "swing", 1.5, restart: true);
        Burst(ability, null, (Hero.X, Hero.Y));
        // Smoke Bomb: into the smoke, hidden.
        if (ability.HideSeconds > 0)
            Hero.HiddenRemaining = ability.HideSeconds;
    }

    /// <summary>
    /// Resolves a burst round <paramref name="at"/> (the hero, or a sprung trap): every living, visible enemy within its
    /// radius (and its arc toward <paramref name="aim"/>, for a cone) saves; damage, conditions and knockback follow.
    /// Every enemy caught turns on the hero.
    /// </summary>
    private void Burst(AbilityDefinition ability, MonsterActor? aim, (double X, double Y) at)
    {
        var c = Hero.Character;
        var dc = ability.IsSpellBurst ? c.SpellDc().Total : c.ClassDc().Total;
        var save = Enum.TryParse<StatisticKind>(ability.Save, out var kind) ? kind : StatisticKind.Reflex;
        var rank = AbilityDefinition.SpellRank(c.Level);
        var cone = aim is not null && ability.Arc < 360;
        var direction = cone ? Math.Atan2(aim!.Y - Hero.Y, aim.X - Hero.X) : 0;
        _bursts.Add(new BurstEffect(at.X, at.Y, ability.Radius, cone ? ability.Arc : 360, direction,
            ability.WeaponDamage ? c.Weapon.Definition.DamageType : ability.DamageType, Time));
        Sound(ability.IsSpellBurst ? SoundCue.Ignition : SoundCue.Swing, at.X, at.Y);
        var caught = _monsters.Where(m => !m.IsDead && IsLit(m.X, m.Y) && m.DistanceTo(at.X, at.Y) <= ability.Radius + m.Radius
                && (!cone || WithinArc(ability.Arc, aim!, m)))
            .ToList();
        var dealtInAll = 0;
        foreach (var m in caught)
        {
            var degree = Check.Roll(Random, m.Creature.Save(save).Total, dc).Degree;
            var failed = degree <= DegreeOfSuccess.Failure;
            Aggro(m);
            if (ability.WeaponDamage || ability.Dice is not null)
            {
                var type = ability.WeaponDamage ? c.Weapon.Definition.DamageType : ability.DamageType;
                var rolled = ability.WeaponDamage
                    ? new Dice(c.Weapon.DiceCount + ability.ExtraDiceAt(c.Level), c.Weapon.Definition.DamageDie).Roll(Random) + c.DamageBonus().Total
                    : ability.DamageAt(rank).Roll(Random);
                var dealt = DamageCalculator.BasicSave(rolled, degree, type, m.Creature.Defenses);
                if (dealt > 0)
                {
                    dealt = DamageMonster(m, dealt);
                    dealtInAll += dealt;
                    var doubled = degree == DegreeOfSuccess.CriticalFailure;
                    Float(m, doubled ? $"{dealt}!" : dealt.ToString(), doubled ? FloatingTextKind.Critical : FloatingTextKind.DamageDealt);
                    m.HitFlinch = 0.15;
                    AddImpact(m.X, m.Y, type);
                }
                else
                {
                    Float(m, "resisted", FloatingTextKind.Miss);
                }
            }
            else if (!failed)
            {
                Float(m, "resisted", FloatingTextKind.Miss);
            }
            if (m.IsDead)
                Kill(m, degree == DegreeOfSuccess.CriticalFailure);
            else if (failed)
                OnHit(ability, m, degree == DegreeOfSuccess.CriticalFailure);
        }
        // Dread Harvest: a share of all it dealt comes back to the hero.
        if (ability.Drain > 0 && (int)Math.Round(dealtInAll * ability.Drain) is > 0 and var drained && c.HitPoints < c.MaxHitPoints)
        {
            c.Heal(drained);
            Float(Hero, $"+{drained}", FloatingTextKind.Heal, 0.3);
        }
        // A weapon burst that lands on anything feeds Fury once, like a sweep's first hit.
        if (ability.WeaponDamage && caught.Count > 0)
            Hero.Resource.OnCombat(false, builds: true);
    }

    /// <summary>Spent bursts leave the screen.</summary>
    private void UpdateBursts() => _bursts.RemoveAll(b => Time - b.At > BurstSeconds);

    /// <summary>
    /// The Pyromancer's Ignition: a spell bolt with <see cref="AbilityDefinition.Splash"/> also burns everyone within
    /// <see cref="SplashRadius"/> of its target for that much, hit or miss (like a bomb's splash).
    /// </summary>
    private void SpellSplash(AbilityDefinition ability, MonsterActor target, int rank)
    {
        if (ability.Splash <= 0)
            return;
        var splash = ability.Splash * Math.Max(1, (rank + 1) / 2);
        foreach (var other in _monsters.Where(m => !m.IsDead && m != target && m.DistanceTo(target) <= SplashRadius).ToList())
        {
            var dealt = DamageMonster(other, DamageCalculator.ApplyDefenses(splash, ability.DamageType, other.Creature.Defenses));
            Float(other, $"splash {dealt}", FloatingTextKind.Graze);
            Aggro(other);
            if (other.IsDead)
                Kill(other, false);
        }
    }
}
