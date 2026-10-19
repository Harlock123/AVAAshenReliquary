using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>M3 step 7: Uniques with traits, Sets with piece bonuses, the drop shares, the Invested limit, shoulders and cloak.</summary>
public class UniqueAndSetTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static Item Make(string id) => LootGenerator.Make(Content.Item(id), 3);

    [Fact]
    public void A_dozen_uniques_for_levels_one_to_ten_each_with_a_trait_and_three_sets()
    {
        var uniques = Content.Items.Values.Where(i => i.Unique).ToList();
        Assert.InRange(uniques.Count, 12, 14);
        Assert.All(uniques, u => Assert.False(string.IsNullOrEmpty(u.Trait), u.Id));
        Assert.All(uniques, u => Assert.NotEmpty(u.Effects!));
        Assert.All(uniques, u => Assert.InRange(u.MinLevel, 1, 10));
        Assert.Equal(["ferrymans_knot", "weeping_soulgem", "ysoldes_phial"], uniques.Where(u => u.Story).Select(u => u.Id).Order());
        Assert.Equal(3, Content.Sets.Count);
        foreach (var set in Content.Sets.Values)
        {
            var pieces = Content.Items.Values.Count(i => i.Set == set.Id);
            Assert.InRange(pieces, 2, 4);
            Assert.All(set.Bonuses, b => Assert.InRange(b.Pieces, 2, pieces));
        }
    }

    [Fact]
    public void Unique_and_set_rolls_give_hand_made_items_and_never_story_ones()
    {
        var random = new SeededRandom(3);
        var uniques = Enumerable.Range(0, 300).Select(_ => Content.Loot.Generate(5, Rarity.Unique, random)).ToList();
        Assert.All(uniques, u => Assert.Equal(Rarity.Unique, u.Rarity));
        Assert.DoesNotContain(uniques, u => u.Definition.Story);
        Assert.Contains(uniques, u => u.Definition.Id == "prophets_weight");    // level 5 reaches it
        var mercy = uniques.First(u => u.Definition.Id == "hangmans_mercy");
        Assert.Equal("Hangman's Mercy", mercy.Name);
        Assert.Equal(1, mercy.Potency);                                         // its fixed rune
        Assert.Empty(mercy.Affixes);
        var sets = Enumerable.Range(0, 100).Select(_ => Content.Loot.Generate(1, Rarity.Set, random)).ToList();
        Assert.All(sets, s => Assert.Equal("gallowsmen", s.Definition.Set));    // only the level-1 set yet
        // Ordinary rolls never give a hand-made item.
        Assert.DoesNotContain(Enumerable.Range(0, 500).Select(_ => Content.Loot.Generate(10, Rarity.Rare, random)), i => i.Definition.IsSpecial);
    }

    [Fact]
    public void A_unique_s_trait_works_while_it_is_worn()
    {
        var hero = TestCharacters.Fighter();
        var gear = new Equipment(hero);
        var perception = hero.Perception().Total;
        gear.Equip(Make("gravewardens_cowl"));
        Assert.Equal(perception + 2, hero.Perception().Total);
        Assert.Equal(1.5, hero.LightBonus);
        gear.Unequip(EquipSlot.Head);
        Assert.Equal(perception, hero.Perception().Total);
        Assert.Equal(0, hero.LightBonus);

        gear.Equip(Make("hangmans_mercy"));
        Assert.Equal(1, hero.UndeadBane);
        Assert.Equal(2, hero.CritDamageBonus);
        gear.Equip(Make("rotgut_girdle"));
        Assert.Equal(4, hero.Defenses.Resistances[DamageType.Poison]);
    }

    [Fact]
    public void Set_bonuses_come_with_the_pieces_worn()
    {
        var hero = TestCharacters.Fighter();
        var gear = new Equipment(hero);
        var (speed, crit) = (hero.SpeedFeet, hero.CritDamageBonus);
        gear.Equip(Make("gallowsmen_hood"));
        Assert.Equal(speed, hero.SpeedFeet);
        gear.Equip(Make("gallowsmen_gloves"));
        Assert.Equal(2, gear.SetPieces("gallowsmen"));
        Assert.Equal(speed + 5, hero.SpeedFeet);                                // two pieces
        Assert.Equal(crit, hero.CritDamageBonus);
        gear.Equip(Make("gallowsmen_boots"));
        Assert.Equal(crit + 3, hero.CritDamageBonus);                           // all three
        gear.Unequip(EquipSlot.Hands);
        Assert.Equal(crit, hero.CritDamageBonus);
        Assert.Equal(speed + 5, hero.SpeedFeet);
    }

    [Fact]
    public void Feats_and_worn_effects_rebuild_cleanly_and_a_respec_keeps_the_gear_s()
    {
        var hero = TestCharacters.Fighter();
        var gear = new Equipment(hero);
        var taint = hero.Taint.RateMultiplier;
        gear.Equip(Make("rotgut_girdle"));
        Assert.Equal(taint * 0.8, hero.Taint.RateMultiplier, 6);
        gear.Equip(Make("weeping_soulgem"));                                    // a second slowing, from a story ring
        Assert.Equal(taint * 0.8 * 0.85, hero.Taint.RateMultiplier, 6);
        hero.LevelUp();
        hero.Choose(2, new LevelChoice(ClassFeat: "lunge", SkillFeat: hero.FeatOptions(FeatType.Skill, 2).First().Id));
        Assert.Equal(1, hero.ReachBonus, 3);
        hero.Respec();
        Assert.Equal(0, hero.ReachBonus, 3);
        Assert.Equal(taint * 0.8 * 0.85, hero.Taint.RateMultiplier, 6);         // worn slowing stays, applied once
        gear.Unequip(EquipSlot.Belt);
        gear.Unequip(EquipSlot.Ring1);
        Assert.Equal(taint, hero.Taint.RateMultiplier, 6);
    }

    [Fact]
    public void Only_ten_invested_items_work_and_the_rest_lie_dormant()
    {
        var hero = TestCharacters.Fighter();
        var gear = new Equipment(hero);
        var random = new SeededRandom(4);
        Item Magic(string id) => Content.Loot.Generate(3, Rarity.Uncommon, random, i => i.Id == id);
        var ids = new[] { "leather_hood", "pauldrons", "leather_chest", "cloak", "leather_gloves", "leather_pants", "leather_boots",
            "belt", "silver_amulet", "silver_ring" };
        foreach (var id in ids)
            gear.Equip(Magic(id));
        gear.Equip(Magic("longsword"));                                         // a weapon is never invested
        Assert.Equal(10, gear.Invested.Count());
        Assert.Empty(gear.Dormant);
        var hp = hero.MaxHitPoints;
        var eleventh = Content.Loot.Generate(3, Rarity.Uncommon, random, i => i.Id == "gold_ring");
        gear.Equip(eleventh, EquipSlot.Ring2);
        Assert.Equal(11, gear.Invested.Count());
        var dormant = Assert.Single(gear.Dormant);
        Assert.Equal(gear.In(EquipSlot.Cloak), dormant);                        // the last in slot order sleeps
        Assert.DoesNotContain(dormant, gear.Active);
        // A plain common piece is no magic and costs nothing.
        Assert.False(LootGenerator.Make(Content.Item("cloak"), 1).IsMagical);
        Assert.True(Make("hangmans_mercy").IsMagical);
    }

    [Fact]
    public void Shoulders_and_cloaks_drop_take_affixes_and_are_saved()
    {
        var random = new SeededRandom(6);
        var drops = Enumerable.Range(0, 2000).Select(_ => Content.Loot.Generate(3, Rarity.Uncommon, random)).ToList();
        Assert.Contains(drops, d => d.Definition.Slot == EquipSlot.Shoulders && d.Affixes.Count > 0);
        Assert.Contains(drops, d => d.Definition.Slot == EquipSlot.Cloak && d.Affixes.Count > 0);

        var world = new GameWorld(Content.Zone("fort_gallowmere"), Content.Creatures, TestCharacters.Fighter(), 5, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, abilities: Content.Abilities);
        world.Hero.Equipment.Equip(Make("shroud_of_the_drowned"));
        world.Hero.Equipment.Equip(Make("warden_pauldrons"));
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "loot", 3, null).ToJson()).Restore(Content);
        var cloak = loaded.Hero.Equipment.In(EquipSlot.Cloak)!;
        Assert.Equal(Rarity.Unique, cloak.Rarity);
        Assert.Equal("Shroud of the Drowned", cloak.Name);
        Assert.Equal(3, loaded.Hero.Character.Defenses.Resistances[DamageType.Void]);
        Assert.Equal(Rarity.Set, loaded.Hero.Equipment.In(EquipSlot.Shoulders)!.Rarity);
    }

    [Fact]
    public void A_unique_drop_rings_out_and_says_what_it_is()
    {
        var zone = new ZoneDefinition("field", "Field", 1, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 8], [], Shrines: [], Exits: []);
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 5, Content.AbilityBar("fighter"), Content.Loot, Content.Zones,
            abilities: Content.Abilities);
        world.DrainSounds();
        typeof(GameWorld).GetMethod("Spill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, [5.0, 8.0, new[] { new LootDrop(Make("whisperknife")) }]);
        Assert.Contains(world.DrainSounds(), s => s.Cue == SoundCue.RareDrop);
        Assert.Equal("A unique falls: Whisperknife!", world.LastMessage);
    }
}
