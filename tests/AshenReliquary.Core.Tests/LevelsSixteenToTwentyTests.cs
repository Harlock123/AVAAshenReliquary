using System.Reflection;
using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 8: each class's level-16 capstone (the forms), each subclass's level-18 signature, the feats to 20, spell
/// ranks 9 and 10, and Act 4's items.
/// </summary>
public class LevelsSixteenToTwentyTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static GameWorld At(string kit, int level, string creature = "fen_zombie", params (double X, double Y, CreatureRank Rank)[] monsters)
    {
        var zone = new ZoneDefinition("field", "Field", level, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 8],
            monsters.Select(m => new SpawnDefinition([(int)m.X, (int)m.Y], [new PackMember(creature, m.Rank, 1)], Radius: 0, Level: level)).ToList(),
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

    [Theory]
    [InlineData("fighter vanguard", "unstoppable", "titans_wrath")]
    [InlineData("fighter bulwark", "unstoppable", "bastion")]
    [InlineData("champion lanternbearer", "avatar_of_the_lantern", "sunburst")]
    [InlineData("champion oathbreaker", "avatar_of_the_lantern", "ashen_cataclysm")]
    [InlineData("champion paladin", "avatar_of_the_lantern", "hammer_of_the_just")]
    [InlineData("rogue cutthroat", "death_mark", "thousand_cuts")]
    [InlineData("rogue trickster", "death_mark", "infernal_device")]
    [InlineData("ranger marksman", "hunters_apex", "arrow_storm")]
    [InlineData("ranger beastmaster", "hunters_apex", "alphas_call")]
    [InlineData("wizard pyromancer", "disintegrate", "meteor_swarm")]
    [InlineData("wizard stormcaller", "disintegrate", "tempest")]
    [InlineData("necromancer bonelord", "lich_form", "army_of_the_dead")]
    [InlineData("necromancer plaguebringer", "lich_form", "plague_tide")]
    [InlineData("gunslinger pistolero", "smoke_and_thunder", "lead_storm")]
    [InlineData("gunslinger sniper", "smoke_and_thunder", "perfect_shot")]
    public void Each_class_gains_its_capstone_at_16_and_its_subclass_signature_at_18(string kit, string sixteen, string eighteen)
    {
        var at15 = At(kit, 15);
        Assert.DoesNotContain(at15.KnownAbilities, a => a.Id == sixteen || a.Id == eighteen);
        var at16 = At(kit, 16);
        Assert.Contains(at16.KnownAbilities, a => a.Id == sixteen);
        Assert.DoesNotContain(at16.KnownAbilities, a => a.Id == eighteen);
        Assert.DoesNotContain("16", at16.Bar.Keys);                              // its home is no bar slot: a free one, or the list
        var at18 = At(kit, 18);
        Assert.Contains(at18.KnownAbilities, a => a.Id == eighteen);
        var c = at18.Hero.Character;
        Assert.Contains(c.FeatOptions(FeatType.Class, 18), f => f.Level == 18 && f.Subclass == c.Subclass!.Id);
        Assert.Equal(2, c.FeatOptions(FeatType.Class, 16).Count(f => f.Level == 16 && f.Class == c.Class.Id));
        Assert.Single(c.FeatOptions(FeatType.Class, 20), f => f.Level == 20 && f.Class == c.Class.Id);
    }

    [Theory]
    [InlineData("fighter vanguard", "titans_wrath")]
    [InlineData("champion lanternbearer", "sunburst")]
    [InlineData("champion lanternbearer", "avatar_of_the_lantern")]
    [InlineData("champion oathbreaker", "ashen_cataclysm")]
    [InlineData("champion paladin", "hammer_of_the_just")]
    [InlineData("rogue cutthroat", "thousand_cuts")]
    [InlineData("ranger marksman", "arrow_storm")]
    [InlineData("wizard pyromancer", "disintegrate")]
    [InlineData("wizard pyromancer", "meteor_swarm")]
    [InlineData("wizard stormcaller", "tempest")]
    [InlineData("necromancer plaguebringer", "plague_tide")]
    [InlineData("gunslinger pistolero", "lead_storm")]
    [InlineData("gunslinger sniper", "perfect_shot")]
    public void The_new_attacks_land(string kit, string ability)
    {
        var hurt = false;
        for (var attempt = 0; attempt < 6 && !hurt; attempt++)
        {
            var world = At(kit, 18, "fen_zombie", (4.5, 8, CreatureRank.Normal), (5.2, 8.8, CreatureRank.Normal), (5.2, 7.2, CreatureRank.Normal));
            if (world.Hero.Character.Class.Id == "necromancer")
                typeof(GameWorld).GetProperty(nameof(GameWorld.GraveCharges))!.SetValue(world, 5);
            if (world.Hero.Character.Class.Id == "champion")
                world.Hero.Character.Taint.Add(80);
            var target = world.Monsters.MinBy(m => m.DistanceTo(world.Hero))!;
            Assert.True(world.UseAbility(ability, target), $"{ability} could not be used");
            Run(world, 2.5);
            hurt = world.Monsters.Any(m => m.IsDead || m.Creature.HitPoints < m.Creature.Stats.HitPoints);
        }
        Assert.True(hurt, $"{ability} never hurt anything");
    }

    [Fact]
    public void Unstoppable_quickens_the_blows_and_shakes_off_every_hold()
    {
        var world = At("fighter vanguard", 16, "fen_zombie", (8.5, 8, CreatureRank.Normal));
        Assert.Equal(1.0, world.FormAttackRate);
        world.Hero.Character.Conditions.Apply(ConditionKind.Stunned, 1, 2);
        world.Hero.Character.Conditions.Apply(ConditionKind.Immobilized, 1, 5);
        Run(world, 2.1);
        Assert.True(world.UseAbility("unstoppable", null));
        Run(world, 0.1);
        Assert.Equal(1.5, world.FormAttackRate, 3);
        Assert.True(world.Steadfast);
        Assert.False(world.Hero.Character.Conditions.Has(ConditionKind.Immobilized));
        world.Hero.Character.Conditions.Apply(ConditionKind.Prone, 1, 3);
        Run(world, 0.1);
        Assert.False(world.Hero.Character.Conditions.Has(ConditionKind.Prone));
        Run(world, 10);
        Assert.False(world.Steadfast);
        Assert.Equal(1.0, world.FormAttackRate);
    }

    [Fact]
    public void Avatar_of_the_lantern_mends_and_guards_while_it_burns()
    {
        var world = At("champion lanternbearer", 16);
        var c = world.Hero.Character;
        var ac = c.ArmorClass().Total;
        c.TakeDamage(c.MaxHitPoints / 2);
        var before = c.HitPoints;
        Assert.True(world.UseAbility("avatar_of_the_lantern", null));
        Run(world, 0.1);
        Assert.Equal(ac + 2, c.ArmorClass().Total);
        Run(world, 4.2);                                                         // three pulses: at once, 2 s, 4 s
        Assert.True(c.HitPoints >= before + 3 * (int)Math.Round(c.MaxHitPoints * 0.04) - 2, $"{before} -> {c.HitPoints}");
    }

    [Fact]
    public void Death_mark_makes_the_next_hit_a_critical_hit_and_is_spent()
    {
        var crits = 0;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var world = At("rogue cutthroat", 16, "fen_zombie", (4.4, 8, CreatureRank.Elite));
            var zombie = world.Monsters.Single();
            Assert.True(world.UseAbility("death_mark", null));
            Run(world, 0.1);
            Assert.Contains(world.Auras, a => a.Ability.Id == "death_mark");
            var before = world.HeroCrits;
            world.Attack(zombie);
            for (var t = 0.0; t < 6 && world.Auras.Any(a => a.Ability.Id == "death_mark"); t += 1 / 30.0)
                world.Update(1 / 30.0);
            if (world.Auras.All(a => a.Ability.Id != "death_mark") && world.HeroCrits > before)
                crits++;
        }
        Assert.True(crits >= 6, $"{crits} of 8 marks ended in a critical hit");
    }

    [Fact]
    public void Hunters_apex_makes_every_monster_near_the_hunters_prey()
    {
        var world = At("ranger marksman", 16, "fen_zombie", (8.5, 8, CreatureRank.Normal), (9.5, 9, CreatureRank.Normal), (25.5, 8, CreatureRank.Normal));
        var near = world.Monsters.Where(m => m.X < 20).ToList();
        var far = world.Monsters.Single(m => m.X > 20);
        Assert.All(world.Monsters, m => Assert.False(world.IsPrey(m)));
        Assert.True(world.UseAbility("hunters_apex", null));
        Run(world, 0.1);
        Assert.All(near, m => Assert.True(world.IsPrey(m)));
        Assert.False(world.IsPrey(far));
    }

    [Fact]
    public void Lich_form_doubles_what_the_dead_deal()
    {
        var world = At("necromancer bonelord", 16);
        Assert.Equal(1.0, world.AllyDamageScale);
        Assert.True(world.UseAbility("lich_form", null));
        Run(world, 0.1);
        Assert.Equal(2.0, world.AllyDamageScale, 3);
        Run(world, 21);
        Assert.Equal(1.0, world.AllyDamageScale);
    }

    [Fact]
    public void Smoke_and_thunder_keeps_the_guns_loaded()
    {
        var world = At("gunslinger pistolero", 16, "fen_zombie", (7.5, 8, CreatureRank.Elite));
        var zombie = world.Monsters.Single();
        Assert.True(world.UseAbility("smoke_and_thunder", null));
        Run(world, 0.1);
        var loaded = world.Hero.Loaded;
        Assert.True(loaded > 0);
        world.Attack(zombie);
        Run(world, 3);
        Assert.Equal(loaded, world.Hero.Loaded);
        Assert.True(zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints);
    }

    [Fact]
    public void Steadfast_holds_against_the_choir_s_song()
    {
        var zone = Content.Zones["bell_tower"];
        var hero = TestCharacters.ForKit("fighter vanguard");
        var world = new GameWorld(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones, abilities: Content.Abilities);
        BalanceReport.Dress(hero, world.Hero.Equipment, "fighter vanguard");
        BalanceReport.LevelTo(world, 16);
        world.Hero.Resource.Gain(1000);
        Assert.True(world.UseAbility("unstoppable", null));
        world.Update(0.1);
        var agnete = world.Monsters.Single(m => m.Definition.Id == "choir_agnete");
        var sing = typeof(GameWorld).GetMethod("Sing", BindingFlags.NonPublic | BindingFlags.Instance)!;
        for (var i = 0; i < 10; i++)
            sing.Invoke(world, [agnete, agnete.Definition.Boss!.Phases[^1]]);
        Assert.False(world.Enthralled);
    }

    [Fact]
    public void Feats_at_16_18_and_20_sharpen_the_kit()
    {
        var world = At("fighter vanguard", 20);
        var c = world.Hero.Character;
        c.Respec();
        foreach (var at in c.PendingLevels.ToList())
        {
            var gains = Progression.LevelProgression.GainsAt(at);
            string? Pick(Progression.LevelGain gain, FeatType type, string? prefer = null) =>
                (gains & gain) == 0 ? null : c.FeatOptions(type, at).Where(f => f.Dedication is null).Select(f => f.Id).OrderBy(id => id == prefer ? 0 : 1).First();
            world.ChooseLevel(at, new LevelChoice(
                Boosts: (gains & Progression.LevelGain.AttributeBoosts) != 0
                    ? new[] { c.KeyAttribute, Rules.AttributeKind.Constitution, Rules.AttributeKind.Dexterity, Rules.AttributeKind.Wisdom, Rules.AttributeKind.Strength }.Distinct().Take(4).ToArray() : null,
                ClassFeat: Pick(Progression.LevelGain.ClassFeat, FeatType.Class, at switch { 16 => "juggernaut", 18 => "earthbreaker", 20 => "living_legend", _ => null }),
                SkillFeat: Pick(Progression.LevelGain.SkillFeat, FeatType.Skill),
                GeneralFeat: Pick(Progression.LevelGain.GeneralFeat, FeatType.General),
                AncestryFeat: Pick(Progression.LevelGain.AncestryFeat, FeatType.Ancestry),
                SkillIncrease: (gains & Progression.LevelGain.SkillIncrease) != 0 ? Skills.Attribute.Keys.Order().First(sk => c.CanIncrease(sk, at)) : null));
        }
        var plain = Content.Abilities["unstoppable"];
        var unstoppable = world.KnownAbilities.Single(a => a.Id == "unstoppable");
        Assert.Equal(plain.Cost - 10, unstoppable.Cost);
        Assert.Equal(plain.BuffSeconds + 4, unstoppable.BuffSeconds);
        Assert.Equal(Content.Abilities["titans_wrath"].Radius + 1, world.KnownAbilities.Single(a => a.Id == "titans_wrath").Radius);
        Assert.Contains(c.Feats, f => f.Id == "living_legend");
    }

    [Fact]
    public void Spells_reach_rank_ten_at_level_19()
    {
        Assert.Equal(8, AbilityDefinition.SpellRank(16));
        Assert.Equal(9, AbilityDefinition.SpellRank(17));
        Assert.Equal(10, AbilityDefinition.SpellRank(19));
        Assert.Equal(10, AbilityDefinition.SpellRank(20));
        var disintegrate = Content.Abilities["disintegrate"];
        Assert.Equal(11, disintegrate.DamageAt(AbilityDefinition.SpellRank(20)).Count);
    }

    [Fact]
    public void Act_four_s_uniques_and_set_reach_level_twenty()
    {
        var uniques = Content.Items.Values.Where(i => i.Unique && i.MinLevel >= 16).Select(i => i.Id).Order().ToList();
        Assert.Equal(["ashurs_ember", "drakescale_mail", "emberfall", "inquisitors_verdict", "pyrebrand_staff"], uniques);
        Assert.Equal(3, Content.Items.Values.Count(i => i.Set == "last_lantern"));
        var random = new Rules.SeededRandom(5);
        var rolled = Enumerable.Range(0, 400).Select(_ => Content.Loot.Generate(20, Items.Rarity.Unique, random)).Select(i => i.Definition.Id).ToHashSet();
        Assert.Contains("ashurs_ember", rolled);
    }
}
