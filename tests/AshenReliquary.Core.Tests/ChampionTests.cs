using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M3 step 4: the Champion (Devotion, Lay on Hands, Shield of Faith, Smite), auras, the Lanternbearer's undead bane and
/// holy light, and the Oathbreaker's Taint as a resource.
/// </summary>
public class ChampionTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 8], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, params SpawnDefinition[] spawns)
    {
        var world = new GameWorld(Field(spawns), Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones,
            abilities: Content.Abilities);
        foreach (var id in new[] { "longsword", "wooden_shield", "chain_cuirass" })
            world.Hero.Equipment.Equip(new Item(Content.Item(id), Rarity.Common, 1));
        world.Hero.Character.RestAtShrine();
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
    public void The_champion_builds_devotion_from_the_blows_it_takes()
    {
        var champion = TestCharacters.Champion();
        Assert.Equal(["lanternbearer", "oathbreaker"], champion.SubclassOptions.Select(s => s.Id));
        var world = World(champion);
        var devotion = world.Hero.Resource;
        Assert.Equal(ResourceKind.Devotion, devotion.Kind);
        Assert.Equal(0, devotion.Current);
        Assert.Equal(["champion_strike", "smite", "lay_on_hands", "shield_of_faith"], world.Bar.Values.Select(a => a.Id));
        devotion.OnCombat(taken: true);
        Assert.Equal(ClassResource.DevotionWhenHit, devotion.Current);
        devotion.OnCombat();
        Assert.Equal(ClassResource.DevotionWhenHit + ClassResource.DevotionPerHit, devotion.Current);
        // It lasts a while after a fight, then drains.
        Run(world, ClassResource.DevotionDecayDelay - 1);
        Assert.Equal(14, devotion.Current, 3);
        Run(world, 5);
        Assert.Equal(0, devotion.Current);
    }

    [Fact]
    public void A_zombie_s_blows_fill_devotion()
    {
        var world = World(TestCharacters.Champion(), At(4, 8, rank: CreatureRank.Elite));
        world.Stop();
        Run(world, 8);
        Assert.True(world.Hero.Character.HitPoints < world.Hero.Character.MaxHitPoints, "the zombie struck");
        Assert.True(world.Hero.Resource.Current >= ClassResource.DevotionWhenHit - 1, $"devotion {world.Hero.Resource.Current}");
    }

    [Fact]
    public void Lay_on_hands_heals_six_a_rank_and_the_lanternbearer_a_quarter_more()
    {
        var world = World(TestCharacters.Champion());
        var c = world.Hero.Character;
        for (var i = 0; i < 3; i++)
            world.Hero.Resource.OnCombat(taken: true);                 // 30 Devotion
        c.TakeDamage(15);
        world.UseSlot(world.SlotOf("lay_on_hands")!, null);
        Assert.Equal(c.MaxHitPoints - 9, c.HitPoints);
        Assert.Equal(0, world.Hero.Resource.Current);
        Assert.True(world.CooldownRemaining(world.Bar[world.SlotOf("lay_on_hands")!]) > 9);

        var lantern = World(TestCharacters.Champion("lanternbearer"));
        for (var i = 0; i < 3; i++)
            lantern.Hero.Resource.OnCombat(taken: true);
        lantern.Hero.Character.TakeDamage(15);
        lantern.UseSlot(lantern.SlotOf("lay_on_hands")!, null);
        Assert.Equal(lantern.Hero.Character.MaxHitPoints - 7, lantern.Hero.Character.HitPoints);   // 6 × 1.25, rounded to 8
    }

    [Fact]
    public void Shield_of_faith_raises_the_shield_lifts_ac_and_saves_and_takes_one_off_every_blow()
    {
        var world = World(TestCharacters.Champion());
        var c = world.Hero.Character;
        var (ac, will) = (c.ArmorClass().Total, c.Save(StatisticKind.Will).Total);
        world.UseSlot(world.SlotOf("shield_of_faith")!, null);
        Assert.Empty(world.Auras);                                     // no Devotion yet
        Assert.Contains("Not enough Devotion", world.LastMessage);
        for (var i = 0; i < 3; i++)
            world.Hero.Resource.OnCombat(taken: true);                 // 25 Devotion
        world.UseSlot(world.SlotOf("shield_of_faith")!, null);
        var aura = Assert.Single(world.Auras);
        Assert.Equal("shield_of_faith", aura.Ability.Id);
        var shield = Content.Item("wooden_shield").AcBonus;
        Assert.Equal(ac + 1 + shield, c.ArmorClass().Total);          // status +1, the raised shield's circumstance bonus
        Assert.Equal(will + 1, c.Save(StatisticKind.Will).Total);
        Assert.NotNull(world.Hero.Ward);
        Run(world, 15.5);
        Assert.Empty(world.Auras);
        Assert.Equal(ac, c.ArmorClass().Total);
        Assert.Equal(1, Content.Abilities["shield_of_faith"].AuraSoakAt(1));
        Assert.Equal(2, Content.Abilities["shield_of_faith"].AuraSoakAt(3));
    }

    [Fact]
    public void Lantern_s_radiance_burns_the_undead_with_vitality_and_heals_the_hero()
    {
        var hero = TestCharacters.Champion("lanternbearer");
        Assert.Equal(1, hero.UndeadBane);
        Assert.Equal(1.5, hero.LightBonus);
        var world = World(hero, At(5, 8, rank: CreatureRank.Elite));
        var zombie = world.Monsters.Single();
        Assert.True(zombie.Definition.Undead);
        Assert.Equal("lanterns_radiance", world.Bar["3"].Id);
        world.Hero.Resource.Restore(100);
        world.UseSlot("3", null);
        Assert.Single(world.Auras);
        Run(world, 0.05);                                              // it pulses at once...
        Assert.All(world.Impacts, i => Assert.Equal(DamageType.Vitality, i.Type));
        Run(world, 4.1);                                               // ...and every 2 s
        Assert.True(zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints);

        // With nothing near, it heals the hero 1 a pulse; a cultist would burn with fire.
        var calm = World(TestCharacters.Champion("lanternbearer"));
        calm.Hero.Resource.Restore(100);
        calm.Hero.Character.TakeDamage(10);
        var hp = calm.Hero.Character.HitPoints;
        calm.UseSlot("3", null);
        Run(calm, 2.1);
        Assert.Equal(hp + 2, calm.Hero.Character.HitPoints);
    }

    [Fact]
    public void Undead_bane_adds_vitality_to_strikes_against_the_undead_only()
    {
        var hero = TestCharacters.Champion("lanternbearer");
        var plain = TestCharacters.Champion();
        foreach (var h in new[] { hero, plain })
            h.Weapon = new Weapon(Content.Item("longsword").AsWeapon());
        // Struck the same way, a zombie (weak to vitality 3) takes 1 + 3 more from the Lanternbearer; a cultist the same.
        double Total(Character who, string creature)
        {
            var world = World(who, At(4, 8, creature));
            var m = world.Monsters.Single();
            var extra = typeof(GameWorld).GetMethod("ChampionStrikeExtra", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (int)extra.Invoke(world, [m, false])!;
        }
        Assert.Equal(4, Total(hero, "fen_zombie"));
        Assert.Equal(0, Total(hero, "cultist"));
        Assert.Equal(0, Total(plain, "fen_zombie"));
    }

    [Fact]
    public void The_oathbreaker_fills_its_taint_with_hits_drains_by_it_and_pays_for_dread_with_it()
    {
        var hero = TestCharacters.Champion("oathbreaker");
        Assert.True(hero.TaintFuel);
        var world = World(hero, At(5, 8, rank: CreatureRank.Elite));
        var zombie = world.Monsters.Single();
        var extra = typeof(GameWorld).GetMethod("ChampionStrikeExtra", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.Equal(0, (int)extra.Invoke(world, [zombie, false])!);   // an empty meter drains nothing...
        Assert.True(hero.Taint.Value > 0);                             // ...and the hit fills it
        hero.Taint.Add(60 / hero.Taint.RateMultiplier);
        hero.TakeDamage(10);
        var hp = hero.HitPoints;
        var drained = (int)extra.Invoke(world, [zombie, true])!;      // 2 points at 50+, doubled on a critical hit
        Assert.Equal(4, drained);
        Assert.Equal(hp + 4, hero.HitPoints);

        // Aura of Dread costs Taint, not Devotion.
        Assert.Equal("aura_of_dread", world.Bar["3"].Id);
        var taint = hero.Taint.Value;
        world.UseSlot("3", null);
        Assert.Single(world.Auras);
        Assert.Equal(taint - 20, hero.Taint.Value, 3);
        Assert.Equal(0, world.Hero.Resource.Current);
        Run(world, 4.1);
        Assert.True(zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints);
        hero.Taint.Cleanse();
        world.UseSlot("3", null);
        Assert.Contains("Not enough Taint", world.LastMessage);
    }

    [Fact]
    public void Feats_widen_the_auras_and_a_fall_ends_them()
    {
        var hero = TestCharacters.Champion("lanternbearer");
        var world = World(hero);
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "beacon", SkillFeat: hero.FeatOptions(FeatType.Skill, 2).First().Id));
        Run(world, 0.1);
        var radiance = world.Bar["3"];
        Assert.Equal(3.5, radiance.Radius);
        Assert.Equal(20, radiance.Cost);
        world.Hero.Resource.Restore(100);
        world.UseSlot("3", null);
        world.UseSlot(world.SlotOf("shield_of_faith")!, null);
        Assert.Equal(2, world.Auras.Count);
        typeof(GameWorld).GetMethod("HeroDied", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, []);
        Assert.Empty(world.Auras);
    }

    [Fact]
    public void Shield_of_faith_takes_its_soak_off_a_blow_but_never_below_nothing()
    {
        var world = World(TestCharacters.Champion());
        world.Hero.Resource.Restore(100);
        world.UseSlot(world.SlotOf("shield_of_faith")!, null);
        var soak = typeof(GameWorld).GetMethod("AuraSoak", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.Equal(6, (int)soak.Invoke(world, [7, world.Hero])!);
        Assert.Equal(0, (int)soak.Invoke(world, [1, world.Hero])!);
    }
}
