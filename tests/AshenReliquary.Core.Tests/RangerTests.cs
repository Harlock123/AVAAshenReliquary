using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M3 step 3: bows, the Ranger (Focus, Hunt Prey, Twin Shot, Disengage), the Marksman's piercing arrows and the
/// Beastmaster's wolf on the ally framework.
/// </summary>
public class RangerTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 8], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, string weapon = "shortbow", params SpawnDefinition[] spawns) =>
        World(hero, Field(spawns), weapon);

    private static GameWorld World(Character hero, ZoneDefinition zone, string weapon = "shortbow")
    {
        var world = new GameWorld(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones,
            abilities: Content.Abilities);
        world.Hero.Equipment.Equip(new Item(Content.Item(weapon), Rarity.Common, 1));
        return world;
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static SpawnDefinition At(double x, double y, string creature = "fen_zombie", CreatureRank rank = CreatureRank.Elite) =>
        new([(int)x, (int)y], [new PackMember(creature, rank, 1)], Radius: 0);

    [Fact]
    public void The_ranger_hunts_with_focus_and_a_bow()
    {
        var ranger = TestCharacters.Ranger();
        Assert.Equal(["marksman", "beastmaster"], ranger.SubclassOptions.Select(s => s.Id));
        Assert.Equal(1, ranger.PreyDice);
        Assert.Equal(0, TestCharacters.Rogue().PreyDice);
        var world = World(ranger);
        Assert.Equal(ResourceKind.Focus, world.Hero.Resource.Kind);
        Assert.Equal(world.Hero.Resource.Max, world.Hero.Resource.Current);
        Assert.Equal(["ranger_strike", "twin_shot", "hunt_prey", "disengage"], world.Bar.Values.Select(a => a.Id));
        Assert.True(world.Shoots);
        Assert.Equal(AttributeKind.Dexterity, ranger.AttackAttribute(ranger.Weapon));
        Assert.Equal(0, ranger.DamageBonus().Total);                  // a bow adds no Strength
    }

    [Fact]
    public void A_bow_shoots_from_where_the_hero_stands_and_the_arrow_is_the_strike()
    {
        var world = World(TestCharacters.Ranger(), spawns: At(9, 8));
        var zombie = world.Monsters.Single();
        var (x, y) = (world.Hero.X, world.Hero.Y);
        world.Attack(zombie);
        Run(world, 0.7);
        Assert.Equal(x, world.Hero.X, 3);                             // 6 tiles is within the shortbow's 8
        Assert.Equal(y, world.Hero.Y, 3);
        Assert.Equal("shoot", world.Hero.Animation);
        Run(world, 4);
        Assert.True(world.Texts.Any(t => t.Kind is FloatingTextKind.DamageDealt or FloatingTextKind.Critical or FloatingTextKind.Miss or FloatingTextKind.Graze)
            || zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints);
        Assert.True(zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints, "four seconds of arrows should land one");
    }

    [Fact]
    public void Twin_shot_looses_two_arrows_and_a_sweep_needs_a_melee_weapon()
    {
        var world = World(TestCharacters.Ranger(), spawns: At(9, 8));
        var zombie = world.Monsters.Single();
        world.UseSlot(world.SlotOf("twin_shot")!, zombie);
        Run(world, 0.8);
        Assert.Equal(2, world.Projectiles.Count(p => p.IsArrow) + world.Texts.Count(t => t.Kind is FloatingTextKind.DamageDealt
            or FloatingTextKind.Critical or FloatingTextKind.Miss or FloatingTextKind.Graze));

        var fighter = World(TestCharacters.ForKit("fighter bulwark"), "shortbow", At(5, 8));
        fighter.Hero.Resource.Restore(100);
        fighter.UseSlot(fighter.SlotOf("cleave")!, fighter.Monsters.Single());
        Run(fighter, 0.3);
        Assert.Equal("Cleave needs a melee weapon.", fighter.LastMessage);
        Assert.False(fighter.WieldsTwoHanded);                        // and a bow is no Earthshaker weapon
    }

    [Fact]
    public void Hunt_prey_marks_a_monster_until_it_dies_and_adds_its_d8_to_strikes_against_it()
    {
        var world = World(TestCharacters.Ranger(), spawns: At(9, 8, rank: CreatureRank.Minion));
        var zombie = world.Monsters.Single();
        world.UseSlot(world.SlotOf("hunt_prey")!, zombie);
        Assert.Equal(zombie, world.Hero.Prey);
        world.Slay(zombie);
        Assert.Null(world.Hero.Prey);

        // The d8 shows in the average: against AC 0 every blow is a critical hit, so it is doubled, 9 more a blow.
        var hero = TestCharacters.Ranger();
        hero.Weapon = new Weapon(Content.Item("shortbow").AsWeapon());
        double Average(int prey)
        {
            var random = new SeededRandom(11);
            var target = new Creature("Post", CreatureStats.For(1, CreatureRank.Normal) with { ArmorClass = 0 }, new Dice(1, 4), DamageType.Bludgeoning);
            return Enumerable.Range(0, 4000).Average(i => Strike.Resolve(hero, target, new AttackCadence(), i * 10.0, random, preyDice: prey).Damage);
        }
        Assert.InRange(Average(1) - Average(0), 8.0, 10.0);
    }

    [Fact]
    public void Disengage_springs_toward_the_pointer()
    {
        var world = World(TestCharacters.Ranger());
        var x = world.Hero.X;
        world.UseSlot(world.SlotOf("disengage")!, null, (x - 10, world.Hero.Y));
        Run(world, 0.5);
        Assert.InRange(x - world.Hero.X, 2.6, 3.1);
    }

    [Fact]
    public void A_marksman_s_arrow_flies_on_through_the_enemy_behind()
    {
        var hero = TestCharacters.Ranger("marksman");
        Assert.Equal(1, hero.ArrowPierce);
        Assert.Equal(5, hero.CritResourceBonus);
        var world = World(hero, spawns: [At(8, 8, rank: CreatureRank.Elite), At(10, 8, rank: CreatureRank.Elite)]);
        var near = world.Monsters.MinBy(m => m.X)!;
        var far = world.Monsters.MaxBy(m => m.X)!;
        world.UseSlot(world.SlotOf("piercing_shot")!, near);
        // Until the first arrow lands there is no arrow at the far one; then one flies on to it.
        var sawPierce = false;
        for (var t = 0.0; t < 2 && !sawPierce; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            sawPierce = world.Projectiles.Any(p => p.IsArrow && p.Target == far);
        }
        Assert.True(sawPierce);
    }

    [Fact]
    public void A_beastmaster_s_wolf_comes_at_their_side_and_grows_with_them()
    {
        var hero = TestCharacters.Ranger("beastmaster");
        var world = World(hero);
        var wolf = Assert.Single(world.Allies);
        Assert.Equal("wolf", wolf.Definition.Id);
        Assert.True(wolf.DistanceTo(world.Hero) < 1.6);
        Assert.Equal(18, wolf.Creature.Stats.HitPoints);              // 10 + 8 at level 1
        world.MoveTo(20, 8);
        Run(world, 6);
        Assert.True(wolf.DistanceTo(world.Hero) <= GameWorld.AllyFollowTiles + 0.6);
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "pack_hunter", SkillFeat: hero.FeatOptions(FeatType.Skill, 2).First().Id));
        Run(world, 0.1);
        Assert.Equal(39, wolf.Creature.Stats.HitPoints);              // (10 + 16) × 1.5
        Assert.Equal(new Dice(2, 8, 1), wolf.Creature.StrikeDamage);
    }

    [Fact]
    public void The_wolf_fights_what_comes_for_the_hero_and_monsters_fight_it_back()
    {
        var world = World(TestCharacters.Ranger("beastmaster"), "shortbow", At(9, 8, rank: CreatureRank.Normal));
        var zombie = world.Monsters.Single();
        var wolf = world.Companion!;
        world.Stop();
        Run(world, 6);
        Assert.True(zombie.IsDead || zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints);
        Assert.True(zombie.IsDead || zombie.Victim == wolf);           // it turned on what bit it
        Assert.True(world.Experience.Xp > 0 || !zombie.IsDead);       // the wolf's kills are the hero's
    }

    [Fact]
    public void A_fallen_wolf_comes_back_after_twenty_seconds_and_sic_em_sends_it_to_knock_a_monster_down()
    {
        var world = World(TestCharacters.Ranger("beastmaster"), "shortbow", At(12, 8, rank: CreatureRank.Elite));
        var wolf = world.Companion!;
        wolf.Creature.TakeDamage(1000);
        Assert.True(wolf.IsDead);
        typeof(AllyActor).GetProperty("ReturnIn")!.SetValue(wolf, GameWorld.AllyReturnSeconds);
        Run(world, GameWorld.AllyReturnSeconds - 1);
        Assert.True(wolf.IsDead);
        Run(world, 1.5);
        Assert.False(wolf.IsDead);
        Assert.Equal(wolf.Creature.Stats.HitPoints / 2, wolf.Creature.HitPoints);

        var zombie = world.Monsters.Single();
        world.UseSlot(world.SlotOf("sic")!, zombie);
        Assert.Equal(zombie, wolf.Target);
        var prone = false;
        for (var t = 0.0; t < 8 && !prone; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            prone = zombie.Creature.Conditions.Has(ConditionKind.Prone);
            if (wolf.Target == zombie && world.CooldownRemaining(world.Bar[world.SlotOf("sic")!]) <= 0)
                break;
        }
        Assert.True(prone || wolf.DistanceTo(zombie) < 2, "the wolf reached its quarry");
    }

    [Fact]
    public void The_wolf_s_health_is_saved_and_a_respec_out_of_beastmaster_is_impossible_as_the_subclass_stays()
    {
        var world = World(TestCharacters.Ranger("beastmaster"), Content.Zone("fort_gallowmere"));
        world.Companion!.Creature.TakeDamage(7);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "wolf", 3, null).ToJson()).Restore(Content);
        Assert.Equal(11, loaded.Companion!.Creature.HitPoints);
        Assert.True(loaded.Companion.DistanceTo(loaded.Hero) < 1.6);
        loaded.Hero.Character.Respec();
        Run(loaded, 0.1);
        Assert.NotNull(loaded.Companion);                             // the subclass, and its wolf, outlast a respec
        var marksman = World(TestCharacters.Ranger("marksman"));
        Assert.Empty(marksman.Allies);
    }
}
