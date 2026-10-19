using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;

namespace AshenReliquary.App.Tests;

/// <summary>M3 step 7 on screen: the shoulders and cloak on the paper doll, a Unique's beam, and a Set worn.</summary>
public class LootUiTests
{
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    private static Item Make(string id) => LootGenerator.Make(ContentDatabase.Embedded.Item(id), 3);

    [AvaloniaFact]
    public void In_the_narrow_window_the_paper_doll_holds_the_shoulders_and_cloak_and_the_bag_still_fits()
    {
        var kit = TestKit.Open();
        kit.Window.MinWidth = 0;
        kit.Window.Width = 731;
        kit.Window.Height = 946;
        var gear = kit.World.Hero.Equipment;
        foreach (var id in new[] { "gallowsmen_hood", "gallowsmen_gloves", "gallowsmen_boots", "shroud_of_the_drowned", "warden_pauldrons" })
            gear.Equip(Make(id));
        Assert.Equal(3, gear.SetPieces("gallowsmen"));
        kit.Window.View.InventoryOpen = true;
        kit.Play(0.1);
        var panel = kit.Window.View.Inventory;
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            var centre = panel.SlotCentre(slot);
            Assert.True(panel.Bounds.Contains(centre) && centre.X < 731 && centre.Y < 946, $"{slot} at {centre}");
        }
        var lastCell = panel.BagCellCentre(Equipment.BagSize - 1);
        Assert.True(panel.Bounds.Contains(lastCell), $"the bag's last cell at {lastCell}");
        // Hovering the worn cloak shows its tooltip (trait and lore); hovering a set piece its set.
        var cloak = panel.SlotCentre(EquipSlot.Cloak);
        kit.Window.MouseMove(new Point(cloak.X, cloak.Y));
        kit.Play(0.05);
        kit.Capture(Shot("inventory-unique-cloak"));
        var hood = panel.SlotCentre(EquipSlot.Head);
        kit.Window.MouseMove(new Point(hood.X, hood.Y));
        kit.Play(0.05);
        kit.Capture(Shot("inventory-set-hood"));
    }

    [AvaloniaFact]
    public void A_unique_on_the_ground_stands_in_a_tall_purple_beam()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        TestHooks.Drop(world, [new LootDrop(Make("whisperknife")), new LootDrop(Make("mirewitch_hood")),
            new LootDrop(ContentDatabase.Embedded.Loot.Generate(2, Rarity.Rare, new Core.Rules.SeededRandom(1)))], world.Hero.X + 1.5, world.Hero.Y);
        kit.Play(0.3);
        kit.Capture(Shot("unique-beam"));
        Assert.Contains(world.GroundItems, g => g.Rarity == Rarity.Unique);
        kit.Window.View.CharacterSheetOpen = true;
        world.Hero.Equipment.Equip(Make("gravewardens_cowl"));
        kit.Play(0.1);
        kit.Capture(Shot("character-sheet-trait"));
    }
}
