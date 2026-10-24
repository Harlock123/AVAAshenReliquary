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
/// smith and trader.
/// </summary>
public class CampaignReport(ITestOutputHelper output)
{
    private static readonly int Runs = int.TryParse(Environment.GetEnvironmentVariable("ASHEN_BALANCE_RUNS"), out var runs) ? runs : 1;
    private const double Tick = BalanceReport.Tick;

    public sealed record ZoneLeg(string Zone, double Minutes, int Deaths, int Potions, int LevelIn, int XpIn, int LevelOut, int XpOut, int Gold);

    public sealed record Campaign(List<ZoneLeg> Legs, bool SallowSlain, int SallowLevel, double Minutes, List<string> Killers, int SuppliesBought = 0,
        int Salvaged = 0, int Crafted = 0, string? Dedication = null, bool HadricSlain = false, int HadricLevel = 0);

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

    public static Campaign Play(string kit, int seed, bool actTwo = false)
    {
        var classId = kit.Split(' ')[0];
        var content = TestCharacters.Content;
        var hero = TestCharacters.ForKit(kit);
        var bar = content.AbilityBar(classId);
        var world = new GameWorld(content.Zone("gallows_road"), content.Creatures, hero, seed, bar, content.Loot, content.Zones,
            content.Dialogues, content.Abilities);
        BalanceReport.Dress(hero, world.Hero.Equipment, kit);
        var bot = new Bot(world, classId, bar) { Subclass = hero.Subclass?.Id };

        bot.Clear("gallows_road", "fort_gallowmere");
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
            bot.Walk("ironhold", "north_gate");
            bot.Clear("barrow_fields", "barracks", wakeFirstShrine: true);
            bot.Clear("old_barracks", "stair");
            bot.WakeAndVisit("ward_shrine", "hilde", "sable");
            bot.Walk("barrow_fields", "siege_walls");
            bot.Clear("siege_walls", "heights");
        }
        return new Campaign(bot.Legs, world.Flags.GetValueOrDefault("act1.sallow") == "slain", bot.SallowLevel, world.Time / 60, bot.Killers,
            bot.SuppliesBought, bot.Salvaged, bot.Crafted, hero.DedicationClass?.Name,
            world.Flags.GetValueOrDefault("act2.hadric") == "slain", bot.HadricLevel);
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

        private int _deaths, _potions;
        private bool _wasDead;
        private double _nextMove, _nextDodge, _pickupUntil;
        private List<(double X, double Y)>? _route;
        private (double X, double Y)? _routeGoal;
        private readonly HashSet<GroundItem> _skipped = [];

        private Characters.Character Hero => world.Hero.Character;

        /// <summary>Clears zone <paramref name="zoneId"/> pack by pack, then walks into exit <paramref name="exitId"/>.</summary>
        public void Clear(string zoneId, string exitId, bool wakeFirstShrine = false)
        {
            var (start, deaths, potions, levelIn, xpIn) = (world.Time, _deaths, _potions, Hero.Level, world.Experience.Xp);
            if (wakeFirstShrine)
                Wake(world.Zone.Shrines[0]);
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
            if (Hero.HitPoints < Hero.MaxHitPoints * 0.45 && world.Hero.HealRemaining <= 0 && world.DrinkPotion())
                _potions++;
            Tactics.Upkeep(world);
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
