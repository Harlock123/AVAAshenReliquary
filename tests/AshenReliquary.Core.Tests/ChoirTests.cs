using System.Reflection;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M4 step 7: the Bell Tower and the Choir of Whispers (three sisters on one health bar, reflections a Perception check
/// sees through, the song's Will saves), Three Candles, and the Penitent's betrayal.
/// </summary>
public class ChoirTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static GameWorld In(string zoneId, ZoneDefinition? zone = null) => new(zone ?? Content.Zone(zoneId), Content.Creatures,
        TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);

    private static object? Invoke(GameWorld world, string method, params object[] args) =>
        typeof(GameWorld).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(world, args);

    /// <summary>The Bell Tower with only the Choir in it, a level-15 hero beside her, the fight begun.</summary>
    private static (GameWorld World, MonsterActor Agnete) Arena(params (string Flag, string Value)[] flags)
    {
        var tower = Content.Zone("bell_tower");
        var zone = tower with { Spawns = [tower.Spawns.Single(s => s.Pack.Any(m => m.Creature == "choir_agnete"))] };
        var world = In("bell_tower", zone);
        foreach (var (flag, value) in flags)
            world.SetFlag(flag, value);
        world.TrainTo(15);
        world.Hero.Character.CannotFall = true;
        var agnete = world.Monsters.Single(m => m.Definition.Id == "choir_agnete");
        (world.Hero.X, world.Hero.Y) = (agnete.X, agnete.Y + 4);
        Run(world, 0.5);
        return (world, agnete);
    }

    private static List<MonsterActor> Sisters(GameWorld world, MonsterActor agnete) =>
        world.Monsters.Where(m => m.SharesWith == agnete && !m.IsReflection && !m.IsDead && m.Definition.Id.StartsWith("choir_")).ToList();

    private static List<MonsterActor> Reflections(GameWorld world) => world.Monsters.Where(m => m.IsReflection && !m.IsDead).ToList();

    private static void Wound(MonsterActor m, double share) => m.Creature.SetHitPoints((int)(m.Creature.Stats.HitPoints * share));

    // ----- The zone ------------------------------------------------------------------

    [Fact]
    public void The_bell_tower_is_act_three_s_and_everything_in_it_can_be_walked_to()
    {
        var zone = Content.Zone("bell_tower");
        Assert.Equal(3, zone.Act);
        Assert.True(zone.IsDark);
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
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 200000) is not null, $"{name} can't be reached");
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), p => Assert.True(Content.Creatures.ContainsKey(p.Creature), p.Creature));
        var choir = zone.Spawns.Single(s => s.Pack.Any(m => m.Creature == "choir_agnete"));
        Assert.Equal(CreatureRank.Herald, choir.Pack.Single().Rank);
    }

    [Fact]
    public void Every_creature_a_boss_calls_on_exists()
    {
        foreach (var boss in Content.Creatures.Values.Where(c => c.Boss is not null))
        {
            var named = (boss.Boss!.Sisters ?? []).Concat(boss.Boss.Phases.SelectMany(p => new[] { p.Summons, p.Banner, p.Reflection, p.Joins }))
                .OfType<string>();
            Assert.All(named, id => Assert.True(Content.Creatures.ContainsKey(id), $"{boss.Id} calls on {id}"));
        }
    }

    [Fact]
    public void The_hollow_abbot_s_fall_opens_the_stair_to_the_bell_tower_and_it_leads_up_and_back()
    {
        var bell = Content.Zone("catacombs_of_oriel").Exits.Single(e => e.Id == "bell_tower");
        Assert.Equal(("bell_tower", "catacombs"), (bell.To, bell.ToExit));
        var back = Content.Zone("bell_tower").Exits.Single(e => e.Id == "catacombs");
        Assert.Equal(("catacombs_of_oriel", "bell_tower"), (back.To, back.ToExit));

        var world = In("catacombs_of_oriel");
        foreach (var m in world.Monsters.Where(m => m.Definition.Id != "hollow_abbot").ToList())
            world.Slay(m);
        Assert.Null(world.Flag("act3.bell_tower"));
        world.Slay(world.Monsters.Single(m => m.Definition.Id == "hollow_abbot"));
        Assert.Equal("open", world.Flag("act3.bell_tower"));
        Assert.Contains("Bell Tower is open", world.LastMessage);

        (world.Hero.X, world.Hero.Y) = (bell.At[0] + 0.5, bell.At[1] - 3.5);
        world.MoveTo(bell.At[0] + 0.5, bell.At[1] + 0.5);
        Run(world, 3);
        Assert.Equal("bell_tower", world.Zone.Id);
    }

    // ----- The Choir -----------------------------------------------------------------

    [Fact]
    public void Three_sisters_share_one_health_bar()
    {
        var (world, agnete) = Arena();
        var sisters = Sisters(world, agnete);
        Assert.Equal(["choir_brigid", "choir_cille"], sisters.Select(s => s.Definition.Id).Order());
        Assert.Same(agnete, world.Boss);
        var before = agnete.Creature.HitPoints;
        Invoke(world, "DamageMonster", sisters[0], 40);
        Assert.Equal(before - 40, agnete.Creature.HitPoints);
        var share = (double)agnete.Creature.HitPoints / agnete.Creature.Stats.HitPoints;
        Assert.All(sisters, s => Assert.Equal(share, (double)s.Creature.HitPoints / s.Creature.Stats.HitPoints, 0.02));
    }

    [Fact]
    public void The_killing_blow_on_a_sister_silences_the_whole_choir_and_opens_the_cliff_road()
    {
        var (world, agnete) = Arena();
        var brigid = Sisters(world, agnete).First();
        agnete.Creature.SetHitPoints(5);
        Invoke(world, "DamageMonster", brigid, 50);
        Assert.True(brigid.IsDead);
        Invoke(world, "Kill", brigid, false);
        Assert.True(agnete.IsDead);
        Assert.Equal("slain", world.Flag("act3.choir"));
        Assert.Empty(Sisters(world, agnete));
        Assert.Contains(world.Journal, j => j.Id == "act3_choir");
        Assert.All(world.Zone.Exits.Single(e => e.Id == "cliff_road").Requires!, r => Assert.Equal(r.Value, world.Flag(r.Key)));
    }

    [Fact]
    public void A_sister_struck_as_she_withdraws_still_falls_with_the_choir()
    {
        // A blow already swinging at Brigid when she faded into the bells once emptied the bar without killing the Choir.
        var (world, agnete) = Arena();
        var brigid = Sisters(world, agnete).Single(s => s.Definition.Id == "choir_brigid");
        Wound(agnete, 0.55);
        Run(world, 0.5);
        Assert.DoesNotContain(brigid, world.Monsters);
        agnete.Creature.SetHitPoints(3);
        Invoke(world, "DamageMonster", brigid, 20);
        Assert.True(brigid.IsDead);
        Invoke(world, "Kill", brigid, false);
        Assert.Equal("slain", world.Flag("act3.choir"));
    }

    [Fact]
    public void Below_sixty_percent_the_sisters_withdraw_and_agnete_hides_among_reflections_that_shatter_at_a_touch()
    {
        var (world, agnete) = Arena();
        Wound(agnete, 0.55);
        Run(world, 0.5);
        Assert.Equal(1, agnete.BossPhase);
        Assert.Empty(Sisters(world, agnete));
        var reflections = Reflections(world);
        Assert.Equal(4, reflections.Count);
        Assert.All(reflections, r => Assert.Equal(agnete.Name, r.Name));
        var before = agnete.Creature.HitPoints;
        var touched = reflections[0];
        Invoke(world, "DamageMonster", touched, 30);
        Assert.True(touched.IsDead);
        Invoke(world, "Kill", touched, false);
        Assert.Equal(before, agnete.Creature.HitPoints);
        // They shuffle: new reflections stand elsewhere after a while.
        Run(world, 12.5);
        Assert.Equal(4, Reflections(world).Count);
        Assert.DoesNotContain(touched, world.Monsters.Where(m => !m.IsDead));
    }

    [Fact]
    public void A_perception_check_near_agnete_hears_the_true_voice()
    {
        var (world, agnete) = Arena();
        Wound(agnete, 0.55);
        var heard = false;
        for (var i = 0; i < 40 * 30 && !heard; i++)
        {
            (world.Hero.X, world.Hero.Y) = (agnete.X + 3, agnete.Y);
            world.Update(1 / 30.0);
            heard = world.Time < agnete.TrueVoiceUntil;
        }
        Assert.True(heard, "the hero never heard the true voice");
        Assert.All(Reflections(world), r => Assert.True(r.TrueVoiceUntil <= world.Time));
    }

    [Fact]
    public void Below_a_quarter_the_three_sing_as_one_and_the_song_enthralls_the_hero_who_walks_to_the_singer()
    {
        var (world, agnete) = Arena();
        world.Hero.Character.CannotFall = true;
        Wound(agnete, 0.2);
        Run(world, 0.5);
        Assert.Equal(2, agnete.BossPhase);
        Assert.Empty(Reflections(world));
        Assert.Equal(2, Sisters(world, agnete).Count);
        var held = false;
        for (var i = 0; i < 60 * 30 && !held; i++)
        {
            world.Update(1 / 30.0);
            held = world.Enthralled;
        }
        Assert.True(held, "the song never took the hero");
        var (x, y) = (world.Hero.X, world.Hero.Y);
        world.MoveTo(x + 6, y + 6);
        Assert.NotEqual((x + 6, y + 6), world.Hero.Goal);
        var singer = world.Monsters.Where(m => (m == agnete || m.SharesWith == agnete) && !m.IsDead).MinBy(m => m.DistanceTo(world.Hero))!;
        var before = world.Hero.DistanceTo(singer);
        Run(world, 0.8);
        Assert.True(world.Hero.DistanceTo(singer) < before || world.Hero.DistanceTo(singer) <= GameWorld.EnthralledStopTiles + 0.1);
    }

    [Theory]
    [InlineData("banished", true)]
    [InlineData("forgiven", false)]
    public void A_banished_caddoc_joins_the_choir_for_its_last_verse(string penitent, bool joins)
    {
        var (world, agnete) = Arena(("act3.penitent", penitent));
        Wound(agnete, 0.2);
        Run(world, 0.5);
        Assert.Equal(joins, world.Monsters.Any(m => m.Definition.Id == "caddoc_turned" && !m.IsDead));
    }

    [Fact]
    public void Falling_starts_the_choir_again_with_her_sisters_beside_her()
    {
        var (world, agnete) = Arena();
        Wound(agnete, 0.5);
        Run(world, 0.5);
        Invoke(world, "ResetBosses");
        Assert.Equal(agnete.Creature.Stats.HitPoints, agnete.Creature.HitPoints);
        Assert.Equal(0, agnete.BossPhase);
        Assert.Empty(Reflections(world));
        Run(world, 0.5);
        Assert.Equal(2, Sisters(world, agnete).Count);
    }

    // ----- Three Candles -------------------------------------------------------------

    [Fact]
    public void Each_sister_s_own_candle_set_on_her_stand_burns_white()
    {
        var world = In("bell_tower");
        Interactive Stand(string sister) => world.Interactives.Single(i => i.Id == "candle_" + sister);
        Assert.Equal(["gold", "blue", "green"], GameWorld.CandleSisters.Select(world.CandleOn));
        Assert.Equal("gold", world.CandleColour(Stand("agnete")));
        int Xp() => world.Experience.Level * 1000 + world.Experience.Xp;
        var xp = Xp();
        Invoke(world, "TouchCandle", Stand("agnete"));
        Assert.Equal("agnete", world.HeldCandle);
        Invoke(world, "TouchCandle", Stand("brigid"));
        Assert.Equal(("blue", "gold"), (world.CandleOn("agnete"), world.CandleOn("brigid")));
        Assert.Null(world.Flag("act3.candles"));
        Invoke(world, "TouchCandle", Stand("cille"));
        Invoke(world, "TouchCandle", Stand("brigid"));
        Assert.Equal(["blue", "green", "gold"], GameWorld.CandleSisters.Select(world.CandleOn));
        Assert.Equal("rekindled", world.Flag("act3.candles"));
        Assert.Equal("white", world.CandleColour(Stand("cille")));
        Assert.True(Xp() > xp);
        Assert.Contains(world.Journal, j => j.Id == "act3_candles" && j.Text.Contains("white"));
    }

    [Fact]
    public void The_snuffer_puts_the_candles_out_for_good_and_the_hymnals_tell_their_colours()
    {
        var world = In("bell_tower");
        var hymnal = world.Interactives.Single(i => i.Id == "cille_hymnal");
        (world.Hero.X, world.Hero.Y) = (hymnal.X + 1, hymnal.Y + 1);
        world.Interact(hymnal);
        Assert.Contains("gold", world.Dialogue!.Text);
        Assert.Contains(world.Journal, j => j.Id == "act3_candles");
        world.EndDialogue();

        var snuffer = world.Interactives.Single(i => i.Id == "snuffer");
        (world.Hero.X, world.Hero.Y) = (snuffer.X + 1, snuffer.Y);
        world.Interact(snuffer);
        world.Choose(0);
        Assert.Equal("snuffed", world.Flag("act3.candles"));
        Assert.Null(world.CandleColour(world.Interactives.Single(i => i.Id == "candle_agnete")));
        Invoke(world, "TouchCandle", world.Interactives.Single(i => i.Id == "candle_agnete"));
        Assert.Null(world.HeldCandle);
    }

    [Fact]
    public void Candles_set_right_help_hear_the_true_voice_and_a_forgiven_caddoc_sings_against_the_song()
    {
        Assert.True(GameWorld.CandlelightBonus > 0);
        Assert.True(GameWorld.CounterSongBonus > 0);
        Assert.DoesNotContain(In("bell_tower").Interactives, i => i.Id == "caddoc");
        var world = In("catacombs_of_oriel");
        world.SetFlag("act3.penitent", "forgiven");
        Invoke(world, "TravelThrough", world.Zone.Exits.Single(e => e.Id == "bell_tower"));
        Assert.Equal("bell_tower", world.Zone.Id);
        Assert.Contains(world.Interactives, i => i.Id == "caddoc");
    }
}
