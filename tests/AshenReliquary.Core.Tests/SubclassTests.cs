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
/// M3 step 1: subclasses for the Fighter and the Wizard (Vanguard, Bulwark, Pyromancer, Stormcaller), the feats that
/// change abilities, bursts and basic saves, and saves from before subclasses.
/// </summary>
public class SubclassTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("................", 12).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 6], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, ZoneDefinition? zone = null, int seed = 5) =>
        new(zone ?? Field(), Content.Creatures, hero, seed, Content.AbilityBar(hero.Class.Id), Content.Loot, abilities: Content.Abilities);

    private static Item Make(string id) => new(Content.Item(id), Rarity.Common, 1);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static SpawnDefinition Zombie(double x, double y, CreatureRank rank = CreatureRank.Champion) =>
        new([(int)x, (int)y], [new PackMember("fen_zombie", rank, 1)], Radius: 0);

    private static Character Hero(string kit) => TestCharacters.ForKit(kit);

    // ----- Choosing a subclass -------------------------------------------------------

    [Fact]
    public void Each_class_has_two_subclasses_from_the_spec()
    {
        Assert.Equal(["vanguard", "bulwark"], TestCharacters.Fighter().SubclassOptions.Select(s => s.Id));
        Assert.Equal(["pyromancer", "stormcaller"], TestCharacters.Wizard().SubclassOptions.Select(s => s.Id));
        Assert.All(Content.Feats.Values.Where(f => f.Type == FeatType.Subclass),
            s => Assert.Contains(s.Name, Content.Class(s.Class!).Subclasses));
    }

    [Fact]
    public void A_subclass_chosen_at_creation_puts_its_ability_on_the_bar_and_changes_the_class_abilities()
    {
        var hero = CharacterBuilder.Create(Content, TestCharacters.FighterChoices with { Subclass = "vanguard" });
        Assert.Equal("vanguard", hero.Subclass?.Id);
        Assert.False(hero.NeedsSubclass);
        var world = World(hero);
        Assert.Equal("earthshaker", world.Bar["5"].Id);   // after the class's 1-4, Finishing Blow's slot kept for level 2
        var cleave = world.Bar["2"];
        Assert.Equal(4, cleave.Targets);                   // Cleave reaches one more enemy
        Assert.Equal(15, cleave.Cost);                     // for 5 less Fury
        Assert.Equal(20, Content.Abilities["cleave"].Cost); // the definition itself is untouched
    }

    [Fact]
    public void A_character_from_before_subclasses_chooses_one_later_and_keeps_it_through_a_respec()
    {
        var hero = TestCharacters.Wizard();
        Assert.True(hero.NeedsSubclass);
        Assert.Throws<RulesException>(() => hero.ChooseSubclass("vanguard"));
        hero.ChooseSubclass("stormcaller");
        Assert.Equal("stormcaller", hero.Choices?.Subclass);
        Assert.Throws<RulesException>(() => hero.ChooseSubclass("pyromancer"));
        TestCharacters.AtLevel(hero, 2);
        hero.Respec();
        Assert.Equal("stormcaller", hero.Subclass?.Id);
        Assert.Contains("thunderclap", hero.GrantedAbilities);
    }

    [Fact]
    public void Subclass_feats_are_only_offered_to_their_subclass()
    {
        var vanguard = Hero("fighter vanguard");
        var bulwark = Hero("fighter bulwark");
        var battering = Content.Feats["battering_blows"];
        Assert.True(vanguard.CanTake(battering, 2));
        Assert.False(bulwark.CanTake(battering, 2));
        Assert.False(TestCharacters.Fighter().CanTake(battering, 2)); // no subclass yet
        Assert.Contains(bulwark.FeatOptions(FeatType.Class, 2), f => f.Id == "steadfast_guard");
    }

    [Fact]
    public void A_feat_that_modifies_an_ability_changes_it_where_it_sits()
    {
        var world = World(Hero("fighter vanguard"), Field(Zombie(6, 6)));
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "battering_blows", SkillFeat: "titan_wrestler"));
        Assert.Equal(1.5, world.Bar["rmb"].Knockback);   // Power Attack now knocks back
        Assert.Equal("power_attack", world.Bar["rmb"].Id);
    }

    [Fact]
    public void Saves_keep_the_subclass_and_an_old_save_still_asks_for_one()
    {
        var road = Content.Zone("gallows_road");
        var world = World(Hero("wizard pyromancer"), road);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.Equal("pyromancer", loaded.Hero.Character.Subclass?.Id);
        Assert.Contains(loaded.Bar.Values, a => a.Id == "burning_hands");

        var old = World(TestCharacters.Wizard(), road);
        var reloaded = SaveGame.FromJson(SaveGame.Capture(old, "s", 3, null).ToJson()).Restore(Content);
        Assert.True(reloaded.Hero.Character.NeedsSubclass);
        reloaded.ChooseSubclass("pyromancer");
        Assert.Contains(reloaded.Bar.Values, a => a.Id == "burning_hands");
        var again = SaveGame.FromJson(SaveGame.Capture(reloaded, "s", 3, null).ToJson()).Restore(Content);
        Assert.Equal("pyromancer", again.Hero.Character.Subclass?.Id);
    }

    // ----- Saves and bursts -----------------------------------------------------------

    [Theory]
    [InlineData(DegreeOfSuccess.CriticalSuccess, 0)]
    [InlineData(DegreeOfSuccess.Success, 5)]
    [InlineData(DegreeOfSuccess.Failure, 10)]
    [InlineData(DegreeOfSuccess.CriticalFailure, 20)]
    public void A_basic_save_sets_the_damage_taken(DegreeOfSuccess save, int taken) =>
        Assert.Equal(taken, DamageCalculator.BasicSave(10, save, DamageType.Fire, null));

    [Fact]
    public void A_monster_saves_at_its_save_dc_less_ten_and_two_less_in_a_pack()
    {
        var normal = new Creature("z", CreatureStats.For(3, CreatureRank.Normal), new Dice(1, 6), DamageType.Bludgeoning);
        var herald = new Creature("h", CreatureStats.For(3, CreatureRank.Herald), new Dice(1, 6), DamageType.Bludgeoning);
        Assert.Equal(normal.Stats.SaveDc - 12, normal.Save(StatisticKind.Reflex).Total);
        Assert.Equal(herald.Stats.SaveDc - 10, herald.Save(StatisticKind.Will).Total);
        normal.Conditions.Apply(ConditionKind.Frightened, 2);
        Assert.Equal(normal.Stats.SaveDc - 14, normal.Save(StatisticKind.Fortitude).Total);
    }

    [Fact]
    public void Thunderclap_strikes_every_enemy_close_by_and_none_further_off()
    {
        var hurtNear = false;
        for (var seed = 1; seed <= 10; seed++)
        {
            var world = World(Hero("wizard stormcaller"), Field(Zombie(5, 6), Zombie(4, 8), Zombie(11, 6)), seed);
            (world.Hero.X, world.Hero.Y) = (4.5, 6.5);
            var far = world.Monsters.Single(m => m.X > 10);
            var farHp = far.Creature.HitPoints;
            var near = world.Monsters.Where(m => m.X < 10).ToList();
            var nearHp = near.Sum(m => m.Creature.HitPoints);
            var slot = world.Bar.Single(kv => kv.Value.Id == "thunderclap").Key;
            world.UseSlot(slot, null);
            Assert.Single(world.Bursts);
            Assert.Equal(farHp, far.Creature.HitPoints);
            hurtNear |= near.Sum(m => m.Creature.HitPoints) < nearHp;
            Assert.True(world.Hero.Resource.Current < world.Hero.Resource.Max); // 14 Mana spent
        }
        Assert.True(hurtNear, "Thunderclap never hurt the zombies beside the hero");
    }

    [Fact]
    public void Burning_hands_is_a_cone_toward_its_target()
    {
        var behindHurt = false;
        var aheadHurt = false;
        for (var seed = 1; seed <= 10; seed++)
        {
            var world = World(Hero("wizard pyromancer"), Field(Zombie(7, 6), Zombie(7, 7), Zombie(4, 6)), seed);
            (world.Hero.X, world.Hero.Y) = (5.5, 6.5);
            var target = world.Monsters.Single(m => m.X > 7 && m.Y < 7);
            var ahead = world.Monsters.Single(m => m.X > 7 && m.Y > 7);
            var behind = world.Monsters.Single(m => m.X < 5);
            int aheadHp = ahead.Creature.HitPoints, behindHp = behind.Creature.HitPoints;
            world.UseSlot(world.Bar.Single(kv => kv.Value.Id == "burning_hands").Key, target);
            Run(world, 1.5);
            behindHurt |= behind.Creature.HitPoints < behindHp;
            aheadHurt |= ahead.Creature.HitPoints < aheadHp;
        }
        Assert.True(aheadHurt, "the cone never caught the second zombie in front");
        Assert.False(behindHurt, "the cone burned the zombie behind the hero");
    }

    [Fact]
    public void Earthshaker_needs_a_two_handed_weapon()
    {
        var world = World(Hero("fighter vanguard"), Field(Zombie(5, 6)));
        world.Hero.Equipment.Equip(Make("longsword"));
        world.Hero.Resource.Restore(100);
        var slot = world.Bar.Single(kv => kv.Value.Id == "earthshaker").Key;
        world.UseSlot(slot, null);
        Assert.Contains("needs a two-handed weapon", world.LastMessage);
        Assert.Empty(world.Bursts);

        world.Hero.Equipment.Equip(Make("greatsword"));
        world.UseSlot(slot, null);
        Assert.Single(world.Bursts);
        Assert.Equal(100 - 20 + ClassResource.FuryPerHit, world.Hero.Resource.Current, 1); // spent 20, then the zombie it caught fed Fury
    }

    [Fact]
    public void A_failed_save_against_earthshaker_knocks_the_enemy_back()
    {
        var pushed = false;
        for (var seed = 1; seed <= 15 && !pushed; seed++)
        {
            var world = World(Hero("fighter vanguard"), Field(Zombie(5, 6, CreatureRank.Elite)), seed);
            world.Hero.Equipment.Equip(Make("greatsword"));
            (world.Hero.X, world.Hero.Y) = (4.0, 6.5);
            var zombie = world.Monsters.Single();
            var before = zombie.DistanceTo(world.Hero);
            world.Hero.Resource.Restore(100);
            world.UseSlot(world.Bar.Single(kv => kv.Value.Id == "earthshaker").Key, null);
            pushed = zombie.DistanceTo(world.Hero) > before + 1;
        }
        Assert.True(pushed);
    }

    [Fact]
    public void Challenge_frightens_those_that_fail_and_turns_the_whole_pack()
    {
        var frightened = false;
        for (var seed = 1; seed <= 10; seed++)
        {
            var world = World(Hero("fighter bulwark"), Field(Zombie(6, 6), Zombie(7, 8)), seed);
            world.Hero.Resource.Restore(100);
            world.UseSlot(world.Bar.Single(kv => kv.Value.Id == "challenge").Key, null);
            Assert.All(world.Monsters, m => Assert.NotEqual(MonsterState.Idle, m.State));
            frightened |= world.Monsters.Any(m => m.Creature.Conditions.Has(ConditionKind.Frightened));
        }
        Assert.True(frightened);
    }

    // ----- The Bulwark's shield and the Pyromancer's splash ------------------------------

    [Fact]
    public void A_bulwarks_shield_block_takes_two_more_and_strikes_back()
    {
        var world = World(Hero("fighter bulwark"), Field(Zombie(4, 6)));
        world.Hero.Equipment.Equip(Make("steel_shield"));
        var blocks = new List<FloatingText>();
        var counters = 0;
        for (var t = 0; t < 30 * 20; t++)
        {
            if (world.Hero.Ward is null)
                world.UseSlot("3", null);
            world.Hero.Character.Heal(999);
            world.Update(1 / 30.0);
            blocks.AddRange(world.Texts.Where(x => x.Text.StartsWith("blocked") && !blocks.Contains(x)));
            counters = Math.Max(counters, world.Texts.Count(x => x.Text == "counter"));
        }
        Assert.Equal(7, world.Hero.Ward!.Hardness);   // a steel shield's 5, and the Bulwark's 2
        Assert.NotEmpty(blocks);
        Assert.True(counters > 0, "no counter-strike after a Shield Block");
    }

    [Fact]
    public void A_pyromancers_ignition_splashes_the_enemies_round_its_target()
    {
        var splashed = false;
        for (var seed = 1; seed <= 10 && !splashed; seed++)
        {
            var world = World(Hero("wizard pyromancer"), Field(Zombie(9, 6), Zombie(9, 7)), seed);
            Assert.Equal(1, world.Bar["lmb"].Splash);
            var target = world.Monsters.First();
            world.Attack(target);
            Run(world, 2);
            splashed = world.Texts.Any(x => x.Text.StartsWith("splash"));
        }
        Assert.True(splashed);
    }
}
