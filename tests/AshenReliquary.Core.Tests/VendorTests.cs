using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class VendorTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld NewWorld(int seed = 5, string background = "deserter")
    {
        var choices = CharacterBuilder.Recommend(Content, "Hale", "human", background, "fighter");
        return new GameWorld(Content.Zone("gallows_road"), Content.Creatures, CharacterBuilder.Create(Content, choices), seed,
            Content.AbilityBar("fighter"), Content.Loot);
    }

    /// <summary>A world with the hero standing beside the trader and their table open.</summary>
    private static (GameWorld World, Vendor Vendor) AtTheTable(int gold = 0)
    {
        var world = NewWorld();
        var vendor = world.Vendors.Single();
        world.Hero.X = vendor.X + 1;
        world.Hero.Y = vendor.Y + 1;
        world.TalkTo(vendor);
        world.Hero.Equipment.AddGold(gold);
        return (world, vendor);
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    [Fact]
    public void The_gallows_road_has_a_peddler_with_a_full_table_of_common_and_uncommon_gear()
    {
        var world = NewWorld();
        var vendor = Assert.Single(world.Vendors);
        Assert.Equal("Wenna Marsh", vendor.Definition.Name);
        Assert.Equal(vendor.Definition.StockSize, vendor.Stock.Count);
        Assert.Equal(8, vendor.Stock.Count(i => i.Rarity == Rarity.Common));
        Assert.Equal(4, vendor.Stock.Count(i => i.Rarity == Rarity.Uncommon));
        Assert.All(vendor.Stock, i => Assert.True(i.ItemLevel >= world.Zone.Level));
        Assert.True(world.Map.IsBlocked(vendor.Definition.At[0], vendor.Definition.At[1]));
    }

    [Fact]
    public void The_table_comes_from_the_seed_and_not_from_the_fight_dice()
    {
        var a = NewWorld(seed: 11).Vendors.Single().Stock.Select(i => i.Name);
        var b = NewWorld(seed: 11).Vendors.Single().Stock.Select(i => i.Name);
        Assert.Equal(a, b);
        // Stocking uses its own dice: the world's first roll is the same as a world without a trader.
        var plain = new GameWorld(Content.Zone("gallows_road") with { Vendors = null }, Content.Creatures,
            CharacterBuilder.Create(Content, CharacterBuilder.Recommend(Content, "Hale", "human", "deserter", "fighter")), 11,
            Content.AbilityBar("fighter"), Content.Loot);
        Assert.Equal(plain.Random.Roll(1, 1_000_000), NewWorld(seed: 11).Random.Roll(1, 1_000_000));
    }

    [Fact]
    public void Clicking_a_trader_walks_over_and_opens_the_table_and_walking_away_closes_it()
    {
        var world = NewWorld();
        var vendor = world.Vendors.Single();
        world.Hero.X = vendor.X + 6;
        world.Hero.Y = vendor.Y + 1;
        var closed = 0;
        world.TradeClosed += _ => closed++;
        world.TalkTo(vendor);
        Assert.Null(world.OpenVendor);
        Run(world, 4);
        Assert.Same(vendor, world.OpenVendor);
        Assert.True(vendor.DistanceTo(world.Hero.X, world.Hero.Y) <= Vendor.TalkRadius);

        world.MoveTo(vendor.X + 8, vendor.Y + 1);
        Run(world, 4);
        Assert.Null(world.OpenVendor);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Buying_costs_the_asking_price_and_puts_the_item_in_the_bag()
    {
        var (world, vendor) = AtTheTable(gold: 1000);
        var item = vendor.Stock[0];
        var price = Pricing.BuyPrice(item, world.Hero.Character);
        Assert.True(world.Buy(0));
        Assert.Equal(1000 - price, world.Hero.Equipment.Gold);
        Assert.Contains(item, world.Hero.Equipment.Bag);
        Assert.DoesNotContain(item, vendor.Stock);
    }

    [Fact]
    public void You_cant_buy_what_you_cant_afford()
    {
        var (world, vendor) = AtTheTable(gold: 1);
        var count = vendor.Stock.Count;
        Assert.False(world.Buy(0));
        Assert.Equal(1, world.Hero.Equipment.Gold);
        Assert.Equal(count, vendor.Stock.Count);
        Assert.StartsWith("You need", world.LastMessage);
    }

    [Fact]
    public void A_full_bag_refunds_the_purchase()
    {
        var (world, vendor) = AtTheTable(gold: 1000);
        while (world.Hero.Equipment.TryAdd(new Item(Content.Item("dagger"), Rarity.Common, 1))) { }
        Assert.False(world.Buy(0));
        Assert.Equal(1000, world.Hero.Equipment.Gold);
        Assert.Equal(vendor.Definition.StockSize, vendor.Stock.Count);
    }

    [Fact]
    public void Selling_pays_a_quarter_of_the_value_and_can_be_bought_back_for_the_same()
    {
        var (world, vendor) = AtTheTable();
        var item = new Item(Content.Item("longsword"), Rarity.Uncommon, 2);
        world.Hero.Equipment.TryAdd(item);
        var index = world.Hero.Equipment.Bag.ToList().IndexOf(item);
        Assert.True(world.Sell(index));
        Assert.Equal(Pricing.SellPrice(item), world.Hero.Equipment.Gold);
        Assert.Equal(Pricing.Value(item) / 4, Pricing.SellPrice(item));
        Assert.Same(item, vendor.Buyback[0]);

        Assert.True(world.BuyBack(0));
        Assert.Equal(0, world.Hero.Equipment.Gold);
        Assert.Contains(item, world.Hero.Equipment.Bag);
        Assert.Empty(vendor.Buyback);
    }

    [Fact]
    public void Nothing_trades_without_an_open_table()
    {
        var world = NewWorld();
        world.Hero.Equipment.AddGold(1000);
        world.Hero.Equipment.TryAdd(new Item(Content.Item("dagger"), Rarity.Common, 1));
        Assert.False(world.Buy(0));
        Assert.False(world.Sell(0));
        Assert.False(world.BuyPotion());
        Assert.Equal(1000, world.Hero.Equipment.Gold);
    }

    [Fact]
    public void Potion_charges_can_be_bought_up_to_the_maximum()
    {
        var (world, _) = AtTheTable(gold: 1000);
        var potion = world.Hero.Character.Potion;
        Assert.False(world.BuyPotion());
        potion.Drink(0, 1, world.Random);
        Assert.True(world.BuyPotion());
        Assert.Equal(HealingPotion.MaxCharges, potion.Charges);
        Assert.Equal(1000 - Pricing.PotionPrice(world.Hero.Character), world.Hero.Equipment.Gold);
    }

    [Fact]
    public void A_new_level_brings_new_wares()
    {
        var world = NewWorld();
        var vendor = world.Vendors.Single();
        var before = vendor.Stock.ToList();
        world.GainXp(1000, world.Hero);
        Assert.Equal(2, world.Hero.Character.Level);
        Assert.Equal(2, vendor.StockedForLevel);
        Assert.NotEqual(before, vendor.Stock);
        Assert.All(vendor.Stock, i => Assert.Equal(2, i.ItemLevel));
    }

    [Fact]
    public void The_table_survives_a_save()
    {
        var (world, vendor) = AtTheTable(gold: 1000);
        world.Buy(0);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "t", 5, null).ToJson()).Restore(Content);
        Assert.Equal(vendor.Stock.Select(i => i.Name), loaded.Vendors.Single().Stock.Select(i => i.Name));
        Assert.Equal(vendor.StockedForLevel, loaded.Vendors.Single().StockedForLevel);
    }

    [Fact]
    public void Prices_rise_with_rarity_level_and_runes()
    {
        var sword = Content.Item("longsword");
        var common = Pricing.Value(new Item(sword, Rarity.Common, 1));
        Assert.True(Pricing.Value(new Item(sword, Rarity.Uncommon, 1)) > common);
        Assert.True(Pricing.Value(new Item(sword, Rarity.Common, 4)) > common);
        Assert.True(Pricing.Value(new Item(sword, Rarity.Common, 1, potency: 1)) > common);
        Assert.Equal(12, common);
    }

    [Fact]
    public void Charisma_and_a_street_urchin_upbringing_haggle()
    {
        var plain = NewWorld().Hero.Character;
        var urchin = NewWorld(background: "street-urchin").Hero.Character;
        var expected = 1 - Math.Clamp(plain.Attributes[Rules.AttributeKind.Charisma] * 0.02, -0.1, 0.1);
        Assert.Equal(expected, Pricing.Haggle(plain), 6);
        Assert.Equal(Pricing.Haggle(urchin) + 0.05, 1 - Math.Clamp(urchin.Attributes[Rules.AttributeKind.Charisma] * 0.02, -0.1, 0.1), 6);
        var item = new Item(Content.Item("longsword"), Rarity.Common, 1);
        Assert.True(Pricing.BuyPrice(item, urchin) <= Pricing.BuyPrice(item, plain) || urchin.Attributes[Rules.AttributeKind.Charisma] < plain.Attributes[Rules.AttributeKind.Charisma]);
    }
}
