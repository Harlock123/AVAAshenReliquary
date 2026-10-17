using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;

namespace AshenReliquary.Core.Tests;

public class StrikeTests
{
    private static readonly WeaponDefinition Longsword = new("longsword", "Longsword", 8, DamageType.Slashing, 1.2);

    // Level-1 normal creature: AC 16, attack +6, 5 HP, 40% damage.
    private static Creature Zombie(DamageProfile? defenses = null) =>
        new("Fen zombie", CreatureStats.For(1, CreatureRank.Normal), new Dice(1, 6, 2), DamageType.Bludgeoning, defenses);

    private static Characters.Character Fighter(Weapon weapon)
    {
        var c = TestCharacters.Fighter();
        c.Weapon = weapon;
        return c;
    }

    [Fact]
    public void A_hit_rolls_weapon_dice_plus_strength()
    {
        var fighter = Fighter(new Weapon(Longsword, Potency: 1)); // attack +10
        var result = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(6, 5));
        Assert.Equal(16, result.Check.Total);
        Assert.Equal(DegreeOfSuccess.Success, result.Degree);
        Assert.Equal(5 + 4, result.Damage);
    }

    [Fact]
    public void A_critical_doubles_the_damage()
    {
        var fighter = Fighter(new Weapon(Longsword, Potency: 1));
        var result = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(16, 5));
        Assert.Equal("CRIT", result.Label);
        Assert.Equal((5 + 4) * 2, result.Damage);
    }

    [Fact]
    public void Striking_runes_add_dice()
    {
        var fighter = Fighter(new Weapon(Longsword, Potency: 1, Striking: 2));
        var result = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(10, 3, 4, 5));
        Assert.Equal(3 + 4 + 5 + 4, result.Damage);
    }

    [Fact]
    public void Deadly_adds_dice_after_doubling_on_a_critical()
    {
        var rapier = new WeaponDefinition("rapier", "Rapier", 6, DamageType.Piercing, 1.4,
            Traits: new HashSet<WeaponTrait> { WeaponTrait.Deadly, WeaponTrait.Finesse }, DeadlyDie: 8);
        var fighter = Fighter(new Weapon(rapier, Striking: 2)); // 3d6, deadly 2d8
        var result = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(20, 1, 2, 3, 7, 8));
        Assert.Equal((1 + 2 + 3 + 4) * 2 + 7 + 8, result.Damage);
    }

    [Fact]
    public void Fatal_raises_the_die_size_and_adds_a_die_on_a_critical()
    {
        var pick = new WeaponDefinition("pick", "Pick", 6, DamageType.Piercing, 1.0,
            Traits: new HashSet<WeaponTrait> { WeaponTrait.Fatal }, FatalDie: 10);
        var fighter = Fighter(new Weapon(pick));
        var result = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(20, 10, 9));
        Assert.Equal((10 + 4) * 2 + 9, result.Damage);
    }

    [Fact]
    public void A_failure_misses_unless_the_ability_grazes()
    {
        var fighter = Fighter(new Weapon(Longsword)); // attack +9
        var miss = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(5));
        Assert.Equal("MISS", miss.Label);
        Assert.Equal(0, miss.Damage);

        var graze = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(5, 6), GrazeEffect.Half);
        Assert.Equal("GRAZE", graze.Label);
        Assert.Equal((6 + 4) / 2, graze.Damage);
    }

    [Fact]
    public void A_critical_failure_staggers_the_attacker()
    {
        var result = Strike.Resolve(Fighter(new Weapon(Longsword)), Zombie(), new AttackCadence(), 0, new ScriptedRandom(1));
        Assert.Equal("MISS", result.Label);
        Assert.True(result.AttackerStaggered);
    }

    [Fact]
    public void Only_attacks_inside_a_swing_take_the_cadence_penalty()
    {
        var fighter = Fighter(new Weapon(Longsword));
        var cadence = new AttackCadence();
        Strike.Resolve(fighter, Zombie(), cadence, 0, new ScriptedRandom(1));
        Assert.Equal(0, Strike.Resolve(fighter, Zombie(), cadence, 0.85, new ScriptedRandom(1)).MultipleAttackPenalty);
        var second = Strike.Resolve(fighter, Zombie(), cadence, 1.25, new ScriptedRandom(10, 4)); // mid-swing
        Assert.Equal(-2, second.MultipleAttackPenalty);
        Assert.Equal(10 + 9 - 2, second.Check.Total);
    }

    [Fact]
    public void Off_guard_targets_are_easier_to_hit()
    {
        var zombie = Zombie();
        zombie.Conditions.Apply(ConditionKind.OffGuard, durationSeconds: 2);
        var result = Strike.Resolve(Fighter(new Weapon(Longsword)), zombie, new AttackCadence(), 0, new ScriptedRandom(5, 2));
        Assert.Equal(14, result.Check.Dc);
        Assert.Equal(DegreeOfSuccess.Success, result.Degree);
    }

    [Fact]
    public void Resistance_and_weakness_apply_to_strikes()
    {
        var zombie = Zombie(new DamageProfile().Weak(DamageType.Slashing, 3));
        var result = Strike.Resolve(Fighter(new Weapon(Longsword)), zombie, new AttackCadence(), 0, new ScriptedRandom(10, 2));
        Assert.Equal(2 + 4 + 3, result.Damage);
    }

    [Fact]
    public void Dazzled_attackers_must_pass_a_dc_11_flat_check_first()
    {
        var fighter = Fighter(new Weapon(Longsword));
        fighter.Conditions.Apply(ConditionKind.Dazzled, durationSeconds: 3);
        var lost = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(10));
        Assert.True(lost.LostToImpairedVision);
        Assert.Equal("MISS", lost.Label);
        Assert.Equal(0, lost.Damage);

        var through = Strike.Resolve(fighter, Zombie(), new AttackCadence(), 0, new ScriptedRandom(11, 10, 2));
        Assert.False(through.LostToImpairedVision);
        Assert.Equal(DegreeOfSuccess.Success, through.Degree);
    }

    [Fact]
    public void Creatures_strike_back_against_the_characters_ac()
    {
        var fighter = TestCharacters.Fighter(); // AC 15
        var result = Strike.Resolve(Zombie(), fighter, new AttackCadence(), 0, new ScriptedRandom(9, 3));
        Assert.Equal(15, result.Check.Total);
        Assert.Equal(2, result.Damage); // (1d6 3 + 2) x 0.4 for a Normal
    }
}
