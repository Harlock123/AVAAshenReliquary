using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M3 step 2: choosing the bar. Any ability the hero has goes in any of the eight slots; the rest wait in the list; a
/// new ability goes into a free slot once; the layout is saved.
/// </summary>
public class BarTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("................", 12).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 6], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, params SpawnDefinition[] spawns) => World(hero, Field(spawns));

    private static GameWorld World(Character hero, ZoneDefinition zone) =>
        new(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones, abilities: Content.Abilities);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    /// <summary>A level-4 Bulwark with two feat abilities: more abilities than number slots.</summary>
    private static GameWorld CrowdedFighter(ZoneDefinition? zone = null)
    {
        var world = World(TestCharacters.ForKit("fighter bulwark"), zone ?? Field());
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "intimidating_strike", SkillFeat: SkillFeat(world, 2)));
        world.GainXp(1000, world.Hero);
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(3, new LevelChoice(GeneralFeat: "toughness", SkillIncrease: "Athletics"));
        world.ChooseLevel(4, new LevelChoice(ClassFeat: "swipe", SkillFeat: SkillFeat(world, 4)));
        return world;
    }

    private static string SkillFeat(GameWorld world, int level) =>
        world.Hero.Character.FeatOptions(FeatType.Skill, level).First().Id;

    [Fact]
    public void A_new_hero_gets_the_class_layout_and_a_ninth_ability_waits_in_the_list()
    {
        var world = CrowdedFighter();
        Assert.Equal(4, world.Hero.Character.Level);
        // Strike, Power Attack, Charge, Cleave, Raise a Shield, Finishing Blow, Challenge, Intimidating Strike, Swipe.
        Assert.Equal(9, world.KnownAbilities.Count);
        Assert.Equal("strike", world.Bar["lmb"].Id);
        Assert.Equal("finishing_blow", world.Bar["4"].Id);
        Assert.Equal("challenge", world.Bar["5"].Id);
        Assert.Equal("intimidating_strike", world.Bar["6"].Id);
        Assert.Equal(["swipe"], world.AbilitiesOffBar.Select(a => a.Id));
        Assert.Null(world.SlotOf("swipe"));
    }

    [Fact]
    public void Placing_from_the_list_sends_the_slot_s_ability_to_the_list_and_moving_on_the_bar_swaps()
    {
        var world = CrowdedFighter();
        Assert.True(world.PlaceAbility("swipe", "6"));
        Assert.Equal("swipe", world.Bar["6"].Id);
        Assert.Equal(["intimidating_strike"], world.AbilitiesOffBar.Select(a => a.Id));

        Assert.True(world.PlaceAbility("swipe", "1"));          // from one slot to another: the two change places
        Assert.Equal("swipe", world.Bar["1"].Id);
        Assert.Equal("sudden_charge", world.Bar["6"].Id);

        Assert.True(world.PlaceAbility("cleave", "rmb"));       // anything goes on a mouse button too
        Assert.Equal("cleave", world.Bar["rmb"].Id);
        Assert.Equal("power_attack", world.Bar["2"].Id);

        Assert.False(world.PlaceAbility("thunderclap", "3"));   // not the fighter's
        Assert.False(world.PlaceAbility("swipe", "7"));         // the belt's keys are not the bar's
    }

    [Fact]
    public void A_cleared_slot_stays_empty_and_its_ability_waits_until_placed_again()
    {
        var world = CrowdedFighter();
        Assert.True(world.ClearSlot("5"));
        Assert.False(world.Bar.ContainsKey("5"));
        Assert.Contains("challenge", world.AbilitiesOffBar.Select(a => a.Id));
        Assert.False(world.ClearSlot("5"));
        // A level-up refreshes the bar; what the player took off stays off.
        world.GainXp(5000, world.Hero);
        Assert.False(world.Bar.ContainsKey("5"));
    }

    [Fact]
    public void A_new_ability_goes_into_its_class_slot_or_a_free_one()
    {
        var world = World(TestCharacters.ForKit("fighter bulwark"));
        world.ClearSlot("6");
        world.GainXp(1000, world.Hero);
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "intimidating_strike", SkillFeat: SkillFeat(world, 2)));
        Assert.Equal("finishing_blow", world.Bar["4"].Id);      // the class ability into its own slot
        Assert.Equal("intimidating_strike", world.Bar["6"].Id); // the feat's into the free one
    }

    [Fact]
    public void A_left_click_uses_the_lmb_ability_and_a_one_off_one_falls_back_on_the_basic_attack()
    {
        var spawn = new SpawnDefinition([4, 6], [new PackMember("fen_zombie", CreatureRank.Elite, 1)], Radius: 0);
        var world = World(TestCharacters.ForKit("fighter bulwark"), spawn);
        Assert.True(world.PlaceAbility("power_attack", "lmb"));
        Assert.Equal("strike", world.Bar["rmb"].Id);            // the two buttons swapped
        Assert.Equal("strike", world.BasicAbility.Id);          // the basic attack is still the Strike
        var zombie = world.Monsters.Single();
        world.Attack(zombie);                                   // Power Attack wants Fury it hasn't got: Strikes instead
        Run(world, 5);
        Assert.True(zombie.IsDead || zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints);
        Assert.True(world.ClearSlot("lmb"));
        Assert.Equal("strike", world.BasicAbility.Id);
    }

    [Fact]
    public void The_layout_is_saved_and_a_save_from_before_it_gets_the_class_layout()
    {
        var world = CrowdedFighter(Content.Zone("fort_gallowmere"));
        world.PlaceAbility("swipe", "1");
        world.ClearSlot("5");
        var save = SaveGame.Capture(world, "bar", 3, null);
        var loaded = SaveGame.FromJson(save.ToJson()).Restore(Content);
        Assert.Equal("swipe", loaded.Bar["1"].Id);
        Assert.False(loaded.Bar.ContainsKey("5"));
        Assert.Contains("sudden_charge", loaded.AbilitiesOffBar.Select(a => a.Id));
        Assert.Contains("challenge", loaded.AbilitiesOffBar.Select(a => a.Id));

        var older = SaveGame.FromJson(save.ToJson().Replace("\"Bar\":", "\"OldBar\":"));
        Assert.Null(older.Bar);
        var fresh = older.Restore(Content);
        Assert.Equal("sudden_charge", fresh.Bar["1"].Id);
        Assert.Equal("challenge", fresh.Bar["5"].Id);
    }

    [Fact]
    public void A_respec_takes_lost_feat_abilities_off_the_bar_and_a_retaken_one_is_placed_again()
    {
        var world = CrowdedFighter();
        world.PlaceAbility("swipe", "1");
        world.Hero.Character.Respec();
        world.ChooseLevel(2, new LevelChoice(ClassFeat: "brutish_shove", SkillFeat: SkillFeat(world, 2)));
        Assert.DoesNotContain(world.KnownAbilities, a => a.Id is "swipe" or "intimidating_strike");
        Assert.False(world.Bar.ContainsKey("1") && world.Bar["1"].Id == "swipe");
        Assert.NotNull(world.SlotOf("brutish_shove"));
    }
}
