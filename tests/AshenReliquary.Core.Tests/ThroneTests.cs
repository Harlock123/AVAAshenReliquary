using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M5 step 1: the Cinder Throne under the Reliquary, and Malachar Vane's court: his Inquisitors, the Herald Echoes the
/// Echo rule brings (faint, at 40% and their first phase), the Order's knights if the beacon was lit, the Order's hidden
/// way if it was kept dark, and the court falling with him.
/// </summary>
public class ThroneTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld At(params (string Flag, string Value)[] flags)
    {
        var world = new GameWorld(Content.Zone("cinder_throne"), Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        world.RestoreStory(flags.Select(f => new KeyValuePair<string, string>(f.Flag, f.Value)), []);
        return world;
    }

    [Fact]
    public void The_throne_lies_below_the_reliquary_and_everything_in_it_can_be_walked_to()
    {
        var zone = Content.Zone("cinder_throne");
        Assert.Equal(20, zone.Level);
        Assert.Equal("cinder_throne", Content.Zone("reliquary").Exits.Single(e => e.Id == "throne").To);
        var map = new TileMap(zone);
        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) }
                .Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        foreach (var (name, at) in zone.Spawns.Select((s, i) => ($"pack {i}", s.At)).Concat(zone.Exits.Select(e => (e.Id, e.At)))
                     .Concat(zone.Shrines.Select(s => (s.Id, s.At))).Concat(zone.Interactables!.Select(i => (i.Id, i.At))))
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 200000) is not null, $"{name} can't be reached");
        var malachar = zone.Spawns[0].Pack[0];
        Assert.Equal("malachar", malachar.Creature);
        Assert.Equal(CreatureRank.Prophet, malachar.Rank);
        Assert.True(malachar.Leads);
    }

    [Fact]
    public void Every_relic_left_undealt_with_brings_its_echo_to_the_court_faint()
    {
        var world = At();
        var echoes = world.Monsters.Where(m => m.IsEcho).ToList();
        Assert.Equal(["choir_agnete", "mother_sallow", "ser_hadric", "vorthrax"], echoes.Select(e => e.Definition.Id).Order());
        var hadric = echoes.Single(e => e.Definition.Id == "ser_hadric");
        var whole = CreatureStats.For(16, CreatureRank.Herald).HitPoints * Content.Creatures["ser_hadric"].HitPointsScale;
        Assert.Equal(whole * 0.4, hadric.Creature.Stats.HitPoints, 1.0);                    // faint in level too: 16
        Assert.Equal(0, GameWorld.LastPhase(hadric));
        Assert.All(echoes, e => Assert.Equal(0, GameWorld.LastPhase(e)));

        var dealt = At(("act1.sallow_relic", "destroyed"), ("act2.hadric_relic", "returned"), ("act3.candles", "rekindled"), ("act4.egg", "smashed"));
        Assert.DoesNotContain(dealt.Monsters, m => m.IsEcho);
        var snuffed = At(("act3.candles", "snuffed"));
        Assert.True(snuffed.Monsters.Single(m => m.IsEcho && m.Definition.Id == "choir_agnete").EchoAlone);
    }

    [Fact]
    public void An_echo_at_the_throne_stays_in_its_first_phase()
    {
        var world = At();
        world.TrainTo(20);
        world.Hero.Character.CannotFall = true;
        var hadric = world.Monsters.Single(m => m.IsEcho && m.Definition.Id == "ser_hadric");
        hadric.Dormant = false;                                                     // as Malachar calls it
        (world.Hero.X, world.Hero.Y) = (hadric.X, hadric.Y + 3);
        Run(world, 1);
        hadric.Creature.SetHitPoints(Math.Max(1, hadric.Creature.Stats.HitPoints / 10));
        Run(world, 1);
        Assert.Equal(0, hadric.BossPhase);
    }

    [Fact]
    public void The_echoes_wait_as_ash_until_malachar_calls_them_one_at_a_time_and_go_back_when_the_hero_falls()
    {
        var world = At();
        world.TrainTo(20);
        world.Hero.Character.CannotFall = true;
        var echoes = world.Monsters.Where(m => m.IsEcho).ToList();
        Assert.All(echoes, e => Assert.True(e.Dormant));
        Assert.All(echoes, e => Assert.False(world.CanTarget(e)));
        var malachar = world.Monsters.Single(m => m.Definition.Id == "malachar");
        (world.Hero.X, world.Hero.Y) = (malachar.X, malachar.Y + 5);
        Run(world, 1.5);
        Assert.All(echoes, e => Assert.True(e.Dormant));                            // not called yet
        Run(world, Content.Creatures["malachar"].Boss!.Phases[0].CallsEchoEverySeconds);
        var called = echoes.Where(e => !e.Dormant).ToList();
        Assert.Single(called);
        Assert.NotEqual(MonsterState.Idle, called[0].State);
        Run(world, 20);
        Assert.Single(echoes, e => !e.Dormant && !e.IsDead);                         // the next waits on this one's fall
        world.Slay(called[0]);
        Run(world, Content.Creatures["malachar"].Boss!.Phases[0].CallsEchoEverySeconds + 0.5);
        Assert.Single(echoes, e => !e.Dormant && !e.IsDead);
        typeof(GameWorld).GetMethod("ResetBosses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, []);
        Assert.All(echoes.Where(e => !e.IsDead), e => Assert.True(e.Dormant));
    }

    [Fact]
    public void The_beacon_lit_brings_the_order_s_knights_and_kept_dark_opens_the_hidden_way()
    {
        var gate = Content.Zone("cinder_throne").Gates!.Single(g => g.Id == "hidden_way");
        foreach (var (beacon, knights, open) in new[] { ("lit", 2, false), ("dark", 0, true), ("", 0, false) })
        {
            var world = beacon == "" ? At() : At(("act4.beacon", beacon));
            Assert.Equal(open, world.GateOpen(gate));
            world.TrainTo(20);
            world.Hero.Character.CannotFall = true;
            var malachar = world.Monsters.Single(m => m.Definition.Id == "malachar");
            (world.Hero.X, world.Hero.Y) = (malachar.X, malachar.Y + 5);
            Run(world, 1.5);
            Assert.Same(malachar, world.Boss);
            Assert.Equal(knights, world.Allies.Count(a => a.Definition.Id == "lantern_knight" && !a.IsDead));
        }
    }

    [Fact]
    public void Malachar_s_fall_brings_his_court_down_and_empties_the_throne()
    {
        var world = At();
        world.TrainTo(20);
        world.Hero.Character.CannotFall = true;
        var malachar = world.Monsters.Single(m => m.Definition.Id == "malachar");
        (world.Hero.X, world.Hero.Y) = (malachar.X, malachar.Y + 5);
        Run(world, 1.5);
        world.Slay(malachar);
        Assert.Equal("slain", world.Flag("act5.malachar"));
        Assert.All(world.Monsters, m => Assert.True(m.IsDead));
        Assert.Contains(world.Journal, j => j.Id == "act5_throne");
        var throne = world.Interactives.Single(i => i.Id == "throne");
        (world.Hero.X, world.Hero.Y) = (throne.X, throne.Y + 1);
        world.Interact(throne);
        Assert.Contains("empty", world.Dialogue!.Text);
    }
}
