using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 8: Act 4's camp and zones (the Ember Gate, the Ash Wastes, the Lava Rivers, the Wyrm's Nest, the Reliquary),
/// their mechanics (ash storms, lava and embers, pack leaders, wards), the Herald Echoes and Act 4's two side stories
/// (the Wyrm's Egg, the Last Lantern).
/// </summary>
public class ActFourTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static readonly string[] Zones = ["ember_gate", "ash_wastes", "lava_rivers", "wyrms_nest", "reliquary"];

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld In(string zoneId, ZoneDefinition? zone = null) => new(zone ?? Content.Zone(zoneId), Content.Creatures,
        TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    /// <summary>A zone made fresh with these world flags (as a load brings them back).</summary>
    private static GameWorld InWith(string zoneId, params (string Flag, string Value)[] flags)
    {
        var world = In(zoneId);
        world.RestoreStory(flags.Select(f => new KeyValuePair<string, string>(f.Flag, f.Value)), []);
        return world;
    }

    private static GameWorld Field(int level, string ground = ".", bool storms = false, params SpawnDefinition[] spawns)
    {
        var zone = new ZoneDefinition("field", "Field", level, "tileset_cinder", Enumerable.Repeat(new string(ground[0], 40), 20).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("ash"), ["e"] = new("embers") }, PlayerStart: [3, 10], spawns,
            Shrines: [], Exits: [], Act: 4, AshStorms: storms);
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 5, Content.AbilityBar("fighter"), Content.Loot);
        world.TrainTo(level);
        world.Hero.Character.CannotFall = true;
        return world;
    }

    // ----- The zones -----------------------------------------------------------------

    [Theory]
    [InlineData("ember_gate")]
    [InlineData("ash_wastes")]
    [InlineData("lava_rivers")]
    [InlineData("wyrms_nest")]
    [InlineData("reliquary")]
    public void Each_zone_is_act_four_s_and_everything_in_it_can_be_walked_to(string id)
    {
        var zone = Content.Zone(id);
        Assert.Equal(4, zone.Act);
        Assert.True(zone.Ashen);
        var map = new TileMap(zone);                                       // the wards are the world's: open here
        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) }
                .Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        var places = zone.Shrines.Select(s => (s.Id, s.At))
            .Concat(zone.Exits.Select(e => (e.Id, e.At)))
            .Concat((zone.Npcs ?? []).Select(n => (n.Id, n.At)))
            .Concat((zone.Vendors ?? []).Select(v => (v.Id, v.At)))
            .Concat((zone.Interactables ?? []).Select(i => (i.Id, i.At)))
            .Concat((zone.Caches ?? []).Select(c => (c.Id, c.At)))
            .Concat(zone.Spawns.Select((s, i) => ($"pack {i}", s.At)));
        foreach (var (name, at) in places)
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 400000) is not null, $"{id}: {name} can't be reached");
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), p => Assert.True(Content.Creatures.ContainsKey(p.Creature), p.Creature));
        Assert.All(zone.Interactables ?? [], i => Assert.True(i.Dialogue is null || Content.Dialogues.ContainsKey(i.Dialogue), i.Id));
        Assert.All(zone.Npcs ?? [], n => Assert.True(Content.Dialogues.ContainsKey(n.Dialogue), n.Id));
    }

    [Fact]
    public void The_road_runs_from_the_bell_tower_to_the_reliquary_and_climbs_a_level_a_zone()
    {
        string? Next(string zone, string exit) => Content.Zone(zone).Exits.Single(e => e.Id == exit).To;
        Assert.Equal("ember_gate", Next("bell_tower", "cliff_road"));
        Assert.Equal("ash_wastes", Next("ember_gate", "wastes"));
        Assert.Equal("lava_rivers", Next("ash_wastes", "rivers"));
        Assert.Equal("wyrms_nest", Next("lava_rivers", "nest"));
        Assert.Equal("reliquary", Next("wyrms_nest", "reliquary"));
        Assert.Equal("slain", Content.Zone("wyrms_nest").Exits.Single(e => e.Id == "reliquary").Requires!["act4.vorthrax"]);
        Assert.Equal([16, 16, 17, 18, 19], Zones.Select(z => Content.Zone(z).Level));
        Assert.True(Content.Zone("ember_gate").Town);
        Assert.True(Content.Zone("ash_wastes").AshStorms);
        Assert.True(Content.Zone("wyrms_nest").IsDark && Content.Zone("reliquary").IsDark);
        Assert.False(Content.Zone("ash_wastes").IsDark);
        // Act 4's pack mix: every pack in the Reliquary is led by an elite (the Echoes aside).
        Assert.All(Content.Zone("reliquary").Spawns.Where(s => s.Pack.All(m => !m.Echo)),
            s => Assert.Contains(s.Pack, m => m.Leads && m.Rank == CreatureRank.Elite));
    }

    // ----- Lava and embers -----------------------------------------------------------

    [Fact]
    public void Lava_is_shaped_by_its_corners_blocked_but_for_bridges_and_embers_can_be_walked()
    {
        var zone = Content.Zone("lava_rivers");
        var map = new TileMap(zone);
        int lava = 0, bridges = 0, embers = 0;
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
        {
            var key = zone.Rows[y][x].ToString();
            if (map.IsLava(x, y))
            {
                lava++;
                Assert.InRange(map.Ground(x, y), TileMap.LavaTile, TileMap.EmberTile + 3);
                if (zone.Legend[key].Bridge)
                {
                    bridges++;
                    Assert.False(map.IsBlocked(x, y));
                }
                else
                {
                    Assert.True(map.IsBlocked(x, y));
                }
            }
            if (map.IsEmbers(x, y))
            {
                embers++;
                Assert.False(map.IsBlocked(x, y));
                Assert.Equal(TileMap.EmberStepCost, map.StepCost(x, y));
            }
        }
        Assert.True(lava > 300 && bridges >= 9 && embers > 20, $"{lava} lava, {bridges} bridge, {embers} ember cells");
        // Open lava (all four corners) shows its own tile, by the cell's parity.
        Assert.Contains(Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x, y))),
            c => map.Ground(c.x, c.y) == TileMap.LavaTile + 60 + (c.x & 1) + 2 * (c.y & 1));
    }

    [Fact]
    public void Embers_burn_and_fire_resistance_takes_the_heat_out_of_them()
    {
        var world = Field(17, "e");
        var c = world.Hero.Character;
        var hp = c.HitPoints;
        Run(world, 8);
        Assert.True(c.HitPoints < hp, "embers never burned");

        var cool = Field(17, ".");
        var before = cool.Hero.Character.HitPoints;
        Run(cool, 8);
        Assert.Equal(before, cool.Hero.Character.HitPoints);

        var shod = Field(17, "e");
        shod.Hero.Character.Defenses.Resist(DamageType.Fire, 200);
        var full = shod.Hero.Character.HitPoints;
        Run(shod, 8);
        Assert.Equal(full, shod.Hero.Character.HitPoints);
    }

    // ----- Ash storms ----------------------------------------------------------------

    [Fact]
    public void An_ash_storm_hides_what_is_far_from_the_hero_and_from_the_monsters()
    {
        var world = Field(16, ".", storms: true, new SpawnDefinition([36, 10], [new PackMember("fire_ant")], Radius: 0));
        var ant = world.Monsters.Single();
        Assert.False(world.AshStorm);
        Assert.True(world.IsLit(ant.X, ant.Y));
        Run(world, GameWorld.AshStormEverySeconds + 1);
        Assert.True(world.AshStorm);
        Assert.False(world.IsLit(ant.X, ant.Y));
        Assert.False(world.CanTarget(ant) && world.IsLit(ant.X, ant.Y));
        // In the storm the ant doesn't notice the hero 7 tiles off, where it would in clear air.
        (world.Hero.X, world.Hero.Y) = (ant.X - 7, ant.Y);
        Run(world, 2);
        Assert.Equal(MonsterState.Idle, ant.State);
        Assert.True(world.IsLit(world.Hero.X + 3, world.Hero.Y));
        Run(world, GameWorld.AshStormSeconds);
        Assert.False(world.AshStorm);
        Assert.NotEqual(MonsterState.Idle, ant.State);
    }

    // ----- Pack leaders --------------------------------------------------------------

    [Fact]
    public void A_leader_drives_its_pack_cries_it_on_and_breaks_it_when_it_falls()
    {
        var world = Field(17, ".", false, new SpawnDefinition([20, 10],
            [new PackMember("inquisitor_captain", CreatureRank.Champion, Leads: true), new PackMember("inquisitor", CreatureRank.Normal, 2)], Radius: 1.5));
        var captain = world.Monsters.Single(m => m.Definition.Id == "inquisitor_captain");
        var led = world.Monsters.Where(m => m != captain).ToList();
        Assert.True(captain.IsLeader);
        Assert.All(world.Monsters, m => Assert.Same(captain, m.Leader));
        Assert.All(led, m => Assert.True(world.Commanded(m)));
        Assert.False(world.Commanded(captain));
        (world.Hero.X, world.Hero.Y) = (captain.X - 4, captain.Y);
        Run(world, GameWorld.LeaderCryEverySeconds);
        Assert.Contains(led, m => m.CriedUntil > 0);
        Assert.Equal(0, captain.CriedUntil);                                       // its cry heals its pack, not itself
        world.Slay(captain);
        Assert.All(led.Where(m => !m.IsDead), m =>
        {
            Assert.False(world.Commanded(m));
            Assert.True(m.Creature.Conditions.Has(ConditionKind.Frightened));
        });
    }

    // ----- The Reliquary: wards and Echoes -------------------------------------------

    [Fact]
    public void Every_echo_comes_while_its_relic_survives_behind_wards_that_hold()
    {
        var world = InWith("reliquary");
        var echoes = world.Monsters.Where(m => m.IsEcho).ToList();
        Assert.Equal(["choir_agnete", "mother_sallow", "ser_hadric", "vorthrax"], echoes.Select(m => m.Definition.Id).Order());
        var sallow = echoes.Single(m => m.Definition.Id == "mother_sallow");
        Assert.Equal("Echo of Mother Sallow", sallow.Name);
        var whole = Creatures.CreatureStats.For(19, CreatureRank.Herald).HitPoints * Content.Creatures["mother_sallow"].HitPointsScale;
        Assert.Equal(whole * GameWorld.EchoHealth, sallow.Creature.Stats.HitPoints, 1.0);
        Assert.False(echoes.Single(m => m.Definition.Id == "choir_agnete").EchoAlone);
        foreach (var gate in Content.Zone("reliquary").Gates!)
        {
            Assert.False(world.GateOpen(gate));
            Assert.All(gate.Cells, c => Assert.True(world.Map.IsBlocked(c[0], c[1]) && world.Map.BlocksSight(c[0], c[1])));
        }
    }

    [Fact]
    public void A_destroyed_relic_lays_its_echo_to_rest_and_the_candles_decide_the_choir_s()
    {
        var world = InWith("reliquary", ("act1.sallow_relic", "destroyed"), ("act2.hadric_relic", "returned"), ("act3.candles", "snuffed"));
        Assert.DoesNotContain(world.Monsters, m => m.IsEcho && m.Definition.Id is "mother_sallow" or "ser_hadric");
        Assert.Equal("laid", world.Flag("act4.echo.sallow"));
        Assert.Equal("laid", world.Flag("act4.echo.hadric"));
        Assert.Null(world.Flag("act4.echo.choir"));
        var gates = Content.Zone("reliquary").Gates!;
        Assert.True(world.GateOpen(gates.Single(g => g.Id == "vault_of_rot")));
        Assert.True(world.GateOpen(gates.Single(g => g.Id == "vault_of_iron")));
        Assert.False(world.GateOpen(gates.Single(g => g.Id == "vault_of_whispers")));
        var choir = world.Monsters.Single(m => m.IsEcho && m.Definition.Id == "choir_agnete");
        Assert.True(choir.EchoAlone);
        // Alone: her sisters never come.
        (world.Hero.X, world.Hero.Y) = (choir.X, choir.Y + 4);
        world.TrainTo(19);
        world.Hero.Character.CannotFall = true;
        Run(world, 2);
        Assert.Same(choir, world.Boss);
        Assert.DoesNotContain(world.Monsters, m => m.SharesWith == choir && !m.IsReflection);

        var rekindled = InWith("reliquary", ("act3.candles", "rekindled"));
        Assert.DoesNotContain(rekindled.Monsters, m => m.Definition.Id == "choir_agnete");
        Assert.Equal("laid", rekindled.Flag("act4.echo.choir"));
    }

    [Fact]
    public void An_echo_never_reaches_its_herald_s_last_phase_and_its_fall_opens_the_next_ward()
    {
        var world = InWith("reliquary");
        world.TrainTo(19);
        world.Hero.Character.CannotFall = true;
        var hadric = world.Monsters.Single(m => m.IsEcho && m.Definition.Id == "ser_hadric");
        (world.Hero.X, world.Hero.Y) = (hadric.X, hadric.Y + 3);
        Run(world, 1);
        Assert.Same(hadric, world.Boss);
        hadric.Creature.SetHitPoints(Math.Max(1, hadric.Creature.Stats.HitPoints / 10));
        Run(world, 1.5);
        Assert.Equal(1, hadric.BossPhase);                                       // the standard, but never the armour cast off
        world.Slay(hadric);
        Run(world, 0.5);
        Assert.Null(world.Flag("act2.hadric"));                                   // his own death flag is the Herald's, not the Echo's
        Assert.Equal("laid", world.Flag("act4.echo.hadric"));
        Assert.True(world.GateOpen(Content.Zone("reliquary").Gates!.Single(g => g.Id == "vault_of_iron")));
        Assert.All(Content.Zone("reliquary").Gates!.Single(g => g.Id == "vault_of_iron").Cells,
            c => Assert.False(world.Map.IsBlocked(c[0], c[1])));
    }

    // ----- The Last Lantern ----------------------------------------------------------

    [Fact]
    public void The_beacon_lit_brings_the_order_into_the_hall_of_vigil_and_kept_dark_opens_the_stealth_way()
    {
        var camp = In("ember_gate");
        var beacon = camp.Interactives.Single(i => i.Id == "beacon");
        (camp.Hero.X, camp.Hero.Y) = (beacon.X, beacon.Y + 1);
        camp.Interact(beacon);
        camp.Choose(0);
        Assert.Equal("lit", camp.Flag("act4.beacon"));
        Assert.Equal("granted", camp.Flag(GameWorld.BoonFlag("last_lantern")));
        Assert.Contains(camp.Journal, j => j.Id == "act4_lantern");

        var stealth = Content.Zone("reliquary").Gates!.Single(g => g.Id == "stealth_way");
        var plain = InWith("reliquary");
        var lit = InWith("reliquary", ("act4.beacon", "lit"));
        var dark = InWith("reliquary", ("act4.beacon", "dark"));
        bool InVigil(MonsterActor m) => m.HomeY is > 19 and < 36;
        Assert.Contains(plain.Monsters, InVigil);
        Assert.DoesNotContain(lit.Monsters, InVigil);
        Assert.Contains(lit.Interactives, i => i.Id == "aldren");
        Assert.DoesNotContain(plain.Interactives, i => i.Id == "aldren");
        Assert.False(plain.GateOpen(stealth));
        Assert.False(lit.GateOpen(stealth));
        Assert.True(dark.GateOpen(stealth));
    }

    // ----- The Wyrm's Egg ------------------------------------------------------------

    [Fact]
    public void The_egg_smashed_lays_vorthrax_s_echo_to_rest_and_hatched_at_the_forge_gives_a_drakeling()
    {
        var nest = In("wyrms_nest");
        var egg = nest.Interactives.Single(i => i.Id == "egg");
        (nest.Hero.X, nest.Hero.Y) = (egg.X + 1, egg.Y);
        nest.Interact(egg);
        nest.Choose(0);
        Assert.Equal("smashed", nest.Flag("act4.egg"));
        Assert.Equal("granted", nest.Flag(GameWorld.BoonFlag("wyrmbane")));

        var taken = In("wyrms_nest");
        egg = taken.Interactives.Single(i => i.Id == "egg");
        (taken.Hero.X, taken.Hero.Y) = (egg.X + 1, egg.Y);
        taken.Interact(egg);
        taken.Choose(1);
        Assert.Equal("taken", taken.Flag("act4.egg"));
        Assert.Contains(taken.Hero.Equipment.Bag, i => i?.Definition.Id == "wyrm_egg");
        Assert.Null(taken.Pet);
    }

    [Fact]
    public void The_drakeling_follows_any_hero_beside_a_beastmaster_s_wolf_and_comes_back_when_it_falls()
    {
        var hero = TestCharacters.ForKit("ranger beastmaster");
        var world = new GameWorld(Content.Zone("ember_gate"), Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot,
            Content.Zones, Content.Dialogues, Content.Abilities);
        world.SetFlag("act4.egg", "taken");
        world.Hero.Equipment.TryAdd(new Items.Item(Content.Item("wyrm_egg"), Items.Rarity.Common, 1));
        var corwen = world.Interactives.Single(i => i.Id == "corwen");
        (world.Hero.X, world.Hero.Y) = (corwen.X + 1, corwen.Y + 1);
        world.Interact(corwen);
        world.Choose(0);
        Assert.Equal("hatched", world.Flag("act4.egg"));
        Assert.DoesNotContain(world.Hero.Equipment.Bag, i => i?.Definition.Id == "wyrm_egg");
        Run(world, 0.5);
        Assert.NotNull(world.Pet);
        Assert.Equal("drakeling", world.Pet!.Definition.Id);
        Assert.NotNull(world.Companion);
        Assert.Equal("wolf", world.Companion!.Definition.Id);
        world.Pet.Creature.TakeDamage(world.Pet.Creature.HitPoints);
        Run(world, GameWorld.AllyReturnSeconds + 1);
        Assert.False(world.Pet.IsDead);
    }
}
