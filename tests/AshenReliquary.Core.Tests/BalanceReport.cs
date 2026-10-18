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
    /// <summary>Fights per pack and runs per class; a handful by default so the suite stays quick, ASHEN_BALANCE_RUNS=40 for a real report.</summary>
    private static readonly int Seeds = int.TryParse(Environment.GetEnvironmentVariable("ASHEN_BALANCE_RUNS"), out var runs) ? runs : 4;

    /// <summary>Runs of a whole later zone: one by default (each is minutes of game time), ASHEN_BALANCE_RUNS when set.</summary>
    private static readonly int ZoneSeeds = Environment.GetEnvironmentVariable("ASHEN_BALANCE_RUNS") is { Length: > 0 } ? Seeds : 1;
    internal const double Tick = 1 / 30.0;

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

    public sealed record RoadResult(int Deaths, int HeroPointsSpent, int Potions, double Minutes, bool Reached, int Level, List<string> Killers, double CalmSeconds = 0, double Regenerated = 0)
    {
        /// <summary>The hero's level on first meeting the zone's boss (0: none).</summary>
        public int BossLevel { get; init; }
    }

    /// <summary>The starting kits as the game hands them out (GameSession.StartingKit), and variants to compare.</summary>
    public static readonly Dictionary<string, string[]> Kits = new()
    {
        ["fighter"] = ["longsword", "wooden_shield", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"],
        ["fighter in leather"] = ["longsword", "wooden_shield", "leather_chest", "leather_pants", "leather_boots", "leather_gloves"],
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
        var runs = Enumerable.Range(1, Seeds).Select(seed => RoadRun(kit, seed)).ToList();
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
                Tactics.Upkeep(world);
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

    [Theory]
    [InlineData("fighter")]
    [InlineData("wizard")]
    public void The_Rotwater_Fens_at_level_2(string kit)
    {
        var runs = Enumerable.Range(1, ZoneSeeds).Select(seed => ZoneRun("rotwater_fens", "chapel", kit, seed, 2)).ToList();
        output.WriteLine($"{kit}: the Rotwater Fens from the landing to the chapel doors, every pack, {runs.Count} runs");
        foreach (var r in runs)
            output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  level {r.Level}  {(r.Reached ? "reached the chapel" : "did not finish")}");
        output.WriteLine($"  average deaths {runs.Average(r => r.Deaths):0.0}, downs {runs.Average(r => r.Deaths + r.HeroPointsSpent):0.0}, minutes {runs.Average(r => r.Minutes):0.0}");
        foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()))
            output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
        Assert.NotEmpty(runs);
    }

    [Theory]
    [InlineData("fighter")]
    [InlineData("wizard")]
    public void Sallows_Mire_at_level_4(string kit)
    {
        var runs = Enumerable.Range(1, ZoneSeeds).Select(seed => ZoneRun("sallows_mire", "iron_march", kit, seed, 4)).ToList();
        output.WriteLine($"{kit}: Sallow's Mire from the tunnel to the Iron March gate, every pack and Mother Sallow, {runs.Count} runs");
        foreach (var r in runs)
            output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  level {r.BossLevel} at Sallow, {r.Level} at the end  {(r.Reached ? "through the gate" : "did not finish")}");
        output.WriteLine($"  average deaths {runs.Average(r => r.Deaths):0.0}, downs {runs.Average(r => r.Deaths + r.HeroPointsSpent):0.0}, minutes {runs.Average(r => r.Minutes):0.0}");
        foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()).Take(10))
            output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
        Assert.NotEmpty(runs);
    }

    [Theory]
    [InlineData("fighter")]
    [InlineData("wizard")]
    public void The_Drowned_Chapel_at_level_3(string kit)
    {
        foreach (var lantern in new[] { true, false })
        {
            var runs = Enumerable.Range(1, ZoneSeeds).Select(seed => ZoneRun("drowned_chapel", "mire", kit, seed, 3, lantern)).ToList();
            output.WriteLine($"{kit} {(lantern ? "with" : "without")} the lantern: the Drowned Chapel, every pack, {runs.Count} runs");
            foreach (var r in runs)
                output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  level {r.Level}  {(r.Reached ? "reached the tunnel" : "did not finish")}");
            output.WriteLine($"  average deaths {runs.Average(r => r.Deaths):0.0}, downs {runs.Average(r => r.Deaths + r.HeroPointsSpent):0.0}, minutes {runs.Average(r => r.Minutes):0.0}");
            foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()).Take(8))
                output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
            Assert.NotEmpty(runs);
        }
    }

    /// <summary>The optional Prior's Ossuary at level 4, lantern in hand (it is under the chapel), down to Prior Aldous and back up.</summary>
    [Theory]
    [InlineData("fighter")]
    [InlineData("wizard")]
    public void The_Priors_Ossuary_at_level_4(string kit)
    {
        var runs = Enumerable.Range(1, ZoneSeeds).Select(seed => ZoneRun("prior_ossuary", "chapel", kit, seed, 4, lantern: true)).ToList();
        output.WriteLine($"{kit}: the Prior's Ossuary, every pack and Prior Aldous, then back up the stair, {runs.Count} runs");
        foreach (var r in runs)
            output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  level {r.Level}  {(r.Reached ? "back in the chapel" : "did not finish")}");
        output.WriteLine($"  average deaths {runs.Average(r => r.Deaths):0.0}, downs {runs.Average(r => r.Deaths + r.HeroPointsSpent):0.0}, minutes {runs.Average(r => r.Minutes):0.0}");
        foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()).Take(8))
            output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
        Assert.NotEmpty(runs);
    }

    /// <summary>
    /// A whole zone from a hero of <paramref name="level"/> (picks made as <see cref="LevelTo"/> makes them): every pack
    /// in the zone file's order (in the Fens the main route, then the Sunken Road, then back for the Old Dyke), then the
    /// exit <paramref name="exitId"/>. Like <see cref="RoadRun"/> otherwise.
    /// The route is planned here over the whole map, then walked a stretch at a time, as a player clicks ahead.
    /// </summary>
    public static RoadResult ZoneRun(string zoneId, string exitId, string kit, int seed, int level, bool lantern = false)
    {
        var classId = kit.Split(' ')[0];
        var content = TestCharacters.Content;
        var zone = content.Zone(zoneId);
        var hero = classId == "wizard" ? TestCharacters.Wizard() : TestCharacters.Fighter();
        var bar = content.AbilityBar(classId);
        var world = new GameWorld(zone, content.Creatures, hero, seed, bar, content.Loot, content.Zones, content.Dialogues, content.Abilities);
        Dress(hero, world.Hero.Equipment, kit);
        LevelTo(world, level);
        if (lantern)
            world.Hero.Equipment.TryAdd(new Items.Item(content.Item("pilgrims_lantern"), Items.Rarity.Common, 1));
        var exit = zone.Exits.Single(e => e.Id == exitId);
        int deaths = 0, spent = 0, potions = 0;
        var killers = new List<string>();
        var heroPoints = hero.HeroPoints.Count;
        var wasDead = false;
        double nextMove = 0;
        List<(double X, double Y)>? route = null;
        (double X, double Y)? routeGoal = null;
        var bossLevel = 0;
        double nextDodge = 0;
        while (!world.ZoneComplete && world.Zone.Id == zoneId && world.Time < 3600)
        {
            if (hero.IsDead != wasDead)
            {
                if (hero.IsDead)
                {
                    deaths++;
                    killers.Add(Nearby(world));
                }
                wasDead = hero.IsDead;
                route = null;
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
                Tactics.Upkeep(world);
                var target = world.Monsters
                    .Where(m => !m.IsDead && (m.State != MonsterState.Idle || m.DistanceTo(world.Hero) < 5))
                    .MinBy(m => m.DistanceTo(world.Hero));
                if (world.Boss is not null && bossLevel == 0)
                    bossLevel = hero.Level;
                if (StepOutOfPool(world, ref nextDodge) is not null)
                {
                    route = null;
                }
                else if (target is not null)
                {
                    if (!world.Hero.IsSwinging && world.Time >= nextDodge)
                        Act(world, bar, classId, target);
                    route = null;
                }
                else if (hero.HitPoints < hero.MaxHitPoints * 0.8)
                {
                    // Catch a breath; and step out of the bog to do it.
                    if (world.Map.IsBog(world.Hero.X, world.Hero.Y) && world.Time >= nextMove)
                    {
                        var (dx, dy) = NearestDry(world.Map, world.Hero.X, world.Hero.Y);
                        world.MoveTo(dx, dy);
                        nextMove = world.Time + 1.0;
                    }
                }
                else if (world.Time >= nextMove)
                {
                    var next = world.Monsters.Where(m => !m.IsDead && m.SpawnIndex >= 0).MinBy(m => m.SpawnIndex);
                    var goal = next is not null ? (next.X, next.Y) : (exit.At[0] + 0.5, exit.At[1] + 0.5);
                    if (route is null || routeGoal is not { } g || Math.Abs(g.X - goal.Item1) + Math.Abs(g.Y - goal.Item2) > 2)
                    {
                        route = Pathfinder.Find(world.Map, ((int)world.Hero.X, (int)world.Hero.Y), ((int)goal.Item1, (int)goal.Item2), maxNodes: 60000);
                        routeGoal = goal;
                    }
                    if (route is { Count: > 0 })
                    {
                        // Click a little way along the route, past the waypoints already reached.
                        var near = route.Select((p, i) => (p, i)).MinBy(t => Math.Abs(t.p.X - world.Hero.X) + Math.Abs(t.p.Y - world.Hero.Y)).i;
                        var ahead = route[Math.Min(route.Count - 1, near + 12)];
                        world.MoveTo(ahead.X, ahead.Y);
                    }
                    else
                    {
                        world.MoveTo(goal.Item1, goal.Item2);
                    }
                    nextMove = world.Time + 1.0;
                }
            }
            world.Update(Tick);
        }
        // Walking into an exit that leads on travels (and the new zone starts incomplete), so leaving counts as finishing.
        return new RoadResult(deaths, spent, potions, world.Time / 60, world.ZoneComplete || world.Zone.Id != zoneId, hero.Level, killers) { BossLevel = bossLevel };
    }



    /// <summary>
    /// Steps out of a boss's rot pool (or its warning ring), as a player would: straight away from its centre, a little
    /// further than its edge; from its very centre (it lands where the hero stands), sideways round the boss, so a melee
    /// hero stays in reach. Returns the pool while the hero is in one.
    /// </summary>
    internal static Hazard? StepOutOfPool(GameWorld world, ref double nextDodge)
    {
        var pool = world.Hazards.FirstOrDefault(h => h.Covers(world.Hero.X, world.Hero.Y) && world.Time < h.EndsAt);
        if (pool is null || world.Time < nextDodge)
            return pool;
        var (dx, dy) = (world.Hero.X - pool.X, world.Hero.Y - pool.Y);
        if (dx * dx + dy * dy < 0.09 && world.Boss is { } boss)
            (dx, dy) = (-(world.Hero.Y - boss.Y), world.Hero.X - boss.X);
        var d = Math.Max(0.1, Math.Sqrt(dx * dx + dy * dy));
        world.MoveTo(pool.X + dx / d * (pool.Radius + 1.2), pool.Y + dy / d * (pool.Radius + 1.2));
        nextDodge = world.Time + 0.8;
        return pool;
    }

    /// <summary>
    /// Takes the hero up to <paramref name="level"/> with the first legal picks (Toughness when a general feat comes up;
    /// at 5 the boosts go to the key attribute, Constitution, Dexterity and Wisdom).
    /// </summary>
    internal static void LevelTo(GameWorld world, int level)
    {
        var c = world.Hero.Character;
        while (c.Level < level)
        {
            world.GainXp(1000, world.Hero);
            ChoosePending(world);
        }
        c.RestAtShrine();
    }

    /// <summary>Makes the picks for every level still waiting, as <see cref="LevelTo"/> would.</summary>
    internal static void ChoosePending(GameWorld world)
    {
        var c = world.Hero.Character;
        foreach (var at in c.PendingLevels.ToList())
        {
            var gains = Progression.LevelProgression.GainsAt(at);
            string? Feat(Progression.LevelGain gain, FeatType type, string? prefer = null) =>
                (gains & gain) == 0 ? null : c.FeatOptions(type, at).Select(f => f.Id).OrderBy(id => id == prefer ? 0 : 1).First();
            var boosts = (gains & Progression.LevelGain.AttributeBoosts) != 0
                ? new[] { c.KeyAttribute, Rules.AttributeKind.Constitution, Rules.AttributeKind.Dexterity, Rules.AttributeKind.Wisdom, Rules.AttributeKind.Strength }
                    .Distinct().Take(4).ToArray()
                : null;
            world.ChooseLevel(at, new LevelChoice(
                Boosts: boosts,
                ClassFeat: Feat(Progression.LevelGain.ClassFeat, FeatType.Class, c.Class.Id == "wizard" ? "reach_spell" : "lunge"),
                SkillFeat: Feat(Progression.LevelGain.SkillFeat, FeatType.Skill),
                GeneralFeat: Feat(Progression.LevelGain.GeneralFeat, FeatType.General, "toughness"),
                AncestryFeat: Feat(Progression.LevelGain.AncestryFeat, FeatType.Ancestry),
                SkillIncrease: (gains & Progression.LevelGain.SkillIncrease) != 0 ? Skills.Attribute.Keys.Order().First(sk => c.CanIncrease(sk, at)) : null));
        }
    }

    public sealed record BossResult(bool Won, double Seconds, int Potions, int Phase, double BossHpLeft);

    [Theory]
    [InlineData("fighter")]
    [InlineData("wizard")]
    public void Mother_Sallow_at_levels_4_and_5(string kit)
    {
        foreach (var (level, runed) in new[] { (4, false), (5, false), (5, true) })
        {
            var fights = Enumerable.Range(1, Math.Max(Seeds, 6)).Select(seed => BossFight(kit, seed, runed, level)).ToList();
            output.WriteLine($"{kit} at level {level}{(runed ? " with +1 striking weapon and +1 armour" : " in the starting kit")}: Mother Sallow, {fights.Count} fights");
            foreach (var f in fights)
                output.WriteLine($"  {(f.Won ? "won " : "lost")}  {f.Seconds,5:0} s  potions {f.Potions}  reached phase {f.Phase + 1}  her HP left {f.BossHpLeft:0%}");
            output.WriteLine($"  won {fights.Count(f => f.Won)} of {fights.Count}, average {fights.Average(f => f.Seconds):0} s");
        }
    }

    /// <summary>
    /// One try at Mother Sallow: a hero of <paramref name="level"/> walks into her clearing and fights to the end (no respawn). The bot
    /// steps out of rot pools and their warning rings, drinks at 45% and otherwise fights as everywhere else.
    /// </summary>
    public static BossResult BossFight(string kit, int seed, bool runed, int level = 5)
    {
        var classId = kit.Split(' ')[0];
        var content = TestCharacters.Content;
        var mire = content.Zone("sallows_mire");
        var zone = mire with { Spawns = [mire.Spawns.Single(s => s.Pack.Any(m => m.Creature == "mother_sallow"))] };
        var hero = classId == "wizard" ? TestCharacters.Wizard() : TestCharacters.Fighter();
        var bar = content.AbilityBar(classId);
        var world = new GameWorld(zone, content.Creatures, hero, seed, bar, content.Loot, content.Zones, content.Dialogues, content.Abilities);
        foreach (var id in Kits[kit])
        {
            var definition = content.Item(id);
            var item = runed && definition.IsWeapon ? new Items.Item(definition, Items.Rarity.Common, 5, potency: 1, striking: 1)
                : runed && definition.IsBodyArmor ? new Items.Item(definition, Items.Rarity.Common, 5, potency: 1)
                : new Items.Item(definition, Items.Rarity.Common, 1);
            world.Hero.Equipment.Equip(item);
        }
        LevelTo(world, level);
        var sallow = world.Monsters.Single(m => m.Definition.Id == "mother_sallow");
        (world.Hero.X, world.Hero.Y) = (sallow.HomeX, sallow.HomeY - 9);
        int potions = 0;
        double start = world.Time, nextDodge = 0;
        while (world.Time - start < 400 && !sallow.IsDead && !hero.IsDead)
        {
            if (hero.HitPoints < hero.MaxHitPoints * 0.45 && world.Hero.HealRemaining <= 0 && world.DrinkPotion())
                potions++;
            Tactics.Upkeep(world);
            if (StepOutOfPool(world, ref nextDodge) is { } pool)
            {
            }
            else if (!world.Hero.IsSwinging && world.Time >= nextDodge)
            {
                var target = world.Monsters.Where(m => !m.IsDead).MinBy(m => m.DistanceTo(world.Hero) + (m == sallow ? 2 : 0));
                if (target is not null)
                    Act(world, bar, classId, target);
            }
            world.Update(Tick);
        }
        return new BossResult(sallow.IsDead, world.Time - start, potions, sallow.BossPhase,
            (double)sallow.Creature.HitPoints / sallow.Creature.Stats.HitPoints);
    }

    internal static (double X, double Y) NearestDry(TileMap map, double x, double y)
    {
        for (var r = 1; r < 12; r++)
        for (var dy = -r; dy <= r; dy++)
        for (var dx = -r; dx <= r; dx++)
        {
            var (cx, cy) = ((int)x + dx, (int)y + dy);
            if (!map.IsBlocked(cx, cy) && !map.IsBog(cx, cy))
                return (cx + 0.5, cy + 0.5);
        }
        return (x, y);
    }

    /// <summary>What the hero was fighting when it went down: the living monsters within 6 tiles.</summary>
    internal static string Nearby(GameWorld world)
    {
        var near = world.Monsters.Where(m => !m.IsDead && m.DistanceTo(world.Hero) < 6)
            .GroupBy(m => m.Definition.Id + (m.Creature.Stats.Rank == Creatures.CreatureRank.Normal ? "" : $" {m.Creature.Stats.Rank}"))
            .Select(g => $"{g.Count()} {g.Key}");
        return string.Join(" + ", near);
    }

    [Theory]
    [InlineData("fighter", 2)]
    [InlineData("wizard", 2)]
    [InlineData("fighter", 3)]
    [InlineData("wizard", 3)]
    public void Rotwater_Fens_packs(string classId, int heroLevel)
    {
        var results = Run(classId, zoneId: "rotwater_fens", heroLevel: heroLevel);
        output.WriteLine($"{classId} at level {heroLevel}: pack, members, downs per {Seeds} fights, HP lost, potions, seconds");
        foreach (var r in results)
            output.WriteLine($"  {r.Pack,2} {r.Members,-64} downs {r.Downs,2}/{r.Fights}  hp lost {r.HpLostPct,3:0}%  potions {r.Potions:0.0}  {r.Seconds:0.0} s");
        output.WriteLine($"  total downs {results.Sum(r => r.Downs)} in {results.Sum(r => r.Fights)} fights");
        Assert.NotEmpty(results);
    }

    public static List<PackResult> Run(string classId, int? fights = null, string zoneId = "gallows_road", int heroLevel = 1)
    {
        var seeds = fights ?? Seeds;
        var content = TestCharacters.Content;
        var zone = content.Zone(zoneId);
        var results = new List<PackResult>();
        for (var p = 0; p < zone.Spawns.Count; p++)
        {
            var spawn = zone.Spawns[p];
            var members = $"L{spawn.Level ?? zone.Level} " + string.Join(", ", spawn.Pack.Select(m => $"{m.Count} {m.Creature}{(m.Rank == Creatures.CreatureRank.Normal ? "" : $" ({m.Rank})")}"));
            int downs = 0;
            double hpLost = 0, potions = 0, seconds = 0;
            for (var seed = 1; seed <= seeds; seed++)
            {
                var fight = Fight(content, zone with { Spawns = [spawn] }, classId, seed, heroLevel);
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
        Content.ContentDatabase content, ZoneDefinition zone, string classId, int seed, int heroLevel = 1)
    {
        var hero = classId == "wizard" ? TestCharacters.Wizard() : TestCharacters.Fighter();
        var world = new GameWorld(zone, content.Creatures, hero, seed, content.AbilityBar(classId), content.Loot, content.Zones, content.Dialogues, content.Abilities);
        Dress(hero, world.Hero.Equipment, classId);
        LevelTo(world, heroLevel);
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
            Tactics.Upkeep(world);
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

    /// <summary>The bot's choice of action (shared with the "--battle" demo: <see cref="Tactics"/>).</summary>
    internal static void Act(GameWorld world, IReadOnlyDictionary<string, Abilities.AbilityDefinition> bar, string classId, MonsterActor target) =>
        Tactics.Act(world, classId, target);

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

    internal static void Dress(Character hero, Items.Equipment gear, string kit)
    {
        foreach (var id in Kits[kit])
            gear.Equip(new Items.Item(TestCharacters.Content.Item(id), Items.Rarity.Common, 1));
        foreach (var (id, count) in StartingBelt)
            gear.TryAdd(new Items.Item(TestCharacters.Content.Item(id), Items.Rarity.Common, 1, count: count));
        hero.RestAtShrine();
    }

    /// <summary>The belt every hero starts with (GameSession.StartingBelt).</summary>
    public static readonly (string Id, int Count)[] StartingBelt = [("alchemists_fire", 3), ("antidote", 1)];

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
