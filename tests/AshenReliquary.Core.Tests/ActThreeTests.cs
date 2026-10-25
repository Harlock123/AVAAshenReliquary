using System.Reflection;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 6: Act 3's zones (the Frostbitten Pass, the Hollow Woods, the Catacombs of Oriel) and their mechanics: cold
/// and blizzards, crowd-control casters, the unseen and illusions, domination; the Library of Ash and the Penitent.
/// </summary>
public class ActThreeTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static readonly string[] Zones = ["frostbitten_pass", "hollow_woods", "catacombs_of_oriel"];

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld In(string zoneId, Characters.Character? hero = null)
    {
        hero ??= TestCharacters.Fighter();
        return new GameWorld(Content.Zone(zoneId), Content.Creatures, hero, 3, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones,
            Content.Dialogues, Content.Abilities);
    }

    /// <summary>An open snowfield, cold or not, with a campfire at (3, 3).</summary>
    private static GameWorld Field(SpawnDefinition[] spawns, Characters.Character? hero = null, bool cold = false)
    {
        hero ??= TestCharacters.Fighter();
        var zone = new ZoneDefinition("field", "Field", 12, "tileset_oriel",
            [
                "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
                "C............................C",
                "C............................C",
                "C..c.........................C",
                "C............................C",
                "C............................C",
                "C............................C",
                "C............................C",
                "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            ],
            new Dictionary<string, LegendEntry>
            {
                ["."] = new("grass"),
                ["C"] = new("grass", [48], Blocks: true, BlocksSight: true),
                ["c"] = new("grass", [102], Blocks: true, Light: 3.5),
            },
            PlayerStart: [20, 4], spawns, Shrines: [], Exits: [], Act: 3, Cold: cold);
        return new GameWorld(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, abilities: Content.Abilities);
    }

    private static SpawnDefinition At(string creature, int x, int y, int level = 12) => new([x, y], [new PackMember(creature)], Radius: 0, Level: level);

    private static void Invoke(GameWorld world, string method, params object[] args) =>
        typeof(GameWorld).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, args);

    // ----- The zones -----------------------------------------------------------------

    [Fact]
    public void The_three_zones_are_act_three_s_and_everything_in_them_can_be_walked_to()
    {
        foreach (var id in Zones)
        {
            var zone = Content.Zone(id);
            Assert.Equal(3, zone.Act);
            var map = new TileMap(zone);
            var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
            (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
                ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) }
                    .Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
                : (at[0], at[1]);
            var places = zone.Shrines.Select(s => (s.Id, s.At))
                .Concat(zone.Exits.Select(e => (e.Id, e.At)))
                .Concat((zone.Npcs ?? []).Select(n => (n.Id, n.At)))
                .Concat((zone.Interactables ?? []).Select(i => (i.Id, i.At)))
                .Concat((zone.Caches ?? []).Select(c => (c.Id, c.At)))
                .Concat(zone.Spawns.Select((s, i) => ($"pack {i}", s.At)));
            foreach (var (name, at) in places)
                Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 200000) is not null, $"{id}: {name} can't be reached");
            Assert.Equal(3, zone.Shrines.Count);
            Assert.All(zone.Spawns.SelectMany(s => s.Pack), p => Assert.True(Content.Creatures.ContainsKey(p.Creature), p.Creature));
        }
        Assert.True(Content.Zone("frostbitten_pass").Cold);
        Assert.False(Content.Zone("hollow_woods").Cold);
        Assert.True(Content.Zone("catacombs_of_oriel").IsDark);
    }

    [Fact]
    public void The_road_runs_siege_walls_pass_rest_woods_catacombs_and_the_bell_tower_is_barred()
    {
        (string To, string? ToExit) Way(string zone, string exit)
        {
            var e = Content.Zone(zone).Exits.Single(x => x.Id == exit);
            return (e.To!, e.ToExit);
        }
        Assert.Equal(("frostbitten_pass", "siege_walls"), Way("siege_walls", "heights"));
        Assert.Equal(("siege_walls", "heights"), Way("frostbitten_pass", "siege_walls"));
        Assert.Equal(("saint_oriels_rest", "south_road"), Way("frostbitten_pass", "rest"));
        Assert.Equal(("frostbitten_pass", "rest"), Way("saint_oriels_rest", "south_road"));
        Assert.Equal(("hollow_woods", "rest"), Way("saint_oriels_rest", "north_door"));
        Assert.Equal(("saint_oriels_rest", "north_door"), Way("hollow_woods", "rest"));
        Assert.Equal(("catacombs_of_oriel", "woods"), Way("hollow_woods", "catacombs"));
        Assert.Equal(("hollow_woods", "catacombs"), Way("catacombs_of_oriel", "woods"));

        var world = In("catacombs_of_oriel");
        var stair = world.Zone.Exits.Single(e => e.Id == "bell_tower");
        foreach (var m in world.Monsters.ToList())
            world.Slay(m);
        world.SetFlag("act3.bell_tower", "");                   // as if the Hollow Abbot still stood (ChoirTests opens it)
        (world.Hero.X, world.Hero.Y) = (stair.At[0] + 0.5, stair.At[1] - 3.5);
        world.MoveTo(stair.At[0] + 0.5, stair.At[1] + 0.5);
        Run(world, 3);
        Assert.Equal("catacombs_of_oriel", world.Zone.Id);
        Assert.Contains("Bell Tower", world.Arrival?.Subtitle);
    }

    // ----- Cold ----------------------------------------------------------------------

    [Fact]
    public void Out_in_the_cold_exposure_rises_and_a_fire_brings_it_down()
    {
        var world = Field([], cold: true);
        Run(world, 10);
        Assert.InRange(world.Hero.Cold, GameWorld.ColdPerSecond * 9.5, GameWorld.ColdPerSecond * 10.5);
        Assert.False(world.Blizzard);
        (world.Hero.X, world.Hero.Y) = (5.5, 4.5);                              // beside the campfire
        Run(world, 2);
        Assert.Equal(0, world.Hero.Cold);
        var warm = Field([]);                                                   // a zone that isn't cold
        Run(warm, 10);
        Assert.Equal(0, warm.Hero.Cold);
    }

    [Fact]
    public void A_blizzard_triples_the_cold_and_full_exposure_freezes_the_hero()
    {
        var world = Field([], cold: true);
        (world.Hero.X, world.Hero.Y) = (5.5, 4.5);
        Run(world, GameWorld.BlizzardEverySeconds + 0.5);
        Assert.True(world.Blizzard);
        Assert.Contains("blizzard", world.LastMessage, StringComparison.OrdinalIgnoreCase);
        (world.Hero.X, world.Hero.Y) = (20.5, 4.5);
        Run(world, 4);
        Assert.InRange(world.Hero.Cold, GameWorld.ColdPerSecond * GameWorld.BlizzardColdScale * 3.5, GameWorld.ColdPerSecond * GameWorld.BlizzardColdScale * 4.5);
        Run(world, GameWorld.BlizzardSeconds);
        Assert.False(world.Blizzard);

        world.Hero.Cold = 60;
        Run(world, 0.1);
        Assert.True(world.Hero.Character.Conditions.Has(ConditionKind.Clumsy));
        world.Hero.Cold = 100;
        var lowest = world.Hero.Character.HitPoints;
        for (var t = 0.0; t < 20; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            lowest = Math.Min(lowest, world.Hero.Character.HitPoints);
        }
        Assert.True(lowest < world.Hero.Character.MaxHitPoints, "freezing never hurt");
    }

    // ----- Crowd control -------------------------------------------------------------

    [Fact]
    public void A_crowd_control_caster_s_hits_lay_its_condition_on_a_failed_save()
    {
        var world = Field([At("frost_hexer", 26, 4, level: 14)]);
        var hexer = world.Monsters.Single();
        Assert.True(hexer.Definition.Kites && hexer.Definition.Caster);
        var slowed = 0;
        for (var i = 0; i < 40; i++)
        {
            world.Hero.Character.Conditions.Remove(ConditionKind.Slowed);
            Invoke(world, "Inflict", hexer);
            if (world.Hero.Character.Conditions.Has(ConditionKind.Slowed))
                slowed++;
        }
        Assert.InRange(slowed, 1, 39);                                          // a save, not a certainty
        Assert.Equal(ConditionKind.Frightened, Content.Creatures["whisperer"].Inflicts!.Condition);
        Assert.Equal(ConditionKind.Stupefied, Content.Creatures["choir_acolyte"].Inflicts!.Condition);
    }

    // ----- The unseen and illusions --------------------------------------------------

    [Fact]
    public void An_unseen_stalker_can_t_be_targeted_until_it_strikes_or_is_spotted()
    {
        var world = Field([At("hollow_stalker", 27, 4)]);
        var stalker = world.Monsters.Single();
        Assert.True(world.IsUnseen(stalker));
        Assert.False(world.CanTarget(stalker));
        Assert.Null(world.MonsterAt(stalker.X, stalker.Y));
        // It comes for the hero and, striking, shows itself.
        var seen = false;
        for (var t = 0.0; t < 8 && !seen; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            seen = world.CanTarget(stalker);
        }
        Assert.True(seen);
        Assert.True(stalker.RevealedUntil > world.Time);
        // Out of sight again a while later.
        world.Stop();
        (stalker.X, stalker.Y) = (27.5, 6.5);
        stalker.RevealedUntil = 0;
        Assert.False(world.CanTarget(stalker));
    }

    [Fact]
    public void A_burst_reveals_the_unseen_it_catches()
    {
        var world = Field([At("hollow_stalker", 24, 4)], TestCharacters.Wizard());
        var stalker = world.Monsters.Single();
        world.TrainTo(5);
        Invoke(world, "RevealMonster", stalker, GameWorld.RevealSeconds);
        Assert.True(world.CanTarget(stalker));
        stalker.RevealedUntil = 0;
        // The Perception check: standing near, the hero makes it out sooner or later.
        (world.Hero.X, world.Hero.Y) = (stalker.X - 3, stalker.Y);
        world.Stop();
        var spotted = false;
        for (var t = 0.0; t < 30 && !spotted; t += 1 / 30.0)
        {
            Invoke(world, "UpdateVeil", stalker, 1 / 30.0);
            spotted = world.CanTarget(stalker);
        }
        Assert.True(spotted);
    }

    [Fact]
    public void An_illusion_seen_through_or_struck_dissolves_and_is_worth_nothing()
    {
        var dissolved = 0;
        var stood = 0;
        for (var seed = 0; seed < 12; seed++)
        {
            var world = Field([At("phantom", 25, 4, level: 12 + seed % 3)]);
            var phantom = world.Monsters.Single();
            Invoke(world, "UpdateVeil", phantom, 1 / 30.0);
            if (phantom.IsDead)
                dissolved++;
            else
            {
                stood++;
                var xp = world.Experience.Xp;
                Invoke(world, "DamageMonster", phantom, 1);
                Assert.True(phantom.IsDead);
                Assert.Equal(xp, world.Experience.Xp);
                Assert.Empty(world.GroundItems);
            }
        }
        Assert.True(dissolved + stood == 12);
        Assert.True(stood > 0);
    }

    // ----- Domination ----------------------------------------------------------------

    [Fact]
    public void A_dominator_turns_the_hero_s_wolf_against_them_for_a_while()
    {
        var world = Field([At("choir_acolyte", 24, 4, level: 16)], TestCharacters.Ranger("beastmaster"));
        var acolyte = world.Monsters.Single();
        var wolf = world.Companion!;
        (wolf.X, wolf.Y) = (world.Hero.X + 1, world.Hero.Y);
        acolyte.State = MonsterState.Chase;
        var turned = false;
        for (var i = 0; i < 20 && !turned; i++)
        {
            acolyte.DominateIn = 0;
            Invoke(world, "UpdateDomination", acolyte, 0.0);
            turned = wolf.IsTurned(world.Time);
        }
        Assert.True(turned);
        Assert.Equal(world.Time + GameWorld.TurnedSeconds, wolf.TurnedUntil, 3);
        // A turned wolf goes for the hero, and the monsters leave it be.
        world.Slay(acolyte);
        var hp = world.Hero.Character.HitPoints;
        Run(world, GameWorld.TurnedSeconds - 0.5);
        Assert.True(world.Hero.Character.HitPoints < hp, "the turned wolf never bit");
        Run(world, 1);
        Assert.False(wolf.IsTurned(world.Time));
    }

    [Fact]
    public void With_no_ally_to_turn_a_dominator_frightens_the_hero()
    {
        var world = Field([At("choir_acolyte", 24, 4, level: 16)]);
        var acolyte = world.Monsters.Single();
        acolyte.State = MonsterState.Chase;
        var frightened = false;
        for (var i = 0; i < 20 && !frightened; i++)
        {
            acolyte.DominateIn = 0;
            Invoke(world, "UpdateDomination", acolyte, 0.0);
            frightened = world.Hero.Character.Conditions.Has(ConditionKind.Frightened);
        }
        Assert.True(frightened);
    }

    // ----- The Library of Ash --------------------------------------------------------

    [Fact]
    public void Seven_burned_pages_across_the_three_zones_make_the_chronicle_whole()
    {
        var pages = Zones.SelectMany(z => (Content.Zone(z).Interactables ?? []).Where(i => i.Id.StartsWith("page_"))).ToList();
        Assert.Equal(GameWorld.LibraryPages, pages.Count);
        var world = In("frostbitten_pass");
        void Read(int n)
        {
            var dialogue = Content.Dialogues[$"library_page_{n}"];
            typeof(GameWorld).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(world, [dialogue.Nodes["start"].Effects![0]]);
        }
        for (var n = 1; n < GameWorld.LibraryPages; n++)
            Read(n);
        Assert.Contains("6 of the seven", world.Journal.Single(e => e.Id == "act3_library").Text);
        Assert.False(world.Flags.ContainsKey("act3.library"));
        var xp = world.Experience.Xp;
        Read(GameWorld.LibraryPages);
        Assert.Equal("complete", world.Flags["act3.library"]);
        Assert.Equal("granted", world.Flags[GameWorld.BoonFlag("ashurs_chronicle")]);
        Assert.True(world.Experience.Xp >= xp + GameWorld.LibraryXp);
        Assert.Equal(5, world.Hero.Character.Defenses.Resistances[DamageType.Fire]);
        Assert.Contains("shepherd", world.Journal.Single(e => e.Id == "act3_library").Text);
    }

    [Fact]
    public void Reading_a_page_in_the_pass_writes_the_library_into_the_journal()
    {
        var world = In("frostbitten_pass");
        foreach (var m in world.Monsters.ToList())
            world.Slay(m);
        var page = world.Interactives.Single(i => i.Id == "page_1");
        (world.Hero.X, world.Hero.Y) = (page.X, page.Y + 1.2);
        world.Interact(page);
        Run(world, 1);
        Assert.Equal("read", world.Flags["act3.page.1"]);
        Assert.Contains("1 of the seven", world.Journal.Single(e => e.Id == "act3_library").Text);
    }

    // ----- The Penitent --------------------------------------------------------------

    private static void Talk(GameWorld world, string id)
    {
        var who = world.Interactives.Single(i => i.Id == id);
        foreach (var m in world.Monsters.Where(m => m.DistanceTo(who.X, who.Y) < 14).ToList())
            world.Slay(m);
        (world.Hero.X, world.Hero.Y) = (who.X, who.Y + 1.2);
        world.Interact(who);
        Run(world, 1);
    }

    [Fact]
    public void Caddoc_goes_ahead_to_the_woods_and_confesses_in_the_catacombs()
    {
        var world = In("saint_oriels_rest");
        Talk(world, "caddoc");
        Assert.Equal("met", world.Flags["act3.caddoc"]);
        world.EndDialogue();
        var flags = world.Flags.ToDictionary();

        GameWorld With(string zone)
        {
            var w = In(zone);
            foreach (var (k, v) in flags)
                w.SetFlag(k, v);
            Invoke(w, "PopulateInteractives", w.Zone);
            return w;
        }
        Assert.DoesNotContain(With("saint_oriels_rest").Interactives, i => i.Id.StartsWith("caddoc"));   // he has gone ahead
        var woods = With("hollow_woods");
        Talk(woods, "caddoc");
        Assert.Contains("hermitage", woods.Journal.Concat(world.Journal).Last(e => e.Id == "act3_penitent").Text);

        var catacombs = With("catacombs_of_oriel");
        Talk(catacombs, "caddoc");
        Assert.Contains("hymn", catacombs.Dialogue!.Text);
        catacombs.Choose(catacombs.Dialogue.Choices.ToList().FindIndex(c => c.Text.Contains("forgive")));
        Assert.Equal("forgiven", catacombs.Flags["act3.penitent"]);
        Assert.Equal("granted", catacombs.Flags[GameWorld.BoonFlag("penitents_grace")]);
        flags = catacombs.Flags.ToDictionary();
        Assert.DoesNotContain(With("catacombs_of_oriel").Interactives, i => i.Id == "caddoc");
        Assert.Contains(With("saint_oriels_rest").Interactives, i => i.Id == "caddoc_back");
    }

    [Fact]
    public void Banished_caddoc_leaves_and_does_not_come_back_to_the_rest()
    {
        var world = In("catacombs_of_oriel");
        world.SetFlag("act3.caddoc", "met");
        Invoke(world, "PopulateInteractives", world.Zone);
        Talk(world, "caddoc");
        world.Choose(world.Dialogue!.Choices.ToList().FindIndex(c => c.Text.Contains("Go,")));
        Assert.Equal("banished", world.Flags["act3.penitent"]);
        Assert.Contains("sent him away", world.Journal.Single(e => e.Id == "act3_penitent").Text);
        var rest = In("saint_oriels_rest");
        foreach (var (k, v) in world.Flags)
            rest.SetFlag(k, v);
        Invoke(rest, "PopulateInteractives", rest.Zone);
        Assert.DoesNotContain(rest.Interactives, i => i.Id.StartsWith("caddoc"));
    }

    // ----- The art -------------------------------------------------------------------

    [Fact]
    public void The_new_families_use_flare_s_minotaur_and_ice_ant_and_ghosts_are_translucent()
    {
        Assert.Equal("minotaur", Content.Creatures["horned_guardian"].Sprite);
        Assert.Equal("ice_ant", Content.Creatures["rime_crawler"].Sprite);
        Assert.InRange(Content.Creatures["frost_wraith"].Alpha, 0.3, 0.8);
        Assert.True(Content.Creatures["hollow_stalker"].Unseen);
        Assert.True(Content.Creatures["phantom"].Illusion);
        Assert.True(Content.Creatures["choir_acolyte"].Dominates);
    }
}
