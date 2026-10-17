using AshenReliquary.Core.Characters;
using AshenReliquary.Core.World;
using Xunit.Abstractions;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// Fights every Gallows Road pack with a fresh level-1 hero over many seeds and reports how each class fares: how
/// often the hero goes down (a Hero Point spent or a death), HP lost and potions drunk. The bot plays plainly:
/// the Fighter strikes the nearest monster and uses Power Attack and Sudden Charge when it can; the Wizard stands
/// still and casts (no kiting, so this is its worst case). Run it with `--logger "console;verbosity=detailed"`.
/// </summary>
public class BalanceReport(ITestOutputHelper output)
{
    private const int Seeds = 40;
    private const double Tick = 1 / 30.0;

    public sealed record PackResult(int Pack, string Members, int Fights, int Downs, double HpLostPct, double Potions, double Seconds);

    [Theory]
    [InlineData("fighter")]
    [InlineData("wizard")]
    public void Gallows_Road_packs_at_level_1(string classId)
    {
        var results = Run(classId);
        output.WriteLine($"{classId}: pack, members, downs per {Seeds} fights, HP lost, potions, seconds");
        foreach (var r in results)
            output.WriteLine($"  {r.Pack,2} {r.Members,-44} downs {r.Downs,2}/{r.Fights}  hp lost {r.HpLostPct,3:0}%  potions {r.Potions:0.0}  {r.Seconds:0.0} s");
        output.WriteLine($"  total downs {results.Sum(r => r.Downs)} in {results.Sum(r => r.Fights)} fights");
        Assert.NotEmpty(results);
    }

    public sealed record RoadResult(int Deaths, int HeroPointsSpent, int Potions, double Minutes, bool Reached, int Level, List<string> Killers, double CalmSeconds = 0, double Regenerated = 0);

    /// <summary>The starting kits as the game hands them out (GameSession.StartingKit), and variants to compare.</summary>
    public static readonly Dictionary<string, string[]> Kits = new()
    {
        ["fighter"] = ["longsword", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"],
        ["fighter in leather"] = ["longsword", "leather_chest", "leather_pants", "leather_boots", "leather_gloves"],
        ["wizard"] = ["staff", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves"],
    };

    [Theory]
    [InlineData("fighter")]
    [InlineData("fighter in leather")]
    [InlineData("wizard")]
    public void The_whole_road_at_level_1(string kit)
    {
        var classId = kit.Split(' ')[0];
        var hero = new Summary(Dressed(classId, kit));
        var runs = Enumerable.Range(1, 40).Select(seed => RoadRun(kit, seed)).ToList();
        output.WriteLine($"{kit}: whole road from the gallows to the camp, {runs.Count} runs");
        foreach (var r in runs)
            output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  calm {r.CalmSeconds,3:0} s  regen {r.Regenerated,3:0} HP  level {r.Level}  {(r.Reached ? "reached the camp" : "did not finish")}");
        output.WriteLine($"  average deaths {runs.Average(r => r.Deaths):0.0}, downs {runs.Average(r => r.Deaths + r.HeroPointsSpent):0.0}");
        output.WriteLine($"  {hero.Describe()}");
        foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()))
            output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
        Assert.NotEmpty(runs);
    }

    /// <summary>
    /// Walks the road north pack by pack, carrying HP, potions and Hero Points from fight to fight, with shrines,
    /// respawns and out-of-combat recovery working as in play; it fights whatever comes for it or stands within
    /// 5 tiles, and waits to recover to 80% before moving on.
    /// </summary>
    public static RoadResult RoadRun(string kit, int seed)
    {
        var classId = kit.Split(' ')[0];
        var content = TestCharacters.Content;
        var zone = content.Zone("gallows_road");
        var hero = classId == "wizard" ? TestCharacters.Wizard() : TestCharacters.Fighter();
        var bar = content.AbilityBar(classId);
        var world = new GameWorld(zone, content.Creatures, hero, seed, bar, content.Loot);
        Dress(hero, world.Hero.Equipment, kit);
        var exit = zone.Exits[0];
        int deaths = 0, spent = 0, potions = 0;
        var killers = new List<string>();
        var heroPoints = hero.HeroPoints.Count;
        var wasDead = false;
        double nextMove = 0, calmSince = 0, calm = 0, regenerated = 0;
        while (!world.ZoneComplete && world.Time < 1800)
        {
            if (hero.IsDead != wasDead)
            {
                if (hero.IsDead)
                {
                    deaths++;
                    killers.Add(Nearby(world));
                }
                wasDead = hero.IsDead;
            }
            if (hero.HeroPoints.Count < heroPoints)
            {
                spent++;
                killers.Add(Nearby(world) + " (hero point)");
            }
            heroPoints = hero.HeroPoints.Count;
            if (!hero.IsDead)
            {
                if (hero.HitPoints < hero.MaxHitPoints * 0.45 && world.Hero.HealRemaining <= 0 && world.DrinkPotion())
                    potions++;
                var target = world.Monsters
                    .Where(m => !m.IsDead && (m.State != MonsterState.Idle || m.DistanceTo(world.Hero) < 5))
                    .MinBy(m => m.DistanceTo(world.Hero));
                if (target is not null || world.Hero.IsSwinging)
                    calmSince = world.Time;
                else if (world.Time - calmSince >= GameWorld.RegenDelaySeconds)
                    calm += Tick;
                if (world.Regenerating)
                    regenerated += hero.MaxHitPoints * GameWorld.RegenShare * Tick;
                if (target is not null)
                {
                    if (!world.Hero.IsSwinging)
                        Act(world, bar, classId, target);
                }
                else if (hero.HitPoints < hero.MaxHitPoints * 0.8)
                {
                    // A careful player catches their breath before the next pack.
                }
                else if (world.Time >= nextMove)
                {
                    // North along the road: the next pack still standing, then the camp.
                    var next = world.Monsters.Where(m => !m.IsDead).MaxBy(m => m.Y);
                    var (gx, gy) = next is not null ? (next.X, next.Y) : (exit.At[0] + 0.5, exit.At[1] + 0.5);
                    world.MoveTo(gx, gy);
                    nextMove = world.Time + 1.0;
                }
            }
            world.Update(Tick);
        }
        return new RoadResult(deaths, spent, potions, world.Time / 60, world.ZoneComplete, hero.Level, killers, calm, regenerated);
    }

    /// <summary>What the hero was fighting when it went down: the living monsters within 6 tiles.</summary>
    private static string Nearby(GameWorld world)
    {
        var near = world.Monsters.Where(m => !m.IsDead && m.DistanceTo(world.Hero) < 6)
            .GroupBy(m => m.Definition.Id + (m.Creature.Stats.Rank == Creatures.CreatureRank.Normal ? "" : $" {m.Creature.Stats.Rank}"))
            .Select(g => $"{g.Count()} {g.Key}");
        return string.Join(" + ", near);
    }

    public static List<PackResult> Run(string classId, int seeds = Seeds)
    {
        var content = TestCharacters.Content;
        var zone = content.Zone("gallows_road");
        var results = new List<PackResult>();
        for (var p = 0; p < zone.Spawns.Count; p++)
        {
            var spawn = zone.Spawns[p];
            var members = string.Join(", ", spawn.Pack.Select(m => $"{m.Count} {m.Creature}{(m.Rank == Creatures.CreatureRank.Normal ? "" : $" ({m.Rank})")}"));
            int downs = 0;
            double hpLost = 0, potions = 0, seconds = 0;
            for (var seed = 1; seed <= seeds; seed++)
            {
                var fight = Fight(content, zone with { Spawns = [spawn] }, classId, seed);
                downs += fight.Downs;
                hpLost += fight.HpLostPct;
                potions += fight.Potions;
                seconds += fight.Seconds;
            }
            results.Add(new PackResult(p + 1, members, seeds, downs, hpLost / seeds, potions / seeds, seconds / seeds));
        }
        return results;
    }

    private static (int Downs, double HpLostPct, int Potions, double Seconds) Fight(
        Content.ContentDatabase content, ZoneDefinition zone, string classId, int seed)
    {
        var hero = classId == "wizard" ? TestCharacters.Wizard() : TestCharacters.Fighter();
        var world = new GameWorld(zone, content.Creatures, hero, seed, content.AbilityBar(classId));
        Dress(hero, world.Hero.Equipment, classId);
        // Start a few tiles from the pack, on the nearest open ground, so it aggroes at once.
        var (sx, sy) = (zone.Spawns[0].At[0] + 0.5, zone.Spawns[0].At[1] + 0.5);
        (world.Hero.X, world.Hero.Y) = NearestOpen(world.Map, sx, sy, 4.0);
        var bar = content.AbilityBar(classId);
        int downs = 0, potions = 0;
        var heroPoints = hero.HeroPoints.Count;
        double lowest = hero.HitPoints, start = world.Time;
        while (world.Time - start < 90 && world.Monsters.Any(m => !m.IsDead) && !hero.IsDead)
        {
            if (hero.HitPoints < hero.MaxHitPoints * 0.45 && world.Hero.HealRemaining <= 0 && world.DrinkPotion())
                potions++;
            var target = world.Monsters.Where(m => !m.IsDead).MinBy(m => m.DistanceTo(world.Hero));
            if (target is not null && !world.Hero.IsSwinging)
                Act(world, bar, classId, target);
            world.Update(Tick);
            if (hero.HeroPoints.Count < heroPoints)
            {
                downs++;
                heroPoints = hero.HeroPoints.Count;
            }
            lowest = Math.Min(lowest, hero.HitPoints);
        }
        if (hero.IsDead)
            downs++;
        return (downs, 100.0 * (hero.MaxHitPoints - Math.Max(0, hero.HitPoints)) / hero.MaxHitPoints, potions, world.Time - start);
    }

    private static void Act(GameWorld world, IReadOnlyDictionary<string, Abilities.AbilityDefinition> bar, string classId, MonsterActor target)
    {
        var resource = world.Hero.Resource;
        bool Ready(string slot) => bar.TryGetValue(slot, out var a) && resource.CanSpend(a.Cost) && world.CooldownRemaining(a) <= 0;
        if (classId == "fighter")
        {
            if (Ready("1") && target.DistanceTo(world.Hero) > 2.5)
                world.UseSlot("1", target);
            else if (Ready("rmb") && target.DistanceTo(world.Hero) < 1.6)
                world.UseSlot("rmb", target);
            else if (world.Hero.CurrentTarget != target)
                world.Attack(target);
        }
        else
        {
            if (Ready("rmb"))
                world.UseSlot("rmb", target);
            else if (world.Hero.CurrentTarget != target)
                world.Attack(target);
        }
    }

    private static (double X, double Y) NearestOpen(TileMap map, double x, double y, double distance)
    {
        for (var d = distance; d >= 1; d -= 0.5)
        for (var angle = 0; angle < 360; angle += 15)
        {
            var (px, py) = (x + d * Math.Cos(angle * Math.PI / 180), y + d * Math.Sin(angle * Math.PI / 180));
            if (map.IsWalkable(px, py))
                return (Math.Floor(px) + 0.5, Math.Floor(py) + 0.5);
        }
        return (x, y);
    }

    private static void Dress(Character hero, Items.Equipment gear, string kit)
    {
        foreach (var id in Kits[kit])
            gear.Equip(new Items.Item(TestCharacters.Content.Item(id), Items.Rarity.Common, 1));
        hero.RestAtShrine();
    }

    private static Character Dressed(string classId, string kit)
    {
        var hero = classId == "wizard" ? TestCharacters.Wizard() : TestCharacters.Fighter();
        Dress(hero, new Items.Equipment(hero), kit);
        return hero;
    }

    private sealed record Summary(Character Hero)
    {
        public string Describe() =>
            $"level-1 hero: {Hero.MaxHitPoints} HP, AC {Hero.ArmorClass().Total}, Fortitude {Hero.Save(Rules.StatisticKind.Fortitude).Total}";
    }
}
