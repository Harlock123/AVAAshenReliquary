using AshenReliquary.Core.Content;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M5 step 3: the Heart's Pull, Malachar's last phase (a tug of war of Fortitude checks that pressing Space strengthens),
/// and Malachar's Daughter: her toy and the memories of her across the acts, and the parley the toy opens.
/// </summary>
public class HeartsPullTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static BossPhase Phase => Content.Creatures["malachar"].Boss!.Phases[2];

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static bool RunUntil(GameWorld world, Func<bool> until, double limit)
    {
        for (var t = 0.0; t < limit; t += 1 / 30.0)
        {
            if (until())
                return true;
            world.Update(1 / 30.0);
        }
        return until();
    }

    /// <summary>The Throne with every relic dealt with (no Echoes), the hero at 20, his court gone, the fight begun.</summary>
    private static (GameWorld World, MonsterActor Malachar) Throne(params (string Flag, string Value)[] more)
    {
        var world = new GameWorld(Content.Zone("cinder_throne"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        var flags = new Dictionary<string, string>
        {
            ["act1.sallow_relic"] = "destroyed", ["act2.hadric_relic"] = "returned", ["act3.candles"] = "rekindled", ["act4.egg"] = "smashed",
        };
        foreach (var (flag, value) in more)
            flags[flag] = value;
        world.RestoreStory(flags, []);
        world.TrainTo(20);
        world.Hero.Character.CannotFall = true;
        var malachar = world.Monsters.Single(m => m.Definition.Id == "malachar");
        foreach (var m in world.Monsters.Where(m => m != malachar).ToList())
            world.Slay(m);
        (world.Hero.X, world.Hero.Y) = (malachar.X, malachar.Y + 4);
        Run(world, 1);
        return (world, malachar);
    }

    /// <summary>Malachar brought below a quarter of his health: into the Heart's Pull.</summary>
    private static void ToThePull(GameWorld world, MonsterActor malachar)
    {
        malachar.Creature.SetHitPoints((int)(malachar.Creature.Stats.HitPoints * 0.2));
        Run(world, 0.1);
        Assert.Equal(2, malachar.BossPhase);
        Assert.Equal("The Heart's Pull", Phase.Name);
    }

    [Fact]
    public void Below_a_quarter_he_roots_at_the_throne_and_reaches_for_the_heart_and_the_hero_is_held()
    {
        var (world, malachar) = Throne();
        ToThePull(world, malachar);
        Assert.Equal((malachar.HomeX, malachar.HomeY), (malachar.X, malachar.Y));
        Assert.True(RunUntil(world, () => world.Pull is not null, 3));
        Assert.Same(malachar, world.Pull!.Puller);
        Assert.Equal(HeartsPull.Start, world.Pull.Meter);
        var at = (world.Hero.X, world.Hero.Y);
        world.MoveTo(world.Hero.X + 5, world.Hero.Y);
        Assert.False(world.Dodge(world.Hero.X + 5, world.Hero.Y));
        Run(world, 0.5);
        Assert.Equal(at, (world.Hero.X, world.Hero.Y));
    }

    [Fact]
    public void Pressing_space_strengthens_each_check_and_holding_the_heart_staggers_him()
    {
        var (world, malachar) = Throne();
        ToThePull(world, malachar);
        RunUntil(world, () => world.Pull is not null, 3);
        var pull = world.Pull!;
        var checks = 0;
        // A person mashing: twelve presses between checks, more than the cap.
        while (world.Pull is not null && checks < 20)
        {
            for (var i = 0; i < 12; i++)
                world.PullHarder();
            Assert.Equal(12, pull.Presses);
            var before = pull.Meter;
            RunUntil(world, () => pull.Last is not null && pull.Presses == 0, Phase.PullCheckSeconds + 0.2);
            Assert.Equal(Math.Clamp(before + HeartsPull.Shift(pull.Last!.Value), 0, 100), pull.Meter);
            pull.Last = null;
            checks++;
        }
        Assert.Null(world.Pull);
        Assert.Equal(100, pull.Meter);
        Assert.True(malachar.StaggeredUntil > world.Time);
        Assert.False(world.Hero.IsDead);

        // Staggered, he makes no attack; the next pull comes round after.
        var hp = world.Hero.Character.HitPoints;
        Run(world, Phase.PullStaggerSeconds - 0.5);
        Assert.Equal(hp, world.Hero.Character.HitPoints);
        Assert.True(RunUntil(world, () => world.Pull is not null, Phase.PullEverySeconds + 1));
    }

    [Fact]
    public void Losing_the_pull_means_falling()
    {
        var (world, malachar) = Throne();
        world.Hero.Character.CannotFall = false;
        while (world.Hero.Character.HeroPoints.TrySpend())
        {
        }
        ToThePull(world, malachar);
        RunUntil(world, () => world.Pull is not null, 3);
        var fell = false;
        for (var tries = 0; tries < 600 && !fell; tries++)
        {
            if (world.Pull is { } pull)
                pull.Meter = 1;
            world.Update(1 / 30.0);
            // Fallen (or already back at the shrine, Malachar whole again).
            fell = world.Hero.IsDead || malachar.BossPhase == 0;
        }
        Assert.True(fell, "a failed check at the bottom of the pull should tear the Heart out");
    }

    [Fact]
    public void The_shift_of_each_degree_favours_the_hero_who_holds()
    {
        Assert.True(HeartsPull.Shift(DegreeOfSuccess.CriticalSuccess) > HeartsPull.Shift(DegreeOfSuccess.Success));
        Assert.True(HeartsPull.Shift(DegreeOfSuccess.Success) > 0);
        Assert.True(HeartsPull.Shift(DegreeOfSuccess.Failure) < 0);
        Assert.True(HeartsPull.Shift(DegreeOfSuccess.CriticalFailure) < HeartsPull.Shift(DegreeOfSuccess.Failure));
    }

    [Fact]
    public void Shown_his_daughter_s_toy_with_every_memory_of_her_he_is_spared_and_his_court_falls()
    {
        var (world, malachar) = Throne(("daughter.toy", "carried"), ("daughter.ironhold", "found"), ("daughter.oriel", "found"),
            ("daughter.ember_gate", "found"), ("daughter.journal", "found"), ("daughter.memories", "4"));
        world.Hero.Equipment.TryAdd(Items.LootGenerator.Make(Content.Loot.Items["lantern_bird"], 20));
        var guard = world.Monsters.First(m => m != malachar);
        ToThePull(world, malachar);
        Assert.True(world.Parleying);
        Assert.NotNull(world.Dialogue);
        Assert.Null(world.Pull);
        // The fight holds while they talk, however long.
        var hp = world.Hero.Character.HitPoints;
        Run(world, 4);
        Assert.Equal(hp, world.Hero.Character.HitPoints);
        Assert.Null(world.Pull);
        Assert.NotNull(world.Dialogue);

        Assert.Equal(0, world.Dialogue!.Choices.ToList().FindIndex(c => c.Text.Contains("what she would say")));
        world.Choose(0);
        Assert.Equal("spared", world.Flag("act5.malachar"));
        Assert.Equal("shown", world.Flag("daughter.toy"));
        Assert.DoesNotContain(malachar, world.Monsters);
        Assert.All(world.Monsters, m => Assert.True(m.IsDead));
        Assert.True(guard.IsDead);
        Assert.Contains(world.Interactives, i => i.Id == "malachar_spared");
        Assert.DoesNotContain(world.Hero.Equipment.Bag, i => i?.Definition.Id == "lantern_bird");
        Assert.Contains(world.Journal, j => j.Id == "act5_throne" && j.Text.Contains("Wren"));
        world.Choose(0);
        Assert.Null(world.Dialogue);
        Assert.False(world.Parleying);
        Assert.Null(world.Pull);
    }

    [Fact]
    public void Turning_from_the_parley_lets_the_pull_begin_and_without_the_toy_there_is_none()
    {
        var (world, malachar) = Throne(("daughter.toy", "carried"), ("daughter.memories", "0"));
        ToThePull(world, malachar);
        Assert.True(world.Parleying);
        var words = world.Dialogue!.Choices.ToList();
        Assert.Contains(words, c => c.CheckLabel == "Diplomacy" && c.Dc == 44);
        world.Choose(words.FindIndex(c => c.Text.StartsWith("Say nothing")));
        Assert.False(world.Parleying);
        Assert.True(RunUntil(world, () => world.Pull is not null, 2));

        var (plain, him) = Throne();
        ToThePull(plain, him);
        Assert.False(plain.Parleying);
        Assert.Null(plain.Dialogue);
    }

    [Fact]
    public void The_toy_and_the_four_memories_lie_where_the_hero_can_reach_them_and_each_is_counted()
    {
        var places = new (string Zone, string Id, string Flag)[]
        {
            ("gallows_road", "daughter_toy", "daughter.toy"), ("ironhold", "daughter_ironhold", "daughter.ironhold"),
            ("saint_oriels_rest", "daughter_oriel", "daughter.oriel"), ("ember_gate", "daughter_ember_gate", "daughter.ember_gate"),
            ("reliquary", "daughter_journal", "daughter.journal"),
        };
        var flags = new Dictionary<string, string>();
        foreach (var (zoneId, id, flag) in places)
        {
            var zone = Content.Zone(zoneId);
            var thing = zone.Interactables!.Single(i => i.Id == id);
            Assert.Equal("keepsake", thing.Action);
            var map = new TileMap(zone);
            Assert.False(map.IsBlocked(thing.At[0], thing.At[1]), $"{id} stands on blocked ground");
            Assert.True(Pathfinder.Find(map, (zone.PlayerStart[0], zone.PlayerStart[1]), (thing.At[0], thing.At[1]), maxNodes: 400000) is not null,
                $"{id} can't be reached in {zoneId}");

            var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
                Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
            world.RestoreStory(flags, []);
            foreach (var m in world.Monsters.ToList())
                world.Slay(m);
            var who = world.Interactives.Single(i => i.Id == id);
            (world.Hero.X, world.Hero.Y) = (who.X, who.Y + 1);
            world.Interact(who);
            Assert.NotNull(world.Dialogue);
            if (id == "daughter_toy")
            {
                world.Choose(0);
                Assert.Contains(world.Hero.Equipment.Bag, i => i?.Definition.Id == "lantern_bird");
            }
            Assert.NotNull(world.Flag(flag));
            foreach (var (k, v) in world.Flags.Where(f => f.Key.StartsWith(GameWorld.DaughterFlag)))
                flags[k] = v;
        }
        Assert.Equal("carried", flags["daughter.toy"]);
        Assert.Equal("4", flags["daughter.memories"]);
    }
}
