using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class FeatTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("................", 12).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 6], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, ZoneDefinition? zone = null, int seed = 5) =>
        new(zone ?? Field(), Content.Creatures, hero, seed, Content.AbilityBar(hero.Class.Id), Content.Loot, abilities: Content.Abilities);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    // ----- Creation --------------------------------------------------------------

    [Fact]
    public void A_new_character_is_trained_in_its_background_and_class_skills_and_its_picks()
    {
        var hero = TestCharacters.Fighter();                 // Deserter fighter, INT +0: Athletics from both, 3 picks
        Assert.Equal(ProficiencyRank.Trained, hero.SkillRank("Athletics"));
        Assert.Equal(1 + 3, hero.SkillRanks.Count);
        Assert.Equal(ProficiencyRank.Trained, hero.SkillRank("Warfare Lore"));
        Assert.Contains(hero.Feats, f => f.Id == "hardy_recovery");     // the Deserter's skill feat
        Assert.Contains(hero.Feats, f => f.Id == "cooperative_nature"); // the chosen ancestry feat
    }

    [Fact]
    public void Creation_checks_the_skill_picks_and_the_ancestry_feat()
    {
        CharacterChoices With(IReadOnlyList<string>? skills = null, string? feat = null) =>
            TestCharacters.FighterChoices with { Skills = skills, AncestryFeat = feat };
        var made = CharacterBuilder.Create(Content, With(["Medicine", "Religion", "Society"]));
        Assert.Equal(ProficiencyRank.Trained, made.SkillRank("Medicine"));
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(Content, With(["Medicine"])));                        // too few
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(Content, With(["Athletics", "Religion", "Society"]))); // already trained
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(Content, With(["Juggling", "Religion", "Society"])));  // no such skill
        Assert.Throws<RulesException>(() => CharacterBuilder.Create(Content, With(feat: "nimble_elf")));                   // not a human feat
    }

    // ----- Levelling ---------------------------------------------------------------

    [Fact]
    public void A_new_level_waits_for_its_picks_and_takes_only_what_it_grants()
    {
        var hero = TestCharacters.Fighter();
        hero.LevelUp();
        Assert.Equal([2], hero.PendingLevels);
        Assert.Throws<RulesException>(() => hero.Choose(2, new LevelChoice(ClassFeat: "intimidating_strike")));                 // the skill feat is missing
        Assert.Throws<RulesException>(() => hero.Choose(2, new LevelChoice(ClassFeat: "reach_spell", SkillFeat: "titan_wrestler"))); // a wizard feat
        Assert.Throws<RulesException>(() => hero.Choose(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "battle_medicine")));  // untrained in Medicine
        Assert.Throws<RulesException>(() => hero.Choose(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler", GeneralFeat: "fleet")));
        hero.Choose(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"));
        Assert.Empty(hero.PendingLevels);
        Assert.Equal(1.0, hero.ReachBonus);
        Assert.Throws<RulesException>(() => hero.Choose(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler")));
    }

    [Fact]
    public void Level_three_brings_a_general_feat_and_a_skill_increase_to_expert()
    {
        var hero = TestCharacters.Fighter();
        hero.LevelUp();
        hero.Choose(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"));
        hero.LevelUp();
        var before = hero.MaxHitPoints;
        Assert.False(hero.CanIncrease("Athletics", 2));     // expert waits for level 3
        hero.Choose(3, new LevelChoice(GeneralFeat: "toughness", SkillIncrease: "Athletics"));
        Assert.Equal(ProficiencyRank.Expert, hero.SkillRank("Athletics"));
        Assert.Equal(before + 3, hero.MaxHitPoints);         // Toughness: +1 HP per level
        Assert.Equal(hero.MaxHitPoints, hero.HitPoints);
    }

    [Fact]
    public void Level_five_brings_boosts_an_ancestry_feat_and_a_skill_increase()
    {
        var hero = TestCharacters.Fighter();
        for (var level = 2; level <= 4; level++)
        {
            hero.LevelUp();
            hero.Choose(level, level switch
            {
                2 => new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"),
                3 => new LevelChoice(GeneralFeat: "fleet", SkillIncrease: "Athletics"),
                _ => new LevelChoice(ClassFeat: "furious_focus", SkillFeat: "cat_fall"),
            });
        }
        hero.LevelUp();
        var str = hero.Attributes[AttributeKind.Strength];
        Assert.Throws<RulesException>(() => hero.Choose(5, new LevelChoice(AncestryFeat: "road_hardened", SkillIncrease: "Medicine")));
        hero.Choose(5, new LevelChoice(Boosts: [AttributeKind.Strength, AttributeKind.Constitution, AttributeKind.Dexterity, AttributeKind.Wisdom],
            AncestryFeat: "road_hardened", SkillIncrease: "Medicine"));
        Assert.Equal(str, hero.Attributes[AttributeKind.Strength]);   // +4 already: a partial boost
        Assert.True(hero.Attributes.HasPartialBoost(AttributeKind.Strength));
        Assert.Equal(ProficiencyRank.Trained, hero.SkillRank("Medicine"));
        Assert.Equal(30, hero.SpeedFeet);                              // Fleet
        Assert.Equal(4, hero.FuryPerHitBonus);                         // Furious Focus
    }

    [Fact]
    public void A_feat_ability_goes_on_the_bar_and_survives_a_save()
    {
        var hero = TestCharacters.Fighter();
        var world = World(hero);
        world.GainXp(1000, world.Hero);
        Assert.Equal([2], hero.PendingLevels);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "intimidating_strike", SkillFeat: "titan_wrestler"));
        Assert.Equal("intimidating_strike", world.Bar["2"].Id);

        var save = SaveGame.Capture(world, "s", 5, null);
        var loaded = SaveGame.FromJson(save.ToJson());
        var character = CharacterBuilder.Create(Content, loaded.Choices);
        character.LevelUp();
        character.Choose(2, loaded.LevelChoices[2]);
        Assert.Equal(["intimidating_strike"], character.GrantedAbilities);
        Assert.Equal(hero.Skill("Athletics").Total, character.Skill("Athletics").Total);
    }

    // ----- In play ---------------------------------------------------------------

    private static (GameWorld World, MonsterActor Zombie) Duel(string classFeat, string skillFeat = "titan_wrestler", int seed = 5,
        string cls = "fighter", int zombies = 1)
    {
        var hero = cls == "wizard" ? TestCharacters.Wizard() : TestCharacters.Fighter();
        // Champions: a level-1 fighter kills a plain zombie in one blow, leaving nothing to frighten or shove.
        var world = World(hero, Field(new SpawnDefinition([6, 6], [new PackMember("fen_zombie", Creatures.CreatureRank.Champion, zombies)], Radius: 0.6)), seed);
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: classFeat, SkillFeat: skillFeat));
        world.Hero.Resource.Restore(100);
        return (world, world.Monsters.OrderBy(m => m.DistanceTo(world.Hero)).First());
    }

    [Fact]
    public void Intimidating_strike_frightens_what_it_hits()
    {
        for (var seed = 1; seed <= 30; seed++)
        {
            var (world, zombie) = Duel("intimidating_strike", seed: seed);
            world.UseSlot("2", zombie);
            Run(world, 3);
            if (zombie.Creature.Conditions.Has(ConditionKind.Frightened))
                return;
        }
        Assert.Fail("Thirty Intimidating Strikes never frightened the zombie.");
    }

    [Fact]
    public void Brutish_shove_knocks_the_target_back()
    {
        for (var seed = 1; seed <= 30; seed++)
        {
            var (world, zombie) = Duel("brutish_shove", seed: seed);
            world.Hero.X = zombie.X - 1.0;
            world.Hero.Y = zombie.Y;
            var hp = zombie.Creature.HitPoints;
            world.UseSlot("2", zombie);
            for (var i = 0; i < 40 && zombie.Creature.HitPoints == hp && !zombie.IsDead; i++)
                world.Update(1 / 30.0);
            if (zombie.Creature.HitPoints < hp && !zombie.IsDead)
            {
                Assert.True(zombie.Creature.Conditions.IsOffGuard);
                Assert.True(world.Hero.DistanceTo(zombie) > 2.0, $"only {world.Hero.DistanceTo(zombie):0.0} tiles away");
                return;
            }
        }
        Assert.Fail("Thirty shoves never landed.");
    }

    [Fact]
    public void A_prone_monster_cant_move()
    {
        var (world, zombie) = Duel("lunge");
        zombie.Creature.Conditions.Apply(ConditionKind.Prone, 1, 5);
        world.Hero.X = 13.5;
        var (x, y) = (zombie.X, zombie.Y);
        Run(world, 1);
        Assert.Equal((x, y), (zombie.X, zombie.Y));
    }

    [Fact]
    public void Lunge_reaches_further_and_reach_spell_casts_further()
    {
        var hero = TestCharacters.Fighter();
        var reach = hero.ReachBonus;
        hero.LevelUp();
        hero.Choose(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"));
        Assert.Equal(reach + 1, hero.ReachBonus);
        var wizard = TestCharacters.Wizard();
        wizard.LevelUp();
        wizard.Choose(2, new LevelChoice(ClassFeat: "reach_spell", SkillFeat: "recognize_spell"));
        Assert.Equal(3, wizard.SpellRangeBonus);
    }

    [Fact]
    public void Electric_arc_leaps_to_a_second_enemy()
    {
        var (world, first) = Duel("electric_arc", "recognize_spell", cls: "wizard", zombies: 2);
        Assert.Equal("electric_arc", world.Bar["2"].Id);
        world.UseSlot("2", first);
        var targets = new HashSet<MonsterActor>();
        for (var i = 0; i < 90; i++)
        {
            world.Update(1 / 30.0);
            foreach (var p in world.Projectiles)
                targets.Add(p.Target);
        }
        Assert.Equal(2, targets.Count);
    }

    [Fact]
    public void Force_volley_looses_one_more_missile()
    {
        var hero = TestCharacters.AtLevel(TestCharacters.Wizard(), 4);
        hero.Choose(2, new LevelChoice(ClassFeat: "reach_spell", SkillFeat: "recognize_spell"));
        hero.Choose(3, new LevelChoice(GeneralFeat: "toughness", SkillIncrease: "Arcana"));
        hero.Choose(4, new LevelChoice(ClassFeat: "force_volley", SkillFeat: "oddity_identification"));
        Assert.Equal(1, hero.ExtraMissiles("force_bolt"));
    }

    // ----- The Step dodge and respec -----------------------------------------------

    [Fact]
    public void A_step_covers_two_tiles_then_waits_its_cooldown()
    {
        var world = World(TestCharacters.Fighter());
        var (x, y) = (world.Hero.X, world.Hero.Y);
        Assert.True(world.Dodge(x + 10, y));
        Assert.True(world.Hero.IsDodging);
        Run(world, 0.5);
        Assert.False(world.Hero.IsDodging);
        Assert.Equal(x + GameWorld.DodgeTiles, world.Hero.X, 1);
        Assert.False(world.Dodge(x, y));                        // on cooldown
        Run(world, GameWorld.DodgeCooldown);
        Assert.True(world.Dodge(x, y));
    }

    [Fact]
    public void Blows_pass_through_while_untouchable()
    {
        var world = World(TestCharacters.Fighter(), Field(new SpawnDefinition([4, 6], [new PackMember("fen_zombie")], Radius: 0)));
        world.Hero.InvulnerableUntil = 1e9;
        var hp = world.Hero.Character.HitPoints;
        Run(world, 6);
        Assert.Equal(hp, world.Hero.Character.HitPoints);
        Assert.Contains(world.Texts, t => t.Text == "dodged");
    }

    [Fact]
    public void A_respec_forgets_every_pick_since_level_one()
    {
        var hero = TestCharacters.Fighter();
        var world = World(hero);
        var (hp, athletics) = (hero.MaxHitPoints, hero.SkillRank("Athletics"));
        world.GainXp(2000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "intimidating_strike", SkillFeat: "titan_wrestler"));
        world.ChooseLevel(3, new LevelChoice(GeneralFeat: "toughness", SkillIncrease: "Athletics"));
        Assert.Equal("intimidating_strike", world.Bar["2"].Id);

        hero.Respec();
        Assert.Equal([2, 3], hero.PendingLevels);
        Assert.Equal(athletics, hero.SkillRank("Athletics"));
        Assert.DoesNotContain(hero.Feats, f => f.Id is "toughness" or "intimidating_strike" or "titan_wrestler");
        Assert.Contains(hero.Feats, f => f.Id == "hardy_recovery");          // level 1's stay
        Assert.Equal(hp + (10 + 2) * 2, hero.MaxHitPoints);                  // two levels' HP, no Toughness
        hero.Choose(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"));
    }

    [Fact]
    public void The_lantern_altar_respecs_free_once_then_for_gold()
    {
        var world = new GameWorld(Content.Zone("fort_gallowmere"), Content.Creatures, TestCharacters.Fighter(), 3,
            Content.AbilityBar("fighter"), Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"));
        var altar = world.Interactives.Single(i => i.Id == "altar");
        world.Interact(altar);
        Run(world, 8);
        Assert.Contains("free", world.Dialogue!.Choices[0].Text);
        world.Choose(0);
        Assert.Equal([2], world.Hero.Character.PendingLevels);

        world.ChooseLevel(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: "titan_wrestler"));
        world.EndDialogue();
        world.Interact(altar);
        Assert.Equal("Let go of what I have chosen, and choose again (40 gold).", world.Dialogue!.Choices[0].Text);
        world.Choose(0);                                                       // no gold: nothing happens
        Assert.Contains("costs 40 gold", world.Dialogue!.LastRoll);
        Assert.Empty(world.Hero.Character.PendingLevels);
        world.Hero.Equipment.AddGold(50);
        world.Choose(0);
        Assert.Equal(10, world.Hero.Equipment.Gold);
        Assert.Equal([2], world.Hero.Character.PendingLevels);
    }

    // ----- The data ----------------------------------------------------------------

    [Fact]
    public void Every_feat_is_well_formed()
    {
        var statistics = Enum.GetNames<StatisticKind>().ToHashSet();
        foreach (var feat in Content.Feats.Values)
        {
            Assert.InRange(feat.Level, 1, 20);
            if (feat.Ancestry is { } ancestry)
                Assert.True(Content.Ancestries.ContainsKey(ancestry), $"{feat.Id}: ancestry {ancestry}");
            if (feat.Class is { } cls)
                Assert.True(Content.Classes.ContainsKey(cls), $"{feat.Id}: class {cls}");
            if (feat.Skill is { } skill)
                Assert.True(Skills.Attribute.ContainsKey(skill), $"{feat.Id}: skill {skill}");
            Assert.NotEmpty(feat.Effects);
            foreach (var e in feat.Effects)
            {
                if (e.Statistic is { } stat)
                    Assert.True(statistics.Contains(stat), $"{feat.Id}: statistic {stat}");
                if (e.Skill is { } bonusSkill)
                    Assert.True(Skills.Attribute.ContainsKey(bonusSkill) || bonusSkill is "Lore" or "Untrained", $"{feat.Id}: skill {bonusSkill}");
                if (e.Ability is { } ability)
                    Assert.True(Content.Abilities.ContainsKey(ability), $"{feat.Id}: ability {ability}");
            }
        }
        foreach (var background in Content.Backgrounds.Values)
            Assert.Contains(Content.Feats.Values, f => f.Name == background.SkillFeat && f.Type == FeatType.Skill);
        foreach (var ancestry in Content.Ancestries.Values)
            Assert.True(CharacterBuilder.AncestryFeats(Content, ancestry.Id).Count >= 4, ancestry.Id);
    }

    [Fact]
    public void Every_playable_character_has_a_legal_pick_at_every_level_to_five()
    {
        foreach (var ancestry in Content.Ancestries.Keys)
        foreach (var background in Content.Backgrounds.Keys)
        foreach (var cls in new[] { "fighter", "wizard" })
        {
            var hero = CharacterBuilder.Create(Content, CharacterBuilder.Recommend(Content, "X", ancestry, background, cls));
            for (var level = 2; level <= 5; level++)
            {
                hero.LevelUp();
                string? First(FeatType type) => hero.FeatOptions(type, level).FirstOrDefault()?.Id;
                var gains = Progression.LevelProgression.GainsAt(level);
                bool Has(Progression.LevelGain gain) => (gains & gain) != 0;
                var choice = new LevelChoice(
                    Boosts: Has(Progression.LevelGain.AttributeBoosts) ? [AttributeKind.Strength, AttributeKind.Dexterity, AttributeKind.Constitution, AttributeKind.Wisdom] : null,
                    ClassFeat: Has(Progression.LevelGain.ClassFeat) ? First(FeatType.Class) : null,
                    SkillFeat: Has(Progression.LevelGain.SkillFeat) ? First(FeatType.Skill) : null,
                    GeneralFeat: Has(Progression.LevelGain.GeneralFeat) ? First(FeatType.General) : null,
                    AncestryFeat: Has(Progression.LevelGain.AncestryFeat) ? First(FeatType.Ancestry) : null,
                    SkillIncrease: Has(Progression.LevelGain.SkillIncrease) ? Skills.Attribute.Keys.First(s => hero.CanIncrease(s, level)) : null);
                hero.Choose(level, choice);
            }
            Assert.Empty(hero.PendingLevels);
        }
    }
}
