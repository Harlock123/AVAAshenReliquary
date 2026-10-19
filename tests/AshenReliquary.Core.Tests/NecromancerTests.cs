using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M3 step 5: the Necromancer (Grave Charges, Raise Bones, Grave Bolt, Siphon Life, Bone Rush), skeleton minions on the
/// ally framework, the Bonelord's Bone Armor and the Plaguebringer's Corpse Explosion.
/// </summary>
public class NecromancerTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 8], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, params SpawnDefinition[] spawns) => World(hero, Field(spawns));

    private static GameWorld World(Character hero, ZoneDefinition zone)
    {
        var world = new GameWorld(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones,
            abilities: Content.Abilities);
        world.Hero.Equipment.Equip(new Item(Content.Item("staff"), Rarity.Common, 1));
        return world;
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static SpawnDefinition At(double x, double y, string creature = "fen_zombie", CreatureRank rank = CreatureRank.Normal) =>
        new([(int)x, (int)y], [new PackMember(creature, rank, 1)], Radius: 0);

    [Fact]
    public void The_necromancer_casts_with_mana_and_gathers_grave_charges_from_the_dead_nearby()
    {
        var necromancer = TestCharacters.Necromancer();
        Assert.Equal(["bonelord", "plaguebringer"], necromancer.SubclassOptions.Select(s => s.Id));
        var world = World(necromancer, At(6, 8), At(25, 8));
        Assert.Equal(ResourceKind.Mana, world.Hero.Resource.Kind);
        Assert.Equal(["grave_bolt", "raise_bones", "siphon_life"], world.Bar.Values.Select(a => a.Id));
        Assert.Equal(5, world.MaxGraveCharges);
        Assert.Equal(0, World(TestCharacters.Fighter()).MaxGraveCharges);
        var (near, far) = (world.Monsters.MinBy(m => m.X)!, world.Monsters.MaxBy(m => m.X)!);
        world.Slay(near);
        Assert.Equal(1, world.GraveCharges);
        world.Slay(far);                                              // 20 tiles off: too far to gather
        Assert.Equal(1, world.GraveCharges);
    }

    [Fact]
    public void Raise_bones_takes_a_charge_or_mana_and_the_oldest_crumbles_at_the_cap()
    {
        var world = World(TestCharacters.Necromancer(), At(6, 8));
        var mana = world.Hero.Resource;
        var full = mana.Current;
        world.UseSlot("rmb", null);
        var first = Assert.Single(world.Minions);
        Assert.Equal("raised_skeleton", first.Definition.Id);
        Assert.Equal(11, first.Creature.Stats.HitPoints);              // 6 + 5 at level 1
        Assert.Equal(full - 12, mana.Current, 3);
        world.Slay(world.Monsters.Single());
        Run(world, 1.1);
        var spent = mana.Current;
        world.UseSlot("rmb", null);                                    // a Grave Charge pays now, not Mana
        Assert.Equal(0, world.GraveCharges);
        Assert.InRange(mana.Current, spent - 0.01, spent + 1);
        Assert.Equal(2, world.Minions.Count(m => !m.IsDead));
        Run(world, 1.1);
        world.UseSlot("rmb", null);                                    // a third at a cap of two: the first crumbles
        Assert.True(first.IsDead);
        Assert.Equal(2, world.Minions.Count(m => !m.IsDead));
        Run(world, GameWorld.MinionCrumbleSeconds + 0.1);
        Assert.Equal(2, world.Minions.Count());
        Assert.Null(world.Companion);                                  // skeletons are no companion
    }

    [Fact]
    public void Skeletons_fight_for_the_necromancer_and_crumble_when_it_falls()
    {
        var world = World(TestCharacters.Necromancer(), At(11, 8));
        world.Hero.Character.CannotFall = true;
        world.UseSlot("rmb", null);
        Run(world, 1.1);
        world.UseSlot("rmb", null);
        Assert.Equal(2, world.Minions.Count(m => !m.IsDead));
        var zombie = world.Monsters.Single();
        world.Attack(zombie);
        for (var t = 0.0; t < 15 && !zombie.IsDead; t += 1 / 30.0)
            world.Update(1 / 30.0);
        Assert.True(zombie.IsDead);
        Assert.Equal(1, world.GraveCharges);                         // its corpse's charge
        typeof(GameWorld).GetMethod("HeroDied", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, []);
        Assert.All(world.Minions, m => Assert.True(m.IsDead));
        Assert.Equal(0, world.GraveCharges);
    }

    [Fact]
    public void Siphon_life_heals_half_its_damage()
    {
        var world = World(TestCharacters.Necromancer(), At(13, 8, rank: CreatureRank.Elite));
        var c = world.Hero.Character;
        c.TakeDamage(10);
        var zombie = world.Monsters.Single();
        var healed = false;
        for (var i = 0; i < 6 && !healed; i++)
        {
            world.Hero.Resource.Gain(100);
            var (hp, life) = (c.HitPoints, zombie.Creature.HitPoints);
            world.UseSlot(world.SlotOf("siphon_life")!, zombie);
            Run(world, 1.0);
            var dealt = life - zombie.Creature.HitPoints;
            if (dealt > 0)
            {
                Assert.Equal(hp + (int)Math.Round(dealt * 0.5), c.HitPoints);
                healed = true;
            }
        }
        Assert.True(healed, "a siphon landed");
    }

    [Fact]
    public void Bone_rush_needs_a_charge_and_sends_every_skeleton()
    {
        var hero = TestCharacters.Necromancer();
        var world = World(hero, At(12, 8, rank: CreatureRank.Elite));
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "grave_reach", SkillFeat: hero.FeatOptions(FeatType.Skill, 2).First().Id));
        Run(world, 0.1);
        Assert.Equal("bone_rush", world.Bar["4"].Id);
        world.UseSlot("rmb", null);
        Run(world, 1.1);
        world.UseSlot("rmb", null);
        var zombie = world.Monsters.Single();
        world.UseSlot("4", zombie);
        Assert.Contains("needs a Grave Charge", world.LastMessage);
        typeof(GameWorld).GetMethod("RestoreGraveCharges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, [2]);
        world.UseSlot("4", zombie);
        Assert.Equal(1, world.GraveCharges);
        Assert.All(world.Minions, m => Assert.Equal(zombie, m.Target));
    }

    [Fact]
    public void The_bonelord_holds_more_bones_and_armours_itself_with_a_charge()
    {
        var hero = TestCharacters.Necromancer("bonelord");
        var world = World(hero);
        Assert.Equal(3, world.MinionCap);
        Assert.Equal("bone_armor", world.Bar["2"].Id);
        world.UseSlot("2", null);
        Assert.Null(world.Hero.Ward);
        typeof(GameWorld).GetMethod("RestoreGraveCharges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, [1]);
        var ac = hero.ArmorClass().Total;
        world.UseSlot("2", null);
        Assert.NotNull(world.Hero.Ward);
        Assert.Equal(ac + 1, hero.ArmorClass().Total);
        Assert.Equal(0, world.GraveCharges);
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "legion", SkillFeat: hero.FeatOptions(FeatType.Skill, 2).First().Id));
        world.UseSlot("rmb", null);
        Assert.Equal(24, world.Minions.Single().Creature.Stats.HitPoints); // (6 + 10) × 1.5
    }

    [Fact]
    public void The_plaguebringer_gathers_two_charges_a_corpse_and_blows_up_a_crowd()
    {
        var hero = TestCharacters.Necromancer("plaguebringer");
        var world = World(hero, At(9, 8), At(10, 8), At(9, 9), At(5, 3));
        world.Slay(world.Monsters.Single(m => m.Y < 5));
        Assert.Equal(2, world.GraveCharges);
        Assert.Equal("corpse_explosion", world.Bar["2"].Id);
        var crowd = world.Monsters.Where(m => !m.IsDead).ToList();
        world.UseSlot("2", crowd[0]);
        Run(world, 2);
        Assert.True(crowd.Count(m => m.IsDead || m.Creature.HitPoints < m.Creature.Stats.HitPoints) >= 2);
    }

    [Fact]
    public void Grave_charges_are_saved_and_skeletons_are_not()
    {
        var world = World(TestCharacters.Necromancer(), Content.Zone("fort_gallowmere"));
        world.UseSlot("rmb", null);
        typeof(GameWorld).GetMethod("RestoreGraveCharges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, [3]);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "bones", 3, null).ToJson()).Restore(Content);
        Assert.Equal(3, loaded.GraveCharges);
        Assert.Empty(loaded.Minions);
    }
}
