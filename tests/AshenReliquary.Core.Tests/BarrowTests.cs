using System.Reflection;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 3: the Barrow Fields and the Old Barracks. The dead that rise again unless their bodies are destroyed, traps
/// found with Perception and disarmed with Thievery, the zones, and the two side stories (Hadric's Oath, the Clockwork
/// Confessor).
/// </summary>
public class BarrowTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static ZoneDefinition Field(SpawnDefinition[] spawns, IReadOnlyList<TrapDefinition>? traps = null) => new(
        "field", "Field", 1, "tileset_grassland",
        [
            "TTTTTTTTTTTTTTTTTTTT",
            "T..................T",
            "T..................T",
            "T..................T",
            "T..................T",
            "T..................T",
            "T..................T",
            "TTTTTTTTTTTTTTTTTTTT",
        ],
        new Dictionary<string, LegendEntry> { ["."] = new("grass"), ["T"] = new("grass", [248], Blocks: true, BlocksSight: true) },
        PlayerStart: [2, 3], spawns, Shrines: [], Exits: [], Traps: traps);

    private static GameWorld World(Characters.Character hero, SpawnDefinition[] spawns, IReadOnlyList<TrapDefinition>? traps = null) =>
        new(Field(spawns, traps), Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, abilities: Content.Abilities);

    private static GameWorld Dead(out MonsterActor m, string creature = "barrow_dead")
    {
        var world = World(TestCharacters.Fighter(), [new SpawnDefinition([12, 3], [new PackMember(creature)], Radius: 0, Level: 1)]);
        m = world.Monsters.Single();
        return world;
    }

    // ----- The rising dead -----------------------------------------------------------

    [Fact]
    public void A_barrow_corpse_rises_again_at_half_health_and_pays_no_xp_the_second_time()
    {
        var world = Dead(out var m);
        world.Slay(m);
        Assert.True(m.IsRising);
        Assert.Equal(7, m.RisesIn);
        var xp = world.Experience.Xp;
        Assert.True(xp > 0);
        Run(world, 7.2);
        Assert.False(m.IsDead);
        Assert.Equal(1, m.Rises);
        Assert.Equal(m.Creature.Stats.HitPoints / 2, m.Creature.HitPoints);
        world.Slay(m);
        Assert.Equal(xp, world.Experience.Xp);
        Assert.True(m.IsRising);
    }

    [Fact]
    public void A_body_that_has_risen_three_times_stays_down()
    {
        var world = Dead(out var m);
        for (var i = 0; i < GameWorld.MaxRises; i++)
        {
            world.Slay(m);
            Run(world, m.RisesIn + 0.2);
            Assert.False(m.IsDead);
        }
        world.Slay(m);
        Assert.False(m.IsRising);
    }

    [Fact]
    public void A_click_walks_the_hero_over_to_smash_a_rising_corpse()
    {
        var world = Dead(out var m);
        world.Slay(m);
        world.SmashCorpse(m);
        Run(world, 3);
        Assert.False(m.IsRising);
        Run(world, 8);
        Assert.True(m.IsDead, "a smashed body stays down");
    }

    [Fact]
    public void Fire_vitality_and_blows_scour_the_rising_dead_but_arrows_do_not()
    {
        var world = World(TestCharacters.Fighter(),
            [new SpawnDefinition([12, 2], [new PackMember("barrow_dead")], Radius: 0, Level: 1),
             new SpawnDefinition([12, 5], [new PackMember("barrow_dead")], Radius: 0, Level: 1)]);
        var (a, b) = (world.Monsters[0], world.Monsters[1]);
        world.Slay(a);
        world.Slay(b);
        world.ScourCorpses(a.X, a.Y, 1, DamageType.Piercing);
        Assert.True(a.IsRising);
        world.ScourCorpses(a.X, a.Y, 1, DamageType.Fire);
        Assert.False(a.IsRising);
        Assert.True(b.IsRising, "out of the blast");
        Assert.True(GameWorld.Scours(DamageType.Vitality) && GameWorld.Scours(DamageType.Bludgeoning) && GameWorld.Scours(DamageType.Force));
    }

    [Fact]
    public void Corpse_explosion_can_be_aimed_at_a_rising_corpse()
    {
        var hero = TestCharacters.ForKit("necromancer plaguebringer");
        var world = World(hero, [new SpawnDefinition([8, 3], [new PackMember("barrow_dead")], Radius: 0, Level: 1)]);
        var m = world.Monsters.Single();
        world.Slay(m);
        typeof(GameWorld).GetProperty(nameof(GameWorld.GraveCharges))!.SetValue(world, 4);
        Assert.True(world.UseAbility("corpse_explosion", m));
        Run(world, 2);
        Assert.False(m.IsRising);
    }

    // ----- Traps ---------------------------------------------------------------------

    [Fact]
    public void A_trap_is_spotted_with_perception_as_the_hero_comes_near_and_remembered_as_a_flag()
    {
        var world = World(TestCharacters.Fighter(), [], [new TrapDefinition("easy", [6, 3], Dc: 1)]);
        var trap = world.ZoneTraps.Single();
        Assert.Equal(TrapState.Hidden, trap.State);
        world.MoveTo(4.5, 3.5);
        Run(world, 2);
        Assert.Equal(TrapState.Found, trap.State);
        Assert.Equal("found", world.Flags["trap.field.easy"]);
        Assert.Equal(TileMap.AvoidStepCost, world.Map.StepCost(6, 3));
    }

    [Fact]
    public void Stepping_on_a_trap_springs_it_and_a_basic_reflex_save_softens_the_blow()
    {
        var world = World(TestCharacters.Fighter(), [], [new TrapDefinition("hard", [6, 3], "flame", Dc: 40)]);
        var trap = world.ZoneTraps.Single();
        var hp = world.Hero.Character.HitPoints;
        world.MoveTo(9.5, 3.5);
        Run(world, 4);
        Assert.Equal(TrapState.Sprung, trap.State);
        Assert.True(world.Hero.Character.HitPoints < hp);
        hp = world.Hero.Character.HitPoints;
        world.MoveTo(3.5, 3.5);
        Run(world, 3);
        Assert.True(world.Hero.Character.HitPoints >= hp);                // spent: nothing more off
        Assert.Equal(TrapState.Sprung, trap.State);
    }

    [Fact]
    public void Disarming_takes_training_in_thievery()
    {
        // A hero untrained in Thievery (the test kits pick their skills themselves: find one that left it out).
        var untrained = new[] { "fighter vanguard", "wizard pyromancer", "ranger marksman", "champion lanternbearer", "necromancer bonelord" }
            .Select(TestCharacters.ForKit).First(c => c.SkillRank("Thievery") == Rules.ProficiencyRank.Untrained);
        var fighter = World(untrained, [], [new TrapDefinition("t", [6, 3], Dc: 1)]);
        var trap = fighter.ZoneTraps.Single();
        fighter.MoveTo(4.5, 3.5);
        Run(fighter, 2);
        fighter.Disarm(trap);
        Run(fighter, 1);
        Assert.Equal(TrapState.Found, trap.State);
        Assert.Contains("Thievery", fighter.LastMessage);

        var rogue = World(TestCharacters.Rogue(), [], [new TrapDefinition("t", [6, 3], Dc: 1)]);
        trap = rogue.ZoneTraps.Single();
        rogue.MoveTo(4.5, 3.5);
        Run(rogue, 2);
        var xp = rogue.Experience.Xp;
        rogue.Disarm(trap);
        Run(rogue, 2);
        Assert.Equal(TrapState.Disarmed, trap.State);
        Assert.True(rogue.Experience.Xp > xp);
        rogue.MoveTo(9.5, 3.5);
        var hp = rogue.Hero.Character.HitPoints;
        Run(rogue, 3);
        Assert.Equal(hp, rogue.Hero.Character.HitPoints);                // walked over it harmlessly
    }

    // ----- The zones -----------------------------------------------------------------

    [Theory]
    [InlineData("barrow_fields")]
    [InlineData("old_barracks")]
    public void The_zones_load_and_everything_in_them_can_be_walked_to(string zoneId)
    {
        var zone = Content.Zone(zoneId);
        Assert.Equal(2, zone.Act);
        var map = new TileMap(zone);
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), m => Assert.True(Content.Creatures.ContainsKey(m.Creature), m.Creature));
        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2))
                .First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        var places = zone.Spawns.Where(s => s.Radius > 0).Select(s => ("pack", s.At))
            .Concat((zone.Caches ?? []).Select(c => (c.Id, c.At)))
            .Concat(zone.Shrines.Select(s => (s.Id, s.At)))
            .Concat(zone.Exits.Select(e => (e.Id, e.At)))
            .Concat((zone.Interactables ?? []).Select(i => (i.Id, i.At)))
            .Concat((zone.Traps ?? []).Select(t => (t.Id, t.At)));
        foreach (var (name, at) in places)
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 200000) is not null, $"{name} at {at[0]},{at[1]} can't be reached");
    }

    [Fact]
    public void The_barrow_fields_dead_rise_and_the_barracks_are_trapped_and_dark()
    {
        var fields = Content.Zone("barrow_fields");
        var risers = fields.Spawns.SelectMany(s => s.Pack).Count(m => Content.Creatures[m.Creature].Revives > 0);
        Assert.True(risers > 25, $"only {risers} risers");
        Assert.Contains(fields.Spawns.SelectMany(s => s.Pack), m => m.Creature == "open_barrow");
        var barracks = Content.Zone("old_barracks");
        Assert.True(barracks.IsDark);
        Assert.InRange(barracks.Traps!.Count, 12, 20);
        Assert.Contains(barracks.Spawns.SelectMany(s => s.Pack), m => m.Creature == "quartermaster_corvin");
    }

    [Fact]
    public void Ironholds_north_gate_opens_once_varro_falls_and_the_barracks_lie_off_the_fields()
    {
        var world = new GameWorld(Content.Zone("ironhold"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        var north = world.Zone.Exits.Single(e => e.Id == "north_gate");
        void WalkIn()
        {
            (world.Hero.X, world.Hero.Y) = (north.At[0] + 0.5, north.At[1] + 4.5);
            world.MoveTo(north.At[0] + 0.5, north.At[1] + 0.5);
            Run(world, 4);
        }
        WalkIn();
        Assert.Equal("ironhold", world.Zone.Id);
        Assert.Contains("Varro", world.Arrival?.Subtitle);
        world.SetFlag("act2.varro", "slain");
        WalkIn();
        Assert.Equal("barrow_fields", world.Zone.Id);

        foreach (var m in world.Monsters.ToList())
            world.Slay(m);
        Run(world, 0.1);
        foreach (var m in world.RisingCorpses.ToList())
            world.ScourCorpses(m.X, m.Y, 0.5, DamageType.Fire);
        var door = world.Zone.Exits.Single(e => e.Id == "barracks");
        (world.Hero.X, world.Hero.Y) = (door.Arrive![0] + 0.5, door.Arrive[1] + 0.5);
        world.MoveTo(door.At[0] + 0.5, door.At[1] + 0.5);
        Run(world, 4);
        Assert.Equal("old_barracks", world.Zone.Id);
        Assert.Equal(16, world.ZoneTraps.Count);
    }

    // ----- The side stories ----------------------------------------------------------

    private static void Say(GameWorld world, string words)
    {
        var choices = world.Dialogue!.Choices;
        var index = choices.ToList().FindIndex(c => c.Text.Contains(words));
        Assert.True(index >= 0, $"no answer with \"{words}\" among: {string.Join(" | ", choices.Select(c => c.Text))}");
        world.Choose(index);
    }

    /// <summary>Stands the hero beside someone or something and uses it, as a click would after the walk.</summary>
    private static void Use(GameWorld world, string id)
    {
        var who = world.Interactives.Single(i => i.Id == id);
        foreach (var (dx, dy) in new[] { (0, 1.2), (1.2, 0), (0, -1.2), (-1.2, 0), (1.2, 1.2), (-1.2, -1.2) })
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

    private static bool Has(GameWorld world, string itemId) => world.Hero.Equipment.Bag.Any(i => i?.Definition.Id == itemId);

    private static GameWorld Zone(string id) => new(Content.Zone(id), Content.Creatures, TestCharacters.Fighter(), 3,
        Content.AbilityBar("fighter"), Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    [Theory]
    [InlineData("Lay Hadric's sword", "returned")]
    [InlineData("Break the sword", "broken")]
    public void Hadrics_sword_goes_back_to_edith_or_breaks_into_a_unique(string answer, string outcome)
    {
        var barracks = Zone("old_barracks");
        foreach (var m in barracks.Monsters.ToList())
            barracks.Slay(m);
        Use(barracks, "hadrics_tomb");
        Assert.True(Has(barracks, "hadrics_oath_sword"));
        Assert.Equal("found", barracks.Flags["act2.oath"]);

        var fields = Zone("barrow_fields");
        fields.SetFlag("act2.oath", "found");
        fields.Hero.Equipment.TryAdd(new Items.Item(Content.Item("hadrics_oath_sword"), Items.Rarity.Common, 8));
        foreach (var m in fields.Monsters.ToList())
            fields.Slay(m);
        Use(fields, "ediths_grave");
        Say(fields, answer);
        Assert.Equal(outcome, fields.Flags["act2.hadric_relic"]);
        Assert.False(Has(fields, "hadrics_oath_sword"));
        if (outcome == "returned")
            Assert.Equal("granted", fields.Flags[GameWorld.BoonFlag("widows_vigil")]);
        else
            Assert.True(Has(fields, "hadrics_severed_oath"));
    }

    [Fact]
    public void The_confessors_parts_lie_in_three_zones_and_it_can_be_mended_or_stripped()
    {
        foreach (var (zone, id) in new[] { ("iron_march", "brass_mainspring"), ("barrow_fields", "governor_cog"), ("old_barracks", "speaking_grille") })
        {
            var world = Zone(zone);
            foreach (var m in world.Monsters.ToList())
                world.Slay(m);
            Use(world, id);
            Assert.Contains(world.Journal, e => e.Id == "act2_confessor");
        }

        GameWorld Ready()
        {
            var world = Zone("ironhold");
            foreach (var (flag, item) in new[] { ("act2.part_spring", "confessor_mainspring"), ("act2.part_cog", "confessor_governor"), ("act2.part_grille", "confessor_grille") })
            {
                world.SetFlag(flag, "found");
                world.Hero.Equipment.TryAdd(new Items.Item(Content.Item(item), Items.Rarity.Common, 7));
            }
            return world;
        }
        var stripped = Ready();
        var dust = stripped.Hero.Equipment.Dust;
        Use(stripped, "confessor");
        Say(stripped, "Strip it for its brass");
        Say(stripped, "Strip it.");
        Assert.Equal("scrapped", stripped.Flags["act2.confessor"]);
        Assert.Equal(dust + 60, stripped.Hero.Equipment.Dust);
        Assert.False(Has(stripped, "confessor_governor"));

        var mended = Ready();
        for (var tries = 0; tries < 40 && !mended.Flags.ContainsKey("act2.confessor"); tries++)
        {
            Use(mended, "confessor");
            Say(mended, "Fit the parts");
            mended.EndDialogue();
        }
        Assert.Equal("repaired", mended.Flags["act2.confessor"]);
        Assert.Equal("granted", mended.Flags[GameWorld.BoonFlag("confessors_absolution")]);
        Use(mended, "confessor");
        Say(mended, "What did the founders confess?");
        Assert.Equal("heard", mended.Flags["act2.revelation"]);
    }

    [Fact]
    public void A_disarmed_trap_stays_disarmed_after_a_load_in_its_zone()
    {
        var world = new GameWorld(Content.Zone("old_barracks"), Content.Creatures, TestCharacters.Rogue(), 3, Content.AbilityBar("rogue"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        var trap = world.ZoneTraps.First();
        world.SetFlag($"trap.old_barracks.{trap.Definition.Id}", "disarmed");
        var loaded = Saving.SaveGame.FromJson(Saving.SaveGame.Capture(world, "barracks", 3, null).ToJson()).Restore(Content);
        Assert.Equal(TrapState.Disarmed, loaded.ZoneTraps.First().State);
    }
}
