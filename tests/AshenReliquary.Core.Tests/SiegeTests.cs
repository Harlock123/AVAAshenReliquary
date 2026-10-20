using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 4: the Siege Walls and Ser Hadric the Unburied (a shield wall and charges, the legion's standard, his armour
/// cast off), the Herald gate to Act 3, the Outlaw Queen (Act 2's faction) and the cure for Ironhold's refugees.
/// </summary>
public class SiegeTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld Zone(string id, Characters.Character? hero = null) => new(Content.Zone(id), Content.Creatures,
        hero ?? TestCharacters.Fighter(), 3, Content.AbilityBar((hero?.Class.Id) ?? "fighter"), Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    /// <summary>The Siege Walls with only Hadric in them, and a level-10 hero at the edge of his wall.</summary>
    private static (GameWorld World, MonsterActor Hadric) Arena()
    {
        var walls = Content.Zone("siege_walls");
        var zone = walls with { Spawns = [walls.Spawns.Single(s => s.Pack.Any(m => m.Creature == "ser_hadric"))] };
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 4, Content.AbilityBar("fighter"), Content.Loot,
            Content.Zones, Content.Dialogues, Content.Abilities);
        world.TrainTo(10);
        var hadric = world.Monsters.Single(m => m.Definition.Id == "ser_hadric");
        return (world, hadric);
    }

    private static void Wound(MonsterActor m, double share) =>
        m.Creature.SetHitPoints((int)(m.Creature.Stats.HitPoints * share));

    // ----- The zone ------------------------------------------------------------------

    [Fact]
    public void The_siege_walls_load_and_everything_in_them_can_be_walked_to()
    {
        var zone = Content.Zone("siege_walls");
        Assert.Equal(2, zone.Act);
        var map = new TileMap(zone);
        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2))
                .First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        var places = zone.Spawns.Select(s => ("pack", s.At))
            .Concat((zone.Caches ?? []).Select(c => (c.Id, c.At)))
            .Concat(zone.Shrines.Select(s => (s.Id, s.At)))
            .Concat(zone.Exits.Select(e => (e.Id, e.At)))
            .Concat((zone.Npcs ?? []).Select(n => (n.Id, n.At)));
        foreach (var (name, at) in places)
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 200000) is not null, $"{name} at {at[0]},{at[1]} can't be reached");
        // Every cell a pack may scatter to can be reached too (the mass graves once walled a barrow dead in).
        foreach (var spawn in zone.Spawns.Where(s => s.Radius > 0))
        {
            for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
            {
                var (x, y) = (spawn.At[0] + dx, spawn.At[1] + dy);
                if (!map.IsBlocked(x, y) && dx * dx + dy * dy <= spawn.Radius * spawn.Radius)
                    Assert.True(Pathfinder.Find(map, from, (x, y), maxNodes: 200000) is not null, $"{x},{y} near a pack can't be reached");
            }
        }
        var hadric = zone.Spawns.Single(s => s.Pack.Any(m => m.Creature == "ser_hadric"));
        Assert.Equal(CreatureRank.Herald, hadric.Pack.Single().Rank);
    }

    [Fact]
    public void The_barrow_fields_north_road_leads_to_the_siege_walls_and_the_herald_gate_opens_to_act_3_when_hadric_falls()
    {
        var world = Zone("barrow_fields");
        foreach (var m in world.Monsters.ToList())
            world.Slay(m);
        foreach (var m in world.RisingCorpses.ToList())
            world.ScourCorpses(m.X, m.Y, 0.5, DamageType.Fire);
        var north = world.Zone.Exits.Single(e => e.Id == "siege_walls");
        (world.Hero.X, world.Hero.Y) = (north.Arrive![0] + 0.5, north.Arrive[1] + 0.5);
        world.MoveTo(north.At[0] + 0.5, north.At[1] + 0.5);
        Run(world, 4);
        Assert.Equal("siege_walls", world.Zone.Id);

        var gate = world.Zone.Exits.Single(e => e.Id == "heights");
        void WalkIn()
        {
            (world.Hero.X, world.Hero.Y) = (gate.At[0] + 0.5, gate.At[1] + 3.5);
            world.MoveTo(gate.At[0] + 0.5, gate.At[1] + 0.5);
            Run(world, 3);
        }
        foreach (var m in world.Monsters.Where(m => m.Definition.Id != "ser_hadric").ToList())
            world.Slay(m);
        WalkIn();
        Assert.Contains("Ser Hadric", world.Arrival?.Subtitle);
        Assert.False(world.ZoneComplete);
        world.Slay(world.Monsters.Single(m => m.Definition.Id == "ser_hadric"));
        Assert.Equal("slain", world.Flags["act2.hadric"]);
        Assert.Contains(world.Journal, e => e.Id == "act2_hadric");
        (world.Hero.X, world.Hero.Y) = (gate.At[0] + 0.5, gate.At[1] + 6.5);
        Run(world, 0.5);
        WalkIn();
        Assert.Equal("frostbitten_pass", world.Zone.Id);                        // Act 3's first zone, up to its hub
        Assert.Equal(3, world.Zone.Act);
    }

    // ----- Ser Hadric ----------------------------------------------------------------

    [Fact]
    public void Hadric_holds_a_shield_wall_raises_the_standard_and_casts_off_his_armour()
    {
        var (world, hadric) = Arena();
        var plainAc = hadric.Creature.Stats.ArmorClass;
        (world.Hero.X, world.Hero.Y) = (hadric.X + 1.5, hadric.Y);
        Run(world, 0.5);
        Assert.Equal(plainAc + 2, hadric.Creature.ArmorClass().Total);           // the shield wall
        Assert.Equal("kite_shield", hadric.Look!["off"]);

        Wound(hadric, 0.6);
        Run(world, 0.3);
        Assert.Equal(1, hadric.BossPhase);
        var standard = world.Monsters.Single(m => m.Definition.Id == "legion_standard" && !m.IsDead);
        Assert.True(world.Rallied(hadric));
        world.Slay(standard);
        Assert.False(world.Rallied(hadric));

        Wound(hadric, 0.25);
        Run(world, 0.3);
        Assert.Equal(2, hadric.BossPhase);
        Assert.Equal(plainAc - 5, hadric.Creature.ArmorClass().Total);           // armour cast off: easier to hit
        Assert.False(hadric.Look!.ContainsKey("head"));
        Assert.Equal("greatsword", hadric.Look["main"]);
    }

    [Fact]
    public void Hadric_marks_a_lane_then_charges_down_it_and_a_step_dodges_the_charge()
    {
        int Hurt(bool dodge)
        {
            var (world, hadric) = Arena();
            world.Hero.Character.CannotFall = true;
            (world.Hero.X, world.Hero.Y) = (hadric.X, hadric.Y + 6);
            world.MoveTo(hadric.X, hadric.Y + 6.5);
            for (var t = 0.0; t < 10 && hadric.Charge is null; t += 1 / 30.0)
            {
                (world.Hero.X, world.Hero.Y) = (hadric.X, hadric.Y + 6);               // keep the distance: no melee
                world.Update(1 / 30.0);
            }
            Assert.NotNull(hadric.Charge);
            var hp = world.Hero.Character.HitPoints;
            for (var t = 0.0; t < 2.5 && hadric.Charge is not null; t += 1 / 30.0)
            {
                if (dodge && hadric.Charge is { } c && world.Time >= c.RushAt - 0.05 && world.Time < c.RushAt + 0.1)
                    world.Dodge(world.Hero.X + 3, world.Hero.Y);
                world.Update(1 / 30.0);
            }
            return hp - world.Hero.Character.HitPoints;
        }
        Assert.True(Enumerable.Range(0, 3).Sum(_ => Hurt(dodge: false)) > 0, "standing in the lane, the charge lands");
        Assert.Equal(0, Hurt(dodge: true));
    }

    // ----- The Outlaw Queen and the refugees -----------------------------------------

    private static void Use(GameWorld world, string id)
    {
        var who = world.Interactives.Single(i => i.Id == id);
        foreach (var (dx, dy) in new[] { (1.2, 0), (0, 1.2), (-1.2, 0), (0, -1.2), (1.2, 1.2) })
        {
            if (world.Map.IsWalkable(who.X + dx, who.Y + dy))
            {
                (world.Hero.X, world.Hero.Y) = (who.X + dx, who.Y + dy);
                break;
            }
        }
        world.Interact(who);
        Run(world, 1);
        Assert.NotNull(world.Dialogue);
    }

    private static void Say(GameWorld world, string words)
    {
        var choices = world.Dialogue!.Choices;
        var index = choices.ToList().FindIndex(c => c.Text.Contains(words));
        Assert.True(index >= 0, $"no answer with \"{words}\" among: {string.Join(" | ", choices.Select(c => c.Text))}");
        world.Choose(index);
    }

    private static bool Has(GameWorld world, string itemId) => world.Hero.Equipment.Bag.Any(i => i?.Definition.Id == itemId);

    [Fact]
    public void Joining_red_mirella_gives_her_bow_she_leaves_and_thale_turns_cold()
    {
        var world = Zone("siege_walls");
        foreach (var m in world.Monsters.ToList())
            world.Slay(m);
        Use(world, "mirella");
        Say(world, "I'll stand with you");
        Assert.Equal("outlaws", world.Flags["act2.faction"]);
        Assert.True(Has(world, "mirellas_draw"));
        world.EndDialogue();
        Assert.DoesNotContain(world.Interactives, i => i.Id == "mirella");

        var city = Zone("ironhold");
        city.SetFlag("act2.faction", "outlaws");
        Use(city, "thale");
        Assert.Contains("ran with Mirella", city.Dialogue!.Text);
        Assert.Equal("cold", city.Flags["act2.thale_mirella"]);
    }

    [Fact]
    public void Talking_mirella_round_hands_her_to_the_guard_or_she_slips_away_and_thale_rewards_her_capture()
    {
        var outcomes = new HashSet<string>();
        for (var seed = 0; seed < 30 && outcomes.Count < 2; seed++)
        {
            var world = new GameWorld(Content.Zone("siege_walls"), Content.Creatures, TestCharacters.Fighter(), seed,
                Content.AbilityBar("fighter"), Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
            world.TrainTo(10);
            foreach (var m in world.Monsters.ToList())
                world.Slay(m);
            Use(world, "mirella");
            Say(world, "(Diplomacy)");
            outcomes.Add(world.Flags["act2.faction"]);
            world.EndDialogue();
            Assert.DoesNotContain(world.Interactives, i => i.Id == "mirella");
        }
        Assert.Contains("guard", outcomes);

        var city = Zone("ironhold");
        city.SetFlag("act2.faction", "guard");
        Use(city, "thale");
        Assert.True(Has(city, "ironhold_oathblade"));
        Assert.Equal("given", city.Flags["act2.thale_mirella"]);
    }

    [Fact]
    public void Mirella_is_gone_from_a_walls_entered_after_her_choice()
    {
        var world = Zone("siege_walls");
        Assert.Contains(world.Interactives, i => i.Id == "mirella");
        world.SetFlag("act2.faction", "none");
        var loaded = Saving.SaveGame.FromJson(Saving.SaveGame.Capture(world, "walls", 3, null).ToJson()).Restore(Content);
        Assert.DoesNotContain(loaded.Interactives, i => i.Id == "mirella");
    }

    [Theory]
    [InlineData("cured", "saved")]
    [InlineData("destroyed", "lost")]
    public void Ysoldes_cure_saves_ironholds_refugees(string relic, string refugees)
    {
        var world = Zone("ironhold");
        world.SetFlag("act1.sallow_relic", relic);
        Use(world, "hale");
        Assert.Equal(refugees, world.Flags["act2.refugees"]);
        Assert.Contains(world.Journal, e => e.Id == "act1_well");
    }
}
