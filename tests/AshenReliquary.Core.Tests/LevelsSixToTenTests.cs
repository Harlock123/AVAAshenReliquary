using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>M4 step 1: each class's level-6 ability, each subclass's level-8 signature, the feats to 10 and Act 2's items.</summary>
public class LevelsSixToTenTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    /// <summary>A hero of a kit at a level, in its starting kit, monsters (of the hero's level) placed round (x, 8).</summary>
    private static GameWorld At(string kit, int level, params (double X, double Y, CreatureRank Rank)[] monsters)
    {
        var zone = new ZoneDefinition("field", "Field", level, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 8],
            monsters.Select(m => new SpawnDefinition([(int)m.X, (int)m.Y], [new PackMember("fen_zombie", m.Rank, 1)], Radius: 0, Level: level)).ToList(),
            Shrines: [], Exits: []);
        var hero = TestCharacters.ForKit(kit);
        var world = new GameWorld(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones, abilities: Content.Abilities);
        BalanceReport.Dress(hero, world.Hero.Equipment, kit);
        BalanceReport.LevelTo(world, level);
        world.Hero.Character.CannotFall = true;
        world.Hero.Resource.Gain(1000);
        world.Hero.Dedication?.Gain(1000);
        return world;
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static bool Hurt(MonsterActor m) => m.IsDead || m.Creature.HitPoints < m.Creature.Stats.HitPoints;

    [Theory]
    [InlineData("fighter bulwark", "intimidating_roar", "shield_slam")]
    [InlineData("fighter vanguard", "intimidating_roar", "whirlwind_strike")]
    [InlineData("champion lanternbearer", "retributive_strike", "beacon_of_hope")]
    [InlineData("champion oathbreaker", "retributive_strike", "dread_harvest")]
    [InlineData("champion paladin", "retributive_strike", "aura_of_justice")]
    [InlineData("rogue cutthroat", "shadowstep", "fan_of_knives")]
    [InlineData("rogue trickster", "shadowstep", "smoke_bomb")]
    [InlineData("ranger marksman", "rain_of_arrows", "deadeye")]
    [InlineData("ranger beastmaster", "rain_of_arrows", "call_of_the_pack")]
    [InlineData("wizard pyromancer", "blink", "fireball")]
    [InlineData("wizard stormcaller", "blink", "chain_lightning")]
    [InlineData("necromancer bonelord", "bone_spear", "raise_bone_knight")]
    [InlineData("necromancer plaguebringer", "bone_spear", "plague_cloud")]
    public void Each_class_gains_its_ability_at_6_and_its_subclass_signature_at_8(string kit, string six, string eight)
    {
        var at5 = At(kit, 5);
        Assert.DoesNotContain(at5.KnownAbilities, a => a.Id == six || a.Id == eight);
        var at6 = At(kit, 6);
        Assert.Contains(at6.KnownAbilities, a => a.Id == six);
        Assert.DoesNotContain(at6.KnownAbilities, a => a.Id == eight);
        var at8 = At(kit, 8);
        Assert.Contains(at8.KnownAbilities, a => a.Id == eight);
        Assert.Contains(at8.Hero.Character.FeatOptions(FeatType.Class, 8), f => f.Subclass == at8.Hero.Character.Subclass!.Id);
    }

    [Fact]
    public void The_fighter_roars_whirls_and_slams()
    {
        var world = At("fighter vanguard", 8, (4.5, 8, CreatureRank.Normal), (3.5, 9, CreatureRank.Normal), (3.5, 7, CreatureRank.Normal));
        var frightened = false;
        for (var i = 0; i < 6 && !frightened; i++)
        {
            world.Hero.Resource.Gain(100);
            world.UseAbility("intimidating_roar", null);
            frightened = world.Monsters.Any(m => m.Creature.Conditions.Has(ConditionKind.Frightened));
            Run(world, 15.2);
        }
        Assert.True(frightened, "a roar frightened one in six tries");
        var whirl = At("fighter vanguard", 8, (4.5, 8, CreatureRank.Normal), (3.5, 9, CreatureRank.Normal), (3.5, 7, CreatureRank.Normal));
        var hit = 0;
        for (var i = 0; i < 4 && hit < 2; i++)
        {
            whirl.Hero.Resource.Gain(100);
            whirl.UseAbility("whirlwind_strike", whirl.Monsters.First(m => !m.IsDead));
            Run(whirl, 6.2);
            hit = whirl.Monsters.Count(Hurt);
        }
        Assert.True(hit >= 2, "a whirlwind hits all round");

        var bulwark = At("fighter bulwark", 8, (4.5, 8, CreatureRank.Elite));
        var zombie = bulwark.Monsters.Single();
        for (var i = 0; i < 6 && !zombie.Creature.Conditions.Has(ConditionKind.Prone); i++)
        {
            bulwark.Hero.Resource.Gain(100);
            bulwark.UseAbility("shield_slam", zombie);
            Run(bulwark, 8.5);
        }
        Assert.True(Hurt(zombie));
        var bare = At("fighter bulwark", 8, (4.5, 8, CreatureRank.Elite));
        bare.Hero.Equipment.Unequip(EquipSlot.OffHand);
        bare.UseAbility("shield_slam", bare.Monsters.Single());
        Run(bare, 0.1);
        Assert.Contains("needs a shield", bare.LastMessage);
    }

    [Fact]
    public void Retributive_strike_answers_blows_and_beacon_of_hope_heals_and_cleanses()
    {
        var world = At("champion lanternbearer", 8, (4.2, 8, CreatureRank.Elite));
        var zombie = world.Monsters.Single();
        world.UseAbility("retributive_strike", null);
        Assert.Contains(world.Auras, a => a.Ability.Retaliate);
        world.Stop();
        Run(world, 10);
        Assert.True(Hurt(zombie), "the zombie's blows were answered");

        var c = world.Hero.Character;
        c.Taint.Add(80);
        c.TakeDamage(c.HitPoints / 2);
        var (hp, taint) = (c.HitPoints, c.Taint.Value);
        world.Hero.Resource.Gain(100);
        world.UseAbility("beacon_of_hope", null);
        Assert.True(c.HitPoints > hp);
        Assert.True(c.Taint.Value <= taint - 49);
    }

    [Fact]
    public void Dread_harvest_is_paid_in_taint_and_drains()
    {
        var world = At("champion oathbreaker", 8, (8, 8, CreatureRank.Normal), (8.8, 8.6, CreatureRank.Normal));
        var c = world.Hero.Character;
        c.Taint.Add(60 / c.Taint.RateMultiplier);
        c.TakeDamage(20);
        var (hp, taint) = (c.HitPoints, c.Taint.Value);
        world.UseAbility("dread_harvest", world.Monsters[0]);
        Run(world, 2);
        Assert.True(c.Taint.Value <= taint - 25, $"taint {taint} -> {c.Taint.Value}");   // 30 paid; its own hits after add a little back
        Assert.True(world.Monsters.Count(Hurt) >= 1);
    }

    [Fact]
    public void The_rogue_shadowsteps_hidden_fans_its_knives_and_vanishes_in_smoke()
    {
        var world = At("rogue cutthroat", 8, (9, 8, CreatureRank.Normal), (9.6, 8.8, CreatureRank.Normal));
        world.UseAbility("shadowstep", null, (9, 3));                       // away from the zombies, who'd notice a hidden hero beside them
        Run(world, 0.5);
        Assert.True(world.Hero.IsHidden);
        Assert.True(world.Hero.DistanceTo(3.5, 8.5) > 4);
        var fan = At("rogue cutthroat", 8, (4.5, 8, CreatureRank.Normal), (3.5, 9.5, CreatureRank.Normal));
        for (var i = 0; i < 4 && fan.Monsters.Count(Hurt) == 0; i++)
        {
            fan.Hero.Resource.Gain(100);
            fan.UseAbility("fan_of_knives", null);
            Run(fan, 6.2);
        }
        Assert.True(fan.Monsters.Count(Hurt) >= 1);

        var trickster = At("rogue trickster", 8, (4.2, 8, CreatureRank.Normal), (3.2, 9, CreatureRank.Normal));
        var dazzled = false;
        for (var i = 0; i < 6 && !dazzled; i++)
        {
            trickster.Hero.Resource.Gain(100);
            trickster.UseAbility("smoke_bomb", null);
            Assert.True(trickster.Hero.IsHidden);
            dazzled = trickster.Monsters.Any(m => m.Creature.Conditions.Has(ConditionKind.Dazzled));
            Run(trickster, 15.5);
        }
        Assert.True(dazzled, "one of the two failed its save in six bombs");
    }

    [Fact]
    public void The_beastmaster_calls_two_wolves_for_twenty_seconds()
    {
        var world = At("ranger beastmaster", 8, (12, 8, CreatureRank.Normal));
        var before = world.Allies.Count;
        world.UseAbility("call_of_the_pack", null);
        Assert.Equal(before + 2, world.Allies.Count);
        Assert.Equal(2, world.Allies.Count(a => a.IsTimed));
        Assert.NotNull(world.Companion);                                    // the wolf of their own stays
        Run(world, 21 + GameWorld.MinionCrumbleSeconds);
        Assert.Equal(0, world.Allies.Count(a => a.IsTimed));
        Assert.Equal(before, world.Allies.Count);
    }

    [Fact]
    public void Area_spells_and_chains_strike_crowds()
    {
        foreach (var (kit, id) in new[] { ("wizard pyromancer", "fireball"), ("wizard stormcaller", "chain_lightning"),
                     ("ranger marksman", "rain_of_arrows"), ("necromancer plaguebringer", "plague_cloud") })
        {
            var world = At(kit, 8, (9, 8, CreatureRank.Normal), (9.8, 8.5, CreatureRank.Normal), (9.2, 9.4, CreatureRank.Normal));
            if (kit.StartsWith("necromancer"))
                typeof(GameWorld).GetMethod("RestoreGraveCharges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, [4]);
            var hit = 0;
            for (var i = 0; i < 4 && hit < 2; i++)
            {
                world.Hero.Resource.Gain(1000);
                world.UseAbility(id, world.Monsters.First(m => !m.IsDead));
                Run(world, 2.5);
                hit = world.Monsters.Count(Hurt);
            }
            Assert.True(hit >= 2, $"{id} struck {hit}");
        }
    }

    [Fact]
    public void The_bone_knight_is_twice_as_tough_as_a_skeleton()
    {
        var world = At("necromancer bonelord", 8);
        typeof(GameWorld).GetMethod("RestoreGraveCharges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, [5]);
        world.UseAbility("raise_bone_knight", null);
        world.UseAbility("raise_bones", null);
        var knight = world.Minions.Single(m => m.Definition.Id == "raised_knight");
        var skeleton = world.Minions.Single(m => m.Definition.Id == "raised_skeleton");
        Assert.InRange(knight.Creature.Stats.HitPoints, 2 * skeleton.Creature.Stats.HitPoints - 1, 2 * skeleton.Creature.Stats.HitPoints + 1);
        Assert.Equal(2, world.GraveCharges);                               // 5 less 2, less 1 for the skeleton
    }

    [Fact]
    public void Act_two_brings_plate_heavier_weapons_its_uniques_and_a_set()
    {
        var plate = Content.Item("full_plate");
        Assert.Equal(ArmorCategory.Heavy, plate.ArmorCategory);
        Assert.Equal(7, plate.MinLevel);
        Assert.DoesNotContain(Enumerable.Range(0, 300).Select(_ => Content.Loot.Generate(5, Rarity.Common, new Rules.SeededRandom(_))), i => i.Definition.Id == "full_plate");
        var random = new Rules.SeededRandom(2);
        var uniques = Enumerable.Range(0, 400).Select(_ => Content.Loot.Generate(9, Rarity.Unique, random)).Select(i => i.Definition.Id).ToHashSet();
        Assert.Contains("siegebreaker", uniques);
        Assert.Contains("crown_of_the_unburied", uniques);
        Assert.Contains("ironhold_guard", Content.Sets.Keys);
    }
}
