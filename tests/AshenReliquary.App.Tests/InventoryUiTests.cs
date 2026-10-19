using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

/// <summary>The bag with the mouse: Sort, junk and locks, drag and drop, Shift-click to the stash, the loot filter.</summary>
public class InventoryUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static Item Make(string id, Rarity rarity = Rarity.Common, int level = 1) =>
        new(Core.Content.ContentDatabase.Embedded.Item(id), rarity, level);

    private static TestKit OpenBag()
    {
        var kit = TestKit.Open(width: 1440, height: 900);
        kit.Window.View.InventoryOpen = true;
        kit.Play(0.1);
        kit.Capture();
        return kit;
    }

    private static void Drag(TestKit kit, Point from, Point to)
    {
        kit.Window.MouseDown(from, MouseButton.Left);
        kit.Window.MouseMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        kit.Window.MouseMove(to);
        kit.Window.MouseUp(to, MouseButton.Left);
        kit.Pump();
    }

    [AvaloniaFact]
    public void Sort_brings_the_upgrade_to_the_front_and_marks_show()
    {
        var kit = OpenBag();
        var gear = kit.World.Hero.Equipment;
        var wand = Make("wand", Rarity.Uncommon, 2);
        var upgrade = new Item(Core.Content.ContentDatabase.Embedded.Item("longsword"), Rarity.Common, 4, potency: 1, striking: 1);
        gear.TryAdd(wand);
        gear.TryAdd(upgrade);
        gear.Move(gear.Bag.ToList().IndexOf(upgrade), 27);
        kit.Capture();
        kit.Click(kit.Window.View.SortButtonAt);
        Assert.Same(upgrade, gear.Bag[0]);
        Assert.Same(wand, gear.Bag[3]); // after the starting belt items
        kit.Capture(Shot("bag-sorted"));
    }

    [AvaloniaFact]
    public void Ctrl_click_marks_junk_and_drop_junk_asks_once_then_drops()
    {
        var kit = OpenBag();
        var world = kit.World;
        var gear = world.Hero.Equipment;
        var (dagger, club) = (Make("dagger"), Make("club"));
        gear.TryAdd(dagger);
        gear.TryAdd(club);
        kit.Capture();
        foreach (var item in new[] { dagger, club })
        {
            var at = kit.Window.View.BagCell(gear.Bag.ToList().IndexOf(item));
            kit.Window.MouseDown(at, MouseButton.Left, RawInputModifiers.Control);
            kit.Window.MouseUp(at, MouseButton.Left, RawInputModifiers.Control);
            kit.Pump();
        }
        Assert.Equal(ItemMark.Junk, dagger.Mark);
        Assert.Equal([dagger, club], world.Junk);
        kit.Capture(Shot("bag-junk"));

        kit.Click(kit.Window.View.JunkButtonAt);
        Assert.Contains(dagger, gear.Bag);           // the first click only asks
        kit.Click(kit.Window.View.JunkButtonAt);
        Assert.DoesNotContain(dagger, gear.Bag);
        Assert.DoesNotContain(club, gear.Bag);
        Assert.Equal(2, world.GroundItems.Count(g => g.Drop.Item == dagger || g.Drop.Item == club));
    }

    [AvaloniaFact]
    public void Items_drag_between_cells_onto_a_slot_and_out_onto_the_ground()
    {
        var kit = OpenBag();
        var world = kit.World;
        var gear = world.Hero.Equipment;
        var ring = Make("gold_ring", Rarity.Uncommon);
        var club = Make("club");
        gear.TryAdd(ring);
        gear.TryAdd(club);
        kit.Capture();
        var view = kit.Window.View;

        Drag(kit, view.BagCell(gear.Bag.ToList().IndexOf(club)), view.BagCell(44));
        Assert.Same(club, gear.Bag[44]);

        Drag(kit, view.BagCell(gear.Bag.ToList().IndexOf(ring)), view.WornSlot(EquipSlot.Ring2));
        Assert.Same(ring, gear.In(EquipSlot.Ring2));

        Drag(kit, view.WornSlot(EquipSlot.Ring2), view.BagCell(50));
        Assert.Same(ring, gear.Bag[50]);

        // Out of the panel and onto the world: dropped at the hero's feet.
        Drag(kit, view.BagCell(44), new Point(300, 400));
        Assert.DoesNotContain(club, gear.Bag);
        Assert.Contains(world.GroundItems, g => g.Drop.Item == club);

        // A plain click on a cell still wears the item.
        kit.Click(view.BagCell(50));
        Assert.NotNull(gear.In(EquipSlot.Ring1) ?? gear.In(EquipSlot.Ring2));
    }

    [AvaloniaFact]
    public void At_the_stash_shift_click_and_drag_send_items_over_and_sort_orders_it()
    {
        var kit = TestKit.Open(seed: 21, width: 1312, height: 900, zoneId: "fort_gallowmere");
        var world = kit.World;
        var chest = world.Interactives.Single(i => i.Id == "stash");
        TestHooks.Place(world, chest.X + 1, chest.Y + 1);
        world.Interact(chest);
        kit.PlayUntil(() => world.StashOpen, 6);
        kit.Window.View.InventoryOpen = true;
        kit.Play(0.1);
        var gear = world.Hero.Equipment;
        var (club, ring) = (Make("club"), Make("gold_ring", Rarity.Rare));
        gear.TryAdd(club);
        gear.TryAdd(ring);
        kit.Capture();
        var view = kit.Window.View;

        var at = view.BagCell(gear.Bag.ToList().IndexOf(club));
        kit.Window.MouseDown(at, MouseButton.Left, RawInputModifiers.Shift);
        kit.Window.MouseUp(at, MouseButton.Left, RawInputModifiers.Shift);
        kit.Pump();
        Assert.Contains(club, world.Stash.Slots);

        Drag(kit, view.BagCell(gear.Bag.ToList().IndexOf(ring)), view.StashCell(30));
        Assert.Same(ring, world.Stash.Slots[30]);
        kit.Capture(Shot("stash-drag"));
    }

    [AvaloniaFact]
    public void The_loot_filter_hides_common_gear_until_alt_is_held()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        var view = kit.Window.View;
        TestHooks.Drop(world, [new LootDrop(Make("club")), new LootDrop(Make("dagger", Rarity.Uncommon)), new LootDrop(null, Gold: 9)],
            world.Hero.X + 2, world.Hero.Y);
        kit.Play(0.1);
        var common = world.GroundItems.Single(g => g.Drop.Item?.Definition.Id == "club");
        var uncommon = world.GroundItems.Single(g => g.Drop.Item?.Definition.Id == "dagger");
        var gold = world.GroundItems.Single(g => g.Drop.Gold > 0);
        Assert.True(view.LabelShows(common));
        view.HideCommonLoot = true;
        Assert.False(view.LabelShows(common));
        Assert.True(view.LabelShows(uncommon));
        Assert.True(view.LabelShows(gold));
        kit.Capture(Shot("loot-filtered"));
        kit.Window.KeyPress(Key.LeftAlt, RawInputModifiers.Alt, PhysicalKey.AltLeft, null);
        Assert.True(view.LabelShows(common));
        kit.Window.KeyRelease(Key.LeftAlt, RawInputModifiers.None, PhysicalKey.AltLeft, null);
        Assert.False(view.LabelShows(common));
    }
}
