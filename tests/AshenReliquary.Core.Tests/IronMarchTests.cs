using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 2: Ironhold and the Iron March. The zones and the road between the acts, cover against shots, crossbowmen
/// that kite, and the Brothers of the Ferry's payoff at Sable's table.
/// </summary>
public class IronMarchTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld World(string zoneId, Characters.Character? hero = null) =>
        new(Content.Zone(zoneId), Content.Creatures, hero ?? TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    /// <summary>Picks the answer whose text contains <paramref name="words"/>.</summary>
    private static void Say(GameWorld world, string words)
    {
        var choices = world.Dialogue!.Choices;
        var index = choices.ToList().FindIndex(c => c.Text.Contains(words));
        Assert.True(index >= 0, $"no answer with \"{words}\" among: {string.Join(" | ", choices.Select(c => c.Text))}");
        world.Choose(index);
    }

    // ----- The zones -----------------------------------------------------------------

    [Theory]
    [InlineData("iron_march")]
    [InlineData("ironhold")]
    public void Act_two_zones_load_and_everything_in_them_can_be_walked_to(string zoneId)
    {
        var zone = Content.Zone(zoneId);
        Assert.Equal(2, zone.Act);
        Assert.Equal("tileset_ironhold", zone.Tileset);
        Assert.True(zone.Outdoors);
        var map = new TileMap(zone);
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), m => Assert.True(Content.Creatures.ContainsKey(m.Creature), m.Creature));
        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        Assert.False(map.IsBlocked(from.Item1, from.Item2));
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        var places = zone.Spawns.Select(s => ("pack", s.At))
            .Concat((zone.Caches ?? []).Select(c => (c.Id, c.At)))
            .Concat(zone.Shrines.Select(s => (s.Id, s.At)))
            .Concat(zone.Exits.Select(e => (e.Id, e.At)))
            .Concat(zone.Exits.Where(e => e.Arrive is not null).Select(e => (e.Id + " arrival", e.Arrive!)))
            .Concat((zone.Npcs ?? []).Select(n => (n.Id, n.At)))
            .Concat((zone.Vendors ?? []).Select(v => (v.Id, v.At)))
            .Concat((zone.Interactables ?? []).Select(i => (i.Id, i.At)));
        foreach (var (name, at) in places)
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 200000) is not null, $"{name} at {at[0]},{at[1]} can't be reached");
    }

    [Fact]
    public void The_march_has_squads_cover_kiters_three_shrines_and_varro_at_the_gate()
    {
        var zone = Content.Zone("iron_march");
        var map = new TileMap(zone);
        Assert.Equal(3, zone.Shrines.Count);
        Assert.InRange(zone.Spawns.Count, 24, 32);
        Assert.InRange(zone.Caches!.Count, 8, 10);
        var cover = Enumerable.Range(0, map.Height).Sum(y => Enumerable.Range(0, map.Width).Count(x => map.IsCover(x, y)));
        Assert.True(cover > 100, $"only {cover} cover cells");
        Assert.True(Content.Creatures["legion_crossbowman"].Kites);
        Assert.True(Content.Creatures["outlaw_bowman"].Kites);
        var varro = zone.Spawns.Single(s => s.Pack.Any(m => m.Creature == "centurion_varro"));
        Assert.Equal("act2.varro", varro.Flag);
        Assert.True(varro.At[1] < 30, "Varro holds the siege line at the north end");
    }

    [Fact]
    public void The_road_runs_from_the_mire_through_the_march_to_ironhold_and_back()
    {
        var world = World("sallows_mire");
        world.SetFlag("act1.sallow", "slain");
        var gate = world.Zone.Exits.Single(e => e.Id == "iron_march");
        (world.Hero.X, world.Hero.Y) = (gate.Arrive![0] + 0.5, gate.Arrive[1] + 0.5);
        world.MoveTo(gate.At[0] + 0.5, gate.At[1] + 0.5);
        Run(world, 4);
        Assert.Equal("iron_march", world.Zone.Id);
        Assert.Equal("The Iron March", world.Arrival?.Title);
        foreach (var m in world.Monsters.ToList())
            world.Slay(m);

        var north = world.Zone.Exits.Single(e => e.Id == "ironhold");
        (world.Hero.X, world.Hero.Y) = (north.Arrive![0] + 0.5, north.Arrive[1] + 0.5);
        world.MoveTo(north.At[0] + 0.5, north.At[1] + 0.5);
        Run(world, 4);
        Assert.Equal("ironhold", world.Zone.Id);
        Assert.Equal("Ironhold", world.Arrival?.Title);

        var back = world.Zone.Exits.Single(e => e.Id == "march_gate");
        world.MoveTo(back.At[0] + 0.5, back.At[1] + 0.5);
        Run(world, 6);
        Assert.Equal("iron_march", world.Zone.Id);
        Assert.True(world.Hero.Y < 12, "back on the March, at its north gate");
    }

    [Fact]
    public void Ironholds_north_gate_is_barred_for_now()
    {
        var world = World("ironhold");
        var north = world.Zone.Exits.Single(e => e.Id == "north_gate");
        (world.Hero.X, world.Hero.Y) = (north.At[0] + 0.5, north.At[1] + 4.5);
        world.MoveTo(north.At[0] + 0.5, north.At[1] + 0.5);
        Run(world, 4);
        Assert.Equal("ironhold", world.Zone.Id);
        Assert.Contains("barred", world.Arrival?.Subtitle);
    }

    // ----- Cover ---------------------------------------------------------------------

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_ironhold",
        [
            "TTTTTTTTTTTTTTTTTTTTTTTT",
            "T......................T",
            "T......................T",
            "T..........u...........T",
            "T..........u...........T",
            "T..........u...........T",
            "T......................T",
            "T......................T",
            "TTTTTTTTTTTTTTTTTTTTTTTT",
        ],
        new Dictionary<string, LegendEntry>
        {
            ["."] = new("grass"),
            ["u"] = new("grass", [1187], Blocks: true, Cover: true),
            ["T"] = new("grass", [248], Blocks: true, BlocksSight: true),
        },
        PlayerStart: [13, 4], spawns, Shrines: [], Exits: []);

    [Fact]
    public void A_shot_across_a_low_wall_at_someone_close_behind_it_is_against_cover()
    {
        var map = new TileMap(Field());
        Assert.True(map.IsCover(11, 4));
        Assert.True(map.HasLineOfSight(4.5, 4.5, 12.5, 4.5));         // seen over
        Assert.True(map.InCover(4.5, 4.5, 12.5, 4.5));                // the hero just behind the wall
        Assert.False(map.InCover(4.5, 4.5, 17.5, 4.5));               // too far behind it to shelter
        Assert.False(map.InCover(10.2, 4.5, 12.5, 4.5));              // shot from right over the wall
        Assert.False(map.InCover(4.5, 1.5, 12.5, 1.5));               // past the wall's end
        Assert.False(map.InCover(19.5, 4.5, 12.5, 4.5));              // from the hero's own side

        var world = new GameWorld(Field(), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot);
        (world.Hero.X, world.Hero.Y) = (12.5, 4.5);
        Assert.Equal(GameWorld.CoverAcBonus, world.CoverAgainst(4.5, 4.5, world.Hero));
        Assert.Equal(0, world.CoverAgainst(19.5, 4.5, world.Hero));
    }

    [Fact]
    public void Crossbow_bolts_across_cover_miss_more_often()
    {
        int Hits(double fromX)
        {
            var hits = 0;
            for (var seed = 0; seed < 30; seed++)
            {
                var spawn = new SpawnDefinition([(int)fromX, 4], [new PackMember("legion_crossbowman")], Radius: 0, Level: 1);
                var world = new GameWorld(Field(spawn), Content.Creatures, TestCharacters.Fighter(), seed, Content.AbilityBar("fighter"), Content.Loot);
                (world.Hero.X, world.Hero.Y) = (12.5, 4.5);
                var start = world.Hero.Character.HitPoints;
                for (var t = 0.0; t < 2.5 && world.Hero.Character.HitPoints == start; t += 1 / 30.0)
                    world.Update(1 / 30.0);
                if (world.Hero.Character.HitPoints < start)
                    hits++;
            }
            return hits;
        }
        // The same range either side of the hero: from the west the bolt crosses the wall, from the east it doesn't.
        var behindCover = Hits(5);
        var inTheOpen = Hits(20);
        Assert.True(behindCover < inTheOpen, $"behind cover {behindCover}/30 hit, in the open {inTheOpen}/30");
    }

    // ----- Kiting --------------------------------------------------------------------

    [Fact]
    public void A_crossbowman_falls_back_when_the_hero_closes_then_stands_its_ground()
    {
        var spawn = new SpawnDefinition([15, 6], [new PackMember("legion_crossbowman")], Radius: 0, Level: 1);
        var world = new GameWorld(Field(spawn), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot);
        var archer = world.Monsters.Single();
        (world.Hero.X, world.Hero.Y) = (archer.X - 2, archer.Y);
        Run(world, 1.2);
        Assert.True(archer.DistanceTo(world.Hero) > 3, $"it should have fallen back (at {archer.DistanceTo(world.Hero):0.0} tiles)");

        // Dogged: the hero keeps stepping up beside it. It tires of running and stands.
        for (var t = 0.0; t < GameWorld.KiteSeconds + 2; t += 1 / 30.0)
        {
            if (archer.DistanceTo(world.Hero) > 1.5)
                (world.Hero.X, world.Hero.Y) = (archer.X - 1.2, archer.Y);
            world.Update(1 / 30.0);
        }
        Assert.True(archer.KiteTime >= GameWorld.KiteSeconds);
        var (x, y) = (archer.X, archer.Y);
        Run(world, 1);
        Assert.True(Math.Abs(archer.X - x) + Math.Abs(archer.Y - y) < 0.5, "it stands its ground once tired");
    }

    // ----- Ironhold's people ---------------------------------------------------------

    [Theory]
    [InlineData("", 1.0, 0)]
    [InlineData("brothers", 0.8, 2)]
    [InlineData("lantern", 1.25, 0)]
    public void Sables_terms_follow_act_ones_ferry_choice(string ferry, double scale, int rares)
    {
        var world = World("ironhold");
        if (ferry.Length > 0)
            world.SetFlag("act1.ferry", ferry);
        var sable = world.Vendors.Single(v => v.Definition.Id == "sable");
        Assert.Equal(scale, world.TermsOf(sable)?.PriceScale ?? 1.0);
        world.GainXp(1000, world.Hero);                               // a new level brings new stock, on today's terms
        Assert.Equal(rares, sable.Stock.Count(i => i.Rarity == Rarity.Rare));

        (world.Hero.X, world.Hero.Y) = (sable.X - 1, sable.Y + 0.6);
        world.TalkTo(sable);
        Assert.Equal(sable, world.OpenVendor);
        var item = sable.Stock[^1];
        var list = Pricing.BuyPrice(item, world.Hero.Character);
        Assert.InRange(world.BuyPrice(item), (int)Math.Floor(list * scale) - 1, (int)Math.Ceiling(list * scale) + 1);
        if (ferry == "brothers")
            Assert.Contains("Brother of the Ferry", world.LastMessage);
    }

    [Fact]
    public void Varros_fall_sets_the_flag_and_captain_thale_pays_for_it_once()
    {
        var march = World("iron_march");
        var index = march.Zone.Spawns.ToList().FindIndex(s => s.Flag == "act2.varro");
        foreach (var m in march.Monsters.Where(m => m.PackId == index + 1).ToList())
            march.Slay(m);
        Assert.Equal("slain", march.Flags["act2.varro"]);

        var world = World("ironhold");
        var thale = world.Interactives.Single(i => i.Id == "thale");
        world.Interact(thale);
        Run(world, 6);
        Assert.Contains(world.Journal, e => e.Id == "act2_ironhold");
        Assert.DoesNotContain(world.Dialogue!.Choices, c => c.Text.Contains("Varro is dead"));
        world.EndDialogue();

        world.SetFlag("act2.varro", "slain");
        var gold = world.Hero.Equipment.Gold;
        world.Interact(thale);
        Say(world, "Varro is dead");
        Assert.Equal(gold + 300, world.Hero.Equipment.Gold);
        Assert.Equal("given", world.Flags["act2.thale_reward"]);
        world.EndDialogue();
        world.Interact(thale);
        Assert.DoesNotContain(world.Dialogue!.Choices, c => c.Text.Contains("Varro is dead"));
    }

    // ----- Loot and saves ------------------------------------------------------------

    [Fact]
    public void Act_two_drops_roll_on_act_twos_shares()
    {
        int Rares(int act) => Enumerable.Range(0, 3000).Count(seed =>
            Content.Loot.Roll(CreatureRank.Normal, 6, new SeededRandom(seed), act: act).Any(d => d.Item is { Rarity: >= Rarity.Rare, Definition.IsBeltItem: false }));
        Assert.True(Rares(2) > Rares(1) * 1.5, $"act 2: {Rares(2)} rare drops, act 1: {Rares(1)}");
    }

    [Fact]
    public void A_save_made_in_ironhold_comes_back_there_with_its_flags()
    {
        var world = World("ironhold");
        world.SetFlag("act2.thale", "met");
        var save = SaveGame.Capture(world, "ironhold", 3, null);
        var loaded = SaveGame.FromJson(save.ToJson()).Restore(Content);
        Assert.Equal("ironhold", loaded.Zone.Id);
        Assert.Equal(2, loaded.Zone.Act);
        Assert.Equal("met", loaded.Flags["act2.thale"]);
    }

    [Fact]
    public void The_battle_demo_trains_its_hero_to_the_zones_level()
    {
        var world = World("iron_march");
        world.TrainTo(world.Zone.Level);
        Assert.Equal(6, world.Hero.Character.Level);
        Assert.Equal([2, 3, 4, 5, 6], world.Hero.Character.PendingLevels);
    }
}
