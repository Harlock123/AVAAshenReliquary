using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using AshenReliquary.Core.World;

namespace AshenReliquary.App.Tests;

public class RenderTests
{
    /// <summary>Frames are saved here when ASHEN_SHOTS names a folder (tools/screenshots.sh sets it).</summary>
    private static string? Shot(string name) =>
        Environment.GetEnvironmentVariable("ASHEN_SHOTS") is { Length: > 0 } dir ? Path.Combine(dir, name + ".png") : null;

    [AvaloniaFact]
    public void The_gallows_renders_ground_sprites_and_hud()
    {
        var kit = TestKit.Open();
        kit.Play(0.5);
        using var frame = kit.Capture(Shot("gallows-start"));
        Assert.Equal(1440, frame.PixelSize.Width);
    }

    [AvaloniaFact]
    public void Clicking_the_ground_walks_the_hero_there()
    {
        var kit = TestKit.Open();
        var (x0, y0) = (kit.World.Hero.X, kit.World.Hero.Y);
        kit.Click(new Point(720 + 200, 450 - 100));
        kit.Play(1.0);
        Assert.True(kit.World.Hero.DistanceTo(x0, y0) > 1.0);
    }

    [AvaloniaFact]
    public void A_fight_on_the_road()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie" && m.Creature.Stats.Rank != AshenReliquary.Core.Creatures.CreatureRank.Minion)
            .MinBy(m => m.DistanceTo(world.Hero))!;
        world.Attack(zombie);
        kit.PlayUntil(() => zombie.IsDead || world.Hero.IsDead, 30);
        kit.Play(0.3);
        kit.Capture(Shot("road-fight"));
        Assert.True(zombie.IsDead || world.Hero.IsDead);
    }

    [AvaloniaFact]
    public void The_wizard_renders_in_robes()
    {
        var kit = TestKit.Open("wizard");
        kit.Play(0.3);
        kit.Capture(Shot("wizard-start"));
        Assert.Equal("wizard", kit.Window.Session!.ClassId);
    }

    [AvaloniaFact]
    public void Wizard_spells_in_flight()
    {
        var kit = TestKit.Open("wizard");
        var world = kit.World;
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie" && m.Creature.Stats.Rank != AshenReliquary.Core.Creatures.CreatureRank.Minion)
            .MinBy(m => m.DistanceTo(world.Hero))!;
        world.UseSlot("rmb", zombie);
        kit.PlayUntil(() => world.Projectiles.Count > 0, 20);
        kit.Play(0.2);
        kit.Capture(Shot("wizard-force-bolt"));
        Assert.NotEmpty(world.Projectiles);
        kit.PlayUntil(() => zombie.IsDead || world.Hero.IsDead, 30);
        Assert.True(zombie.IsDead || world.Hero.IsDead);
    }

    [AvaloniaFact]
    public void Fighter_charges_in()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        var target = world.Monsters.Where(m => !m.Definition.Stationary).MinBy(m => m.DistanceTo(world.Hero))!;
        world.UseSlot("1", target);
        kit.PlayUntil(() => world.Hero.CurrentAbility?.Id == "sudden_charge", 30);
        kit.Play(0.15);
        kit.Capture(Shot("fighter-charge"));
        Assert.True(world.CooldownRemaining(world.Bar["1"]) > 0);
    }

    [AvaloniaFact]
    public void Loot_on_the_ground()
    {
        var kit = TestKit.Open(seed: 77);
        var world = kit.World;
        var loot = AshenReliquary.Core.Content.ContentDatabase.Embedded.Loot;
        var random = new AshenReliquary.Core.Rules.SeededRandom(5);
        // Scatter a few drops around the hero as if a champion had just died there.
        var drops = new[]
        {
            new AshenReliquary.Core.Items.LootDrop(loot.Generate(2, AshenReliquary.Core.Items.Rarity.Rare, random)),
            new AshenReliquary.Core.Items.LootDrop(loot.Generate(2, AshenReliquary.Core.Items.Rarity.Uncommon, random)),
            new AshenReliquary.Core.Items.LootDrop(loot.Generate(2, AshenReliquary.Core.Items.Rarity.Common, random)),
            new AshenReliquary.Core.Items.LootDrop(null, Gold: 37),
        };
        var zombie = world.Monsters.Where(m => m.Definition.Id == "fen_zombie").MinBy(m => m.DistanceTo(world.Hero))!;
        world.Attack(zombie);
        kit.PlayUntil(() => zombie.IsDead, 30);
        TestHooks.Drop(world, drops, world.Hero.X + 1.5, world.Hero.Y - 0.5);
        kit.Play(1.0);
        kit.Capture(Shot("loot-on-ground"));
        Assert.True(world.GroundItems.Count >= 3);
    }

    [AvaloniaFact]
    public void Inventory_panel_with_tooltip()
    {
        // The VM's real window: 1312x824 logical pixels on a 2x screen.
        var kit = TestKit.Open(seed: 77, width: 1312, height: 824);
        var world = kit.World;
        var content = AshenReliquary.Core.Content.ContentDatabase.Embedded;
        var loot = content.Loot;
        var random = new AshenReliquary.Core.Rules.SeededRandom(12);
        var vicious = content.Affixes.Single(a => a.Id == "vicious");
        var quick = content.Affixes.Single(a => a.Id == "quick");
        world.Hero.Equipment.TryAdd(new AshenReliquary.Core.Items.Item(content.Item("longsword"), AshenReliquary.Core.Items.Rarity.Uncommon, 2,
            [new AshenReliquary.Core.Items.AffixRoll(vicious, 1), new AshenReliquary.Core.Items.AffixRoll(quick, 6)], potency: 1));
        for (var i = 0; i < 8; i++)
            world.Hero.Equipment.TryAdd(loot.Generate(3, (AshenReliquary.Core.Items.Rarity)(i % 3), random));
        world.Hero.Equipment.AddGold(143);
        kit.Window.View.InventoryOpen = true;
        kit.Play(0.2);
        // Bag cell 0: the panel's left edge is 1312 - 560 - 16, the bag starts under the worn section.
        kit.Window.MouseMove(new Point(1312 - 560 - 16 + 20 + 18, 16 + 58 + 250 + 34 + 18));
        kit.Play(0.1);
        kit.Capture(Shot("inventory"));
        Assert.True(kit.Window.View.InventoryOpen);
    }
}
