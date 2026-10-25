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
    [InlineData("rogue")]
    [InlineData("ranger")]
    [InlineData("champion")]
    [InlineData("necromancer")]
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
        // The subclasses (M3): a Vanguard starts with a greatsword instead of the sword and shield.
        ["fighter vanguard"] = ["greatsword", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"],
        ["fighter bulwark"] = ["longsword", "wooden_shield", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"],
        ["wizard pyromancer"] = ["staff", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves"],
        ["wizard stormcaller"] = ["staff", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves"],
        // The Rogue (M3 step 2): a shortsword and leathers.
        ["rogue"] = ["shortsword", "leather_chest", "leather_pants", "leather_boots", "leather_gloves", "leather_hood"],
        ["rogue cutthroat"] = ["shortsword", "leather_chest", "leather_pants", "leather_boots", "leather_gloves", "leather_hood"],
        ["rogue trickster"] = ["shortsword", "leather_chest", "leather_pants", "leather_boots", "leather_gloves", "leather_hood"],
        // The Ranger (M3 step 3): a shortbow and leathers.
        ["ranger"] = ["shortbow", "leather_chest", "leather_pants", "leather_boots", "leather_gloves", "leather_hood"],
        ["ranger marksman"] = ["shortbow", "leather_chest", "leather_pants", "leather_boots", "leather_gloves", "leather_hood"],
        ["ranger beastmaster"] = ["shortbow", "leather_chest", "leather_pants", "leather_boots", "leather_gloves", "leather_hood"],
        // The Champion (M3 step 4): the fighter's sword, shield and mail.
        ["champion"] = ["longsword", "wooden_shield", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"],
        ["champion lanternbearer"] = ["longsword", "wooden_shield", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"],
        ["champion oathbreaker"] = ["longsword", "wooden_shield", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"],
        ["champion paladin"] = ["longsword", "wooden_shield", "chain_cuirass", "leather_pants", "leather_boots", "leather_gloves"],
        // The Gunslinger (after M4 step 6): a brace of pistols or an arquebus, in leathers.
        ["gunslinger pistolero"] = ["flintlock_pistol", "flintlock_pistol", "leather_chest", "leather_pants", "leather_boots", "leather_gloves", "leather_hood"],
        ["gunslinger sniper"] = ["arquebus", "leather_chest", "leather_pants", "leather_boots", "leather_gloves", "leather_hood"],
        // The Necromancer (M3 step 5): the wizard's staff and robes.
        ["necromancer"] = ["staff", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves"],
        ["necromancer bonelord"] = ["staff", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves"],
        ["necromancer plaguebringer"] = ["staff", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves"],
    };

    [Theory]
    [InlineData("fighter")]
    [InlineData("fighter in leather")]
    [InlineData("wizard")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
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
        var hero = TestCharacters.ForKit(kit);
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
                    .Where(m => world.CanTarget(m) && (m.State != MonsterState.Idle || m.DistanceTo(world.Hero) < 5))
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
    /// Levels 6-10 before Act 2 exists (M4 step 1): Sallow's Mire with every pack and Mother Sallow raised to the
    /// hero's level, the hero in the starting kit with the runes a player would have etched by then (+1 at 6 and 8, +2
    /// at 10). Its deaths and minutes are the yardstick for the class kits past level 5.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Sallows_Mire_raised_to_levels_6_8_and_10(string kit)
    {
        foreach (var level in new[] { 6, 8, 10 })
        {
            var runs = Enumerable.Range(1, ZoneSeeds).Select(seed => ZoneRun("sallows_mire", "iron_march", kit, seed, level,
                monsterLevel: level, runes: level >= 10 ? 2 : 1)).ToList();
            output.WriteLine($"{kit} at level {level}: Sallow's Mire raised to {level}, {runs.Count} runs");
            foreach (var r in runs)
                output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  {(r.Reached ? "through the gate" : "did not finish")}");
            foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()).Take(4))
                output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
        }
    }

    /// <summary>
    /// The Iron March (M4 step 2): the whole zone, every pack in order, from the Mire causeway to Ironhold's gate, for a
    /// level-6 hero (Act 1 ends at 5-6) in the starting kit with +1 runes. Its deaths and minutes measure Act 2's
    /// squads, cover and kiting crossbowmen.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Iron_March_at_level_6(string kit)
    {
        var runs = Enumerable.Range(1, ZoneSeeds).Select(seed => ZoneRun("iron_march", "ironhold", kit, seed, 6, runes: 1)).ToList();
        output.WriteLine($"{kit}: the Iron March from the causeway to Ironhold's gate, every pack, {runs.Count} runs");
        foreach (var r in runs)
            output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  level {r.Level}  {(r.Reached ? "reached Ironhold" : "did not finish")}");
        foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()).Take(4))
            output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
    }

    /// <summary>
    /// The Barrow Fields (M4 step 3): every pack, the open barrows and the rising dead, then down into the Old Barracks;
    /// a level-7 hero in the starting kit with +1 runes. The bot smashes rising corpses between fights (Tactics.Tidy).
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Barrow_Fields_at_level_7(string kit) => ReportZone(kit, "barrow_fields", "barracks", 7, "the Barrow Fields to the barracks door");

    /// <summary>
    /// The Old Barracks (M4 step 3): every pack, in the dark, past sixteen traps it must spot or spring (a rogue disarms
    /// what it spots), and back up the stair; a level-8 hero in the starting kit with +1 runes.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Old_Barracks_at_level_8(string kit) => ReportZone(kit, "old_barracks", "stair", 8, "the Old Barracks and back up the stair");

    /// <summary>
    /// The Siege Walls (M4 step 4): every pack, then Ser Hadric on his wall, to the Herald gate; a level-9 hero in the
    /// starting kit with +2 runes on the weapon.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Siege_Walls_at_level_9(string kit) => ReportZone(kit, "siege_walls", "heights", 9, "the Siege Walls and Ser Hadric", runes: 2);

    /// <summary>
    /// The Frostbitten Pass (M4 step 6): every pack, in the cold, warming at the campfires between fights (Tactics.WarmUp),
    /// up to Saint Oriel's Rest; a level-11 hero in the starting kit with +2 runes.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Frostbitten_Pass_at_level_11(string kit) => ReportZone(kit, "frostbitten_pass", "rest", 11, "the Frostbitten Pass to the Rest", runes: 2);

    /// <summary>
    /// The Hollow Woods (M4 step 6): every pack, the unseen stalkers and the phantoms, to the catacombs' door; a level-12
    /// hero in the starting kit with +2 runes.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Hollow_Woods_at_level_12(string kit) => ReportZone(kit, "hollow_woods", "catacombs", 12, "the Hollow Woods to the catacombs", runes: 2);

    /// <summary>
    /// The Catacombs of Oriel (M4 step 6): every pack, the Choir's acolytes turning the hero's allies, the Hollow Abbot,
    /// and back up the stair; a level-13 hero in the starting kit with +2 runes.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Catacombs_of_Oriel_at_level_13(string kit) => ReportZone(kit, "catacombs_of_oriel", "woods", 13, "the Catacombs of Oriel and back up the stair", runes: 2);

    /// <summary>
    /// The Bell Tower (M4 step 7): every pack, the Choir of Whispers in the belfry, and out by the cliff road; a level-14
    /// hero in the starting kit with +2 runes.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Bell_Tower_at_level_14(string kit) => ReportZone(kit, "bell_tower", "cliff_road", 14, "the Bell Tower and the Choir of Whispers", runes: 2);

    /// <summary>
    /// Act 4's zones (M4 step 8), each at its own level in the starting kit with the runes a player would have by then
    /// (+2 in the wastes and on the rivers, +3 from the nest). The nest's way on waits on Vorthrax (step 9), so its run
    /// starts with him slain; the Reliquary's with every relic undealt with, so all three Echoes stand in the gauntlet.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Ash_Wastes_at_level_16(string kit) => ReportZone(kit, "ash_wastes", "rivers", 16, "the Ash Wastes", runes: 2);

    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Lava_Rivers_at_level_17(string kit) => ReportZone(kit, "lava_rivers", "nest", 17, "the Lava Rivers", runes: 2);

    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Wyrms_Nest_at_level_18(string kit) => ReportZone(kit, "wyrms_nest", "reliquary", 18, "the Wyrm's Nest", runes: 3,
        flags: new Dictionary<string, string> { ["act4.vorthrax"] = "slain" });

    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Reliquary_at_level_19(string kit) => ReportZone(kit, "reliquary", "throne", 19, "the Reliquary and the gauntlet of Echoes", runes: 3);

    private void ReportZone(string kit, string zoneId, string exitId, int level, string what, int runes = 1,
        IReadOnlyDictionary<string, string>? flags = null)
    {
        var runs = Enumerable.Range(1, ZoneSeeds).Select(seed => ZoneRun(zoneId, exitId, kit, seed, level, runes: runes, flags: flags)).ToList();
        output.WriteLine($"{kit}: {what}, every pack, {runs.Count} runs");
        foreach (var r in runs)
            output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  level {r.Level}  {(r.Reached ? "finished" : "did not finish")}");
        foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()).Take(4))
            output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
    }

    /// <summary>
    /// Levels 11-15 before Act 3's zones exist (M4 step 5): the Siege Walls with every pack and Ser Hadric raised to the
    /// hero's level, in the starting kit with +2 runes. The yardstick for the class kits past level 10.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Siege_Walls_raised_to_levels_11_13_and_15(string kit)
    {
        foreach (var level in new[] { 11, 13, 15 })
        {
            var runs = Enumerable.Range(1, ZoneSeeds).Select(seed => ZoneRun("siege_walls", "heights", kit, seed, level,
                monsterLevel: level, runes: 2)).ToList();
            output.WriteLine($"{kit} at level {level}: the Siege Walls raised to {level}, {runs.Count} runs");
            foreach (var r in runs)
                output.WriteLine($"  deaths {r.Deaths}  hero points {r.HeroPointsSpent}  potions {r.Potions,2}  {r.Minutes:0.0} min  {(r.Reached ? "through the gate" : "did not finish")}");
            foreach (var g in runs.SelectMany(r => r.Killers).GroupBy(k => k).OrderByDescending(g => g.Count()).Take(3))
                output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
        }
    }

    /// <summary>
    /// A whole zone from a hero of <paramref name="level"/> (picks made as <see cref="LevelTo"/> makes them): every pack
    /// in the zone file's order (in the Fens the main route, then the Sunken Road, then back for the Old Dyke), then the
    /// exit <paramref name="exitId"/>. Like <see cref="RoadRun"/> otherwise.
    /// The route is planned here over the whole map, then walked a stretch at a time, as a player clicks ahead.
    /// </summary>
    public static RoadResult ZoneRun(string zoneId, string exitId, string kit, int seed, int level, bool lantern = false,
        int? monsterLevel = null, int runes = 0, IReadOnlyDictionary<string, string>? flags = null)
    {
        var classId = kit.Split(' ')[0];
        var content = TestCharacters.Content;
        var zone = content.Zone(zoneId);
        // Only the exit the run is for: a Step out of a pool near the Mire's chapel door had walked the hero back into
        // the chapel, which counted as finishing the zone.
        zone = zone with { Exits = zone.Exits.Where(e => e.Id == exitId).ToList() };
        // Raised: every pack (and the boss) at this level, a stand-in for the acts not yet built.
        if (monsterLevel is { } raised)
            zone = zone with { Level = raised, Spawns = zone.Spawns.Select(s => s with { Level = raised }).ToList() };
        var hero = TestCharacters.ForKit(kit);
        var bar = content.AbilityBar(classId);
        var world = new GameWorld(zone, content.Creatures, hero, seed, bar, content.Loot, content.Zones, content.Dialogues, content.Abilities);
        Dress(hero, world.Hero.Equipment, kit);
        LevelTo(world, level);
        // World flags the run starts with (as a load brings them back: the packs and wards that wait on them follow).
        if (flags is not null)
            world.RestoreStory(flags, []);
        // Runes a player of that level would have had etched: potency and striking on the weapon, potency and
        // resilient on the body armour, each up to what the item level allows.
        if (runes > 0)
        {
            foreach (var worn in world.Hero.Equipment.Worn.Values.ToList())
                foreach (var kind in Items.Runework.Kinds(worn.Definition))
                    worn.SetRune(kind, runes);
            world.Hero.Equipment.Refresh();
        }
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
                    .Where(m => world.CanTarget(m) && (m.State != MonsterState.Idle || m.DistanceTo(world.Hero) < 5))
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
                else if (Tactics.Tidy(world))
                {
                    route = null;
                }
                else if (world.Time >= nextMove && Tactics.WarmUp(world))
                {
                    route = null;
                    nextMove = world.Time + 1.0;
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
        var reached = world.ZoneComplete || world.Zone.Id != zoneId;
        if (!reached)
        {
            var next = world.Monsters.Where(m => !m.IsDead && m.SpawnIndex >= 0).MinBy(m => m.SpawnIndex);
            killers.Add($"STUCK at {world.Hero.X:0.0},{world.Hero.Y:0.0} (cold {world.Hero.Cold:0}), next {next?.Definition.Id} #{next?.SpawnIndex} "
                + $"at {next?.X:0.0},{next?.Y:0.0} {next?.State}, path {(next is null ? "-" : Pathfinder.Find(world.Map, ((int)world.Hero.X, (int)world.Hero.Y), ((int)next.X, (int)next.Y), maxNodes: 60000)?.Count.ToString() ?? "none")}");
        }
        return new RoadResult(deaths, spent, potions, world.Time / 60, reached, hero.Level, killers) { BossLevel = bossLevel };
    }



    /// <summary>
    /// Steps out of a boss's rot pool (or its warning ring), as a player would: straight away from its centre, a little
    /// further than its edge; from its very centre (it lands where the hero stands), sideways round the boss, so a melee
    /// hero stays in reach. Returns the pool while the hero is in one.
    /// </summary>
    internal static Hazard? StepOutOfPool(GameWorld world, ref double nextDodge)
    {
        var pool = world.Hazards.FirstOrDefault(h => h.Covers(world.Hero.X, world.Hero.Y) && world.Time < h.EndsAt);
        if (pool is null && StepOutOfLane(world, ref nextDodge) is { } lane)
            return lane;
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
    /// A boss's charge lane marked over the hero (Ser Hadric): walk out of it sideways, as from a rot pool. Returns a
    /// stand-in hazard for the lane while the hero is in it.
    /// </summary>
    internal static Hazard? StepOutOfLane(GameWorld world, ref double nextDodge)
    {
        var hero = world.Hero;
        foreach (var m in world.Monsters)
        {
            if (m.Charge is not { } c || m.IsDead || world.Time >= c.RushAt)
                continue;
            var (dx, dy) = (c.ToX - c.FromX, c.ToY - c.FromY);
            var length = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
            var t = Math.Clamp(((hero.X - c.FromX) * dx + (hero.Y - c.FromY) * dy) / (length * length), 0, 1);
            var (px, py) = (c.FromX + dx * t, c.FromY + dy * t);
            var off = Math.Sqrt((hero.X - px) * (hero.X - px) + (hero.Y - py) * (hero.Y - py));
            if (off > GameWorld.ChargeReach + 0.6)
                continue;
            var lane = new Hazard(px, py, GameWorld.ChargeReach, c.RushAt, c.RushAt, new Rules.Dice(0, 1), 0);
            if (world.Time < nextDodge)
                return lane;
            // Sideways, away from the lane's line (either side when right on it).
            var (sx, sy) = off > 0.05 ? ((hero.X - px) / off, (hero.Y - py) / off) : (-dy / length, dx / length);
            var (gx, gy) = (hero.X + sx * 2.6, hero.Y + sy * 2.6);
            if (!world.Map.IsWalkable(gx, gy))
                (gx, gy) = (hero.X - sx * 2.6, hero.Y - sy * 2.6);
            world.MoveTo(gx, gy);
            nextDodge = world.Time + 0.6;
            return lane;
        }
        return null;
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

    /// <summary>
    /// The dedication a bot takes at level 4: Lay on Hands for those who can't heal themselves, Hunt Prey's d8 for the
    /// rogue, Power Attack for the champion.
    /// </summary>
    internal static string Dedication(string classId) => classId switch
    {
        "rogue" => "ranger_dedication",
        "champion" => "fighter_dedication",
        _ => "champion_dedication",
    };

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
                // Level 2 a class feat; level 4 a multiclass dedication that suits the class (the bots use what it grants).
                ClassFeat: Feat(Progression.LevelGain.ClassFeat, FeatType.Class, at >= 4 ? Dedication(c.Class.Id)
                    : c.Class.Id switch { "wizard" => "reach_spell", "rogue" => "nimble_dodge", "ranger" => "far_shot", "champion" => "fervor", "necromancer" => "soul_siphon", _ => "lunge" }),
                SkillFeat: Feat(Progression.LevelGain.SkillFeat, FeatType.Skill),
                GeneralFeat: Feat(Progression.LevelGain.GeneralFeat, FeatType.General, "toughness"),
                AncestryFeat: Feat(Progression.LevelGain.AncestryFeat, FeatType.Ancestry),
                SkillIncrease: (gains & Progression.LevelGain.SkillIncrease) != 0 ? Skills.Attribute.Keys.Order().First(sk => c.CanIncrease(sk, at)) : null));
        }
    }

    public sealed record BossResult(bool Won, double Seconds, int Potions, int Phase, double BossHpLeft);

    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
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
    /// Ser Hadric (M4 step 4) alone on his wall at level 10, a level 9 hero a level under, with the runes a player would
    /// have by then (+2 potency and striking on the weapon, +1 armour) or in the starting kit.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void Ser_Hadric_at_levels_9_and_10(string kit)
    {
        foreach (var (level, runed) in new[] { (9, true), (10, false), (10, true) })
        {
            var fights = Enumerable.Range(1, Math.Max(Seeds, 6))
                .Select(seed => BossFight(kit, seed, runed, level, "siege_walls", "ser_hadric", potency: 2)).ToList();
            output.WriteLine($"{kit} at level {level}{(runed ? " with a +2 striking weapon and +1 armour" : " in the starting kit")}: Ser Hadric, {fights.Count} fights");
            foreach (var f in fights)
                output.WriteLine($"  {(f.Won ? "won " : "lost")}  {f.Seconds,5:0} s  potions {f.Potions}  reached phase {f.Phase + 1}  his HP left {f.BossHpLeft:0%}");
            output.WriteLine($"  won {fights.Count(f => f.Won)} of {fights.Count}, average {fights.Average(f => f.Seconds):0} s");
        }
    }

    /// <summary>
    /// The Choir of Whispers (M4 step 7) alone in the belfry at level 13: a level-13 hero with the runes a player would
    /// have (+2 potency and striking, +1 armour), and a level-14 hero (the level the campaign meets her at) in the starting
    /// kit and runed.
    /// </summary>
    [Theory]
    [InlineData("fighter vanguard")]
    [InlineData("fighter bulwark")]
    [InlineData("wizard pyromancer")]
    [InlineData("wizard stormcaller")]
    [InlineData("rogue cutthroat")]
    [InlineData("rogue trickster")]
    [InlineData("ranger marksman")]
    [InlineData("ranger beastmaster")]
    [InlineData("champion lanternbearer")]
    [InlineData("champion oathbreaker")]
    [InlineData("champion paladin")]
    [InlineData("gunslinger pistolero")]
    [InlineData("gunslinger sniper")]
    [InlineData("necromancer bonelord")]
    [InlineData("necromancer plaguebringer")]
    public void The_Choir_at_levels_13_and_14(string kit)
    {
        foreach (var (level, runed) in new[] { (13, true), (14, false), (14, true) })
        {
            var fights = Enumerable.Range(1, Math.Max(Seeds, 6))
                .Select(seed => BossFight(kit, seed, runed, level, "bell_tower", "choir_agnete", potency: 2)).ToList();
            output.WriteLine($"{kit} at level {level}{(runed ? " with a +2 striking weapon and +1 armour" : " in the starting kit")}: the Choir, {fights.Count} fights");
            foreach (var f in fights)
                output.WriteLine($"  {(f.Won ? "won " : "lost")}  {f.Seconds,5:0} s  potions {f.Potions}  reached phase {f.Phase + 1}  its HP left {f.BossHpLeft:0%}");
            output.WriteLine($"  won {fights.Count(f => f.Won)} of {fights.Count}, average {fights.Average(f => f.Seconds):0} s");
        }
    }

    /// <summary>
    /// One try at Mother Sallow: a hero of <paramref name="level"/> walks into her clearing and fights to the end (no respawn). The bot
    /// steps out of rot pools and their warning rings, drinks at 45% and otherwise fights as everywhere else.
    /// </summary>
    public static BossResult BossFight(string kit, int seed, bool runed, int level = 5, string zoneId = "sallows_mire",
        string bossId = "mother_sallow", int potency = 1)
    {
        var classId = kit.Split(' ')[0];
        var content = TestCharacters.Content;
        var arena = content.Zone(zoneId);
        var zone = arena with { Spawns = [arena.Spawns.Single(s => s.Pack.Any(m => m.Creature == bossId))] };
        var hero = TestCharacters.ForKit(kit);
        var bar = content.AbilityBar(classId);
        var world = new GameWorld(zone, content.Creatures, hero, seed, bar, content.Loot, content.Zones, content.Dialogues, content.Abilities);
        foreach (var id in Kits[kit])
        {
            var definition = content.Item(id);
            var item = runed && definition.IsWeapon ? new Items.Item(definition, Items.Rarity.Common, Math.Max(5, level), potency: potency, striking: 1)
                : runed && definition.IsBodyArmor ? new Items.Item(definition, Items.Rarity.Common, Math.Max(5, level), potency: 1)
                : new Items.Item(definition, Items.Rarity.Common, 1);
            world.Hero.Equipment.Equip(item);
        }
        LevelTo(world, level);
        var sallow = world.Monsters.Single(m => m.Definition.Id == bossId);
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
        var near = world.Monsters.Where(m => !m.IsDead && m.DistanceTo(world.Hero) < 8)
            .GroupBy(m => m.Definition.Id + (m.Creature.Stats.Rank == Creatures.CreatureRank.Normal ? "" : $" {m.Creature.Stats.Rank}"))
            .Select(g => $"{g.Count()} {g.Key}")
            .Concat(world.Allies.Where(a => a.IsTurned(world.Time)).GroupBy(a => a.Definition.Id).Select(g => $"{g.Count()} turned {g.Key}"));
        var said = string.Join(" + ", near);
        return said.Length > 0 ? said : "nothing near";
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
        var hero = TestCharacters.ForKit(classId);
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
        var hero = TestCharacters.ForKit(kit);
        Dress(hero, new Items.Equipment(hero), kit);
        return hero;
    }

    private sealed record Summary(Character Hero)
    {
        public string Describe() =>
            $"level-1 hero: {Hero.MaxHitPoints} HP, AC {Hero.ArmorClass().Total}, Fortitude {Hero.Save(Rules.StatisticKind.Fortitude).Total}";
    }
}
