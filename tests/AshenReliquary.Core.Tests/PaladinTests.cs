using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// The Champion's third subclass, the Paladin (the oath of justice): its reaction always ready, Blade of Justice, Aura of
/// Justice guarding the allies within, Final Judgment's sweep.
/// </summary>
public class PaladinTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static GameWorld World(string subclass, int level, params (double X, double Y, CreatureRank Rank)[] monsters)
    {
        var zone = new ZoneDefinition("field", "Field", level, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 8],
            monsters.Select(m => new SpawnDefinition([(int)m.X, (int)m.Y], [new PackMember("fen_zombie", m.Rank, 1)], Radius: 0, Level: level)).ToList(),
            Shrines: [], Exits: []);
        var hero = TestCharacters.Champion(subclass);
        var world = new GameWorld(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones, abilities: Content.Abilities);
        BalanceReport.Dress(hero, world.Hero.Equipment, $"champion {subclass}");
        BalanceReport.LevelTo(world, level);
        world.Hero.Character.CannotFall = true;
        return world;
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static bool Hurt(MonsterActor m) => m.IsDead || m.Creature.HitPoints < m.Creature.Stats.HitPoints;

    [Fact]
    public void The_paladin_starts_with_blade_of_justice_a_cheaper_smite_and_its_reaction()
    {
        var world = World("paladin", 1);
        var c = world.Hero.Character;
        Assert.True(c.Retribution);
        Assert.False(TestCharacters.Champion("lanternbearer").Retribution);
        Assert.Equal(["champion_strike", "smite", "lay_on_hands", "shield_of_faith", "blade_of_justice"], world.Bar.Values.Select(a => a.Id));
        Assert.Equal(5, world.Bar["rmb"].Cost);
        var blade = world.Bar[world.SlotOf("blade_of_justice")!];
        Assert.Equal(2, blade.ExtraDice);
        Assert.Equal("Enfeebled", blade.OnHit);
    }

    [Fact]
    public void The_paladin_strikes_back_at_a_blow_without_an_aura_and_a_lanternbearer_does_not()
    {
        foreach (var (subclass, answers) in new[] { ("paladin", true), ("lanternbearer", false) })
        {
            var world = World(subclass, 3, (4.2, 8, CreatureRank.Elite));
            var zombie = world.Monsters.Single();
            Assert.Empty(world.Auras);
            world.Stop();
            Run(world, 12);
            Assert.True(Hurt(zombie) == answers, $"{subclass}: the zombie's blows {(answers ? "went unanswered" : "were answered")}");
        }
    }

    [Fact]
    public void Blade_of_justice_hits_harder_and_enfeebles()
    {
        var enfeebled = false;
        for (var attempt = 0; attempt < 8 && !enfeebled; attempt++)
        {
            var world = World("paladin", 3, (4.2, 8, CreatureRank.Elite));
            world.Hero.Resource.Gain(100);
            var zombie = world.Monsters.Single();
            Assert.True(world.UseAbility("blade_of_justice", zombie));
            Run(world, 1.5);
            enfeebled = zombie.Creature.Conditions.Value(Combat.ConditionKind.Enfeebled) > 0;
        }
        Assert.True(enfeebled, "Blade of Justice never landed in eight tries");
    }

    [Theory]
    [InlineData("paladin", true)]
    [InlineData("lanternbearer", false)]
    public void The_paladin_s_reaction_answers_a_blow_at_an_ally_beside_it(string subclass, bool answers)
    {
        var world = World(subclass, 1, (4.6, 8, CreatureRank.Elite));
        var c = world.Hero.Character;
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "necromancer_dedication", SkillFeat: c.FeatOptions(FeatType.Skill, 2).First().Id));
        Run(world, 0.1);
        world.Hero.Dedication?.Gain(1000);
        world.Hero.Resource.Gain(1000);
        Assert.True(world.UseAbility("raise_bones", null));
        var skeleton = world.Minions.Single();
        var zombie = world.Monsters.Single();
        world.Stop();
        var bite = typeof(GameWorld).GetMethod("MonsterBitesAlly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var answered = false;
        for (var i = 0; i < 30 && !answered; i++)
        {
            // Only the blow's own answer counts: the skeleton's and the hero's other blows land between.
            Run(world, GameWorld.RetributionSeconds + 0.1);
            zombie.Creature.Restore();
            skeleton.Creature.Restore();
            (skeleton.X, skeleton.Y) = (world.Hero.X + 0.8, world.Hero.Y + 0.6);
            bite.Invoke(world, [zombie, skeleton]);
            answered = zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints;
        }
        Assert.Equal(answers, answered);
    }

    [Fact]
    public void Aura_of_justice_guards_the_allies_within_and_final_judgment_sweeps()
    {
        var world = World("paladin", 13, (4.5, 8, CreatureRank.Normal), (4.2, 8.9, CreatureRank.Normal), (4.2, 7.1, CreatureRank.Normal));
        world.Hero.Resource.Gain(1000);
        Assert.Contains(world.KnownAbilities, a => a.Id == "aura_of_justice");
        Assert.True(world.UseAbility("aura_of_justice", null));
        var aura = Assert.Single(world.Auras);
        Assert.True(aura.Ability.Retaliate);
        Assert.Equal(1, aura.Ability.ExtraDice);

        var swept = 0;
        for (var attempt = 0; attempt < 6 && swept < 2; attempt++)
        {
            var fresh = World("paladin", 13, (4.5, 8, CreatureRank.Normal), (4.2, 8.9, CreatureRank.Normal), (4.2, 7.1, CreatureRank.Normal));
            fresh.Hero.Resource.Gain(1000);
            var target = fresh.Monsters.MinBy(m => m.DistanceTo(fresh.Hero))!;
            Assert.True(fresh.UseAbility("final_judgment", target));
            Run(fresh, 1.5);
            swept = fresh.Monsters.Count(Hurt);
        }
        Assert.True(swept >= 2, $"Final Judgment hurt {swept}");
    }
}
