using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class RuneworkTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld AtTheSmith(int gold = 1000)
    {
        var world = new GameWorld(Content.Zone("fort_gallowmere"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues);
        world.Hero.Equipment.AddGold(gold);
        world.TalkTo(world.Vendors.Single(v => v.Definition.Id == "garrow"));
        for (var t = 0.0; t < 10 && world.OpenVendor is null; t += 1 / 30.0)
            world.Update(1 / 30.0);
        Assert.NotNull(world.OpenVendor);
        return world;
    }

    private static Item Add(GameWorld world, Item item)
    {
        Assert.True(world.Hero.Equipment.TryAdd(item));
        return item;
    }

    [Fact]
    public void Item_level_sets_each_rune_ceiling_as_in_the_rune_table()
    {
        var sword = Content.Item("longsword");
        var chain = Content.Item("chain_cuirass");
        Assert.Equal([0, 1, 1, 1, 2, 3], new[] { 1, 2, 9, 3, 10, 16 }.Select(l => Runework.Ceiling(sword, l, RuneKind.Potency)));
        Assert.Equal([0, 1, 2, 3], new[] { 3, 4, 12, 19 }.Select(l => Runework.Ceiling(sword, l, RuneKind.Striking)));
        Assert.Equal([0, 1, 2, 3], new[] { 4, 5, 11, 18 }.Select(l => Runework.Ceiling(chain, l, RuneKind.Potency)));
        Assert.Equal([0, 1, 2, 3], new[] { 7, 8, 14, 20 }.Select(l => Runework.Ceiling(chain, l, RuneKind.Resilient)));
        Assert.Equal(0, Runework.Ceiling(sword, 20, RuneKind.Resilient));
        Assert.Empty(Runework.Kinds(Content.Item("chain_coif")));                         // only body armour takes runes
        Assert.Equal(4, Runework.LevelFor(sword, RuneKind.Striking, 1));
    }

    [Fact]
    public void A_striking_rune_moves_from_an_old_sword_to_a_new_one_and_the_tooltips_agree()
    {
        var world = AtTheSmith();
        var gear = world.Hero.Equipment;
        var old = Add(world, new Item(Content.Item("longsword"), Rarity.Common, 4, potency: 1, striking: 1));
        var fresh = Add(world, new Item(Content.Item("longsword"), Rarity.Uncommon, 5));
        Assert.Contains("2d8 slashing", string.Join("|", old.Describe()));
        Assert.Contains("1d8 slashing", string.Join("|", fresh.Describe()));
        var gold = gear.Gold;
        var price = Runework.MovePrice(old, RuneKind.Striking, world.Hero.Character);
        Assert.Equal(20, price);                                                          // half of 40, the striking rune's value

        Assert.True(world.MoveRune(old, fresh, RuneKind.Striking));
        Assert.Equal(gold - price, gear.Gold);
        Assert.Equal((1, 0), (fresh.Striking, old.Striking));
        Assert.Equal(1, old.Potency);                                                     // only the striking rune moved
        Assert.StartsWith("Striking ", fresh.Name);                                          // uncommon: an affix may follow
        Assert.Contains("2d8 slashing", string.Join("|", fresh.Describe()));
        Assert.Contains("2 damage dice (striking rune)", fresh.Describe());
        Assert.Contains("1d8 slashing", string.Join("|", old.Describe()));
        Assert.DoesNotContain(old.Describe(), l => l.Contains("striking rune"));
        Assert.Equal("+1 Longsword", old.Name);

        // The new sword, worn, rolls two dice.
        gear.EquipFromBag(gear.Bag.ToList().IndexOf(fresh));
        Assert.Equal(1, world.Hero.Character.Weapon.Striking);
    }

    [Fact]
    public void A_rune_moves_only_where_it_fits()
    {
        var world = AtTheSmith();
        var striking = Add(world, new Item(Content.Item("longsword"), Rarity.Common, 4, striking: 1));
        var low = Add(world, new Item(Content.Item("shortsword"), Rarity.Common, 3));
        var armour = Add(world, new Item(Content.Item("chain_cuirass"), Rarity.Common, 6));
        var gold = world.Hero.Equipment.Gold;

        Assert.Equal("Striking needs item level 4.", Runework.CannotMove(striking, low, RuneKind.Striking));
        Assert.False(world.MoveRune(striking, low, RuneKind.Striking));
        Assert.NotNull(Runework.CannotMove(striking, armour, RuneKind.Striking));
        Assert.NotNull(Runework.CannotMove(low, striking, RuneKind.Striking));           // nothing to move
        Assert.NotNull(Runework.CannotMove(striking, striking, RuneKind.Striking));
        Assert.Equal(gold, world.Hero.Equipment.Gold);
        Assert.Equal(1, striking.Striking);
    }

    [Fact]
    public void Moving_onto_a_weaker_rune_swaps_them()
    {
        var world = AtTheSmith();
        var strong = Add(world, new Item(Content.Item("longsword"), Rarity.Common, 10, potency: 2));
        var weak = Add(world, new Item(Content.Item("longsword"), Rarity.Common, 10, potency: 1));
        Assert.True(world.MoveRune(strong, weak, RuneKind.Potency));
        Assert.Equal((1, 2), (strong.Potency, weak.Potency));
    }

    [Fact]
    public void The_smith_etches_fundamental_runes_up_to_the_item_level_for_gold()
    {
        var world = AtTheSmith(gold: 0);
        var gear = world.Hero.Equipment;
        var sword = Add(world, new Item(Content.Item("longsword"), Rarity.Common, 3));
        var armour = Add(world, new Item(Content.Item("chain_cuirass"), Rarity.Common, 5));

        Assert.Equal(50, Runework.EtchPrice(sword, RuneKind.Potency, world.Hero.Character));
        Assert.False(world.Etch(sword, RuneKind.Potency));                                 // no gold
        Assert.Equal(0, sword.Potency);

        gear.AddGold(500);
        Assert.True(world.Etch(sword, RuneKind.Potency));
        Assert.Equal(450, gear.Gold);
        Assert.Equal("+1 Longsword", sword.Name);
        Assert.Equal("Its potency rune is already the strongest there is.", Runework.CannotEtch(new Item(Content.Item("longsword"), Rarity.Common, 20, potency: 3), RuneKind.Potency));
        Assert.Equal("+2 potency needs item level 10.", Runework.CannotEtch(sword, RuneKind.Potency));
        Assert.Equal("Striking needs item level 4.", Runework.CannotEtch(sword, RuneKind.Striking));
        Assert.False(world.Etch(sword, RuneKind.Striking));
        Assert.NotNull(Runework.CannotEtch(sword, RuneKind.Resilient));

        // Worn armour takes the rune too, and the character sheet follows.
        gear.EquipFromBag(gear.Bag.ToList().IndexOf(armour));
        var ac = world.Hero.Character.ArmorClass().Total;
        Assert.True(world.Etch(armour, RuneKind.Potency));
        Assert.Equal(ac + 1, world.Hero.Character.ArmorClass().Total);
        Assert.Contains("AC +5", string.Join("|", armour.Describe()));                     // 4 + the rune
    }

    [Fact]
    public void Only_a_smith_does_rune_work_and_only_on_the_heros_own_items()
    {
        var world = AtTheSmith();
        var stranger = new Item(Content.Item("longsword"), Rarity.Common, 4, striking: 1);
        var mine = Add(world, new Item(Content.Item("longsword"), Rarity.Common, 4));
        Assert.False(world.MoveRune(stranger, mine, RuneKind.Striking));
        Assert.False(world.Etch(stranger, RuneKind.Potency));

        world.CloseVendor();
        Assert.False(world.Etch(mine, RuneKind.Potency));
        Assert.False(Content.Zone("fort_gallowmere").Vendors!.Single(v => v.Id == "wenna").Runework);
        Assert.True(Content.Zone("fort_gallowmere").Vendors!.Single(v => v.Id == "garrow").Runework);
    }

    [Fact]
    public void Runes_the_smith_moved_survive_a_save()
    {
        var world = AtTheSmith();
        var old = Add(world, new Item(Content.Item("longsword"), Rarity.Common, 4, striking: 1));
        var fresh = Add(world, new Item(Content.Item("longsword"), Rarity.Common, 4));
        world.MoveRune(old, fresh, RuneKind.Striking);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        var swords = loaded.Hero.Equipment.Bag.OfType<Item>().Where(i => i.Definition.Id == "longsword").ToList();
        Assert.Equal([0, 1], swords.Select(i => i.Striking).Order());
        Assert.Contains(swords, i => i.Name == "Striking Longsword");
    }
}
