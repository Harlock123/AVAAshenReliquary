using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>An aura round the hero while it lasts (the Champion's); <see cref="NextTick"/> is when it next pulses.</summary>
public sealed class ActiveAura(AbilityDefinition ability, double endsAt)
{
    public AbilityDefinition Ability { get; } = ability;
    public double EndsAt { get; internal set; } = endsAt;
    public double NextTick { get; internal set; }
}

/// <summary>
/// The Champion's rules (spec, section 7): Devotion, built by the blows the hero takes, is spent on Lay on Hands and on
/// auras; the Lanternbearer's undead bane and lantern; the Oathbreaker's Taint, a resource as well as a danger.
/// <list type="bullet">
/// <item>An aura (<see cref="AbilityKind.Aura"/>) lasts its <see cref="AbilityDefinition.BuffSeconds"/> round the hero
/// and pulses every <see cref="AuraTickSeconds"/>: damage to the enemies within (a basic save against the class DC, its
/// condition on a failure), healing to the hero and the allies within. Its status bonus to AC and saves is the hero's;
/// its soak comes off every monster's blow at the hero, or at an ally within it. Several auras can be up at once.</item>
/// <item>The Oathbreaker's hits fill their own Taint meter (<see cref="OathTaintPerHit"/>, slowed as any Taint is),
/// their Strikes deal a point of Taint damage for every <see cref="TaintPerDrain"/> in it and heal them as much, and Aura
/// of Dread is paid for out of the meter. Its whispers at 100 still turn their feet.</item>
/// </list>
/// </summary>
public sealed partial class GameWorld
{
    public const double AuraTickSeconds = 2.0;
    public const double OathTaintPerHit = 3.0;
    public const double TaintPerDrain = 25.0;

    private readonly List<ActiveAura> _auras = [];

    /// <summary>The auras round the hero right now (the renderer draws them, the HUD lists them).</summary>
    public IReadOnlyList<ActiveAura> Auras => _auras;

    /// <summary>
    /// Whether the hero can pay for an ability now: its resource (the Taint meter for the Oathbreaker's), and the
    /// Grave Charges it takes; one that a charge can pay for instead (Raise Bones) needs only that.
    /// </summary>
    public bool CanAfford(AbilityDefinition ability) =>
        ability.ChargeInstead && GraveCharges > 0
        || HasCharges(ability) && (ability.CostsTaint ? Hero.Character.Taint.Value + 1e-9 >= ability.Cost : ResourceFor(ability).CanSpend(ability.Cost));

    private void Pay(AbilityDefinition ability)
    {
        if (ability.ChargeInstead && GraveCharges > 0)
        {
            GraveCharges--;
            return;
        }
        SpendCharges(ability);
        if (ability.CostsTaint)
            Hero.Character.Taint.Cleanse(ability.Cost);
        else
            ResourceFor(ability).TrySpend(ability.Cost);
    }

    /// <summary>Lay on Hands: the hero, and every living ally within its radius, heal its amount at once.</summary>
    private void LayOnHands(AbilityDefinition ability)
    {
        if (!BeginSelf(ability))
            return;
        var c = Hero.Character;
        var amount = (int)Math.Round(ability.HealAt(AbilityDefinition.SpellRank(c.Level)) * (1 + c.HealShare));
        var healed = Math.Min(amount, c.MaxHitPoints - c.HitPoints);
        c.Heal(amount);
        Float(Hero, $"+{healed}", FloatingTextKind.Heal, 0.4);
        if (ability.CleansesTaint > 0 && c.Taint.Value > 0)
        {
            c.Taint.Cleanse(ability.CleansesTaint);
            Float(Hero, "cleansed", FloatingTextKind.Heal, 0.7);
        }
        foreach (var ally in _allies.Where(a => !a.IsDead && a.DistanceTo(Hero) <= ability.Radius))
        {
            ally.Creature.Heal(amount);
            Float(ally, $"+{amount}", FloatingTextKind.Heal, 0.4);
        }
        if (!Hero.IsSwinging)
            Hero.Play("cast", 1.5, restart: true);
        Sound(SoundCue.Ward, Hero, "spell");
    }

    /// <summary>Raises an aura round the hero, or renews it; it pulses at once.</summary>
    private void RaiseAura(AbilityDefinition ability)
    {
        if (!BeginSelf(ability))
            return;
        EndAura(_auras.FirstOrDefault(a => a.Ability.Id == ability.Id));
        var aura = new ActiveAura(ability, Time + ability.BuffSeconds) { NextTick = Time };
        _auras.Add(aura);
        var source = AuraSource(ability);
        if (ability.AuraAc > 0)
        {
            foreach (var stat in new[] { StatisticKind.ArmorClass, StatisticKind.Fortitude, StatisticKind.Reflex, StatisticKind.Will })
                Hero.Character.AddModifier(stat, new Modifier(source, ability.AuraAc, BonusType.Status));
        }
        // Shield of Faith raises a shield in hand for as long: its AC, and Shield Block.
        if (ability.RaisesShield && Hero.Equipment.In(EquipSlot.OffHand) is { Definition.IsShield: true } shield)
        {
            EndWard();
            Hero.Ward = new HeroWard(ability, shield.Definition.AcBonus, shield.Definition.Hardness + Hero.Character.BlockHardnessBonus,
                Time + ability.BuffSeconds);
            Hero.Character.AddModifier(StatisticKind.ArmorClass, new Modifier(WardSource(ability), shield.Definition.AcBonus, BonusType.Circumstance));
        }
        if (!Hero.IsSwinging && Hero.Path.Count == 0)
            Hero.Play("cast", 1.5, restart: true);
        Sound(SoundCue.Ward, Hero, "spell");
        Float(Hero, ability.Name.ToLowerInvariant(), FloatingTextKind.Info, 0.7);
    }

    private static string AuraSource(AbilityDefinition ability) => $"{ability.Name} (aura)";

    private void EndAura(ActiveAura? aura)
    {
        if (aura is null)
            return;
        _auras.Remove(aura);
        Hero.Character.RemoveModifiers(AuraSource(aura.Ability));
    }

    /// <summary>A fall ends every aura.</summary>
    private void EndAuras()
    {
        foreach (var aura in _auras.ToList())
            EndAura(aura);
    }

    /// <summary>Auras pulse, and run out.</summary>
    private void UpdateAuras()
    {
        foreach (var aura in _auras.ToList())
        {
            if (Time >= aura.EndsAt)
            {
                EndAura(aura);
                continue;
            }
            if (Hero.IsDead || Time < aura.NextTick)
                continue;
            aura.NextTick += AuraTickSeconds;
            Pulse(aura.Ability);
        }
    }

    /// <summary>
    /// One pulse of an aura: its damage on every enemy within (a basic save; the condition on a failure; a share
    /// drained back to the hero), its healing on the hero and the allies within.
    /// </summary>
    private void Pulse(AbilityDefinition ability)
    {
        var c = Hero.Character;
        var rank = AbilityDefinition.SpellRank(c.Level);
        if (ability.Dice is not null)
        {
            var dc = c.ClassDc().Total;
            var save = Enum.TryParse<StatisticKind>(ability.Save, out var kind) ? kind : StatisticKind.Fortitude;
            var drained = 0;
            foreach (var m in _monsters.Where(m => !m.IsDead && !m.Definition.IsSpawner && IsLit(m.X, m.Y)
                         && m.DistanceTo(Hero) <= ability.Radius + m.Radius).ToList())
            {
                var degree = Check.Roll(Random, m.Creature.Save(save).Total, dc).Degree;
                var type = ability.Holy ? (m.Definition.Undead ? DamageType.Vitality : DamageType.Fire) : ability.DamageType;
                var dealt = DamageCalculator.BasicSave(ability.AuraDiceAt(rank).Roll(Random), degree, type, m.Creature.Defenses);
                Aggro(m);
                if (dealt > 0)
                {
                    dealt = DamageMonster(m, dealt);
                    drained += dealt;
                    Float(m, dealt.ToString(), FloatingTextKind.DamageDealt, 0.2);
                    AddImpact(m.X, m.Y, type);
                }
                if (m.IsDead)
                    Kill(m, false);
                else if (degree <= DegreeOfSuccess.Failure)
                    OnHit(ability, m, degree == DegreeOfSuccess.CriticalFailure);
            }
            // A holy aura hallows the rising dead within it.
            ScourCorpses(Hero.X, Hero.Y, ability.Radius, ability.Holy ? DamageType.Vitality : ability.DamageType);
            if (ability.Drain > 0 && (int)Math.Round(drained * ability.Drain) is > 0 and var back && c.HitPoints < c.MaxHitPoints)
            {
                c.Heal(back);
                Float(Hero, $"+{back}", FloatingTextKind.Heal, 0.2);
            }
        }
        if (ability.AuraHealAt(rank) is > 0 and var heal)
        {
            if (c.HitPoints < c.MaxHitPoints)
            {
                c.Heal(heal);
                Float(Hero, $"+{heal}", FloatingTextKind.Heal, 0.2);
            }
            foreach (var ally in _allies.Where(a => !a.IsDead && a.DistanceTo(Hero) <= ability.Radius))
                ally.Creature.Heal(heal);
        }
    }

    /// <summary>What the auras take off a monster's blow at the hero, or at an ally within them.</summary>
    private int AuraSoak(int damage, Actor victim)
    {
        var rank = AbilityDefinition.SpellRank(Hero.Character.Level);
        foreach (var aura in _auras)
        {
            if (aura.Ability.AuraSoakAt(rank) is > 0 and var soak && (victim == Hero || victim.DistanceTo(Hero) <= aura.Ability.Radius))
                damage -= soak;
        }
        return Math.Max(0, damage);
    }

    /// <summary>
    /// What a Champion's Strike adds when it hits: the Lanternbearer's vitality against the undead, the Oathbreaker's
    /// Taint by how full their meter is (healing them as much), both through the target's defences and doubled on a
    /// critical hit. The Oathbreaker's hit then fills their meter.
    /// </summary>
    private int ChampionStrikeExtra(MonsterActor target, bool critical)
    {
        var c = Hero.Character;
        var extra = 0;
        var times = critical ? 2 : 1;
        if (c.UndeadBane > 0 && target.Definition.Undead)
            extra += DamageCalculator.ApplyDefenses(c.UndeadBane * times, DamageType.Vitality, target.Creature.Defenses);
        if (c.TaintFuel)
        {
            if ((int)(c.Taint.Value / TaintPerDrain) is > 0 and var points)
            {
                var drained = DamageCalculator.ApplyDefenses(points * times, DamageType.Taint, target.Creature.Defenses);
                extra += drained;
                c.Heal(drained);
            }
            AddTaint(OathTaintPerHit);
        }
        return extra;
    }

    private double _retaliateReadyAt;

    /// <summary>How often the Paladin's reaction strikes back without an aura that retaliates (one makes it every second).</summary>
    public const double RetributionSeconds = 3.0;

    /// <summary>How near the hero an ally must stand for the Paladin's reaction to answer a blow at it.</summary>
    public const double RetributionGuardTiles = 1.5;

    /// <summary>
    /// The champion's reaction. While an aura that retaliates is up (Retributive Strike, the Paladin's Aura of Justice),
    /// a monster that lands a blow on the hero, or on an ally within the aura, from within the hero's reach is struck back
    /// at once, at most once a second, with the aura's extra dice. The Paladin's Oath of Justice keeps it ready without
    /// one, for blows at the hero or an ally beside them, at most once every <see cref="RetributionSeconds"/>.
    /// </summary>
    private void Retaliate(MonsterActor attacker, Actor? victim = null)
    {
        victim ??= Hero;
        if (Time < _retaliateReadyAt || attacker.IsDead || Hero.IsDead || Shoots
            || Hero.DistanceTo(attacker) > MeleeReach + Hero.Character.ReachBonus + attacker.Radius + 0.3)
            return;
        var aura = _auras.Where(a => a.Ability.Retaliate && (victim == Hero || victim.DistanceTo(Hero) <= a.Ability.Radius))
            .MaxBy(a => a.Ability.ExtraDice);
        var oath = Hero.Character.Retribution && (victim == Hero || victim.DistanceTo(Hero) <= RetributionGuardTiles);
        if (aura is null && !oath)
            return;
        _retaliateReadyAt = Time + (aura is not null ? 1 : RetributionSeconds);
        Float(Hero, "retribution", FloatingTextKind.Info, 0.8);
        HeroStrike(attacker, BasicAbility, aura?.Ability.ExtraDiceAt(Hero.Character.Level) ?? 0);
    }

    /// <summary>The Taint meter fills; at 100 the whispers take the hero's feet.</summary>
    private void AddTaint(double amount)
    {
        if (amount <= 0 || !Hero.Character.Taint.Add(amount))
            return;
        Say("The whispers take hold: your feet are not your own.");
        Float(Hero, "WHISPERS", FloatingTextKind.Critical, 1.0);
    }
}
