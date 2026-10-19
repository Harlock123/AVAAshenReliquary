using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.World;

/// <summary>
/// Raised shields and the Shield cantrip (spec, sections 10 and 11). A ward is a circumstance bonus to AC for a few
/// seconds; while it is up, Shield Block, a reaction that triggers by itself, takes the ward's hardness off a monster's
/// blow, at most once every <see cref="ShieldBlockCooldown"/> seconds. Shields don't wear down here: Pathfinder's
/// shield hit points would need a repair loop the game doesn't have.
/// </summary>
public sealed partial class GameWorld
{
    /// <summary>The spec's internal cooldown on reactions.</summary>
    public const double ShieldBlockCooldown = 6.0;

    private double _blockReadyAt;

    /// <summary>Seconds until Shield Block can trigger again (0 when ready).</summary>
    public double ShieldBlockReadyIn => Math.Max(0, _blockReadyAt - Time);

    /// <summary>Times the hero has landed a critical hit, and been critically hit; the app shakes the screen on each.</summary>
    public int HeroCrits { get; private set; }
    public int HeroCritsTaken { get; private set; }

    private static string WardSource(AbilityDefinition ability) => ability.Name;

    /// <summary>Raises a shield, or casts the Shield cantrip: the ward replaces any ward already up.</summary>
    private void RaiseWard(AbilityDefinition ability)
    {
        if (WhyNot(ability) is { } problem)
        {
            Say(problem);
            return;
        }
        int ac, hardness;
        if (ability.NeedsShield)
        {
            if (Hero.Equipment.In(EquipSlot.OffHand) is not { Definition.IsShield: true } shield)
            {
                Say($"{ability.Name} needs a shield in your off hand.");
                return;
            }
            (ac, hardness) = (shield.Definition.AcBonus, shield.Definition.Hardness + Hero.Character.BlockHardnessBonus);
        }
        else
        {
            (ac, hardness) = (ability.WardAc, ability.HardnessAt(AbilityDefinition.SpellRank(Hero.Character.Level)));
        }
        Pay(ability);
        if (ability.Cooldown > 0)
            Hero.ReadyAt[ability.Id] = Time + ability.Cooldown;
        EndWard();
        Hero.Ward = new HeroWard(ability, ac, hardness, Time + ability.WardSeconds);
        Hero.Character.AddModifier(StatisticKind.ArmorClass, new Modifier(WardSource(ability), ac, BonusType.Circumstance));
        if (!Hero.IsSwinging && Hero.Path.Count == 0)
            Hero.Play(ability.NeedsShield ? "block" : "cast", restart: true);
        Sound(SoundCue.Ward, Hero, ability.NeedsShield ? "shield" : "spell");
        Float(Hero, $"+{ac} AC", FloatingTextKind.Info, 0.6);
    }

    /// <summary>The ward runs out.</summary>
    private void UpdateWard()
    {
        if (Hero.Ward is { } ward && Time >= ward.EndsAt)
            EndWard();
    }

    private void EndWard()
    {
        if (Hero.Ward is not { } ward)
            return;
        Hero.Character.RemoveModifiers(WardSource(ward.Ability));
        Hero.Ward = null;
    }

    /// <summary>
    /// Shield Block against a blow of <paramref name="damage"/>: with a ward up and the reaction ready, the ward's
    /// hardness comes off it. The Shield cantrip's ward breaks when it blocks and can't be cast again for a while. A
    /// Bulwark strikes straight back at <paramref name="attacker"/> when it is in reach. Returns the damage left to take.
    /// </summary>
    private int ShieldBlock(int damage, MonsterActor? attacker = null)
    {
        if (Hero.Ward is not { } ward || Time < _blockReadyAt || ward.Hardness <= 0)
            return damage;
        _blockReadyAt = Time + ShieldBlockCooldown;
        var blocked = Math.Min(damage, ward.Hardness);
        Float(Hero, $"blocked {blocked}", FloatingTextKind.Info, 0.4);
        Sound(SoundCue.Block, Hero);
        if (ward.Ability.BlockLockout > 0)
        {
            Hero.ReadyAt[ward.Ability.Id] = Time + ward.Ability.BlockLockout;
            EndWard();
        }
        if (Hero.Character.BlockCounter && ward.Ability.NeedsShield && attacker is { IsDead: false }
            && Hero.DistanceTo(attacker) <= MeleeReach + Hero.Character.ReachBonus + attacker.Radius + 0.3)
        {
            Float(Hero, "counter", FloatingTextKind.Info, 0.8);
            HeroStrike(attacker, BasicAbility);
        }
        return damage - blocked;
    }
}
