using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>Sallow's Mire and Mother Sallow (M2 step 8): the boss framework, her phases, the Herald gate.</summary>
public class BossTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static void Run(GameWorld world, double seconds, bool keepHeroAlive = true)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
        {
            if (keepHeroAlive)
                world.Hero.Character.Heal(10000);
            world.Update(1 / 30.0);
        }
    }

    /// <summary>The Mire with only Mother Sallow in it, the hero a few tiles from her cauldron.</summary>
    private static (GameWorld World, MonsterActor Sallow) Arena(int seed = 4)
    {
        var mire = Content.Zone("sallows_mire");
        var zone = mire with { Spawns = [mire.Spawns.Single(s => s.Pack.Any(m => m.Creature == "mother_sallow"))] };
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), seed, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        var sallow = world.Monsters.Single();
        (world.Hero.X, world.Hero.Y) = (sallow.HomeX, sallow.HomeY - 6);
        return (world, sallow);
    }

    [Fact]
    public void The_mire_loads_and_everything_in_it_can_be_walked_to()
    {
        var zone = Content.Zone("sallows_mire");
        var map = new TileMap(zone);
        Assert.All(zone.Spawns.SelectMany(s => s.Pack), m => Assert.True(Content.Creatures.ContainsKey(m.Creature), m.Creature));
        Assert.Single(zone.Spawns.SelectMany(s => s.Pack), m => m.Rank == CreatureRank.Herald);
        Assert.Equal(3, zone.Shrines.Count);
        Assert.Equal(["chapel", "iron_march"], zone.Exits.Select(e => e.Id).Order());
        var from = (zone.PlayerStart[0], zone.PlayerStart[1]);
        (int, int) Near(int[] at) => map.IsBlocked(at[0], at[1])
            ? new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Select(d => (at[0] + d.Item1, at[1] + d.Item2)).First(c => !map.IsBlocked(c.Item1, c.Item2))
            : (at[0], at[1]);
        foreach (var (name, at) in zone.Spawns.Select(s => ("pack", s.At)).Concat(zone.Caches!.Select(c => (c.Id, c.At)))
                     .Concat(zone.Shrines.Select(s => (s.Id, s.At))).Concat(zone.Exits.Select(e => (e.Id, e.At))))
            Assert.True(Pathfinder.Find(map, from, Near(at), maxNodes: 100000) is not null, $"{name} at {at[0]},{at[1]} can't be reached");
    }

    [Fact]
    public void The_chapel_tunnel_leads_into_the_mire()
    {
        var chapel = Content.Zone("drowned_chapel");
        var world = new GameWorld(chapel, Content.Creatures, TestCharacters.Fighter(), 3, Content.AbilityBar("fighter"), Content.Loot, Content.Zones);
        var tunnel = chapel.Exits.Single(e => e.Id == "mire");
        (world.Hero.X, world.Hero.Y) = (tunnel.Arrive![0] + 0.5, tunnel.Arrive[1] + 0.5);
        world.MoveTo(tunnel.At[0] + 0.5, tunnel.At[1] + 0.5);
        Run(world, 8);
        Assert.Equal("sallows_mire", world.Zone.Id);
    }

    [Fact]
    public void Mother_Sallow_is_a_herald_with_a_bar_and_drops_rot_pools_on_the_hero()
    {
        var (world, sallow) = Arena();
        Assert.Equal(CreatureRank.Herald, sallow.Creature.Stats.Rank);
        Assert.Null(world.Boss);
        Run(world, 1);
        Assert.Same(sallow, world.Boss);
        Run(world, 8);
        var pool = Assert.Single(world.Hazards, h => h.EndsAt > world.Time);
        Assert.Equal(GameWorld.PoolDc(sallow), pool.Dc);
        Assert.Equal(18, pool.Dc);                                             // 14 + her level 4
    }

    [Fact]
    public void Standing_in_a_rot_pool_hurts_and_its_warning_ring_does_not()
    {
        var (world, sallow) = Arena();
        var hero = world.Hero.Character;
        TestSupportMove(world, sallow.HomeX, sallow.HomeY - 40);              // out of her sight: only the pool hurts
        var start = hero.HitPoints;
        world.AddHazard(new Hazard(world.Hero.X, world.Hero.Y, 1.6, world.Time + 1, world.Time + 20, new Rules.Dice(10, 6), 30));
        Run(world, 0.8, keepHeroAlive: false);
        Assert.Equal(start, hero.HitPoints);                                   // still a warning ring
        Run(world, 1.5, keepHeroAlive: false);
        Assert.True(hero.HitPoints < start);
    }

    [Fact]
    public void Below_60_percent_the_corpses_rise_and_the_mire_floods()
    {
        var (world, sallow) = Arena();
        Run(world, 1);
        sallow.Creature.TakeDamage((int)(sallow.Creature.Stats.HitPoints * 0.45));
        Run(world, 4);
        Assert.Equal(1, sallow.BossPhase);
        Assert.InRange(world.Monsters.Count(m => m.Definition.Id == "fen_zombie" && !m.IsDead), 1, 4);
        var rim = (X: (int)(sallow.HomeX + 9.5), Y: (int)sallow.HomeY);
        Assert.True(world.Map.IsBog(rim.X, rim.Y) || world.Map.IsBlocked(rim.X, rim.Y), "the arena's rim should be under water");
        Assert.False(world.Map.IsBog((int)sallow.HomeX + 2, (int)sallow.HomeY), "the heart stays dry");
        Run(world, 30);
        Assert.True(world.Monsters.Count(m => m.Definition.Id == "fen_zombie" && !m.IsDead) <= 4);
    }

    [Fact]
    public void Below_25_percent_she_fuses_with_her_cauldron_and_the_deluge_comes_if_she_lives()
    {
        var (world, sallow) = Arena();
        Run(world, 1);
        TestSupportMove(world, sallow.HomeX + 6, sallow.HomeY);
        sallow.Creature.TakeDamage((int)(sallow.Creature.Stats.HitPoints * 0.8));
        Run(world, 0.5);
        Assert.Equal(2, sallow.BossPhase);
        Assert.True(sallow.Fused);
        Assert.Equal((sallow.HomeX, sallow.HomeY), (sallow.X, sallow.Y));
        Assert.InRange(sallow.EnrageLeft!.Value, 30, 35);
        Run(world, 5);
        Assert.Equal((sallow.HomeX, sallow.HomeY), (sallow.X, sallow.Y));     // rooted
        var hero = world.Hero.Character;
        Run(world, 31);
        Assert.Equal(0, sallow.EnrageLeft);
        var before = hero.HitPoints;
        Run(world, 2.1, keepHeroAlive: false);
        Assert.True(hero.HitPoints < before || hero.IsDead);
    }

    private static void TestSupportMove(GameWorld world, double x, double y) => (world.Hero.X, world.Hero.Y) = (x, y);

    [Fact]
    public void She_never_gives_up_the_chase_but_starts_again_when_the_hero_falls()
    {
        var (world, sallow) = Arena();
        Run(world, 1);
        TestSupportMove(world, sallow.HomeX, sallow.HomeY - 30);              // further than any leash
        Run(world, 1);
        Assert.NotEqual(MonsterState.Idle, sallow.State);

        TestSupportMove(world, sallow.HomeX, sallow.HomeY - 6);
        sallow.Creature.TakeDamage((int)(sallow.Creature.Stats.HitPoints * 0.5));
        Run(world, 3);
        Assert.Equal(1, sallow.BossPhase);
        world.Hero.Character.TakeDamage(100000);
        world.Hero.Character.TakeDamage(100000);
        Run(world, GameWorld.RespawnSeconds + 1, keepHeroAlive: false);
        Assert.Equal(sallow.Creature.Stats.HitPoints, sallow.Creature.HitPoints);
        Assert.Equal(0, sallow.BossPhase);
        Assert.Empty(world.Hazards);
        Assert.DoesNotContain(world.Monsters, m => m.Definition.Id == "fen_zombie");
        Assert.False(world.Map.IsBog((int)(sallow.HomeX + 9.5), (int)sallow.HomeY));  // the flood drained (the rim is dry ground)
    }

    [Fact]
    public void Her_death_sets_the_acts_flag_writes_the_journal_says_her_last_words_and_opens_the_herald_gate()
    {
        var mire = Content.Zone("sallows_mire");
        var world = new GameWorld(mire, Content.Creatures, TestCharacters.Fighter(), 4, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        var gate = mire.Exits.Single(e => e.Id == "iron_march");

        // Sealed while she lives: it neither goes anywhere nor completes the zone.
        (world.Hero.X, world.Hero.Y) = (gate.At[0] - 4.5, gate.At[1] + 0.5);
        world.MoveTo(gate.At[0] + 0.5, gate.At[1] + 0.5);
        Run(world, 3);
        Assert.Equal("The Iron March", world.Arrival?.Title);
        Assert.Contains("will not fall", world.Arrival?.Subtitle);
        Assert.False(world.ZoneComplete);

        var sallow = world.Monsters.Single(m => m.Definition.Id == "mother_sallow");
        world.Slay(sallow);
        Assert.Equal("slain", world.Flags["act1.sallow"]);
        Assert.Contains(world.Journal, e => e.Id == "act1_sallow");
        Assert.Equal("Mother Sallow is dead", world.Arrival?.Title);
        Assert.Contains("vessel", world.Arrival?.Subtitle);
        Assert.Contains(world.GroundItems, g => g.Drop.Item?.Rarity == Rarity.Rare);  // a Herald's first drop is rare

        // Open now.
        world.MoveTo(gate.At[0] - 4.5, gate.At[1] + 0.5);
        Run(world, 3);
        world.MoveTo(gate.At[0] + 0.5, gate.At[1] + 0.5);
        Run(world, 3);
        // Act 2 lies beyond: the road east is the Iron March.
        Assert.Equal("iron_march", world.Zone.Id);
        Assert.Equal("The Iron March", world.Arrival?.Title);
    }
}
