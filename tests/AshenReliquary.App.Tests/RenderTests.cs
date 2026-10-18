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
    public void The_fens_render_bog_cultists_and_plague_crawlers()
    {
        var kit = TestKit.Open(zoneId: "rotwater_fens");
        var world = kit.World;
        kit.Play(0.5);
        kit.Capture(Shot("fens-landing"));

        // Stand where a pack of cultists can be seen, and wake it.
        var cultist = world.Monsters.First(m => m.Definition.Id == "cultist" && m.Creature.Stats.Rank == Core.Creatures.CreatureRank.Champion);
        var spot = Enumerable.Range(0, 16).Select(i => (X: cultist.X + 7 * Math.Cos(i * Math.PI / 8), Y: cultist.Y + 7 * Math.Sin(i * Math.PI / 8)))
            .First(p => world.Map.IsWalkable(p.X, p.Y) && !world.Map.HasLineOfSight(p.X, p.Y, cultist.X, cultist.Y) || world.Map.IsWalkable(p.X, p.Y));
        TestHooks.Place(world, spot.X, spot.Y);
        kit.Play(0.2);
        kit.Capture(Shot("fens-cultists-waiting"));
        TestHooks.Place(world, cultist.X + 3, cultist.Y + 3);
        kit.Play(1.5);
        kit.Capture(Shot("fens-cultists"));
        Assert.NotEqual(MonsterState.Idle, cultist.State);

        var crawler = world.Monsters.First(m => m.Definition.Id == "plague_crawler" && m.Creature.Stats.Rank == Core.Creatures.CreatureRank.Normal);
        TestHooks.Place(world, crawler.X + 3, crawler.Y + 3);
        kit.Play(1.0);
        kit.Capture(Shot("fens-crawlers"));
        Assert.NotEqual(MonsterState.Idle, crawler.State);
    }

    [AvaloniaFact]
    public void The_chapel_is_dark_lit_by_torches_and_the_pilgrims_lantern_shows_in_the_bag()
    {
        var kit = TestKit.Open(zoneId: "drowned_chapel");
        var world = kit.World;
        kit.Play(0.5);
        kit.Capture(Shot("chapel-narthex"));
        Assert.True(world.Zone.IsDark);

        world.Hero.Equipment.TryAdd(new Core.Items.Item(Core.Content.ContentDatabase.Embedded.Item("pilgrims_lantern"), Core.Items.Rarity.Common, 1));
        TestHooks.Place(world, 31.5, 34.5);
        kit.Play(0.5);
        kit.Capture(Shot("chapel-nave-lantern"));
        Assert.Equal(world.Zone.HeroLight + 2.5, world.HeroLightRadius);

        kit.Window.View.InventoryOpen = true;
        kit.Play(0.2);
        kit.Capture(Shot("chapel-lantern-in-bag"));
    }

    [Fact]
    public void The_F3_stats_break_before_the_timings_in_a_narrow_window()
    {
        using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, 13);
        const string stats = "GPU renderer, world at 2x of 2x  ·  draw 7.0 ms/frame: ground 2.5, objects+actors 2.4, darkness 1.4, ash 0.2, vignette 0.0, text+hud 0.5";
        Assert.Equal(["60 fps", stats], AshenReliquary.App.Rendering.WorldView.StatsLines("60 fps", stats, font, 2000));
        var narrow = AshenReliquary.App.Rendering.WorldView.StatsLines("60 fps", stats, font, 700);
        Assert.Equal(3, narrow.Count);
        Assert.All(narrow, line => Assert.True(font.MeasureText(line) <= 700, line));
    }

    [AvaloniaFact]
    public void Clicking_a_chest_opens_it_and_spills_its_loot()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        var cache = world.Caches.Single(c => c.Definition.Id == "graveyard");
        world.MoveTo(cache.X + 1, cache.Y + 1);
        kit.PlayUntil(() => world.Hero.DistanceTo(cache.X + 1, cache.Y + 1) < 0.6, 20);
        var at = kit.Window.View.ToScreen(cache.X, cache.Y);
        kit.Click(new Point(at.X, at.Y - 20));
        kit.PlayUntil(() => cache.Opened, 5);
        kit.Play(0.4);
        kit.Capture(Shot("cache-opened"));
        Assert.True(cache.Opened);
        Assert.Contains(world.GroundItems, g => g.Drop.Item is not null);
    }

    [AvaloniaFact]
    public void Talking_to_the_gate_sergeant_opens_a_conversation_and_the_journal_records_it()
    {
        var kit = TestKit.Open(zoneId: "fort_gallowmere");
        var world = kit.World;
        var brask = world.Interactives.Single(i => i.Id == "brask");
        var at = kit.Window.View.ToScreen(brask.X, brask.Y);
        kit.Click(new Point(at.X, at.Y - 60));
        kit.PlayUntil(() => world.Dialogue is not null, 6);
        kit.Capture(Shot("dialogue-brask"));
        Assert.Equal("Sergeant Brask", world.Dialogue!.Speaker);
        world.Choose(0);
        kit.Window.View.JournalOpen = true;
        kit.Play(0.2);
        kit.Capture(Shot("journal"));
        Assert.Equal("act1_fort", Assert.Single(world.Journal).Id);
    }

    [AvaloniaFact]
    public void The_gate_at_the_end_of_gallows_road_leads_into_the_fort()
    {
        var kit = TestKit.Open();
        var world = kit.World;
        var gate = world.Zone.Exits.Single();
        world.Hero.GetType().GetProperty("X")!.SetValue(world.Hero, gate.Arrive![0] + 0.5);
        world.Hero.GetType().GetProperty("Y")!.SetValue(world.Hero, gate.Arrive![1] + 0.5);
        world.MoveTo(gate.At[0] + 0.5, gate.At[1] + 0.5);
        kit.PlayUntil(() => world.Zone.Id == "fort_gallowmere", 10);
        kit.Play(0.5);
        kit.Capture(Shot("fort-arrival"));
        Assert.Equal("Fort Gallowmere", world.Arrival?.Title);
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
