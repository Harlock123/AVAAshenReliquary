using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// The belt (spec, section 11's consumables): four slots, keys 7 to 0, beside the ability bar. A bomb or elixir that
/// comes into the bag takes the first free slot and keeps it while any are left. Bombs are thrown like a ranged Strike
/// (DEX + the class's attack proficiency against AC) and splash around the target even on a miss; elixirs are drunk
/// at once. What they do heightens with the hero's level, a step every two levels like a cantrip.
/// </summary>
public sealed partial class GameWorld
{
    public const int BeltSize = 4;

    /// <summary>The keys of the belt's slots, in order.</summary>
    public static readonly string[] BeltKeys = ["7", "8", "9", "0"];

    /// <summary>How far round a bomb's target its splash reaches, in tiles.</summary>
    public const double SplashRadius = 1.5;

    private readonly ItemDefinition?[] _belt = new ItemDefinition?[BeltSize];
    private int _beltBagVersion = -1;
    private int _antidoteBonus;

    /// <summary>What sits in each belt slot (null when empty).</summary>
    public IReadOnlyList<ItemDefinition?> Belt => _belt;

    /// <summary>The ability a belt slot's item is used as, or null.</summary>
    public AbilityDefinition? BeltAbility(int slot) =>
        _belt[slot]?.Ability is { } id && _abilities.TryGetValue(id, out var ability) ? ability : null;

    /// <summary>How many of a belt slot's item the bag holds.</summary>
    public int BeltCount(int slot) => _belt[slot] is { } item ? Hero.Equipment.CountOf(item) : 0;

    /// <summary>
    /// Throws or drinks a belt slot's item (a bomb at <paramref name="target"/>). False when the slot is empty.
    /// </summary>
    public bool UseBelt(int slot, MonsterActor? target)
    {
        if (slot < 0 || slot >= BeltSize || BeltAbility(slot) is not { } ability)
            return false;
        Use(ability, target);
        return true;
    }

    /// <summary>Empties belt slots whose item has run out, and gives new belt items in the bag the first free slots.</summary>
    private void RefreshBelt()
    {
        var equipment = Hero.Equipment;
        _beltBagVersion = equipment.BagVersion;
        for (var i = 0; i < BeltSize; i++)
        {
            if (_belt[i] is { } held && equipment.CountOf(held) == 0)
                _belt[i] = null;
        }
        foreach (var item in equipment.Bag)
        {
            if (item is not { Definition.IsBeltItem: true } || _belt.Contains(item.Definition))
                continue;
            var free = Array.IndexOf(_belt, null);
            if (free < 0)
                return;
            _belt[free] = item.Definition;
        }
    }

    /// <summary>Uses up one of the item a belt ability comes from; false (and says so) when there is none left.</summary>
    private bool UseUpBeltItem(AbilityDefinition ability)
    {
        var item = _belt.FirstOrDefault(i => i?.Ability == ability.Id)
            ?? Hero.Equipment.Bag.FirstOrDefault(i => i?.Definition.Ability == ability.Id)?.Definition;
        if (item is not null && Hero.Equipment.ConsumeOne(item))
            return true;
        Say($"You have no {ability.Name.ToLowerInvariant()} left.");
        return false;
    }

    /// <summary>Something used on the hero, no target: a ward goes up, an elixir is drunk.</summary>
    private void UseOnSelf(AbilityDefinition ability, (double X, double Y)? toward = null)
    {
        switch (ability.Kind)
        {
            case AbilityKind.Hide:
                Hide(ability);
                return;
            case AbilityKind.Trap:
                SetTrap(ability);
                return;
            case AbilityKind.Tumble:
                Tumble(ability, toward);
                return;
            case AbilityKind.Aura:
                RaiseAura(ability);
                return;
            case AbilityKind.Heal:
                LayOnHands(ability);
                return;
            case AbilityKind.Summon:
                RaiseMinion(ability);
                return;
        }
        if (ability.Kind == AbilityKind.Ward)
        {
            RaiseWard(ability);
            return;
        }
        if (ability.Kind == AbilityKind.Burst)
        {
            SelfBurst(ability);
            return;
        }
        if (!UseUpBeltItem(ability))
            return;
        var c = Hero.Character;
        if (ability.Cures is { } cured && c.Persistent.DiceFor(cured) is not null)
        {
            c.Persistent.Remove(cured);
            Float(Hero, $"{cured.ToString().ToLowerInvariant()} cured", FloatingTextKind.Heal, 0.6);
        }
        if (ability.ResistsPoison > 0)
        {
            _antidoteBonus = ability.ResistsPoison;
            Hero.AntidoteUntil = Time + ability.BuffSeconds;
        }
        Sound(SoundCue.Potion, Hero);
        Say($"{ability.Name}: {ability.Description}");
    }

    /// <summary>An antidote's item bonus to a save against poison while it lasts; nothing against anything else.</summary>
    private int PoisonSaveBonus(DamageType type) => type == DamageType.Poison && Time < Hero.AntidoteUntil ? _antidoteBonus : 0;

    /// <summary>
    /// A bomb arrives: an attack roll (DEX + attack proficiency) against the target's AC. A hit deals its dice and
    /// persistent damage (doubled on a critical hit) and any condition it carries; anything but a critical failure
    /// splashes the target and everyone within <see cref="SplashRadius"/> of it.
    /// </summary>
    private void BombLands(Projectile p)
    {
        var target = p.Target;
        var ability = p.Ability;
        var degree = LostInTheDark(target)
            ? DegreeOfSuccess.Failure
            : Check.Roll(Random, Hero.Character.ThrowAttack().Total, target.Creature.ArmorClass().Total).Degree;
        Sound(SoundCue.Bomb, target, ability.DamageType.ToString().ToLowerInvariant());
        AddImpact(target.X, target.Y, ability.DamageType);
        Aggro(target);
        if (degree == DegreeOfSuccess.CriticalFailure)
        {
            Float(target, "MISS", FloatingTextKind.Miss);
            return;
        }
        var splash = ability.Splash * p.Rank;
        if (degree >= DegreeOfSuccess.Success)
        {
            var critical = degree == DegreeOfSuccess.CriticalSuccess;
            var dice = ability.DamageAt(p.Rank);
            var damage = DamageCalculator.Compute(dice.RollDiceOnly(Random), dice.Bonus, degree, ability.DamageType, target.Creature.Defenses);
            damage = DamageMonster(target, damage + DamageCalculator.ApplyDefenses(splash, ability.DamageType, target.Creature.Defenses));
            if (critical)
                HeroCrits++;
            Float(target, critical ? $"{damage}!" : damage.ToString(), critical ? FloatingTextKind.Critical : FloatingTextKind.DamageDealt);
            target.HitFlinch = 0.15;
            if (ability.PersistentDice is { } text && !target.IsDead)
            {
                var persistent = Dice.Parse(text);
                var scale = p.Rank * (critical ? 2 : 1);
                target.Creature.Persistent.Add(ability.DamageType, persistent with { Count = persistent.Count * scale, Bonus = persistent.Bonus * scale });
            }
            if (target.IsDead)
                Kill(target, critical);
            else
                OnHit(ability, target, critical);
        }
        else
        {
            var dealt = DamageMonster(target, DamageCalculator.ApplyDefenses(splash, ability.DamageType, target.Creature.Defenses));
            Float(target, $"splash {dealt}", FloatingTextKind.Graze);
            if (target.IsDead)
                Kill(target, false);
        }
        foreach (var other in _monsters.Where(m => !m.IsDead && m != target && m.DistanceTo(target) <= SplashRadius).ToList())
        {
            var dealt = DamageMonster(other, DamageCalculator.ApplyDefenses(splash, ability.DamageType, other.Creature.Defenses));
            Float(other, $"splash {dealt}", FloatingTextKind.Graze);
            Aggro(other);
            if (other.IsDead)
                Kill(other, false);
        }
        ScourCorpses(target.X, target.Y, SplashRadius, ability.DamageType);
    }
}
