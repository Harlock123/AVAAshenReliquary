using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>M3 step 8: salvage into Ashen Dust and Rune Fragments, and the forge's four recipes on a Crafting check.</summary>
public class CraftingTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    /// <summary>A hero at Garrow's forge, with gold to pay his fees.</summary>
    private static GameWorld AtForge()
    {
        var world = new GameWorld(Content.Zone("fort_gallowmere"), Content.Creatures, TestCharacters.Fighter(), 5, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        world.Hero.Equipment.AddGold(5000);
        world.TalkTo(world.Vendors.Single(v => v.Definition.Id == "garrow"));
        for (var t = 0.0; t < 20 && world.OpenVendor is null; t += 1 / 30.0)
            world.Update(1 / 30.0);
        Assert.True(world.AtForge);
        return world;
    }

    private static Item Give(GameWorld world, Item item)
    {
        Assert.True(world.Hero.Equipment.TryAdd(item));
        return item;
    }

    [Fact]
    public void The_level_based_dcs_are_pathfinder_s()
    {
        Assert.Equal(15, Crafting.Dc(1));
        Assert.Equal(18, Crafting.Dc(3));
        Assert.Equal(20, Crafting.Dc(5));
        Assert.Equal(27, Crafting.Dc(10));
        Assert.Equal(40, Crafting.Dc(20));
    }

    [Fact]
    public void Salvage_breaks_gear_into_dust_and_fragments_but_never_a_worn_or_locked_piece()
    {
        var world = AtForge();
        var gear = world.Hero.Equipment;
        var rare = Give(world, new Item(Content.Item("longsword"), Rarity.Rare, 4, potency: 1, striking: 1));
        Assert.Equal((8, 5), Crafting.Salvage(rare));                  // 4 + level; 2 a rune rank + 1 for a rare
        Assert.True(world.Salvage(rare));
        Assert.Equal((8, 5), (gear.Dust, gear.Fragments));
        Assert.DoesNotContain(rare, gear.Bag);
        var locked = Give(world, new Item(Content.Item("club"), Rarity.Common, 1));
        locked.CycleMark();
        locked.CycleMark();
        Assert.False(world.Salvage(locked));
        gear.Equip(new Item(Content.Item("longsword"), Rarity.Common, 1));
        Assert.False(world.Salvage(gear.In(EquipSlot.MainHand)!));
        Assert.Contains("Take", world.LastMessage);
        world.CloseVendor();
        Assert.False(world.Salvage(Give(world, new Item(Content.Item("club"), Rarity.Common, 1))));   // only at the forge
    }

    [Fact]
    public void Raise_add_and_reroll_work_on_a_success_and_fees_are_paid_either_way()
    {
        var world = AtForge();
        var gear = world.Hero.Equipment;
        gear.AddMaterials(500, 50);
        var hood = Give(world, new Item(Content.Item("leather_hood"), Rarity.Common, 2));
        var gold = gear.Gold;
        CraftOutcome? outcome = null;
        for (var i = 0; i < 40 && hood.Rarity == Rarity.Common; i++)
            outcome = world.Craft(CraftRecipe.Raise, hood);
        Assert.Equal(Rarity.Uncommon, hood.Rarity);
        Assert.InRange(hood.Affixes.Count, 1, 2);
        Assert.True(gear.Gold < gold);
        Assert.Contains("Crafting", outcome!.Message);

        for (var i = 0; i < 40 && hood.Affixes.Count < 2; i++)
            world.Craft(CraftRecipe.AddAffix, hood);
        Assert.Equal(2, hood.Affixes.Count);
        Assert.Equal(1, hood.Affixes.Count(a => a.Definition.Prefix));      // one prefix, one suffix
        Assert.Null(world.Craft(CraftRecipe.AddAffix, hood));             // full
        Assert.Contains("all the affixes", world.LastMessage);

        var before = hood.Affixes.ToList();
        for (var i = 0; i < 60 && hood.Affixes.SequenceEqual(before); i++)
            world.Craft(CraftRecipe.Reroll, hood, affix: 0);
        Assert.NotEqual(before[0], hood.Affixes[0]);
        Assert.Equal(before[1], hood.Affixes[1]);                          // only the chosen one changes
        Assert.Equal(2, hood.Affixes.Select(a => (a.Definition.Stat, a.Definition.Resist)).Distinct().Count());

        Assert.Null(world.Craft(CraftRecipe.Raise, Give(world, LootGenerator.Make(Content.Item("whisperknife"), 2))));
        Assert.Contains("made as it is", world.LastMessage);
    }

    [Fact]
    public void Degrees_of_success_decide_the_bonus_and_the_loss()
    {
        var world = AtForge();
        var gear = world.Hero.Equipment;
        gear.AddGold(100_000);
        var seen = new HashSet<DegreeOfSuccess>();
        for (var i = 0; i < 400 && seen.Count < 4; i++)
        {
            gear.AddMaterials(Math.Max(0, 40 - gear.Dust), Math.Max(0, 4 - gear.Fragments));
            var ring = Give(world, new Item(Content.Item("silver_ring"), Rarity.Common, 3));
            var (dust, fragments) = (gear.Dust, gear.Fragments);
            var outcome = world.Craft(CraftRecipe.Raise, ring)!;
            seen.Add(outcome.Check.Degree);
            var cost = Crafting.Cost(CraftRecipe.Raise, 3);
            switch (outcome.Check.Degree)
            {
                case DegreeOfSuccess.CriticalSuccess:
                    Assert.Equal(2, ring.Affixes.Count);                  // the bonus affix
                    Assert.Equal(dust - cost.Dust, gear.Dust);
                    break;
                case DegreeOfSuccess.Success:
                    Assert.Single(ring.Affixes);
                    break;
                case DegreeOfSuccess.Failure:
                    Assert.Equal(Rarity.Common, ring.Rarity);
                    Assert.Equal(dust, gear.Dust);                        // materials kept
                    break;
                default:
                    Assert.Equal(dust - cost.Dust / 2, gear.Dust);         // half lost
                    break;
            }
            gear.TakeFromBag(gear.Bag.ToList().IndexOf(ring));
        }
        Assert.Equal(4, seen.Count);                                       // DC 18 for an untrained fighter: every degree
    }

    [Fact]
    public void Brewing_makes_two_belt_items_and_materials_are_saved()
    {
        var world = AtForge();
        var gear = world.Hero.Equipment;
        gear.AddMaterials(30, 2);
        var fire = Content.Item("alchemists_fire");
        Assert.Contains(fire, world.Brewable);
        var had = gear.CountOf(fire);
        CraftOutcome? outcome = null;
        for (var i = 0; i < 10 && gear.CountOf(fire) == had; i++)
            outcome = world.Craft(CraftRecipe.Brew, brew: fire);
        Assert.InRange(gear.CountOf(fire) - had, 2, 3);
        Assert.Contains("brew", outcome!.Message);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "forge", 3, null).ToJson()).Restore(Content);
        Assert.Equal((gear.Dust, gear.Fragments), (loaded.Hero.Equipment.Dust, loaded.Hero.Equipment.Fragments));
    }
}
