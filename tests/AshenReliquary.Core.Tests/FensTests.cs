using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>The Rotwater Fens (M2 step 6): the zone, poison bog, the Vengeful affix, flanking and poisoned blades.</summary>
public class FensTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    /// <summary>A small walled field: '.' grass, '~' bog, 'T' trees.</summary>
    private static ZoneDefinition Field(string[] rows, int level = 2, params SpawnDefinition[] spawns) => new(
        "field", "Field", level, "tileset_grassland", rows,
        new Dictionary<string, LegendEntry>
        {
            ["."] = new("grass"),
            ["~"] = new("bog"),
            ["T"] = new("grass", [248], Blocks: true, BlocksSight: true),
        },
        PlayerStart: [2, 2], spawns, Shrines: [], Exits: []);

    private static GameWorld World(ZoneDefinition zone, IReadOnlyDictionary<string, CreatureDefinition>? creatures = null, int seed = 5) =>
        new(zone, creatures ?? Content.Creatures, TestCharacters.Fighter(), seed, Content.AbilityBar("fighter"), Content.Loot);

    private static readonly string[] Open =
    [
        "TTTTTTTTTTTTTTTT",
        "T..............T",
        "T..............T",
        "T..............T",
        "T..............T",
        "T..............T",
        "T..............T",
        "TTTTTTTTTTTTTTTT",
    ];

    // ----- The zone ------------------------------------------------------------------

    [Fact]
    public void The_fens_load_and_every_pack_chest_shrine_and_exit_can_be_walked_to()
    {
        var zone = Content.Zone("rotwater_fens");
        var map = new TileMap(zone);
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), m => Assert.True(Content.Creatures.ContainsKey(m.Creature), m.Creature));
        Assert.Equal(4, zone.Shrines.Count);
        Assert.Equal(["chapel", "fort"], zone.Exits.Select(e => e.Id).Order());
        Assert.Null(zone.Exits.Single(e => e.Id == "chapel").To);          // the Drowned Chapel opens in step 7
        Assert.InRange(zone.Spawns.Count, 30, 40);
        Assert.InRange(zone.Caches!.Count, 8, 12);
        Assert.Contains(zone.Spawns.SelectMany(s => s.Pack), m => m.Affixes?.Contains("vengeful") == true);

        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        Assert.False(map.IsBlocked(from.Item1, from.Item2));
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        var places = zone.Spawns.Select(s => ("pack", s.At))
            .Concat(zone.Caches!.Select(c => (c.Id, c.At)))
            .Concat(zone.Shrines.Select(s => (s.Id, s.At)))
            .Concat(zone.Exits.Select(e => (e.Id, e.At)));
        foreach (var (name, at) in places)
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 100000) is not null, $"{name} at {at[0]},{at[1]} can't be reached");
    }

    [Fact]
    public void The_fens_hold_poison_bog_and_packs_of_two_levels()
    {
        var zone = Content.Zone("rotwater_fens");
        var map = new TileMap(zone);
        var bog = Enumerable.Range(0, map.Height).Sum(y => Enumerable.Range(0, map.Width).Count(x => map.IsBog(x, y)));
        Assert.True(bog > map.Width * map.Height / 5, $"only {bog} bog cells");
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones);
        Assert.Equal([2, 3], world.Monsters.Select(m => m.Creature.Stats.Level).Distinct().Order());
        var champion = world.Monsters.First(m => m.Has(MonsterAffix.Vengeful));
        Assert.StartsWith("Vengeful ", champion.Name);
    }

    [Fact]
    public void The_forts_west_gate_leads_into_the_fens_and_back()
    {
        var fort = Content.Zone("fort_gallowmere");
        var world = new GameWorld(fort, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones);
        var gate = fort.Exits.Single(e => e.Id == "west_gate");
        world.Hero.X = gate.Arrive![0] + 0.5;
        world.Hero.Y = gate.Arrive[1] + 0.5;
        world.MoveTo(gate.At[0] + 0.5, gate.At[1] + 0.5);
        Run(world, 5);
        Assert.Equal("rotwater_fens", world.Zone.Id);
        Assert.Equal("The Rotwater Fens", world.Arrival?.Title);

        var back = world.Zone.Exits.Single(e => e.Id == "fort");
        world.MoveTo(back.At[0] + 0.5, back.At[1] + 0.5);
        Run(world, 6);
        Assert.Equal("fort_gallowmere", world.Zone.Id);
    }

    // ----- Poison bog ----------------------------------------------------------------

    [Fact]
    public void Standing_in_the_bog_poisons_on_failed_fortitude_saves_and_a_shrine_cleanses_it()
    {
        var world = World(Field(
        [
            "TTTTTTTT",
            "T~~~~~~T",
            "T~~~~~~T",
            "T~~~~~~T",
            "TTTTTTTT",
        ]));
        var hero = world.Hero.Character;
        var start = hero.HitPoints;
        var poisoned = false;
        for (var t = 0.0; t < 40 && !poisoned; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            poisoned = hero.Persistent.Types.Contains(DamageType.Poison);
        }
        Assert.True(poisoned, "twenty Fortitude saves against DC 15 all succeeded");
        Assert.Equal(15, world.BogDc);
        Run(world, 3);
        Assert.True(hero.HitPoints < start);
        hero.RestAtShrine();
        Assert.Empty(hero.Persistent.Types);
    }

    [Fact]
    public void Wading_through_bog_is_slower_than_walking_on_grass()
    {
        double Crossing(string ground)
        {
            var row = "T" + new string(ground[0], 12) + "T";
            var world = World(Field(["TTTTTTTTTTTTTT", row, row, "TTTTTTTTTTTTTT"]));
            world.Hero.X = 1.5;
            world.Hero.Y = 1.5;
            world.MoveTo(12.5, 1.5);
            var t = 0.0;
            while (world.Hero.X < 12.3 && t < 20)
            {
                world.Update(1 / 30.0);
                t += 1 / 30.0;
            }
            return t;
        }
        var grass = Crossing(".");
        var bog = Crossing("~");
        Assert.InRange(bog / grass, 1.3, 1.6);                                  // 0.7 of the speed
    }

    [Fact]
    public void Paths_go_round_a_bog_pool_when_the_way_round_is_short()
    {
        var zone = Field(
        [
            "TTTTTTTTT",
            "T.......T",
            "T.~~~~~.T",
            "T.~~~~~.T",
            "T.......T",
            "TTTTTTTTT",
        ]);
        var map = new TileMap(zone);
        var path = Pathfinder.Find(map, (1, 3), (7, 3))!;
        Assert.DoesNotContain(path, p => map.IsBog(p.X, p.Y));

        // And the hero walking there keeps to dry ground too.
        var world = World(zone);
        world.Hero.X = 1.5;
        world.Hero.Y = 3.5;
        world.MoveTo(7.5, 3.5);
        var wet = false;
        for (var t = 0.0; t < 6; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            wet |= map.IsBog(world.Hero.X, world.Hero.Y);
        }
        Assert.False(wet);
        Assert.True(world.Hero.X > 7);
    }

    // ----- Vengeful, flanking, poisoned blades -----------------------------------------

    [Fact]
    public void A_vengeful_champion_takes_a_stack_of_vengeance_for_each_pack_mate_that_falls()
    {
        var pack = new SpawnDefinition([11, 4],
            [new PackMember("cultist", CreatureRank.Champion, 1, ["vengeful"]), new PackMember("cultist", CreatureRank.Normal, 4)], Radius: 1.5);
        var world = World(Field(Open, 2, pack));
        var champion = world.Monsters.Single(m => m.Has(MonsterAffix.Vengeful));
        Assert.Equal("Vengeful Rot cultist Champion", champion.Name);
        Assert.Equal(0, champion.Vengeance);
        var followers = world.Monsters.Where(m => m != champion).ToList();
        world.Slay(followers[0]);
        Assert.Equal(1, champion.Vengeance);
        Assert.Equal(MonsterState.Chase, champion.State);                       // it comes for the hero
        foreach (var m in followers.Skip(1))
            world.Slay(m);
        Assert.Equal(GameWorld.VengeanceMax, champion.Vengeance);               // capped at three
        Assert.All(followers, m => Assert.Equal(0, m.Vengeance));
    }

    [Fact]
    public void Two_monsters_on_opposite_sides_of_the_hero_flank_it()
    {
        var pack = new SpawnDefinition([8, 4], [new PackMember("cultist", CreatureRank.Normal, 2)], Radius: 0);
        var world = World(Field(Open, 2, pack));
        var (a, b) = (world.Monsters[0], world.Monsters[1]);
        (world.Hero.X, world.Hero.Y) = (7.5, 3.5);
        (a.X, a.Y) = (6.5, 3.5);
        (b.X, b.Y) = (8.5, 3.5);                                               // 180 degrees apart
        Assert.True(world.IsFlanking(a));
        Assert.True(world.IsFlanking(b));
        (b.X, b.Y) = (7.5, 2.5);                                               // 90 degrees: not a flank
        Assert.False(world.IsFlanking(a));
        (b.X, b.Y) = (10.5, 3.5);                                              // out of reach: no partner
        Assert.False(world.IsFlanking(a));
    }

    [Fact]
    public void A_flanker_circles_to_the_far_side_while_an_ally_holds_the_hero()
    {
        var pack = new SpawnDefinition([4, 4], [new PackMember("cultist", CreatureRank.Normal, 2)], Radius: 0);
        var world = World(Field(Open, 1, pack), seed: 9);
        var (a, b) = (world.Monsters[0], world.Monsters[1]);
        (world.Hero.X, world.Hero.Y) = (8.5, 3.5);
        (a.X, a.Y) = (7.6, 3.5);                                               // already in reach, west of the hero
        (b.X, b.Y) = (5.0, 3.6);                                               // coming from the same side
        world.Hero.Character.Heal(1000);
        var flanked = false;
        for (var t = 0.0; t < 4 && !flanked; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            flanked = world.IsFlanking(b) || world.IsFlanking(a);
        }
        Assert.True(flanked, $"the second cultist stopped at {b.X:0.0},{b.Y:0.0}");
        // One of the two went round: they now stand more than 120 degrees apart about the hero.
        var (ax, ay, bx, by) = (a.X - world.Hero.X, a.Y - world.Hero.Y, b.X - world.Hero.X, b.Y - world.Hero.Y);
        var cos = (ax * bx + ay * by) / (Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by));
        Assert.True(cos < Math.Cos(120 * Math.PI / 180), $"cos {cos:0.00}");
    }

    [Fact]
    public void A_poisoned_blade_can_leave_the_hero_poisoned()
    {
        var creatures = Content.Creatures.ToDictionary(c => c.Key, c => c.Key == "cultist" ? c.Value with { PoisonChance = 1.0 } : c.Value);
        var pack = new SpawnDefinition([5, 3], [new PackMember("cultist", CreatureRank.Normal, 1)], Radius: 0);
        var world = World(Field(Open, 2, pack), creatures);
        (world.Hero.X, world.Hero.Y) = (4.5, 3.5);
        var hero = world.Hero.Character;
        var poisoned = false;
        for (var t = 0.0; t < 20 && !poisoned && !hero.IsDead; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            poisoned = hero.Persistent.Types.Contains(DamageType.Poison);
        }
        Assert.True(poisoned);
        Assert.Equal("1d2", hero.Persistent.DiceFor(DamageType.Poison)!.ToString());
    }

    [Fact]
    public void The_fens_creatures_are_well_formed()
    {
        var crawler = Content.Creatures["plague_crawler"];
        Assert.Equal("antlion_small", crawler.Sprite);
        Assert.NotNull(crawler.Tint);
        Assert.True(crawler.Flanks);
        foreach (var id in new[] { "cultist", "cult_chanter" })
        {
            var c = Content.Creatures[id];
            Assert.NotNull(c.Look);
            Assert.All(c.Look!.Values, layer => Assert.Matches("^[a-z_0-9]+$", layer));
        }
        Assert.True(Content.Creatures["cult_chanter"].Caster);
        Assert.True(Content.Creatures["cult_chanter"].Ranged);
    }
}
