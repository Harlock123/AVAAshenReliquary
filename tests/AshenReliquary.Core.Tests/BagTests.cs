using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>Keeping the bag in order: judging items for the hero, sorting, moving, junk and locks, auto-pickup.</summary>
public class BagTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static Item Make(string id, Rarity rarity = Rarity.Common, int level = 1, int count = 1) =>
        new(Content.Item(id), rarity, level, count: count);

    private static GameWorld World(Character hero, string zone = "gallows_road") =>
        new(Content.Zone(zone), Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones,
            Content.Dialogues, Content.Abilities);

    private static GameWorld Dressed(string classId)
    {
        var hero = classId == "wizard" ? TestCharacters.Wizard() : TestCharacters.Fighter();
        var world = World(hero);
        var kit = classId == "wizard" ? new[] { "staff", "mage_vest" } : ["longsword", "wooden_buckler", "chain_cuirass"];
        foreach (var id in kit)
            world.Hero.Equipment.Equip(Make(id));
        return world;
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    // ----- Judging ------------------------------------------------------------------------

    [Fact]
    public void Gear_a_class_cant_use_well_is_a_poor_fit()
    {
        var fighter = Dressed("fighter");
        var wizard = Dressed("wizard");
        Assert.Equal(ItemFit.PoorFit, fighter.Judge(Make("wand")).Fit);          // a caster's focus on a fighter
        Assert.Equal(ItemFit.PoorFit, wizard.Judge(Make("longsword")).Fit);      // a blade on a wizard
        Assert.Equal(ItemFit.PoorFit, wizard.Judge(Make("leather_chest")).Fit);  // armour the wizard isn't trained in
        Assert.Equal(ItemFit.PoorFit, wizard.Judge(Make("steel_shield")).Fit);   // no way to raise it
        Assert.Equal(ItemFit.Usable, fighter.Judge(Make("steel_shield")).Fit);
        Assert.Equal(ItemFit.Belt, fighter.Judge(Make("frost_vial")).Fit);
        Assert.Equal(ItemFit.Key, fighter.Judge(Make("pilgrims_lantern")).Fit);
    }

    [Fact]
    public void An_upgrade_is_better_in_every_way_that_changes_and_a_downgrade_worse()
    {
        var world = Dressed("fighter");
        Assert.Equal(ItemComparison.Better, world.Judge(new Item(Content.Item("longsword"), Rarity.Common, 4, potency: 1, striking: 1)).Comparison);
        Assert.Equal(ItemComparison.Mixed, world.Judge(Make("dagger")).Comparison); // quicker, but each hit weaker
        Assert.Equal(ItemComparison.Better, world.Judge(Make("steel_shield")).Comparison); // +2 and hardness 5 against +1 and 3
        Assert.Equal(ItemComparison.Same, world.Judge(Make("longsword")).Comparison);
        world.Hero.Equipment.Equip(new Item(Content.Item("longsword"), Rarity.Common, 4, potency: 1, striking: 1));
        Assert.Equal(ItemComparison.Worse, world.Judge(Make("longsword")).Comparison);
    }

    // ----- Sorting and moving ------------------------------------------------------------------

    [Fact]
    public void Sort_puts_upgrades_first_then_usable_gear_belt_items_poor_fits_and_keys()
    {
        var world = Dressed("fighter");
        var gear = world.Hero.Equipment;
        var lantern = Make("pilgrims_lantern");
        var wand = Make("wand", Rarity.Uncommon, 3);
        var fire = Make("alchemists_fire", count: 2);
        var dagger = Make("dagger");
        var gloves = Make("leather_gloves", Rarity.Uncommon);
        var upgrade = new Item(Content.Item("longsword"), Rarity.Common, 4, potency: 1, striking: 1);
        foreach (var item in new[] { lantern, wand, fire, dagger, gloves, upgrade })
            gear.TryAdd(item);
        gear.Move(0, 30);                          // gaps don't survive a sort
        world.SortBag();
        var order = gear.Bag.Take(6).ToList();
        Assert.Same(upgrade, order[0]);
        Assert.Equal(new[] { gloves, dagger }.OrderByDescending(Pricing.Value), order.Skip(1).Take(2));
        Assert.Same(fire, order[3]);
        Assert.Same(wand, order[4]);
        Assert.Same(lantern, order[5]);
        Assert.All(gear.Bag.Skip(6), Assert.Null);
    }

    [Fact]
    public void Moving_a_bag_item_swaps_it_or_tops_up_a_stack()
    {
        var gear = new Equipment(TestCharacters.Fighter());
        var sword = Make("longsword");
        gear.TryAdd(sword);
        gear.TryAdd(Make("frost_vial", count: 4));
        Assert.True(gear.Move(0, 12));
        Assert.Same(sword, gear.Bag[12]);
        Assert.True(gear.Move(12, 1));             // onto the vials: a swap
        Assert.Same(sword, gear.Bag[1]);
        Assert.Equal("frost_vial", gear.Bag[12]!.Definition.Id);

        var more = Make("frost_vial", count: 9);
        gear.Move(Array.IndexOf(gear.Bag.ToArray(), null), 0); // nothing in an empty cell to move
        Assert.True(gear.TryAdd(more));            // tops up the four to ten, three spill into the next free cell
        var stacks = gear.Bag.OfType<Item>().Where(i => i.Definition.Id == "frost_vial").Select(i => i.Count).ToList();
        Assert.Equal(13, stacks.Sum());
        var small = Array.FindIndex(gear.Bag.ToArray(), i => i?.Definition.Id == "frost_vial" && i.Count == 3);
        Assert.True(gear.Move(small, 12));         // ten is full: a swap, not a merge
        Assert.Equal(13, gear.CountOf(Content.Item("frost_vial")));
    }

    [Fact]
    public void A_ring_dragged_onto_the_second_ring_slot_goes_there_and_gear_comes_off_into_a_chosen_cell()
    {
        var gear = new Equipment(TestCharacters.Fighter());
        gear.TryAdd(Make("gold_ring", Rarity.Uncommon));
        Assert.True(gear.EquipFromBag(0, EquipSlot.Ring2));
        Assert.NotNull(gear.In(EquipSlot.Ring2));
        Assert.Null(gear.In(EquipSlot.Ring1));
        gear.TryAdd(Make("longsword"));
        Assert.False(gear.EquipFromBag(0, EquipSlot.Head));
        Assert.True(gear.UnequipTo(EquipSlot.Ring2, 25));
        Assert.Equal("gold_ring", gear.Bag[25]!.Definition.Id);
        Assert.False(gear.UnequipTo(EquipSlot.Ring2, 25));
    }

    // ----- Junk and locks ------------------------------------------------------------------

    [Fact]
    public void Junk_is_dropped_or_sold_in_one_go_and_a_locked_item_never_is()
    {
        var world = Dressed("fighter");
        var gear = world.Hero.Equipment;
        var (dagger, club, ring) = (Make("dagger"), Make("club"), Make("gold_ring", Rarity.Uncommon));
        foreach (var item in new[] { dagger, club, ring })
            gear.TryAdd(item);
        dagger.CycleMark();
        club.CycleMark();
        ring.CycleMark();
        ring.CycleMark();
        Assert.Equal(ItemMark.Locked, ring.Mark);
        Assert.Equal([dagger, club], world.Junk);

        Assert.Equal(2, world.DropJunk());
        Assert.DoesNotContain(dagger, gear.Bag);
        Assert.Contains(world.GroundItems, g => g.Drop.Item == dagger);
        Assert.False(world.DropFromBag(gear.Bag.ToList().IndexOf(ring)));
        Assert.Contains(ring, gear.Bag);
    }

    [Fact]
    public void At_a_trader_junk_sells_for_its_total_and_locked_items_refuse()
    {
        var world = World(TestCharacters.Fighter(), "fort_gallowmere");
        var wenna = world.Vendors.Single(v => v.Definition.Id == "wenna");
        (world.Hero.X, world.Hero.Y) = (wenna.X + 1, wenna.Y + 1);
        world.TalkTo(wenna);
        var gear = world.Hero.Equipment;
        var junk = new[] { Make("dagger"), Make("club", Rarity.Uncommon) };
        var locked = Make("gold_ring", Rarity.Uncommon);
        foreach (var item in junk.Append(locked))
            gear.TryAdd(item);
        foreach (var item in junk)
            item.Mark = ItemMark.Junk;
        locked.Mark = ItemMark.Locked;
        var gold = gear.Gold;
        var (count, earned) = world.SellJunk();
        Assert.Equal(2, count);
        Assert.Equal(junk.Sum(Pricing.SellPrice), earned);
        Assert.Equal(gold + earned, gear.Gold);
        Assert.False(world.Sell(gear.Bag.ToList().IndexOf(locked)));
    }

    [Fact]
    public void Marks_survive_a_save()
    {
        var world = Dressed("fighter");
        var dagger = Make("dagger");
        world.Hero.Equipment.TryAdd(dagger);
        dagger.Mark = ItemMark.Locked;
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.Contains(loaded.Hero.Equipment.Bag, i => i?.Definition.Id == "dagger" && i.Mark == ItemMark.Locked);
    }

    [Fact]
    public void Worn_gear_dragged_out_is_dropped_unless_locked()
    {
        var world = Dressed("fighter");
        var gear = world.Hero.Equipment;
        Assert.True(world.DropWorn(EquipSlot.OffHand));
        Assert.Null(gear.In(EquipSlot.OffHand));
        Assert.Contains(world.GroundItems, g => g.Drop.Item?.Definition.Id == "wooden_buckler");
        gear.In(EquipSlot.MainHand)!.Mark = ItemMark.Locked;
        Assert.False(world.DropWorn(EquipSlot.MainHand));
        Assert.NotNull(gear.In(EquipSlot.MainHand));
    }

    // ----- The stash ------------------------------------------------------------------------

    [Fact]
    public void Items_go_to_and_from_chosen_stash_and_bag_cells_and_the_stash_sorts()
    {
        var world = World(TestCharacters.Fighter(), "fort_gallowmere");
        var chest = world.Interactives.Single(i => i.Id == "stash");
        (world.Hero.X, world.Hero.Y) = (chest.X + 1, chest.Y + 1);
        world.Interact(chest);
        Run(world, 1);
        Assert.True(world.StashOpen);
        var gear = world.Hero.Equipment;
        var (club, ring) = (Make("club"), Make("gold_ring", Rarity.Rare));
        gear.TryAdd(club);
        gear.TryAdd(ring);
        Assert.True(world.StoreAt(gear.Bag.ToList().IndexOf(club), 40));
        Assert.Same(club, world.Stash.Slots[40]);
        Assert.True(world.Store(gear.Bag.ToList().IndexOf(ring)));
        world.SortStash();
        Assert.Same(ring, world.Stash.Slots[0]);   // the rare ring is worth more
        Assert.Same(club, world.Stash.Slots[1]);
        Assert.True(world.RetrieveAt(1, 33));
        Assert.Same(club, gear.Bag[33]);
    }

    // ----- Auto-pickup --------------------------------------------------------------------

    [Fact]
    public void Walking_over_a_bomb_picks_it_up_like_gold_but_not_gear()
    {
        var world = Dressed("fighter");
        var gear = world.Hero.Equipment;
        var bombs = Make("acid_flask", count: 2);
        var sword = Make("dagger");
        var ground = (List<GroundItem>)typeof(GameWorld).GetField("_ground", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(world)!;
        ground.Add(new GroundItem(world.Hero.X + 0.5, world.Hero.Y, new LootDrop(bombs), -10));
        ground.Add(new GroundItem(world.Hero.X - 0.5, world.Hero.Y, new LootDrop(sword), -10));
        Run(world, 0.2);
        Assert.Equal(2, gear.CountOf(Content.Item("acid_flask")));
        Assert.DoesNotContain(sword, gear.Bag);
        Assert.Contains(world.GroundItems, g => g.Drop.Item == sword);
    }
}
