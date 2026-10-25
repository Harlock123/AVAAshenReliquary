using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;
using Xunit.Abstractions;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// Act 1 played end to end by one hero (M2 plan, step 10): from the gallows through Gallows Road, the fort, the Rotwater
/// Fens, the Drowned Chapel and Sallow's Mire to Mother Sallow's death, in one world, as a careful player would. The bot
/// clears every pack in each zone's order, picks up what drops and wears what is better, takes the pilgrim's lantern,
/// makes its level-up picks as levels come, and between zones goes back to the fort by Rift to sell its spare loot and
/// have Garrow etch runes. It fights as <see cref="BalanceReport"/>'s bot does (no kiting) and steps out of rot pools.
/// Run it with `--logger "console;verbosity=detailed"`; ASHEN_BALANCE_RUNS sets the runs per class (1 by default).
/// <see cref="Acts_1_and_2_to_Ser_Hadric"/> plays on through Act 2 (M4 step 4), with Ironhold's Hilde and Sable for the
/// smith and trader; <see cref="Acts_1_to_3_to_the_Choir"/> on through Act 3 (M4 step 7), with the Rest's Halvard and Mora;
/// <see cref="The_whole_game_to_an_ending"/> on through Act 4, past the Cinderwyrm and down through the Reliquary, with the
/// Ember Gate's Brannoc and Hesk, to the Cinder Throne, Malachar and an ending (M5 step 5).
/// </summary>
public class CampaignReport(ITestOutputHelper output)
{
    private static readonly int Runs = int.TryParse(Environment.GetEnvironmentVariable("ASHEN_BALANCE_RUNS"), out var runs) ? runs : 1;
    private const double Tick = BalanceReport.Tick;

    public sealed record ZoneLeg(string Zone, double Minutes, int Deaths, int Potions, int LevelIn, int XpIn, int LevelOut, int XpOut, int Gold);

    public sealed record Campaign(List<ZoneLeg> Legs, bool SallowSlain, int SallowLevel, double Minutes, List<string> Killers, int SuppliesBought = 0,
        int Salvaged = 0, int Crafted = 0, string? Dedication = null, bool HadricSlain = false, int HadricLevel = 0, bool ChoirSlain = false,
        int ChoirLevel = 0, bool VorthraxSlain = false, int VorthraxLevel = 0, bool ThroneReached = false, string? Malachar = null,
        int MalacharLevel = 0, string? Ending = null, string Story = "");

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
    public void Act_1_from_the_gallows_to_Mother_Sallow(string kit)
    {
        for (var seed = int.TryParse(Environment.GetEnvironmentVariable("ASHEN_CAMPAIGN_SEED"), out var only) ? only : 1;
             seed <= (only > 0 ? only : Runs); seed++)
        {
            var c = Play(kit, seed);
            output.WriteLine($"{kit}, run {seed}: {(c.SallowSlain ? "Mother Sallow slain" : "Mother Sallow lives")} (met at level {c.SallowLevel}), {c.Minutes:0} min of play, {c.SuppliesBought} supplies bought, {c.Salvaged} salvaged, {c.Crafted} crafted, dedication {c.Dedication ?? "none"}");
            foreach (var leg in c.Legs)
                output.WriteLine($"  {leg.Zone,-22} {leg.Minutes,5:0.0} min  deaths {leg.Deaths,2}  potions {leg.Potions,2}  level {leg.LevelIn} ({leg.XpIn,3}) -> {leg.LevelOut} ({leg.XpOut,3})  gold {leg.Gold}");
            foreach (var g in c.Killers.GroupBy(k => k).OrderByDescending(g => g.Count()).Take(6))
                output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
            // The exit test: Act 1 can be finished, and it ends about level 5.
            Assert.True(c.SallowSlain, $"{kit} run {seed} did not kill Mother Sallow");
            Assert.InRange(c.Legs[^1].LevelOut, 5, 6);
            Assert.DoesNotContain(c.Killers, k => k.StartsWith("STUCK"));
        }
    }

    /// <summary>
    /// Act 2 played on from Act 1's end (M4 step 4): the Iron March to Ironhold (Hilde's forge, Sable's supplies), the
    /// Barrow Fields, the Old Barracks and back, the Siege Walls and Ser Hadric. The exit test: Hadric falls, and Act 2
    /// ends about level 10.
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
    public void Acts_1_and_2_to_Ser_Hadric(string kit)
    {
        for (var seed = int.TryParse(Environment.GetEnvironmentVariable("ASHEN_CAMPAIGN_SEED"), out var only) ? only : 1;
             seed <= (only > 0 ? only : Runs); seed++)
        {
            var c = Play(kit, seed, actTwo: true);
            output.WriteLine($"{kit}, run {seed}: Sallow {(c.SallowSlain ? "slain" : "lives")} at {c.SallowLevel}, Hadric {(c.HadricSlain ? "slain" : "lives")} at {c.HadricLevel}, {c.Minutes:0} min of play, {c.SuppliesBought} supplies, {c.Salvaged} salvaged, {c.Crafted} crafted");
            foreach (var leg in c.Legs)
                output.WriteLine($"  {leg.Zone,-22} {leg.Minutes,5:0.0} min  deaths {leg.Deaths,2}  potions {leg.Potions,2}  level {leg.LevelIn} ({leg.XpIn,3}) -> {leg.LevelOut} ({leg.XpOut,3})  gold {leg.Gold}");
            foreach (var g in c.Killers.GroupBy(k => k).OrderByDescending(g => g.Count()).Take(6))
                output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
            Assert.True(c.HadricSlain, $"{kit} run {seed} did not kill Ser Hadric");
            Assert.InRange(c.Legs[^1].LevelOut, 9, 11);
            Assert.DoesNotContain(c.Killers, k => k.StartsWith("STUCK"));
        }
    }

    /// <summary>
    /// Act 3 played on from Act 2's end (M4 step 7): the Frostbitten Pass to Saint Oriel's Rest (Halvard's forge, Mora's
    /// supplies), the Hollow Woods, the Catacombs of Oriel and up the Bell Tower to the Choir of Whispers. The exit test: the
    /// Choir falls, and Act 3 ends about level 15.
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
    public void Acts_1_to_3_to_the_Choir(string kit)
    {
        for (var seed = int.TryParse(Environment.GetEnvironmentVariable("ASHEN_CAMPAIGN_SEED"), out var only) ? only : 1;
             seed <= (only > 0 ? only : Runs); seed++)
        {
            var c = Play(kit, seed, actTwo: true, actThree: true);
            output.WriteLine($"{kit}, run {seed}: Sallow at {c.SallowLevel}, Hadric at {c.HadricLevel}, the Choir {(c.ChoirSlain ? "slain" : "lives")} at {c.ChoirLevel}, {c.Minutes:0} min of play, {c.SuppliesBought} supplies, {c.Salvaged} salvaged, {c.Crafted} crafted");
            foreach (var leg in c.Legs)
                output.WriteLine($"  {leg.Zone,-22} {leg.Minutes,5:0.0} min  deaths {leg.Deaths,2}  potions {leg.Potions,2}  level {leg.LevelIn} ({leg.XpIn,3}) -> {leg.LevelOut} ({leg.XpOut,3})  gold {leg.Gold}");
            foreach (var g in c.Killers.GroupBy(k => k).OrderByDescending(g => g.Count()).Take(6))
                output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
            Assert.True(c.ChoirSlain, $"{kit} run {seed} did not silence the Choir");
            Assert.InRange(c.Legs[^1].LevelOut, 14, 16);
            Assert.DoesNotContain(c.Killers, k => k.StartsWith("STUCK"));
        }
    }

    /// <summary>
    /// The whole game (M5 step 5): Acts 1 to 4 as before (Act 4 down the cliff road to the Ember Gate camp, the Ash Wastes,
    /// the Lava Rivers and the Wyrm's Nest to Vorthrax, then the Reliquary's gauntlet of Echoes), then the Cinder Throne:
    /// Malachar's court and his three phases, and the choice. Each kit plays for one ending (<see cref="EndingFor"/>), so
    /// all four are reached. The exit test: Malachar falls (or is spared) at level 20, and the ending is the one played for.
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
    public void The_whole_game_to_an_ending(string kit)
    {
        var ending = EndingFor(kit);
        for (var seed = int.TryParse(Environment.GetEnvironmentVariable("ASHEN_CAMPAIGN_SEED"), out var only) ? only : 1;
             seed <= (only > 0 ? only : Runs); seed++)
        {
            var c = Play(kit, seed, actTwo: true, actThree: true, actFour: true, ending: ending);
            output.WriteLine($"{kit}, run {seed}: Sallow at {c.SallowLevel}, Hadric at {c.HadricLevel}, the Choir at {c.ChoirLevel}, Vorthrax {(c.VorthraxSlain ? "slain" : "lives")} at {c.VorthraxLevel}, Malachar {c.Malachar ?? "lives"} at {c.MalacharLevel}, ending {c.Ending ?? "none"} (played for {ending}), {c.Minutes:0} min of play, {c.SuppliesBought} supplies, {c.Salvaged} salvaged, {c.Crafted} crafted");
            output.WriteLine($"  the story: {c.Story}");
            foreach (var leg in c.Legs)
                output.WriteLine($"  {leg.Zone,-22} {leg.Minutes,5:0.0} min  deaths {leg.Deaths,2}  potions {leg.Potions,2}  level {leg.LevelIn} ({leg.XpIn,3}) -> {leg.LevelOut} ({leg.XpOut,3})  gold {leg.Gold}");
            foreach (var g in c.Killers.GroupBy(k => k).OrderByDescending(g => g.Count()).Take(6))
                output.WriteLine($"  went down {g.Count(),2}x near {g.Key}");
            Assert.True(c.ChoirSlain, $"{kit} run {seed} did not silence the Choir");
            Assert.True(c.VorthraxSlain, $"{kit} run {seed} did not kill Vorthrax");
            Assert.True(c.ThroneReached, $"{kit} run {seed} did not come through the Reliquary");
            Assert.Equal(ending == "mercy" ? "spared" : "slain", c.Malachar);
            Assert.Equal(20, c.MalacharLevel);
            Assert.Equal(ending, c.Ending);
            Assert.DoesNotContain(c.Killers, k => k.StartsWith("STUCK"));
        }
    }

    /// <summary>
    /// The ending a kit's campaign plays for, the four in turn down the list of subclasses (so every ending is reached by
    /// three or four of them): Bind's kits read the Chronicle, light the beacon and smash the egg on the way (the Chronicle
    /// and three side stories); the Lantern-Bird's carry Wren's toy and every memory of her, so Malachar is spared.
    /// </summary>
    public static string EndingFor(string kit) => (Kits.IndexOf(kit) % 4) switch { 0 => "destroy", 1 => "claim", 2 => "bind", _ => "mercy" };

    private static readonly List<string> Kits =
    [
        "fighter vanguard", "fighter bulwark", "wizard pyromancer", "wizard stormcaller", "rogue cutthroat", "rogue trickster", "ranger marksman",
        "ranger beastmaster", "champion lanternbearer", "champion oathbreaker", "champion paladin", "gunslinger pistolero", "gunslinger sniper",
        "necromancer bonelord", "necromancer plaguebringer",
    ];

    public static Campaign Play(string kit, int seed, bool actTwo = false, bool actThree = false, bool actFour = false, string? ending = null)
    {
        var bind = ending == "bind";
        var mercy = ending == "mercy";
        var classId = kit.Split(' ')[0];
        var content = TestCharacters.Content;
        var hero = TestCharacters.ForKit(kit);
        var bar = content.AbilityBar(classId);
        var world = new GameWorld(content.Zone("gallows_road"), content.Creatures, hero, seed, bar, content.Loot, content.Zones,
            content.Dialogues, content.Abilities);
        BalanceReport.Dress(hero, world.Hero.Equipment, kit);
        var bot = new Bot(world, classId, bar) { Subclass = hero.Subclass?.Id };

        bot.Clear("gallows_road", "fort_gallowmere", false, mercy ? ["daughter_toy:Take the bird."] : []);
        bot.Fort();
        bot.Walk("fort_gallowmere", "west_gate");
        bot.Clear("rotwater_fens", "chapel", wakeFirstShrine: true);
        bot.WakeAndVisitFort();
        bot.TakeLantern();
        bot.Clear("drowned_chapel", "mire");
        bot.WakeAndVisitFort();
        bot.Clear("sallows_mire", "iron_march");
        if (actTwo)
        {
            bot.Clear("iron_march", "ironhold");
            bot.Town("hilde", "sable");
            if (mercy)
                bot.Visit("daughter_ironhold");
            bot.Walk("ironhold", "north_gate");
            bot.Clear("barrow_fields", "barracks", wakeFirstShrine: true);
            bot.Clear("old_barracks", "stair");
            bot.WakeAndVisit("ward_shrine", "hilde", "sable");
            bot.Walk("barrow_fields", "siege_walls");
            bot.Clear("siege_walls", "heights");
        }
        if (actThree)
        {
            bot.Clear("frostbitten_pass", "rest", true, bind ? ["page_1", "page_2"] : []);
            bot.Town("halvard", "mora");
            if (mercy)
                bot.Visit("daughter_oriel");
            bot.Walk("saint_oriels_rest", "north_door");
            bot.Clear("hollow_woods", "catacombs", true, bind ? ["page_3", "page_4"] : []);
            bot.Clear("catacombs_of_oriel", "bell_tower", false, bind ? ["page_5", "page_6", "page_7"] : []);
            bot.WakeAndVisit("cloister_shrine", "halvard", "mora");
            bot.Clear("bell_tower", "cliff_road");
        }
        if (actFour)
        {
            bot.Town("brannoc", "hesk");
            if (mercy)
                bot.Visit("daughter_ember_gate");
            if (bind)
                bot.Visit("beacon", "Light the beacon");
            bot.Walk("ember_gate", "wastes");
            bot.Clear("ash_wastes", "rivers", wakeFirstShrine: true);
            bot.Clear("lava_rivers", "nest", wakeFirstShrine: true);
            bot.WakeAndVisit("ember_gate_shrine", "brannoc", "hesk");
            bot.Clear("wyrms_nest", "reliquary", true, bind ? ["egg:Smash it."] : []);
            bot.WakeAndVisit("ember_gate_shrine", "brannoc", "hesk");
            bot.Clear("reliquary", "throne", true, mercy ? ["daughter_journal"] : []);
        }
        if (ending is not null && world.Zone.Id == "cinder_throne")
            bot.Throne(ending);
        return new Campaign(bot.Legs, world.Flags.GetValueOrDefault("act1.sallow") == "slain", bot.SallowLevel, world.Time / 60, bot.Killers,
            bot.SuppliesBought, bot.Salvaged, bot.Crafted, hero.DedicationClass?.Name,
            world.Flags.GetValueOrDefault("act2.hadric") == "slain", bot.HadricLevel,
            world.Flags.GetValueOrDefault("act3.choir") == "slain", bot.ChoirLevel,
            world.Flags.GetValueOrDefault("act4.vorthrax") == "slain", bot.VorthraxLevel,
            world.Zone.Id == "cinder_throne", world.Flag("act5.malachar"), bot.MalacharLevel, world.Flag("act5.ending"),
            string.Join(", ", world.Flags.Where(f => f.Key.StartsWith("daughter.") || f.Key is "act5.parley" or "act3.library" or "act4.beacon" or "act4.egg")
                .OrderBy(f => f.Key).Select(f => $"{f.Key}={f.Value}")));
    }

    /// <summary>The player: fights, rests, loots, walks on, and keeps count.</summary>
    private sealed class Bot(GameWorld world, string classId, IReadOnlyDictionary<string, Abilities.AbilityDefinition> bar)
    {
        /// <summary>The hero's subclass, which decides the weapons it keeps.</summary>
        public string? Subclass { get; init; }

        public List<ZoneLeg> Legs { get; } = [];
        public List<string> Killers { get; } = [];
        public int SallowLevel { get; private set; }
        public int HadricLevel { get; private set; }
        public int ChoirLevel { get; private set; }
        public int VorthraxLevel { get; private set; }

        private int _deaths, _potions;
        private bool _wasDead;
        private double _nextMove, _nextDodge, _pickupUntil;
        private List<(double X, double Y)>? _route;
        private (double X, double Y)? _routeGoal;
        private readonly HashSet<GroundItem> _skipped = [];

        private Characters.Character Hero => world.Hero.Character;

        /// <summary>Clears zone <paramref name="zoneId"/> pack by pack, then walks into exit <paramref name="exitId"/>.</summary>
        public void Clear(string zoneId, string exitId, bool wakeFirstShrine = false, params string[] visits)
        {
            var (start, deaths, potions, levelIn, xpIn) = (world.Time, _deaths, _potions, Hero.Level, world.Experience.Xp);
            if (wakeFirstShrine)
                Wake(world.Zone.Shrines[0]);
            if (world.Zone.Id != zoneId)
            {
                Killers.Add($"STUCK: to clear {zoneId}, but in {world.Zone.Id} at {world.Hero.X:0.0},{world.Hero.Y:0.0}");
                return;
            }
            var exit = world.Zone.Exits.Single(e => e.Id == exitId);
            while (world.Zone.Id == zoneId && !(exit.To is null && world.ZoneComplete) && world.Time - start < 3600)
            {
                if (Turn())
                    continue;
                if (world.Time < _nextMove)
                {
                    world.Update(Tick);
                    continue;
                }
                var next = world.Monsters.Where(m => !m.IsDead && m.SpawnIndex >= 0).MinBy(m => m.SpawnIndex);
                // The zone cleared: what is to be read or picked up here (a page, a keepsake), before the way on.
                if (next is null && visits.Length > 0)
                {
                    // "egg:Smash it." visits the egg and answers so.
                    foreach (var visit in visits)
                        Visit(visit.Split(':')[0], visit.Split(':').Skip(1).ToArray());
                    visits = [];
                    continue;
                }
                GoToward(next is not null ? (next.X, next.Y) : (exit.At[0] + 0.5, exit.At[1] + 0.5));
                world.Update(Tick);
            }
            if (world.Zone.Id == zoneId && !world.ZoneComplete)
            {
                var next = world.Monsters.Where(m => !m.IsDead && m.SpawnIndex >= 0).MinBy(m => m.SpawnIndex);
                Killers.Add($"STUCK in {zoneId} at {world.Hero.X:0.0},{world.Hero.Y:0.0}, next {next?.Definition.Id} #{next?.SpawnIndex} at {next?.X:0.0},{next?.Y:0.0} state {next?.State}, "
                    + $"path {(next is null ? "-" : Pathfinder.Find(world.Map, ((int)world.Hero.X, (int)world.Hero.Y), ((int)next.X, (int)next.Y), maxNodes: 60000)?.Count.ToString() ?? "none")}, "
                    + $"pickup {world.Hero.PickupTarget is not null}, dialogue {world.Dialogue is not null}, vendor {world.OpenVendor is not null}, rift {world.OpenShrine is not null}");
            }
            Legs.Add(new ZoneLeg(zoneId, (world.Time - start) / 60, _deaths - deaths, _potions - potions, levelIn, xpIn, Hero.Level,
                world.Experience.Xp, world.Hero.Equipment.Gold));
        }

        /// <summary>Walks to exit <paramref name="exitId"/> of a zone with nothing to fight (the fort).</summary>
        public void Walk(string zoneId, string exitId)
        {
            var exit = world.Zone.Exits.Single(e => e.Id == exitId);
            var start = world.Time;
            while (world.Zone.Id == zoneId && world.Time - start < 120)
            {
                if (world.Time >= _nextMove)
                    GoToward((exit.At[0] + 0.5, exit.At[1] + 0.5));
                world.Update(Tick);
            }
        }

        /// <summary>One step of play that isn't walking on: returns true when it used the tick.</summary>
        private bool Turn()
        {
            if (Hero.IsDead != _wasDead)
            {
                if (Hero.IsDead)
                {
                    _deaths++;
                    Killers.Add(BalanceReport.Nearby(world));
                }
                _wasDead = Hero.IsDead;
                _route = null;
            }
            if (Hero.IsDead)
            {
                world.Update(Tick);
                return true;
            }
            if (Hero.PendingLevels.Count > 0)
                BalanceReport.ChoosePending(world);
            if (world.Boss is { } boss && boss.Definition.Id == "mother_sallow" && SallowLevel == 0)
                SallowLevel = Hero.Level;
            if (world.Boss is { Definition.Id: "ser_hadric" } && HadricLevel == 0)
                HadricLevel = Hero.Level;
            if (world.Boss is { Definition.Id: "choir_agnete" } && ChoirLevel == 0)
                ChoirLevel = Hero.Level;
            if (world.Boss is { Definition.Id: "vorthrax" } && VorthraxLevel == 0)
                VorthraxLevel = Hero.Level;
            if (Hero.HitPoints < Hero.MaxHitPoints * 0.45 && world.Hero.HealRemaining <= 0 && world.DrinkPotion())
                _potions++;
            Tactics.Upkeep(world);
            // Malachar's parley (his daughter's toy): show it to him.
            if (world.Parleying && world.Dialogue is not null)
                world.Choose(0);
            if (BalanceReport.StepOutOfPool(world, ref _nextDodge) is not null)
            {
                _route = null;
                world.Update(Tick);
                return true;
            }
            var target = world.Monsters.Where(m => world.CanTarget(m) && (m.State != MonsterState.Idle || m.DistanceTo(world.Hero) < 5))
                .MinBy(m => m.DistanceTo(world.Hero));
            if (target is not null)
            {
                if (!world.Hero.IsSwinging && world.Time >= _nextDodge)
                    BalanceReport.Act(world, bar, classId, target);
                _route = null;
                world.Update(Tick);
                return true;
            }
            if (Tactics.Tidy(world) || Tactics.WarmUp(world))
            {
                _route = null;
                world.Update(Tick);
                return true;
            }
            if (world.Hero.PickupTarget is not null && world.Time < _pickupUntil)
            {
                world.Update(Tick);
                return true;
            }
            if (world.Hero.PickupTarget is not null)
                world.Stop();                                               // out of reach: give up on it
            // Loot first: anything lying within 12 tiles (gold and draughts always, items while the bag has room).
            var loot = world.GroundItems.Where(g => !_skipped.Contains(g) && world.Hero.DistanceTo(g.X, g.Y) < 12
                    && (g.Drop.Item is null || world.Hero.Equipment.Bag.Any(i => i is null)))
                .MinBy(g => world.Hero.DistanceTo(g.X, g.Y));
            if (loot is not null)
            {
                _skipped.Add(loot);                                         // one try each, so nothing unreachable holds the bot up
                world.PickUp(loot);
                _pickupUntil = world.Time + 6;
                world.Update(Tick);
                return true;
            }
            WearUpgrades();
            if (Hero.HitPoints < Hero.MaxHitPoints * 0.8)
            {
                if (world.Map.IsBog(world.Hero.X, world.Hero.Y) && world.Time >= _nextMove)
                {
                    var (dx, dy) = BalanceReport.NearestDry(world.Map, world.Hero.X, world.Hero.Y);
                    world.MoveTo(dx, dy);
                    _nextMove = world.Time + 1.0;
                }
                world.Update(Tick);
                return true;
            }
            return false;
        }

        private void GoToward((double X, double Y) goal)
        {
            if (_route is null || _routeGoal is not { } g || Math.Abs(g.X - goal.X) + Math.Abs(g.Y - goal.Y) > 2)
            {
                _route = Pathfinder.Find(world.Map, ((int)world.Hero.X, (int)world.Hero.Y), ((int)goal.X, (int)goal.Y), maxNodes: 60000);
                _routeGoal = goal;
            }
            if (_route is { Count: > 0 })
            {
                var near = _route.Select((p, i) => (p, i)).MinBy(t => Math.Abs(t.p.X - world.Hero.X) + Math.Abs(t.p.Y - world.Hero.Y)).i;
                var ahead = _route[Math.Min(_route.Count - 1, near + 12)];
                world.MoveTo(ahead.X, ahead.Y);
            }
            else
            {
                world.MoveTo(goal.X, goal.Y);
            }
            _nextMove = world.Time + 1.0;
        }

        /// <summary>
        /// Wears what is better: by value (rarity, level, runes, affixes), weapons the class fights with (a caster's
        /// focus for the wizard, a melee weapon for the fighter, an agile or finesse one for the rogue), body armour of
        /// the kind already worn.
        /// </summary>
        private void WearUpgrades()
        {
            var gear = world.Hero.Equipment;
            for (var i = 0; i < gear.Bag.Count; i++)
            {
                if (gear.Bag[i] is not { Definition.Slot: not null } item || item.Definition.Category == ItemCategory.Key)
                    continue;
                var d = item.Definition;
                if (d.IsWeapon && (classId is "wizard" or "necromancer" ? !d.CasterFocus : classId == "ranger" ? !d.Ranged || d.IsFirearm
                        : classId == "gunslinger" ? !d.IsFirearm || d.IsPistol != (Subclass == "pistolero") : d.CasterFocus || d.Ranged))
                    continue; // a ranger keeps to bows, a Pistolero to pistols and a Sniper to long guns
                if (d.IsShield && classId is not ("fighter" or "champion") || d.IsShield && Subclass == "vanguard")
                    continue; // a shield would take the staff (or the Vanguard's greatsword) out of both hands; a rogue never raises one
                if (d.IsWeapon && classId == "rogue" && d.Traits?.Contains(WeaponTrait.Finesse) != true)
                    continue; // a finesse blade: sneak attack, and Dexterity to damage (an agile hatchet hits for its Strength)
                if (d.IsWeapon && classId == "champion" && d.IsTwoHanded)
                    continue; // a champion keeps sword and shield
                if (d.IsWeapon && classId == "fighter" && d.IsTwoHanded != (Subclass == "vanguard"))
                    continue; // a Vanguard fights two-handed, a Bulwark with sword and shield
                if (d.IsBodyArmor && gear.In(EquipSlot.Chest) is { } chest && chest.Definition.ArmorCategory != d.ArmorCategory)
                    continue;
                if (gear.SlotFor(item) is not { } slot)
                    continue;
                var worn = gear.In(slot);
                if (worn is null || Pricing.Value(item) > Pricing.Value(worn) * 1.05)
                    gear.EquipFromBag(i);
            }
        }

        /// <summary>
        /// In the fort: sell the spare loot to Garrow (keeping the belt), have him etch runes, restock the belt at Wenna's
        /// (five alchemist's fires, two antidotes), and wake the Fort Shrine.
        /// </summary>
        public void Fort() => Town("garrow", "wenna");

        /// <summary>In a hub: the smith <paramref name="smithId"/> (sell, salvage, craft, etch) and the supplies of <paramref name="traderId"/>.</summary>
        public void Town(string smithId, string traderId)
        {
            var gear = world.Hero.Equipment;
            var garrow = world.Vendors.Single(v => v.Definition.Id == smithId);
            world.TalkTo(garrow);
            for (var t = 0.0; t < 20 && world.OpenVendor is null; t += Tick)
                world.Update(Tick);
            if (world.OpenVendor is not null)
            {
                WearUpgrades();
                // Spare commons go to the forge for Ashen Dust; the rest is sold for the runes.
                foreach (var spare in gear.Bag.OfType<Item>().Where(i => i.Rarity == Rarity.Common && i.Definition.Slot is not null).ToList())
                    if (world.Salvage(spare))
                        Salvaged++;
                for (var i = 0; i < gear.Bag.Count; i++)
                {
                    if (gear.Bag[i] is { } item && item.Definition.Category != ItemCategory.Key && !item.Definition.IsBeltItem)
                        world.Sell(i);
                }
                // The worn gear at the forge: commons raised, uncommons given their second affix, while dust and gold last.
                foreach (var worn in gear.Worn.Values.ToList())
                {
                    var recipe = worn.Rarity == Rarity.Common ? CraftRecipe.Raise : CraftRecipe.AddAffix;
                    if (Crafting.CannotCraft(recipe, worn) is not null)
                        continue;
                    var (dust, fragments, fee) = Crafting.Cost(recipe, worn.ItemLevel);
                    if (gear.Dust >= dust && gear.Fragments >= fragments && gear.Gold >= fee + 100 && world.Craft(recipe, worn) is { } outcome)
                        Crafted += outcome.Check.Degree >= DegreeOfSuccess.Success ? 1 : 0;
                }
                // Etch what the gold allows, the weapon first.
                foreach (var (slot, kind) in new[] { (EquipSlot.MainHand, RuneKind.Striking), (EquipSlot.MainHand, RuneKind.Potency), (EquipSlot.Chest, RuneKind.Potency) })
                {
                    if (gear.In(slot) is { } item && Runework.CannotEtch(item, kind) is null && gear.Gold >= Runework.EtchPrice(item, kind, Hero))
                        world.Etch(item, kind);
                }
                world.CloseVendor();
            }
            var wenna = world.Vendors.Single(v => v.Definition.Id == traderId);
            world.TalkTo(wenna);
            for (var t = 0.0; t < 20 && world.OpenVendor is null; t += Tick)
                world.Update(Tick);
            if (world.OpenVendor is not null)
            {
                foreach (var (id, keep) in new[] { ("alchemists_fire", 5), ("antidote", 2) })
                {
                    var supply = world.Supplies.Single(s => s.Id == id);
                    while (gear.CountOf(supply) < keep && gear.Gold >= world.SupplyPrice(supply) && world.BuySupply(supply))
                        SuppliesBought++;
                }
                world.CloseVendor();
            }
            Wake(world.Zone.Shrines[0]);
        }

        /// <summary>Belt items bought from Wenna over the run.</summary>
        public int SuppliesBought { get; private set; }

        /// <summary>Items salvaged and pieces crafted at Garrow's forge over the run.</summary>
        public int Salvaged { get; private set; }
        public int Crafted { get; private set; }

        /// <summary>Wakes the zone's first shrine, Rifts to the fort for the smith, and comes back.</summary>
        public void WakeAndVisitFort() => WakeAndVisit("fort_shrine", "garrow", "wenna");

        /// <summary>Wakes the zone's first shrine, Rifts to a hub's shrine for its smith and trader, and comes back.</summary>
        public void WakeAndVisit(string townShrine, string smithId, string traderId)
        {
            var here = world.Zone.Shrines.MinBy(sh => world.Hero.DistanceTo(sh.At[0] + 0.5, sh.At[1] + 0.5))!;
            Wake(here);
            if (!Rift(here, townShrine))
                return;
            Town(smithId, traderId);
            Rift(world.Zone.Shrines[0], here.Id);
        }

        private void Wake(ShrineDefinition shrine)
        {
            world.UseShrine(shrine);
            for (var t = 0.0; t < 30 && !world.ActivatedShrines.Contains(shrine.Id); t += Tick)
                world.Update(Tick);
        }

        private bool Rift(ShrineDefinition from, string to)
        {
            world.UseShrine(from);
            for (var t = 0.0; t < 30 && world.OpenShrine is null; t += Tick)
            {
                world.Update(Tick);
                if (world.OpenShrine is null && world.Hero.ShrineTarget is null)
                    world.UseShrine(from);
            }
            return world.OpenShrine is not null && world.RiftTravel(to);
        }

        /// <summary>The pilgrim's lantern, from its lectern in the chapel's narthex.</summary>
        /// <summary>
        /// Walks to someone or something in this zone and answers its dialogue: each answer is the first choice that starts
        /// with it (none: it is only read). False when it isn't here or wasn't reached.
        /// </summary>
        public bool Visit(string id, params string[] answers)
        {
            if (world.Interactives.FirstOrDefault(i => i.Id == id) is not { } who)
                return false;
            // Whatever was still being said (Malachar's last words at the parley) is done with.
            if (world.Dialogue is not null)
                world.EndDialogue();
            var start = world.Time;
            while (world.Dialogue is null && world.Time - start < 180)
            {
                if (Turn())
                    continue;
                // Near it, the world's own approach (a click on it) finds the open side to stand on.
                if (who.DistanceTo(world.Hero.X, world.Hero.Y) <= 6)
                {
                    if (world.Hero.InteractTarget != who)
                        world.Interact(who);
                }
                else if (world.Time >= _nextMove)
                    GoToward((who.X, who.Y + 1));
                world.Update(Tick);
            }
            if (world.Dialogue is null)
            {
                Killers.Add($"STUCK on the way to {id} in {world.Zone.Id}: at {world.Hero.X:0.0},{world.Hero.Y:0.0}, {who.DistanceTo(world.Hero.X, world.Hero.Y):0.0} off, "
                    + $"pull {world.Pull is not null}, parley {world.Parleying}, dead {Hero.IsDead}, alive {world.Monsters.Count(m => !m.IsDead)}, "
                    + $"path {Pathfinder.Find(world.Map, ((int)world.Hero.X, (int)world.Hero.Y), ((int)who.X, (int)(who.Y + 1)), maxNodes: 60000)?.Count.ToString() ?? "none"}");
                return false;
            }
            foreach (var answer in answers)
            {
                var index = world.Dialogue?.Choices.ToList().FindIndex(c => c.Text.StartsWith(answer, StringComparison.Ordinal)) ?? -1;
                if (index < 0)
                    break;
                world.Choose(index);
            }
            if (world.Dialogue is not null)
                world.EndDialogue();
            return true;
        }

        public int MalacharLevel { get; private set; }

        /// <summary>
        /// The Cinder Throne: through the honour guard to Malachar's court, the fight to his fall (or his sparing, shown his
        /// daughter's toy), then the choice at the Throne.
        /// </summary>
        public void Throne(string ending)
        {
            var (start, deaths, potions, levelIn, xpIn) = (world.Time, _deaths, _potions, Hero.Level, world.Experience.Xp);
            while (world.Zone.Id == "cinder_throne" && world.Flag("act5.malachar") is null && world.Time - start < 3600)
            {
                if (world.Boss is { Definition.Id: "malachar" } && MalacharLevel == 0)
                    MalacharLevel = Hero.Level;
                if (Turn())
                    continue;
                if (world.Time < _nextMove)
                {
                    world.Update(Tick);
                    continue;
                }
                var next = world.Monsters.Where(m => !m.IsDead && m.SpawnIndex >= 0 && !m.Dormant).MinBy(m => m.SpawnIndex);
                if (next is null)
                    break;
                GoToward((next.X, next.Y));
                world.Update(Tick);
            }
            if (world.Flag("act5.malachar") is null)
                Killers.Add($"STUCK at the Cinder Throne at {world.Hero.X:0.0},{world.Hero.Y:0.0}");
            Visit("throne", ending switch
            {
                "destroy" => "Break the Ember Heart",
                "bind" => "Take the god into yourself, as Ashur",
                "claim" => "Sit on the Throne",
                _ => "Let Malachar take the Heart",
            }, "Do it.");
            Legs.Add(new ZoneLeg("cinder_throne", (world.Time - start) / 60, _deaths - deaths, _potions - potions, levelIn, xpIn, Hero.Level,
                world.Experience.Xp, world.Hero.Equipment.Gold));
        }

        public void TakeLantern()
        {
            if (world.Interactives.FirstOrDefault(i => i.Id == "pilgrims_lantern") is not { } lectern)
                return;
            world.Interact(lectern);
            for (var t = 0.0; t < 20 && world.Dialogue is null; t += Tick)
                world.Update(Tick);
            if (world.Dialogue is { Choices.Count: > 1 })
                world.Choose(0);
            world.EndDialogue();
        }
    }
}
