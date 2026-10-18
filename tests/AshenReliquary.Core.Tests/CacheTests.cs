using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class CacheTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static ZoneDefinition Field(params CacheDefinition[] caches) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("............", 12).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [2, 2], Spawns: [], Shrines: [], Exits: [], Caches: caches);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld World(ZoneDefinition zone, int level = 1) =>
        new(zone, Content.Creatures, TestCharacters.AtLevel(TestCharacters.Fighter(), level), 5, Content.AbilityBar("fighter"), Content.Loot);

    [Fact]
    public void A_chest_blocks_its_tile()
    {
        var map = new TileMap(Field(new CacheDefinition("c", [8, 8])));
        Assert.True(map.IsBlocked(8, 8));
        Assert.False(map.IsBlocked(7, 8));
    }

    [Fact]
    public void Clicking_a_cache_walks_over_and_spills_loot_at_the_heros_level()
    {
        var world = World(Field(new CacheDefinition("c", [8, 8])), level: 3);
        var cache = world.Caches.Single();
        world.OpenCache(cache);
        Assert.False(cache.Opened); // too far: the hero walks there first
        Run(world, 6);
        Assert.True(cache.Opened);
        // Gold lands on the ground, or within reach of the hero, who scoops it up.
        Assert.True(world.Hero.Equipment.Gold > 0 || world.GroundItems.Any(g => g.Drop.Gold > 0));
        // One piece of gear (a bomb or elixir may come with it).
        var items = world.GroundItems.Where(g => g.Drop.Item is { Definition.IsBeltItem: false }).Select(g => g.Drop.Item!).ToList();
        Assert.Single(items);
        Assert.Equal(3, items[0].ItemLevel);
        Assert.All(world.GroundItems, g => Assert.True(g.X >= 0 && g.Y >= 0 && world.Map.IsWalkable(g.X, g.Y)));
    }

    [Fact]
    public void A_chest_opens_only_once()
    {
        var world = World(Field(new CacheDefinition("c", [3, 3])));
        var cache = world.Caches.Single();
        world.OpenCache(cache);
        Assert.True(cache.Opened);
        var lying = world.GroundItems.Count;
        world.OpenCache(cache);
        Assert.Equal(lying, world.GroundItems.Count);
        Assert.Contains("empty", world.LastMessage);
    }

    [Fact]
    public void A_reliquary_holds_two_items_the_first_at_least_uncommon()
    {
        for (var seed = 1; seed <= 200; seed++)
        {
            var drops = Content.Loot.RollCache(CacheKind.Reliquary, 2, new SeededRandom(seed));
            var items = drops.Where(d => d.Item is { Definition.IsBeltItem: false }).Select(d => d.Item!).ToList();
            Assert.Equal(2, items.Count);
            Assert.True(items[0].Rarity >= Rarity.Uncommon);
            Assert.All(items, i => Assert.Equal(2, i.ItemLevel));
            Assert.True(drops.Sum(d => d.Gold) >= 3 * 3 * 3);
        }
    }

    [Fact]
    public void A_plain_cache_is_kinder_than_a_monster_drop()
    {
        var better = 0;
        for (var seed = 1; seed <= 400; seed++)
        {
            var item = Content.Loot.RollCache(CacheKind.Cache, 1, new SeededRandom(seed)).Single(d => d.Item is { Definition.IsBeltItem: false }).Item!;
            if (item.Rarity >= Rarity.Uncommon)
                better++;
        }
        Assert.InRange(better, 400 * 45 / 100, 400 * 65 / 100); // 55% expected, against 30% from a monster
    }

    [Fact]
    public void Opened_chests_stay_open_through_a_save()
    {
        var world = World(Field(new CacheDefinition("a", [3, 3]), new CacheDefinition("b", [9, 9], CacheKind.Reliquary)));
        world.OpenCache(world.Caches[0]);
        var save = SaveGame.FromJson(SaveGame.Capture(world, "s", 5, null).ToJson());
        Assert.Equal(["a"], save.OpenedCaches);
        var character = TestCharacters.Fighter();
        var loaded = new GameWorld(world.Zone, Content.Creatures, character, 5, Content.AbilityBar("fighter"), Content.Loot);
        loaded.Restore(loaded.Hero.X, loaded.Hero.Y, loaded.RespawnPoint, [], [], [], false, 1, 0, openedCaches: save.OpenedCaches);
        Assert.True(loaded.Caches.Single(c => c.Definition.Id == "a").Opened);
        Assert.False(loaded.Caches.Single(c => c.Definition.Id == "b").Opened);
    }
}
