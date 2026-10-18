using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// The arsenal added after M2's step 10: grazes, Cleave and Finishing Blow, shields and the Shield cantrip, the belt
/// (bombs, antidotes, supplies), and the Hasted, Shielded and Tainted affixes.
/// </summary>
public class ArsenalTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("................", 12).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 6], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, ZoneDefinition? zone = null, int seed = 5) =>
        new(zone ?? Field(), Content.Creatures, hero, seed, Content.AbilityBar(hero.Class.Id), Content.Loot, abilities: Content.Abilities);

    private static Item Make(string id, int count = 1) => new(Content.Item(id), Rarity.Common, 1, count: count);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static SpawnDefinition Zombie(int x, int y, CreatureRank rank = CreatureRank.Champion, params string[] affixes) =>
        new([x, y], [new PackMember("fen_zombie", rank, 1, affixes.Length > 0 ? affixes : null)], Radius: 0);

    // ----- Grazes -------------------------------------------------------------------

    [Theory]
    [InlineData(14, 17, true)]   // missed by 3: a graze
    [InlineData(13, 17, false)]  // missed by 4: a miss
    [InlineData(17, 17, false)]  // a hit is a hit
    public void A_weapon_strike_that_barely_misses_grazes(int total, int ac, bool grazes)
    {
        var check = new CheckResult(10, total - 10, ac, Check.Degree(total, ac, 10));
        Assert.Equal(grazes ? GrazeEffect.Half : GrazeEffect.None, Strike.Effective(GrazeEffect.Near, check));
        Assert.Equal(GrazeEffect.None, Strike.Effective(GrazeEffect.None, check));
    }

    [Fact]
    public void A_graze_builds_half_the_fury_and_a_sweeps_other_targets_none()
    {
        var resource = new ClassResource(TestCharacters.Fighter());
        resource.OnCombat(graze: true);
        Assert.Equal(ClassResource.FuryPerHit / 2, resource.Current);
        resource.OnCombat(builds: false);
        Assert.Equal(ClassResource.FuryPerHit / 2, resource.Current);
    }

    [Fact]
    public void A_fighter_in_a_long_fight_grazes_now_and_then()
    {
        var grazes = new HashSet<FloatingText>();
        for (var seed = 1; seed <= 12; seed++)
        {
            var world = World(TestCharacters.Fighter(), Field(new SpawnDefinition([6, 6], [new PackMember("fen_zombie", CreatureRank.Elite)], Radius: 0)), seed);
            var elite = world.Monsters.Single();
            for (var t = 0; t < 600 && !elite.IsDead; t++)
            {
                if (world.Hero.CurrentTarget is null)
                    world.Attack(elite);
                world.Update(1 / 30.0);
                world.Hero.Character.Heal(999);
                grazes.UnionWith(world.Texts.Where(x => x.Kind == FloatingTextKind.Graze));
            }
        }
        Assert.NotEmpty(grazes);
        Assert.All(grazes, g => Assert.Matches(@"^graze \d+$", g.Text));
    }

    // ----- Fighter: Cleave, Finishing Blow ----------------------------------------------

    [Fact]
    public void Cleave_strikes_the_enemies_in_front_and_never_the_one_behind()
    {
        var frontHit = false;
        for (var seed = 1; seed <= 25; seed++)
        {
            var world = World(TestCharacters.Fighter(), Field(Zombie(7, 6), Zombie(7, 7), Zombie(5, 6)), seed);
            (world.Hero.X, world.Hero.Y) = (6.5, 6.5);
            var target = world.Monsters.Single(m => m.X > 7 && m.Y < 7);
            var side = world.Monsters.Single(m => m.X > 7 && m.Y > 7);
            var behind = world.Monsters.Single(m => m.X < 6);
            int sideHp = side.Creature.HitPoints, behindHp = behind.Creature.HitPoints;
            world.Hero.Resource.Restore(100);
            world.UseSlot("2", target);
            Run(world, 0.8);
            Assert.Equal("cleave", world.Bar["2"].Id);
            Assert.Equal(behindHp, behind.Creature.HitPoints);
            frontHit |= side.Creature.HitPoints < sideHp;
        }
        Assert.True(frontHit, "Cleave never reached the second zombie in front");
    }

    [Fact]
    public void Finishing_blow_joins_the_bar_at_level_two_and_spends_every_point_of_fury()
    {
        var world = World(TestCharacters.Fighter(), Field(Zombie(5, 6, CreatureRank.Elite)));
        Assert.False(world.Bar.ContainsKey("4"));
        world.GainXp(1000, world.Hero);
        var finisher = world.Bar["4"];
        Assert.Equal("finishing_blow", finisher.Id);
        Assert.Equal(4, finisher.DiceForSpent(100));
        Assert.Equal(1, finisher.DiceForSpent(30));

        world.Hero.Resource.Restore(20);
        world.UseSlot("4", world.Monsters.Single());
        Run(world, 0.3);
        Assert.NotEqual("finishing_blow", world.Hero.CurrentAbility?.Id); // under 30 Fury: a plain Strike instead
        Assert.Contains("Not enough Fury", world.LastMessage);
        Run(world, 2);

        world.Hero.Resource.Restore(90);
        world.UseSlot("4", world.Monsters.Single());
        // A Strike already swinging finishes first.
        for (var i = 0; i < 180 && world.Hero.CurrentAbility?.Id != "finishing_blow"; i++)
            world.Update(1 / 60.0);
        Assert.Equal("finishing_blow", world.Hero.CurrentAbility?.Id);
        Assert.True(world.Hero.Resource.Current < ClassResource.FuryPerCrit + 1);
    }

    // ----- Shields ---------------------------------------------------------------------

    [Fact]
    public void Raising_a_shield_adds_its_ac_for_six_seconds_and_needs_a_shield()
    {
        var hero = TestCharacters.Fighter();
        var world = World(hero);
        var ac = hero.ArmorClass().Total;
        world.UseSlot("3", null);
        Assert.Null(world.Hero.Ward);
        Assert.Contains("needs a shield", world.LastMessage);

        world.Hero.Equipment.Equip(Make("wooden_shield"));
        world.UseSlot("3", null);
        Assert.NotNull(world.Hero.Ward);
        Assert.Equal(ac + 2, hero.ArmorClass().Total);
        Assert.Equal(3, world.Hero.Ward!.Hardness);
        Run(world, 6.2);
        Assert.Null(world.Hero.Ward);
        Assert.Equal(ac, hero.ArmorClass().Total);
    }

    [Fact]
    public void Shield_block_takes_the_hardness_off_a_blow_at_most_every_six_seconds()
    {
        var world = World(TestCharacters.Fighter(), Field(Zombie(4, 6)));
        world.Hero.Equipment.Equip(Make("steel_shield"));
        var blocks = new HashSet<FloatingText>();
        for (var t = 0; t < 30 * 20; t++)
        {
            if (world.Hero.Ward is null)
                world.UseSlot("3", null);
            world.Hero.Character.Heal(999);
            world.Update(1 / 30.0);
            blocks.UnionWith(world.Texts.Where(x => x.Text.StartsWith("blocked")));
        }
        var times = blocks.Select(b => b.BornAt).Order().ToList();
        Assert.NotEmpty(times);
        Assert.All(times.Zip(times.Skip(1)), pair => Assert.True(pair.Second - pair.First >= GameWorld.ShieldBlockCooldown - 1e-6));
        // A steel shield blocks up to its hardness of 5 (all of a smaller blow).
        Assert.All(blocks, b => Assert.InRange(int.Parse(b.Text["blocked ".Length..]), 1, 5));
    }

    [Fact]
    public void The_shield_cantrip_breaks_when_it_blocks_and_waits_ten_seconds()
    {
        var world = World(TestCharacters.Wizard(), Field(Zombie(4, 6)));
        var shield = world.Bar["2"];
        Assert.Equal("shield", shield.Id);
        Assert.Equal(5, shield.HardnessAt(1));
        Assert.Equal(10, shield.HardnessAt(3));
        var broke = false;
        for (var t = 0; t < 30 * 20 && !broke; t++)
        {
            if (world.Hero.Ward is null && world.CooldownRemaining(shield) <= 0)
                world.UseSlot("2", null);
            world.Hero.Character.Heal(999);
            world.Update(1 / 30.0);
            broke = world.Texts.Any(x => x.Text.StartsWith("blocked"));
        }
        Assert.True(broke);
        Assert.Null(world.Hero.Ward);
        Assert.InRange(world.CooldownRemaining(shield), 9.9, 10.0);
    }

    [Fact]
    public void A_two_handed_staff_and_a_shield_put_each_other_away()
    {
        var hero = TestCharacters.Wizard();
        var gear = new Equipment(hero);
        gear.Equip(Make("longsword"));
        gear.Equip(Make("wooden_buckler"));
        gear.TryAdd(Make("staff"));
        Assert.True(gear.EquipFromBag(0));
        Assert.Equal("staff", gear.In(EquipSlot.MainHand)!.Definition.Id);
        Assert.Null(gear.In(EquipSlot.OffHand));
        Assert.Contains(gear.Bag, i => i?.Definition.Id == "wooden_buckler");
        Assert.Contains(gear.Bag, i => i?.Definition.Id == "longsword");

        Assert.True(gear.EquipFromBag(gear.Bag.ToList().FindIndex(i => i?.Definition.Id == "wooden_buckler")));
        Assert.Equal("wooden_buckler", gear.In(EquipSlot.OffHand)!.Definition.Id);
        Assert.Null(gear.In(EquipSlot.MainHand));
        Assert.Contains(gear.Bag, i => i?.Definition.Id == "staff");
    }

    // ----- The belt -------------------------------------------------------------------

    [Fact]
    public void Consumables_stack_ten_to_a_cell_and_are_used_from_the_smallest_stack()
    {
        var gear = new Equipment(TestCharacters.Fighter());
        gear.TryAdd(Make("alchemists_fire", 2));
        gear.TryAdd(Make("alchemists_fire", 3));
        Assert.Equal(5, gear.Bag[0]!.Count);
        Assert.Null(gear.Bag[1]);
        gear.TryAdd(Make("alchemists_fire", 8));
        Assert.Equal([10, 3], gear.Bag.OfType<Item>().Select(i => i.Count));
        Assert.Equal(13, gear.CountOf(Content.Item("alchemists_fire")));
        Assert.True(gear.ConsumeOne(Content.Item("alchemists_fire")));
        Assert.Equal([10, 2], gear.Bag.OfType<Item>().Select(i => i.Count));
        Assert.Equal(Pricing.Value(gear.Bag[0]!), 10 * Content.Item("alchemists_fire").Price);
    }

    [Fact]
    public void The_belt_fills_from_the_bag_and_keeps_its_slots_until_an_item_runs_out()
    {
        var world = World(TestCharacters.Fighter(), Field(Zombie(9, 6)));
        var bag = world.Hero.Equipment;
        bag.TryAdd(Make("alchemists_fire", 1));
        bag.TryAdd(Make("antidote", 1));
        Run(world, 0.1);
        Assert.Equal(["alchemists_fire", "antidote", null, null], world.Belt.Select(i => i?.Id));
        Assert.Equal(1, world.BeltCount(0));

        world.UseBelt(0, world.Monsters.Single());
        Run(world, 2);
        Assert.Equal([null, "antidote", null, null], world.Belt.Select(i => i?.Id));
        bag.TryAdd(Make("frost_vial", 2));
        Run(world, 0.1);
        Assert.Equal(["frost_vial", "antidote", null, null], world.Belt.Select(i => i?.Id));
    }

    [Fact]
    public void A_bomb_splashes_its_target_and_the_monster_beside_it()
    {
        var splashed = false;
        for (var seed = 1; seed <= 10 && !splashed; seed++)
        {
            var world = World(TestCharacters.Fighter(), Field(Zombie(8, 6), Zombie(8, 7)), seed);
            world.Hero.Equipment.TryAdd(Make("alchemists_fire", 1));
            Run(world, 0.1);
            var (target, beside) = (world.Monsters[0], world.Monsters[1]);
            var hp = beside.Creature.HitPoints;
            world.UseBelt(0, target);
            Run(world, 1.5);
            Assert.Equal(0, world.Hero.Equipment.CountOf(Content.Item("alchemists_fire")));
            splashed = beside.Creature.HitPoints < hp;
        }
        Assert.True(splashed, "ten bombs never splashed the zombie beside the target");
    }

    [Fact]
    public void A_bomb_is_thrown_from_where_the_hero_stands_not_from_arms_length()
    {
        var world = World(TestCharacters.Fighter(), Field(Zombie(9, 6)));
        world.Hero.Equipment.TryAdd(Make("frost_vial", 1));
        Run(world, 0.1);
        var (x, y) = (world.Hero.X, world.Hero.Y);
        world.UseBelt(0, world.Monsters.Single());
        for (var i = 0; i < 60 && world.BeltCount(0) > 0; i++)
            world.Update(1 / 60.0);
        // Used up as the throw starts, where the hero stood (afterwards, as with any ability, they close in to Strike).
        Assert.Equal(0, world.BeltCount(0));
        Assert.True(world.Hero.DistanceTo(x, y) < 0.5, "the hero walked up to the zombie to throw");
    }

    [Fact]
    public void An_antidote_ends_poison_and_helps_against_the_next_dose()
    {
        var world = World(TestCharacters.Fighter());
        world.Hero.Character.Persistent.Add(DamageType.Poison, new Dice(1, 4));
        world.Hero.Equipment.TryAdd(Make("antidote", 2));
        Run(world, 0.1);
        world.UseBelt(0, null);
        Assert.Null(world.Hero.Character.Persistent.DiceFor(DamageType.Poison));
        Assert.Equal(world.Time + 120, world.Hero.AntidoteUntil, 3);
        Assert.Equal(1, world.BeltCount(0));
    }

    [Fact]
    public void Wenna_sells_supplies_at_a_price_that_grows_with_the_heros_level()
    {
        var hero = TestCharacters.Fighter();
        var world = new GameWorld(Content.Zone("fort_gallowmere"), Content.Creatures, hero, 3, Content.AbilityBar("fighter"),
            Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        var wenna = world.Vendors.Single(v => v.Definition.Id == "wenna");
        (world.Hero.X, world.Hero.Y) = (wenna.X + 1, wenna.Y + 1);
        world.TalkTo(wenna);
        Assert.Equal(5, world.Supplies.Count);
        var fire = Content.Item("alchemists_fire");
        world.Hero.Equipment.AddGold(100);
        var price = Pricing.SupplyPrice(fire, hero);
        Assert.True(world.BuySupply(fire));
        Assert.Equal(100 - price, world.Hero.Equipment.Gold);
        Assert.Equal(1, world.Hero.Equipment.CountOf(fire));
        Assert.True(Pricing.SupplyPrice(fire, TestCharacters.AtLevel(TestCharacters.Fighter(), 5)) > price);
    }

    [Fact]
    public void A_stack_of_bombs_survives_a_save()
    {
        var world = World(TestCharacters.Fighter(), Content.Zone("gallows_road"));
        world.Hero.Equipment.TryAdd(Make("frost_vial", 7));
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.Equal(7, loaded.Hero.Equipment.CountOf(Content.Item("frost_vial")));
    }

    // ----- Monster affixes ------------------------------------------------------------

    [Fact]
    public void A_hasted_monster_swings_a_third_faster()
    {
        var world = World(TestCharacters.Fighter(), Field(Zombie(9, 6), Zombie(9, 9, CreatureRank.Champion, "hasted")));
        var (plain, hasted) = (world.Monsters[0], world.Monsters[1]);
        Assert.Equal(plain.Creature.AttacksPerSecond * GameWorld.HasteAttackRate, hasted.Creature.AttacksPerSecond, 6);
        Assert.StartsWith("Hasted", hasted.Name);
    }

    [Fact]
    public void A_shielded_monster_wards_its_pack_and_the_ward_soaks_damage()
    {
        var world = World(TestCharacters.Fighter(), Field(new SpawnDefinition([6, 6],
            [new PackMember("fen_zombie", CreatureRank.Champion, 1, ["shielded"]), new PackMember("fen_zombie", CreatureRank.Normal, 1)], Radius: 1)));
        var (warder, ally) = (world.Monsters[0], world.Monsters[1]);
        Run(world, 1);
        var ward = GameWorld.PackWard(warder.Creature.Stats.Level);
        Assert.Equal(ward, warder.Ward);
        Assert.Equal(ward, ally.Ward);

        var hp = ally.Creature.HitPoints;
        ally.Creature.Persistent.Add(DamageType.Fire, new Dice(0, 1, 3));
        Run(world, 1.05);
        Assert.Equal(hp, ally.Creature.HitPoints);
        Assert.True(ally.Ward <= ward - 3);
    }

    [Fact]
    public void A_tainted_monster_dies_in_a_pool_that_fills_the_taint_meter()
    {
        var world = World(TestCharacters.Fighter(), Field(Zombie(4, 6, CreatureRank.Normal, "tainted")));
        var zombie = world.Monsters.Single();
        world.Slay(zombie);
        var pool = Assert.Single(world.Hazards);
        Assert.Equal(DamageType.Taint, pool.Type);
        (world.Hero.X, world.Hero.Y) = (pool.X, pool.Y);
        for (var t = 0; t < 30 * 4; t++)
        {
            world.Update(1 / 30.0);
            world.Hero.Character.Heal(999);
        }
        Assert.True(world.Hero.Character.Taint.Value > 0);
    }

    [Fact]
    public void The_whispers_turn_the_heros_feet()
    {
        var world = World(TestCharacters.Fighter());
        Assert.True(world.Hero.Character.Taint.Add(TaintMeter.Max * 2)); // the rate is slowed by CON
        var x = world.Hero.X;
        world.MoveTo(x + 4, world.Hero.Y);
        Run(world, 0.5);
        Assert.True(world.Hero.X < x);
    }
}
