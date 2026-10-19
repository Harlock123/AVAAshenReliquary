using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>The Drowned Chapel (M2 step 7): the dungeon, darkness and light, concealment, the pilgrim's lantern.</summary>
public class ChapelTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld Chapel(int seed = 3) =>
        new(Content.Zone("drowned_chapel"), Content.Creatures, TestCharacters.Fighter(), seed, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    /// <summary>A dark hall: one torch ('t') at its east end.</summary>
    private static ZoneDefinition Hall(params SpawnDefinition[] spawns) => new(
        "hall", "Hall", 3, "tileset_dungeon",
        [
            "####################",
            "#..................#",
            "#.................t#",
            "#..................#",
            "####################",
        ],
        new Dictionary<string, LegendEntry>
        {
            ["."] = new("floor", Tiles: [16, 17, 18, 19]),
            ["#"] = new("none", Blocks: true, BlocksSight: true),
            ["t"] = new("none", [102], Blocks: true, BlocksSight: true, Light: 3.5),
        },
        PlayerStart: [2, 2], spawns, Shrines: [], Exits: [], Darkness: 0.93, HeroLight: 4.5);

    [Fact]
    public void In_the_dark_only_what_the_hero_can_see_is_explored()
    {
        // Two rooms side by side; the wall between them has no door.
        var zone = new ZoneDefinition("rooms", "Rooms", 3, "tileset_dungeon",
        [
            "###########",
            "#....#....#",
            "#....#....#",
            "#....#....#",
            "###########",
        ],
        new Dictionary<string, LegendEntry>
        {
            ["."] = new("floor", Tiles: [16, 17, 18, 19]),
            ["#"] = new("none", [65], Blocks: true, BlocksSight: true),
        },
        PlayerStart: [2, 2], [], Shrines: [], Exits: [], Darkness: 0.93, HeroLight: 4.5);
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 1);
        Run(world, 0.1);
        Assert.True(world.IsExplored(4, 2));   // the hero's own room
        Assert.True(world.IsExplored(5, 2));   // the wall that bounds it
        Assert.False(world.IsExplored(7, 2));  // the room behind the wall, within reach but out of sight

        var outdoors = zone with { Darkness = 0.5 };
        var open = new GameWorld(outdoors, Content.Creatures, TestCharacters.Fighter(), 1);
        Run(open, 0.1);
        Assert.True(open.IsExplored(7, 2));    // outdoors the whole circle is explored, as before
    }

    [Fact]
    public void The_chapel_loads_and_every_pack_chest_shrine_exit_and_the_lantern_can_be_walked_to()
    {
        var zone = Content.Zone("drowned_chapel");
        var map = new TileMap(zone);
        Assert.Equal("tileset_dungeon", zone.Tileset);
        Assert.True(zone.IsDark);
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), m => Assert.True(Content.Creatures.ContainsKey(m.Creature), m.Creature));
        Assert.Equal(3, zone.Shrines.Count);
        Assert.Equal(["fens", "mire", "ossuary"], zone.Exits.Select(e => e.Id).Order());
        Assert.Equal("sallows_mire", zone.Exits.Single(e => e.Id == "mire").To);
        Assert.Equal([3, 4], zone.Spawns.Select(s => s.Level ?? zone.Level).Distinct().Order());
        Assert.True(map.Lights.Count >= 20, $"only {map.Lights.Count} lights");

        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        var places = zone.Spawns.Select(s => ("pack", s.At))
            .Concat(zone.Caches!.Select(c => (c.Id, c.At)))
            .Concat(zone.Shrines.Select(s => (s.Id, s.At)))
            .Concat(zone.Exits.Select(e => (e.Id, e.At)))
            .Concat(zone.Interactables!.Select(i => (i.Id, i.At)));
        foreach (var (name, at) in places)
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 100000) is not null, $"{name} at {at[0]},{at[1]} can't be reached");
    }

    [Fact]
    public void The_fens_chapel_doors_lead_into_the_chapel_and_back()
    {
        var fens = Content.Zone("rotwater_fens");
        var world = new GameWorld(fens, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones);
        var doors = fens.Exits.Single(e => e.Id == "chapel");
        world.Hero.X = doors.Arrive![0] + 0.5;
        world.Hero.Y = doors.Arrive[1] + 0.5;
        world.MoveTo(doors.At[0] + 0.5, doors.At[1] + 0.5);
        Run(world, 6);
        Assert.Equal("drowned_chapel", world.Zone.Id);
        Assert.Equal("The Drowned Chapel", world.Arrival?.Title);
        var back = world.Zone.Exits.Single(e => e.Id == "fens");
        world.MoveTo(back.At[0] + 0.5, back.At[1] + 0.5);
        Run(world, 6);
        Assert.Equal("rotwater_fens", world.Zone.Id);
    }

    [Fact]
    public void In_the_dark_only_what_a_light_reaches_can_be_seen()
    {
        var world = new GameWorld(Hall(), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot);
        (world.Hero.X, world.Hero.Y) = (2.5, 2.5);
        Assert.Equal(4.5, world.HeroLightRadius);
        Assert.True(world.IsLit(4.5, 2.5));                                    // 2 tiles from the hero
        Assert.False(world.IsLit(10.5, 2.5));                                  // 8 tiles: dark
        Assert.True(world.IsLit(16.5, 2.5));                                   // beside the torch
        Assert.False(world.IsLit(13.5, 2.5));                                  // 5 tiles from the torch: beyond its reach

        // Outdoors nothing is ever too dark to see.
        var fens = new GameWorld(Content.Zone("rotwater_fens"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"));
        Assert.False(fens.Zone.IsDark);
        Assert.True(fens.IsLit(fens.Hero.X + 40, fens.Hero.Y + 40));
    }

    [Fact]
    public void The_pilgrims_lantern_widens_the_heros_light()
    {
        var world = new GameWorld(Hall(), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot);
        (world.Hero.X, world.Hero.Y) = (2.5, 2.5);
        Assert.False(world.IsLit(7.5, 2.5));
        world.Hero.Equipment.TryAdd(new Item(Content.Item("pilgrims_lantern"), Rarity.Common, 1));
        Assert.Equal(7.0, world.HeroLightRadius);
        Assert.True(world.IsLit(7.5, 2.5));
    }

    [Fact]
    public void A_monster_outside_every_light_is_concealed_a_fifth_of_the_time()
    {
        var pack = new SpawnDefinition([9, 2], [new PackMember("drowned_dead", CreatureRank.Normal, 2)], Radius: 0);
        var world = new GameWorld(Hall(pack), Content.Creatures, TestCharacters.Fighter(), 11, Content.AbilityBar("fighter"), Content.Loot);
        (world.Hero.X, world.Hero.Y) = (2.5, 2.5);
        var (dark, lit) = (world.Monsters[0], world.Monsters[1]);
        (dark.X, dark.Y) = (9.5, 2.5);
        (lit.X, lit.Y) = (3.5, 2.5);
        var lost = Enumerable.Range(0, 400).Count(_ => world.LostInTheDark(dark));
        Assert.InRange(lost, 50, 115);                                         // DC 5 flat check: 4 in 20 fail
        Assert.Equal(0, Enumerable.Range(0, 100).Count(_ => world.LostInTheDark(lit)));
    }

    [Fact]
    public void The_lantern_is_taken_from_its_lectern_once_and_can_be_neither_sold_nor_dropped()
    {
        var world = Chapel();
        var lectern = world.Interactives.Single(i => i.Id == "pilgrims_lantern");
        world.Interact(lectern);
        Run(world, 8);
        Assert.NotNull(world.Dialogue);
        world.Choose(0);
        var gear = world.Hero.Equipment;
        var index = gear.Bag.ToList().FindIndex(i => i?.Definition.Id == "pilgrims_lantern");
        Assert.True(index >= 0);
        Assert.Equal("taken", world.Flags["act1.lantern"]);
        Assert.Equal(7.0, world.HeroLightRadius);
        Assert.Contains(gear.Bag[index]!.Describe(), l => l.Contains("2.5 tiles further"));

        Assert.False(world.DropFromBag(index));
        Assert.NotNull(gear.Bag[index]);

        world.EndDialogue();
        world.Interact(lectern);
        Run(world, 2);
        Assert.DoesNotContain(world.Dialogue!.Choices, c => c.Text.Contains("take"));
        Assert.Single(gear.Bag.OfType<Item>(), i => i.Definition.Id == "pilgrims_lantern");
    }

    [Fact]
    public void A_key_item_never_rolls_as_loot()
    {
        var loot = Content.Loot;
        var random = new Rules.SeededRandom(5);
        for (var i = 0; i < 500; i++)
            Assert.NotEqual(ItemCategory.Key, loot.Generate(3, Rarity.Common, random).Definition.Category);
    }
}
