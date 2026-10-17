using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class WorldTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static ZoneDefinition Arena(string[] rows, params SpawnDefinition[] spawns) => new(
        "arena", "Arena", 1, "tileset_grassland", rows,
        new Dictionary<string, LegendEntry>
        {
            ["."] = new("grass"),
            ["T"] = new("grass", [248], Blocks: true, BlocksSight: true),
            ["S"] = new("grass", [265]),
        },
        PlayerStart: [2, 2], spawns, Shrines: [], Exits: []);

    /// <summary>Plays <paramref name="seconds"/> at 30 frames a second (Update caps one call at 0.25 s).</summary>
    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    [Theory]
    [InlineData(-1, 1, 0)]   // screen west
    [InlineData(-1, 0, 1)]   // north-west
    [InlineData(-1, -1, 2)]  // north
    [InlineData(0, -1, 3)]   // north-east
    [InlineData(1, -1, 4)]   // east
    [InlineData(1, 0, 5)]    // south-east
    [InlineData(1, 1, 6)]    // south (towards the camera)
    [InlineData(0, 1, 7)]    // south-west
    public void Map_directions_become_flare_sprite_directions(double dx, double dy, int expected) =>
        Assert.Equal(expected, Actor.DirectionOf(dx, dy));

    [Fact]
    public void Gallows_road_loads_and_every_spawn_and_creature_resolves()
    {
        var zone = Content.Zone("gallows_road");
        var map = new TileMap(zone);
        Assert.False(map.IsBlocked(zone.PlayerStart[0], zone.PlayerStart[1]));
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), m => Assert.True(Content.Creatures.ContainsKey(m.Creature), m.Creature));
        Assert.Equal(2, zone.Shrines.Count);
        var exit = zone.Exits.Single();
        Assert.NotNull(Pathfinder.Find(map, (zone.PlayerStart[0], zone.PlayerStart[1]), (exit.At[0], exit.At[1]), maxNodes: 20000));
    }

    [Fact]
    public void Pathfinder_goes_around_walls()
    {
        var map = new TileMap(Arena([
            ".......",
            ".TTTTT.",
            ".....T.",
            ".TTT.T.",
            ".......",
        ]));
        var path = Pathfinder.Find(map, (0, 0), (4, 2))!;
        Assert.Equal((4.5, 2.5), path[^1]);
        Assert.All(path, p => Assert.False(map.IsBlocked((int)p.X, (int)p.Y)));
        Assert.Null(Pathfinder.Find(map, (0, 0), (3, 2), maxNodes: 1)); // too far for a tiny budget
    }

    [Fact]
    public void Line_of_sight_is_blocked_by_trees()
    {
        var map = new TileMap(Arena([".....", "..T..", "....."]));
        Assert.False(map.HasLineOfSight(0.5, 1.5, 4.5, 1.5));
        Assert.True(map.HasLineOfSight(0.5, 0.5, 4.5, 0.5));
    }

    [Fact]
    public void Hero_walks_to_a_clicked_point()
    {
        var world = new GameWorld(Arena(Enumerable.Repeat("..........", 10).ToArray()), Content.Creatures, TestCharacters.Fighter(), 1);
        world.MoveTo(7.5, 7.5);
        Assert.Equal("stance", world.Hero.Animation);
        world.Update(0.1);
        Assert.Equal("run", world.Hero.Animation);
        Run(world, 5);
        Assert.Equal(7.5, world.Hero.X, 2);
        Assert.Equal(7.5, world.Hero.Y, 2);
        Assert.Equal("stance", world.Hero.Animation);
    }

    [Fact]
    public void Hero_kills_a_zombie_and_earns_xp()
    {
        var zone = Arena(Enumerable.Repeat("............", 12).ToArray(),
            new SpawnDefinition([6, 6], [new PackMember("fen_zombie")], Radius: 0));
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 7);
        var zombie = world.Monsters.Single();
        world.Attack(zombie);
        for (var i = 0; i < 600 && !zombie.IsDead; i++)
            world.Update(1 / 30.0);
        Assert.True(zombie.IsDead);
        Assert.Equal(MonsterState.Dead, zombie.State);
        Assert.Equal(20, world.Experience.Xp); // 40 XP x Normal weight 0.5
        Assert.Contains(world.Texts, t => t.Kind == FloatingTextKind.Xp);
    }

    [Fact]
    public void A_corpse_stops_flinching_from_the_killing_blow()
    {
        var zone = Arena(Enumerable.Repeat("............", 12).ToArray(),
            new SpawnDefinition([6, 6], [new PackMember("fen_zombie")], Radius: 0));
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 7);
        var zombie = world.Monsters.Single();
        world.Attack(zombie);
        for (var i = 0; i < 600 && !zombie.IsDead; i++)
            world.Update(1 / 30.0);
        Assert.True(zombie.IsDead);
        Assert.True(zombie.HitFlinch > 0);
        for (var i = 0; i < 10; i++)
            world.Update(1 / 30.0);
        Assert.True(zombie.HitFlinch <= 0);
    }

    [Fact]
    public void Monsters_aggro_on_sight_and_their_pack_follows()
    {
        var zone = Arena(Enumerable.Repeat("....................", 20).ToArray(),
            new SpawnDefinition([6, 6], [new PackMember("fen_zombie", CreatureRank.Normal, 3)], Radius: 1.0));
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 3);
        world.Update(0.2);
        Assert.All(world.Monsters, m => Assert.Equal(MonsterState.Chase, m.State));
        var before = world.Monsters.Average(m => m.DistanceTo(world.Hero));
        Run(world, 1.0);
        Assert.True(world.Monsters.Average(m => m.DistanceTo(world.Hero)) < before);
    }

    [Fact]
    public void Cursed_graves_raise_zombies_until_destroyed()
    {
        var zone = Arena(Enumerable.Repeat("............", 12).ToArray(),
            new SpawnDefinition([5, 5], [new PackMember("cursed_grave")], Radius: 0));
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 5);
        Run(world, 1.5);
        Assert.Contains(world.Monsters, m => m.Definition.Id == "fen_zombie" && m.Creature.Stats.Rank == CreatureRank.Minion);
    }

    [Fact]
    public void A_dead_hero_respawns_at_the_last_shrine_with_full_hp()
    {
        var zone = Arena(Enumerable.Repeat("..........", 10).ToArray()) with
        {
            Shrines = [new ShrineDefinition("s", "Test Shrine", [2, 3])],
        };
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 1);
        world.Update(0.1);
        Assert.Contains("s", world.ActivatedShrines);
        world.Hero.Character.HeroPoints.TrySpend();
        world.Hero.Character.TakeDamage(999);
        world.MoveTo(8, 8);
        Run(world, GameWorld.RespawnSeconds + 0.2);
        Assert.False(world.Hero.IsDead);
        Assert.Equal(world.Hero.Character.MaxHitPoints, world.Hero.Character.HitPoints);
        Assert.Equal(world.RespawnPoint, (world.Hero.X, world.Hero.Y));
    }

    [Fact]
    public void A_dead_hero_respawns_at_the_nearest_shrine_even_one_not_yet_touched()
    {
        var zone = Arena(Enumerable.Repeat("....................", 10).ToArray()) with
        {
            PlayerStart = [1, 1],
            Shrines = [new ShrineDefinition("near-start", "Start Shrine", [1, 2]), new ShrineDefinition("far", "Far Shrine", [17, 6])],
        };
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 1);
        world.Update(0.1);
        Assert.Contains("near-start", world.ActivatedShrines);
        world.Hero.X = 14.5;
        world.Hero.Y = 5.5;
        world.Hero.Character.HeroPoints.TrySpend();
        world.Hero.Character.TakeDamage(999);
        world.Update(0.05);
        Assert.Equal((17.5, 7.5), world.RespawnPoint);
        Run(world, GameWorld.RespawnSeconds + 0.2);
        Assert.False(world.Hero.IsDead);
        Assert.Equal((17.5, 7.5), (world.Hero.X, world.Hero.Y));
        Assert.Contains("far", world.ActivatedShrines);
    }

    [Fact]
    public void The_nearest_shrine_is_measured_by_walking_not_through_walls()
    {
        // The west shrine is closer as the crow flies, but the wall means a long walk round.
        var zone = Arena([
            "..........",
            "....T.....",
            "....T.....",
            "....T.....",
            "....T.....",
            "....T.....",
            "....T.....",
            "....T.....",
            "....T.....",
            "..........",
        ]) with
        {
            Shrines = [new ShrineDefinition("west", "West", [2, 4]), new ShrineDefinition("east", "East", [9, 0])],
        };
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 1);
        Assert.Equal("east", world.NearestShrine(5.5, 5.5)?.Id);
        Assert.Equal("west", world.NearestShrine(3.5, 5.5)?.Id);
    }

    [Fact]
    public void Walking_into_a_town_completes_the_zone_once_and_shows_its_banner_each_time()
    {
        var zone = Arena(Enumerable.Repeat("....................", 10).ToArray()) with
        {
            Exits = [new ExitDefinition("town", "Test Town", [16, 2], 1.5)],
        };
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 1);
        Assert.Null(world.Arrival);
        world.MoveTo(16.5, 2.5);
        Run(world, 8);
        Assert.True(world.ZoneComplete);
        var first = Assert.NotNull(world.Arrival);
        Assert.Equal("Test Town", first.Exit.Label);

        // Standing about in town doesn't restart it; leaving and coming back does.
        Run(world, 1);
        Assert.Equal(first.At, world.Arrival!.Value.At);
        world.MoveTo(5.5, 5.5);
        Run(world, 8);
        world.MoveTo(16.5, 2.5);
        Run(world, 8);
        Assert.True(world.Arrival!.Value.At > first.At);
    }

    [Fact]
    public void Same_seed_same_fight()
    {
        static string Fight()
        {
            var zone = Arena(Enumerable.Repeat("............", 12).ToArray(),
                new SpawnDefinition([6, 6], [new PackMember("drowned_dead", CreatureRank.Normal, 2)], Radius: 1));
            var world = new GameWorld(zone, ContentDatabase.Embedded.Creatures, TestCharacters.Fighter(), 99);
            world.Attack(world.Monsters[0]);
            WorldTests.Run(world, 6);
            return $"{world.Hero.Character.HitPoints} {string.Join(",", world.Monsters.Select(m => m.Creature.HitPoints))} {world.Texts.Count}";
        }
        Assert.Equal(Fight(), Fight());
    }
}
