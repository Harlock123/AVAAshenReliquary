using System.Reflection;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 5: each class's level-11 ability, each subclass's level-13 signature, the feats to 14, Act 3's items, and the
/// Mirrored affix (with stacked affixes).
/// </summary>
public class LevelsElevenToFifteenTests
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
    [InlineData("fighter vanguard", "sundering_blow", "earthsplitter")]
    [InlineData("fighter bulwark", "sundering_blow", "shield_rush")]
    [InlineData("champion lanternbearer", "divine_wrath", "dawnbreak")]
    [InlineData("champion oathbreaker", "divine_wrath", "ruinous_blow")]
    [InlineData("rogue cutthroat", "crippling_strike", "assassinate")]
    [InlineData("rogue trickster", "crippling_strike", "caltrops")]
    [InlineData("ranger marksman", "pinning_shot", "killshot")]
    [InlineData("ranger beastmaster", "pinning_shot", "hunt_as_one")]
    [InlineData("wizard pyromancer", "cone_of_cold", "meteor")]
    [InlineData("wizard stormcaller", "cone_of_cold", "ball_lightning")]
    [InlineData("necromancer bonelord", "grasp_of_the_grave", "bone_colossus")]
    [InlineData("necromancer plaguebringer", "grasp_of_the_grave", "blight")]
    public void Each_class_gains_its_ability_at_11_and_its_subclass_signature_at_13(string kit, string eleven, string thirteen)
    {
        var at10 = At(kit, 10);
        Assert.DoesNotContain(at10.KnownAbilities, a => a.Id == eleven || a.Id == thirteen);
        var at11 = At(kit, 11);
        Assert.Contains(at11.KnownAbilities, a => a.Id == eleven);
        Assert.DoesNotContain(at11.KnownAbilities, a => a.Id == thirteen);
        Assert.DoesNotContain("11", at11.Bar.Keys);                              // its home is no bar slot: a free one, or the list
        var at13 = At(kit, 13);
        Assert.Contains(at13.KnownAbilities, a => a.Id == thirteen);
        Assert.Contains(at13.Hero.Character.FeatOptions(FeatType.Class, 14), f => f.Subclass == at13.Hero.Character.Subclass!.Id);
        Assert.Equal(2, at13.Hero.Character.FeatOptions(FeatType.Class, 12).Count(f => f.Level == 12 && f.Class == at13.Hero.Character.Class.Id));
    }

    [Theory]
    [InlineData("fighter vanguard", "sundering_blow")]
    [InlineData("fighter vanguard", "earthsplitter")]
    [InlineData("champion lanternbearer", "divine_wrath")]
    [InlineData("champion lanternbearer", "dawnbreak")]
    [InlineData("champion oathbreaker", "ruinous_blow")]
    [InlineData("rogue cutthroat", "crippling_strike")]
    [InlineData("rogue cutthroat", "assassinate")]
    [InlineData("ranger marksman", "pinning_shot")]
    [InlineData("ranger marksman", "killshot")]
    [InlineData("wizard pyromancer", "cone_of_cold")]
    [InlineData("wizard pyromancer", "meteor")]
    [InlineData("wizard stormcaller", "ball_lightning")]
    [InlineData("necromancer plaguebringer", "grasp_of_the_grave")]
    [InlineData("necromancer plaguebringer", "blight")]
    public void The_new_attacks_land(string kit, string ability)
    {
        var hurt = false;
        for (var attempt = 0; attempt < 6 && !hurt; attempt++)
        {
            var world = At(kit, 13, "fen_zombie", (4.5, 8, CreatureRank.Normal), (5.2, 8.8, CreatureRank.Normal), (5.2, 7.2, CreatureRank.Normal));
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
    public void Shield_rush_charges_a_monster_and_knocks_it_down()
    {
        var landed = false;
        for (var attempt = 0; attempt < 6 && !landed; attempt++)
        {
            var world = At("fighter bulwark", 13, "fen_zombie", (8.5, 8, CreatureRank.Normal));
            var zombie = world.Monsters.Single();
            Assert.True(world.UseAbility("shield_rush", zombie));
            Run(world, 1.5);
            landed = zombie.Creature.Conditions.Has(ConditionKind.Prone) || zombie.IsDead;
        }
        Assert.True(landed);
    }

    [Fact]
    public void The_bonelord_raises_a_bone_colossus_for_a_while_and_hunt_as_one_sends_the_wolves()
    {
        var world = At("necromancer bonelord", 13, "fen_zombie", (8.5, 8, CreatureRank.Normal));
        typeof(GameWorld).GetProperty(nameof(GameWorld.GraveCharges))!.SetValue(world, 5);
        Assert.True(world.UseAbility("bone_colossus", null));
        Run(world, 1.5);
        var colossus = world.Allies.Single(a => a.Definition.Id == "bone_colossus");
        Assert.True(colossus.IsTimed);
        Run(world, 25);
        Assert.True(colossus.IsDead || !world.Allies.Contains(colossus));

        var ranger = At("ranger beastmaster", 13, "fen_zombie", (8.5, 8, CreatureRank.Champion));
        Run(ranger, 1);
        Assert.True(ranger.UseAbility("hunt_as_one", ranger.Monsters.Single()));
        Assert.All(ranger.Allies.Where(a => !a.IsDead), a => Assert.Equal(ranger.Monsters.Single(), a.Target));
    }

    [Fact]
    public void Caltrops_slow_what_crosses_them()
    {
        var slowed = false;
        for (var attempt = 0; attempt < 6 && !slowed; attempt++)
        {
            var world = At("rogue trickster", 13, "fen_zombie", (9.5, 8, CreatureRank.Normal));
            Assert.True(world.UseAbility("caltrops", null));
            Run(world, 0.6);
            world.MoveTo(1.0, 8.5);                                                  // set them and step back: it crosses them to follow
            Run(world, 8);
            slowed = world.Monsters.Any(m => m.Creature.Conditions.Has(ConditionKind.Slowed)) || world.Monsters.Any(m => m.IsDead);
        }
        Assert.True(slowed);
    }

    [Fact]
    public void Feats_at_12_and_14_sharpen_the_new_abilities()
    {
        var world = At("fighter vanguard", 14);
        var plain = world.KnownAbilities.Single(a => a.Id == "sundering_blow").Cost;
        var c = world.Hero.Character;
        c.Respec();
        BalanceReport.ChoosePending(world);
        // Take Shattering Blows at 12 by hand, over whatever the bot's picks were.
        c.Respec();
        foreach (var at in c.PendingLevels.ToList())
        {
            var gains = Progression.LevelProgression.GainsAt(at);
            string? Pick(Progression.LevelGain gain, FeatType type, string? prefer = null) =>
                (gains & gain) == 0 ? null : c.FeatOptions(type, at).Where(f => f.Dedication is null).Select(f => f.Id).OrderBy(id => id == prefer ? 0 : 1).First();
            world.ChooseLevel(at, new LevelChoice(
                Boosts: (gains & Progression.LevelGain.AttributeBoosts) != 0
                    ? new[] { c.KeyAttribute, Rules.AttributeKind.Constitution, Rules.AttributeKind.Dexterity, Rules.AttributeKind.Wisdom, Rules.AttributeKind.Strength }.Distinct().Take(4).ToArray() : null,
                ClassFeat: Pick(Progression.LevelGain.ClassFeat, FeatType.Class, at == 12 ? "shattering_blows" : at == 14 ? "tectonic" : null),
                SkillFeat: Pick(Progression.LevelGain.SkillFeat, FeatType.Skill),
                GeneralFeat: Pick(Progression.LevelGain.GeneralFeat, FeatType.General),
                AncestryFeat: Pick(Progression.LevelGain.AncestryFeat, FeatType.Ancestry),
                SkillIncrease: (gains & Progression.LevelGain.SkillIncrease) != 0 ? Skills.Attribute.Keys.Order().First(sk => c.CanIncrease(sk, at)) : null));
        }
        Assert.Equal(plain - 10, world.KnownAbilities.Single(a => a.Id == "sundering_blow").Cost);
        Assert.Equal(5, world.KnownAbilities.Single(a => a.Id == "earthsplitter").Radius);
    }

    // ----- Mirrored, and stacked affixes ---------------------------------------------

    private static int Hurt(GameWorld world, MonsterActor m, int amount) =>
        (int)typeof(GameWorld).GetMethod("DamageMonster", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, [m, amount])!;

    [Fact]
    public void A_mirrored_champion_splits_into_two_images_at_half_health_and_they_shatter_when_it_falls()
    {
        var zone = new ZoneDefinition("field", "Field", 11, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 8],
            [new SpawnDefinition([12, 8], [new PackMember("legionary", CreatureRank.Champion, 1, ["mirrored", "hasted"])], Radius: 0)],
            Shrines: [], Exits: []);
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 5, Content.AbilityBar("fighter"), Content.Loot);
        var champion = world.Monsters.Single();
        // Stacked affixes: both name it and both work.
        Assert.Equal("Mirrored Hasted Legionary Champion", champion.Name);
        Assert.True(champion.Has(MonsterAffix.Mirrored) && champion.Has(MonsterAffix.Hasted));
        Assert.Equal(Content.Creatures["legionary"].AttacksPerSecond * GameWorld.HasteAttackRate, champion.Creature.AttacksPerSecond, 3);

        Hurt(world, champion, champion.Creature.Stats.HitPoints / 4);
        Assert.Single(world.Monsters);
        Hurt(world, champion, champion.Creature.Stats.HitPoints / 3);
        var images = world.Monsters.Where(m => m.IsMirrorImage).ToList();
        Assert.Equal(GameWorld.MirrorImages, images.Count);
        Assert.All(images, i => Assert.Equal(CreatureRank.Minion, i.Creature.Stats.Rank));
        Hurt(world, champion, 1);
        Assert.Equal(GameWorld.MirrorImages, world.Monsters.Count(m => m.IsMirrorImage));   // it splits once
        var xp = world.Experience.Xp;
        world.Slay(images[0]);
        Assert.Equal(xp, world.Experience.Xp);                                              // an image is worth nothing
        world.Slay(champion);
        Assert.All(images, i => Assert.True(i.IsDead));
    }
}
