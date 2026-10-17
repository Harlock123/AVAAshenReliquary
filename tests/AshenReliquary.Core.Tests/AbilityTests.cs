using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class AbilityTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("....................", 20).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") }, [2, 2], spawns, [], []);

    private static GameWorld World(Character hero, string classId, params SpawnDefinition[] spawns) =>
        new(Field(spawns), Content.Creatures, hero, 11, Content.AbilityBar(classId));

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    [Fact]
    public void Each_vertical_slice_class_has_its_bar()
    {
        var fighter = Content.AbilityBar("fighter");
        Assert.Equal(("strike", "power_attack", "sudden_charge"), (fighter["lmb"].Id, fighter["rmb"].Id, fighter["1"].Id));
        var wizard = Content.AbilityBar("wizard");
        Assert.Equal(("ignition", "force_bolt", "staff_strike"), (wizard["lmb"].Id, wizard["rmb"].Id, wizard["1"].Id));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(10, 5)]
    [InlineData(20, 10)]
    public void Spell_rank_is_half_the_level_rounded_up(int level, int rank) =>
        Assert.Equal(rank, AbilityDefinition.SpellRank(level));

    [Fact]
    public void Heightening_adds_dice_extra_weapon_dice_and_missiles()
    {
        var ignition = Content.Abilities["ignition"];
        Assert.Equal(new Dice(2, 4), ignition.DamageAt(1));
        Assert.Equal(new Dice(4, 4), ignition.DamageAt(3));
        var power = Content.Abilities["power_attack"];
        Assert.Equal((1, 2, 3), (power.ExtraDiceAt(1), power.ExtraDiceAt(10), power.ExtraDiceAt(18)));
        var force = Content.Abilities["force_bolt"];
        Assert.Equal((2, 2, 3), (force.MissilesAt(1), force.MissilesAt(2), force.MissilesAt(3)));
    }

    [Fact]
    public void Fury_builds_on_hits_and_drains_out_of_combat()
    {
        var fury = new ClassResource(TestCharacters.Fighter());
        Assert.Equal(ResourceKind.Fury, fury.Kind);
        Assert.Equal(0, fury.Current);
        fury.OnCombat();
        fury.OnCombat(critical: true);
        fury.OnCombat(taken: true);
        Assert.Equal(12 + 20 + 6, fury.Current);
        fury.Update(3.9);
        Assert.Equal(38, fury.Current);
        fury.Update(1.0);
        Assert.True(fury.Current < 38);
    }

    [Fact]
    public void Mana_starts_full_and_regenerates_with_int_and_level()
    {
        var mana = new ClassResource(TestCharacters.Wizard()); // INT +4, level 1
        Assert.Equal(ResourceKind.Mana, mana.Kind);
        Assert.Equal(20 + 5 + 16, mana.Max);
        Assert.Equal(mana.Max, mana.Current);
        Assert.True(mana.TrySpend(12));
        Assert.Equal(1 + 1.0 + 0.1, mana.RegenPerSecond, 6);
        mana.Update(2);
        Assert.Equal(mana.Max - 12 + 2 * 2.1, mana.Current, 6);
    }

    [Fact]
    public void Power_attack_without_fury_falls_back_to_the_basic_attack()
    {
        var world = World(TestCharacters.Fighter(), "fighter", new SpawnDefinition([4, 4], [new PackMember("fen_zombie")], 0));
        var zombie = world.Monsters.Single();
        world.UseSlot("rmb", zombie);
        Run(world, 1.5);
        Assert.Contains("Not enough Fury", world.LastMessage);
        Assert.Equal("strike", world.Hero.CurrentAbility?.Id ?? "strike");
    }

    [Fact]
    public void Power_attack_spends_fury_and_starts_a_flourish_lockout()
    {
        var world = World(TestCharacters.Fighter(), "fighter", new SpawnDefinition([3, 3], [new PackMember("grave_knight", Creatures.CreatureRank.Elite)], 0));
        var knight = world.Monsters.Single();
        world.Hero.Resource.OnCombat();
        world.Hero.Resource.OnCombat();
        var before = world.Hero.Resource.Current;
        world.UseSlot("rmb", knight);
        Run(world, 0.1);
        Assert.Equal("power_attack", world.Hero.CurrentAbility?.Id);
        Assert.True(world.Hero.Resource.Current <= before - 15 + 1e-6);
        Assert.True(world.CooldownRemaining(Content.Abilities["sudden_charge"]) > 2.5); // Flourish lockout
    }

    [Fact]
    public void Sudden_charge_dashes_in_and_goes_on_cooldown()
    {
        var world = World(TestCharacters.Fighter(), "fighter", new SpawnDefinition([7, 7], [new PackMember("cursed_grave")], 0));
        var grave = world.Monsters.Single();
        var start = world.Hero.DistanceTo(grave);
        world.UseSlot("1", grave);
        Run(world, 0.4);
        Assert.True(world.Hero.DistanceTo(grave) < start - 3, $"only closed {start - world.Hero.DistanceTo(grave):0.0} tiles");
        Assert.True(world.CooldownRemaining(Content.Abilities["sudden_charge"]) > 7);
    }

    [Fact]
    public void Ignition_throws_a_fire_bolt_that_lands_on_its_target()
    {
        var world = World(TestCharacters.Wizard(), "wizard", new SpawnDefinition([7, 7], [new PackMember("cursed_grave")], 0));
        var grave = world.Monsters.Single();
        world.Attack(grave);
        Run(world, 0.8);
        Assert.Contains(world.Projectiles, p => p.Ability.Id == "ignition");
        Run(world, 1.0);
        Assert.True(grave.Creature.HitPoints < grave.Creature.Stats.HitPoints
            || world.Texts.Any(t => t.Kind == FloatingTextKind.Miss));
    }

    [Fact]
    public void Force_bolt_never_misses_and_costs_mana()
    {
        var world = World(TestCharacters.Wizard(), "wizard", new SpawnDefinition([8, 8], [new PackMember("cursed_grave")], 0));
        var grave = world.Monsters.Single();
        var mana = world.Hero.Resource.Current;
        world.UseSlot("rmb", grave);
        Run(world, 0.7);
        Assert.Equal(2, world.Projectiles.Count(p => p.Ability.Id == "force_bolt"));
        Assert.True(world.Hero.Resource.Current < mana - 12 + 2.1 * 0.8); // 12 spent, ~2 Mana a second back
        Run(world, 1.5);
        Assert.True(grave.Creature.HitPoints < grave.Creature.Stats.HitPoints);
    }
}
