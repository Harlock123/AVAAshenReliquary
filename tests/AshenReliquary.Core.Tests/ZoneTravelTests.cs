using AshenReliquary.Core.Content;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class ZoneTravelTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static readonly Dictionary<string, LegendEntry> Grass = new() { ["."] = new("grass") };

    /// <summary>Field A (a zombie, a chest, a shrine, an exit east to B) and B (a shrine, an exit back), 16 x 10 each.</summary>
    private static Dictionary<string, ZoneDefinition> TwoFields(int levelB = 1) => new()
    {
        ["a"] = new("a", "Field A", 1, "tileset_grassland", Enumerable.Repeat("................", 10).ToArray(), Grass,
            PlayerStart: [2, 5],
            Spawns: [new SpawnDefinition([8, 8], [new PackMember("fen_zombie")], Radius: 0)],
            Shrines: [new ShrineDefinition("shrine_a", "Shrine A", [3, 2])],
            Exits: [new ExitDefinition("east", "Field B", [14, 5], 1.2, To: "b", ToExit: "west", Arrive: [11, 5])],
            Caches: [new CacheDefinition("chest_a", [5, 8])]),
        ["b"] = new("b", "Field B", levelB, "tileset_grassland", Enumerable.Repeat("................", 10).ToArray(), Grass,
            PlayerStart: [8, 5], Spawns: [],
            Shrines: [new ShrineDefinition("shrine_b", "Shrine B", [12, 2])],
            Exits: [new ExitDefinition("west", "Field A", [1, 5], 1.2, To: "a", ToExit: "east", Arrive: [4, 5])]),
    };

    private static GameWorld World(Dictionary<string, ZoneDefinition> zones) =>
        new(zones["a"], Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, zones);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static void WalkTo(GameWorld world, double x, double y, double seconds = 8)
    {
        world.MoveTo(x, y);
        Run(world, seconds);
    }

    [Fact]
    public void An_exit_leads_to_its_zone_and_the_zone_left_is_remembered()
    {
        var world = World(TwoFields());
        var entered = new List<string>();
        world.ZoneEntered += z => entered.Add(z.Id);
        world.Slay(world.Monsters.Single());
        world.OpenCache(world.Caches.Single());
        Run(world, 5);
        Assert.True(world.Caches.Single().Opened);
        var points = world.Hero.Character.HeroPoints.Count;

        WalkTo(world, 14.5, 5.5);
        Assert.Equal("b", world.Zone.Id);
        Assert.Equal(["b"], entered);
        Assert.Equal((4.5, 5.5), (world.Hero.X, world.Hero.Y)); // B's west exit's arrival spot
        Assert.True(world.Memories["a"].Complete);
        Assert.Equal(Math.Min(3, points + 1), world.Hero.Character.HeroPoints.Count); // first visit to B

        WalkTo(world, 1.5, 5.5);
        Assert.Equal("a", world.Zone.Id);
        Assert.Equal((11.5, 5.5), (world.Hero.X, world.Hero.Y)); // A's east exit's arrival spot
        Assert.Empty(world.Monsters.Where(m => !m.IsDead));          // the zombie stays dead
        Assert.True(world.Caches.Single().Opened);                  // and the chest stays open
        Assert.NotEmpty(world.GroundItems);                         // and its loot still lies there
        Assert.True(world.ZoneComplete);
        Assert.True(world.Memories.ContainsKey("b"));
    }

    [Fact]
    public void Arriving_through_an_exit_doesnt_bounce_straight_back()
    {
        var world = World(TwoFields());
        WalkTo(world, 14.5, 5.5);
        Run(world, 2);
        Assert.Equal("b", world.Zone.Id);
    }

    [Fact]
    public void A_zone_well_above_the_hero_warns_on_the_first_visit()
    {
        var world = World(TwoFields(levelB: 4));
        world.MoveTo(14.5, 5.5);
        for (var i = 0; i < 300 && world.Zone.Id == "a"; i++)
            world.Update(1 / 30.0);
        Assert.Equal("b", world.Zone.Id);
        Assert.Contains("level 4", world.LastMessage);
    }

    [Fact]
    public void A_save_keeps_the_zones_left_behind()
    {
        var zones = TwoFields();
        var world = World(zones);
        world.Slay(world.Monsters.Single());
        WalkTo(world, 14.5, 5.5);
        var save = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()) with { ZoneId = "b" };
        Assert.Equal("a", save.OtherZones.Single().Id);

        // Restore through the real content path needs these zones in the database, so rebuild by hand as Restore does.
        var loaded = new GameWorld(zones["b"], Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, zones);
        var other = save.OtherZones.Single();
        loaded.RestoreMemory(other.Id, new ZoneMemory { Revision = other.Revision, Slain = other.Slain, Complete = other.Complete });
        WalkTo(loaded, 1.5, 5.5);
        Assert.Equal("a", loaded.Zone.Id);
        Assert.Empty(loaded.Monsters.Where(m => !m.IsDead));
    }

    [Fact]
    public void A_memory_of_an_older_layout_is_forgotten()
    {
        var zones = TwoFields();
        var world = new GameWorld(zones["b"], Content.Creatures, TestCharacters.Fighter(), 3, null, Content.Loot, zones);
        world.RestoreMemory("a", new ZoneMemory { Revision = 0, Slain = [0] });
        Assert.Empty(world.Memories);
    }

    [Fact]
    public void A_woken_shrine_opens_a_rift_to_the_others_in_any_zone()
    {
        var world = World(TwoFields());
        WalkTo(world, 3.5, 3.5);                                     // wakes Shrine A
        WalkTo(world, 14.5, 5.5);                                    // to B
        world.UseShrine(world.Zone.Shrines.Single());                // walks over, wakes it, opens its Rift
        Run(world, 6);
        world.UseShrine(world.Zone.Shrines.Single());
        Run(world, 1);
        Assert.Equal("shrine_b", world.OpenShrine?.Id);
        Assert.Equal(["shrine_a"], world.RiftDestinations.Select(d => d.Shrine.Id));

        Assert.True(world.RiftTravel("shrine_a"));
        Assert.Equal("a", world.Zone.Id);
        Assert.Null(world.OpenShrine);
        Assert.True(world.Hero.DistanceTo(3.5, 2.5) < 1.5);
    }

    [Fact]
    public void Every_exit_leads_somewhere_real_and_shrine_ids_are_unique()
    {
        var zones = Content.Zones;
        foreach (var zone in zones.Values)
        foreach (var exit in zone.Exits.Where(e => e.To is not null))
        {
            Assert.True(zones.ContainsKey(exit.To!), $"{zone.Id}/{exit.Id} leads to unknown zone {exit.To}");
            var back = zones[exit.To!].Exits.SingleOrDefault(e => e.Id == exit.ToExit);
            Assert.NotNull(back);
            var arrive = back!.Arrive ?? back.At;
            Assert.False(new TileMap(zones[exit.To!]).IsBlocked(arrive[0], arrive[1]), $"{exit.To}/{back.Id} arrives on a blocked tile");
            Assert.True(Math.Sqrt(Math.Pow(arrive[0] - back.At[0], 2) + Math.Pow(arrive[1] - back.At[1], 2)) > back.Radius,
                $"{exit.To}/{back.Id} arrives inside its own exit");
        }
        var shrineIds = zones.Values.SelectMany(z => z.Shrines).Select(s => s.Id).ToList();
        Assert.Equal(shrineIds.Count, shrineIds.Distinct().Count());
    }

    [Fact]
    public void The_gallows_road_gate_leads_into_fort_gallowmere_and_back()
    {
        var road = Content.Zone("gallows_road");
        var world = new GameWorld(road, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones);
        var gate = road.Exits.Single();
        // Walk the last stretch: from the arrival spot in front of the gate, into it.
        var front = gate.Arrive!;
        world.Hero.X = front[0] + 0.5;
        world.Hero.Y = front[1] + 0.5;
        WalkTo(world, gate.At[0] + 0.5, gate.At[1] + 0.5, 4);
        Assert.Equal("fort_gallowmere", world.Zone.Id);
        Assert.Equal("Fort Gallowmere", world.Arrival?.Title);
        var back = world.Zone.Exits.Single();
        WalkTo(world, back.At[0] + 0.5, back.At[1] + 0.5);
        Assert.Equal("gallows_road", world.Zone.Id);
    }

    [Fact]
    public void Loading_a_save_made_in_an_exit_stays_put()
    {
        var road = Content.Zone("gallows_road");
        var world = new GameWorld(road, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones);
        var gate = road.Exits.Single();
        var save = SaveGame.Capture(world, "s", 3, null) with { HeroX = gate.At[0] + 0.5, HeroY = gate.At[1] + 0.5, ZoneComplete = true };
        var loaded = SaveGame.FromJson(save.ToJson()).Restore(Content);
        Run(loaded, 1);
        Assert.Equal("gallows_road", loaded.Zone.Id);
    }

    [Fact]
    public void A_save_made_in_the_fort_restores_both_zones()
    {
        var road = Content.Zone("gallows_road");
        var world = new GameWorld(road, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones);
        world.Slay(world.Monsters[0]);
        var front = road.Exits.Single().Arrive!;
        world.Hero.X = front[0] + 0.5;
        world.Hero.Y = front[1] + 0.5;
        WalkTo(world, road.Exits.Single().At[0] + 0.5, road.Exits.Single().At[1] + 0.5, 4);
        Assert.Equal("fort_gallowmere", world.Zone.Id);

        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.Equal("fort_gallowmere", loaded.Zone.Id);
        Assert.Contains(0, loaded.Memories["gallows_road"].Slain);
        Assert.True(loaded.Memories["gallows_road"].Complete);
        var back = loaded.Zone.Exits.Single();
        WalkTo(loaded, back.At[0] + 0.5, back.At[1] + 0.5);
        Assert.Equal("gallows_road", loaded.Zone.Id);
        Assert.DoesNotContain(loaded.Monsters, m => m.SpawnIndex == 0);
    }
}
