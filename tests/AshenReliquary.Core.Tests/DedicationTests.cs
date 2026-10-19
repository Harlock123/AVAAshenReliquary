using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>M3 step 6: multiclass dedications, a class feat open to every other class that grants its ability.</summary>
public class DedicationTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 8], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, ZoneDefinition? zone = null, params SpawnDefinition[] spawns)
    {
        var world = new GameWorld(zone ?? Field(spawns), Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones,
            abilities: Content.Abilities);
        foreach (var id in BalanceReport.Kits[hero.Class.Id])
            world.Hero.Equipment.Equip(new Item(Content.Item(id), Rarity.Common, 1));
        return world;
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    /// <summary>Takes level 2 with the dedication as its class feat.</summary>
    private static void Dedicate(GameWorld world, string feat)
    {
        var hero = world.Hero.Character;
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: feat, SkillFeat: hero.FeatOptions(FeatType.Skill, 2).First().Id));
        Run(world, 0.05);
    }

    [Fact]
    public void Every_class_may_take_another_class_s_dedication_but_only_one()
    {
        var fighter = TestCharacters.Fighter();
        fighter.LevelUp();
        var offered = fighter.FeatOptions(FeatType.Class, 2).Where(f => f.Dedication is not null).Select(f => f.Dedication).ToList();
        Assert.Equal(["wizard", "rogue", "ranger", "champion", "necromancer"], offered);   // never its own
        var world = World(TestCharacters.Fighter());
        Dedicate(world, "wizard_dedication");
        var hero = world.Hero.Character;
        Assert.Equal("wizard", hero.DedicationClass?.Id);
        world.GainXp(2000, world.Hero);
        Assert.DoesNotContain(hero.FeatOptions(FeatType.Class, 4), f => f.Dedication is not null);
        Assert.Contains(hero.FeatOptions(FeatType.Class, 4), f => f.Id == "knockdown");
    }

    [Fact]
    public void A_fighter_with_wizard_dedication_casts_force_bolt_from_a_small_mana_pool()
    {
        var world = World(TestCharacters.Fighter(), null, new SpawnDefinition([7, 8], [new PackMember("fen_zombie", CreatureRank.Elite, 1)], Radius: 0));
        Dedicate(world, "wizard_dedication");
        var pool = world.Hero.Dedication!;
        Assert.Equal(ResourceKind.Mana, pool.Kind);
        Assert.Equal(ResourceKind.Fury, world.Hero.Resource.Kind);
        Assert.Equal(15, pool.Max, 3);                                // (20 + 10 + 4 × INT 0) ÷ 2
        Assert.Equal(pool.Max, pool.Current, 3);
        var slot = world.SlotOf("force_bolt")!;
        var zombie = world.Monsters.Single();
        world.UseSlot(slot, zombie);
        Run(world, 0.2);
        Assert.Equal(pool.Max - 12, pool.Current, 0);                 // paid in Mana, not Fury
        Assert.Equal(0, world.Hero.Resource.Current, 0);
        Run(world, 2);
        Assert.True(zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints);
        Assert.True(pool.Current > pool.Max - 12, "the pool refills");
    }

    [Fact]
    public void A_wizard_with_fighter_dedication_builds_fury_for_power_attack_from_its_own_blows()
    {
        var world = World(TestCharacters.Wizard());
        Dedicate(world, "fighter_dedication");
        var fury = world.Hero.Dedication!;
        Assert.Equal(ResourceKind.Fury, fury.Kind);
        Assert.Equal(0, fury.Current);
        world.Hero.Resource.OnCombat();                               // the hero's own pool passes every blow on
        Assert.Equal(ClassResource.FuryPerHit, fury.Current);
        Assert.Equal(fury, world.ResourceFor(Content.Abilities["power_attack"]));
        Assert.Equal(world.Hero.Resource, world.ResourceFor(Content.Abilities["force_bolt"]));
    }

    [Fact]
    public void Dedications_that_cost_nothing_need_no_pool_and_the_ranger_s_brings_its_d8()
    {
        var rogue = World(TestCharacters.Fighter());
        Dedicate(rogue, "rogue_dedication");
        Assert.Null(rogue.Hero.Dedication);
        Assert.NotNull(rogue.SlotOf("tumble"));

        var hunter = World(TestCharacters.Wizard());
        Dedicate(hunter, "ranger_dedication");
        Assert.Null(hunter.Hero.Dedication);
        Assert.Equal(1, hunter.Hero.Character.PreyDice);
        Assert.NotNull(hunter.SlotOf("hunt_prey"));
    }

    [Fact]
    public void A_necromancer_s_dedication_gathers_grave_charges_for_a_single_skeleton()
    {
        var world = World(TestCharacters.Champion(), null, new SpawnDefinition([6, 8], [new PackMember("fen_zombie", CreatureRank.Minion, 1)], Radius: 0));
        Assert.Equal(0, world.MaxGraveCharges);
        Dedicate(world, "necromancer_dedication");
        Assert.Equal(5, world.MaxGraveCharges);
        Assert.Equal(1, world.MinionCap);
        Assert.Equal(ResourceKind.Mana, world.Hero.Dedication!.Kind);
        world.Slay(world.Monsters.Single());
        Assert.Equal(1, world.GraveCharges);
        var slot = world.SlotOf("raise_bones")!;
        world.UseSlot(slot, null);
        Run(world, 1.1);
        world.UseSlot(slot, null);                                    // a second: the first crumbles
        Assert.Equal(1, world.Minions.Count(m => !m.IsDead));
    }

    [Fact]
    public void A_dedication_and_its_pool_are_saved_and_a_respec_lets_it_go()
    {
        var world = World(TestCharacters.Fighter(), Content.Zone("fort_gallowmere"));
        Dedicate(world, "wizard_dedication");
        world.Hero.Dedication!.TrySpend(5);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "multi", 3, null).ToJson()).Restore(Content);
        Assert.Equal("wizard", loaded.Hero.Character.DedicationClass?.Id);
        Assert.Equal(world.Hero.Dedication.Current, loaded.Hero.Dedication!.Current, 3);
        Assert.NotNull(loaded.SlotOf("force_bolt"));
        loaded.Hero.Character.Respec();                                // as the Lantern altar does, then the bar is refreshed
        typeof(GameWorld).GetMethod("RefreshBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(loaded, []);
        Assert.Null(loaded.Hero.Character.Dedication);
        Assert.Null(loaded.Hero.Dedication);
        Assert.Null(loaded.SlotOf("force_bolt"));
    }
}
