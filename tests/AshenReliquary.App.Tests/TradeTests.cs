using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Input;

namespace AshenReliquary.App.Tests;

public class TradeTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void Clicking_the_peddler_opens_her_table_beside_the_bag_and_clicks_buy_and_sell()
    {
        var kit = TestKit.Open(seed: 21, width: 1312, height: 824, zoneId: "fort_gallowmere");
        var world = kit.World;
        var vendor = world.Vendors.Single(v => v.Definition.Id == "wenna");
        TestHooks.Place(world, vendor.X + 4, vendor.Y + 1);
        kit.Play(0.1);
        var foot = kit.Window.View.ToScreen(vendor.X, vendor.Y);
        kit.Window.MouseMove(new Point(foot.X, foot.Y - 60));
        kit.Play(0.1);
        kit.Capture(Shot("trader"));
        kit.Click(new Point(foot.X, foot.Y - 60));
        kit.PlayUntil(() => world.OpenVendor is not null, 6);
        Assert.Same(vendor, world.OpenVendor);
        kit.Play(0.1);
        Assert.True(kit.Window.View.InventoryOpen);

        world.Hero.Equipment.AddGold(500);
        var ware = vendor.Stock[0];
        var bought = vendor.Stock.Count;
        // Ware 0: the table is at (16, 16); wares start 92 down and 20 in.
        var wareAt = new Point(16 + 20 + 26, 16 + 92 + 26);
        kit.Window.MouseMove(wareAt);
        kit.Play(0.1);
        kit.Capture(Shot("trade"));
        kit.Click(wareAt);
        Assert.Equal(bought - 1, vendor.Stock.Count);
        var index = world.Hero.Equipment.Bag.ToList().IndexOf(ware);
        Assert.True(index >= 0);

        // Right-click it in the bag: sold, and on the buyback row.
        var bagAt = new Point(1312 - 560 - 16 + 20 + 18 + index % 10 * 50, 16 + 58 + 250 + 34 + 18 + index / 10 * 50);
        kit.Window.MouseDown(bagAt, MouseButton.Right);
        kit.Window.MouseUp(bagAt, MouseButton.Right);
        kit.Pump();
        Assert.Same(ware, vendor.Buyback.Single());
        Assert.DoesNotContain(ware, world.Hero.Equipment.Bag);

        // Escape leaves the trader and the bag closes with it.
        kit.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        kit.Play(0.1);
        Assert.Null(world.OpenVendor);
        Assert.False(kit.Window.View.InventoryOpen);
        Assert.False(kit.Window.FindControl<Control>("GameMenu")!.IsVisible);
    }

    [AvaloniaFact]
    public void At_the_smiths_bench_a_striking_rune_moves_from_an_old_sword_to_a_new_one()
    {
        var kit = TestKit.Open(seed: 21, width: 1312, height: 824, zoneId: "fort_gallowmere");
        var world = kit.World;
        var garrow = world.Vendors.Single(v => v.Definition.Id == "garrow");
        TestHooks.Place(world, garrow.X + 1, garrow.Y + 1);
        world.TalkTo(garrow);
        kit.PlayUntil(() => world.OpenVendor is not null, 6);
        kit.Play(0.1);
        var gear = world.Hero.Equipment;
        gear.AddGold(500);
        var longsword = Core.Content.ContentDatabase.Embedded.Item("longsword");
        var old = new Core.Items.Item(longsword, Core.Items.Rarity.Common, 4, potency: 1, striking: 1);
        var fresh = new Core.Items.Item(longsword, Core.Items.Rarity.Rare, 5, name: "Gallows Oath");
        gear.TryAdd(fresh);
        gear.TryAdd(old);

        void RightClickBag(Core.Items.Item item)
        {
            var index = gear.Bag.ToList().IndexOf(item);
            var at = new Point(1312 - 560 - 16 + 20 + 18 + index % 10 * 50, 16 + 58 + 250 + 34 + 18 + index / 10 * 50);
            kit.Window.MouseDown(at, MouseButton.Right);
            kit.Window.MouseUp(at, MouseButton.Right);
            kit.Pump();
        }

        // The Runework tab, then the new sword to work on and the old one to take runes from.
        kit.Click(new Point(16 + 20 + 106 + 50, 16 + 58 + 13));
        Assert.True(kit.Window.View.InventoryOpen);
        RightClickBag(fresh);
        RightClickBag(old);
        Assert.Contains(fresh, gear.Bag);                        // on the bench, not sold
        Assert.Contains(old, gear.Bag);
        kit.Play(0.1);
        kit.Capture(Shot("runework"));

        // Etch rows for potency and striking, then the moves: potency first, striking second.
        var moveStriking = new Point(16 + 20 + 100, 16 + 192 + 62 + 2 * 38 + 22 + 38 + 16);
        kit.Click(moveStriking);
        Assert.Equal((1, 0), (fresh.Striking, old.Striking));
        Assert.Equal(500 - 20, gear.Gold);
        kit.Play(0.1);
        kit.Capture(Shot("runework-moved"));
    }
}
