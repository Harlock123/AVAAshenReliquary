using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// The Gunslinger (after M4 step 6): Grit, guns that hold a few rounds and must be loaded again, misfires, the Pistolero's
/// twin pistols and the Sniper's long gun, special rounds and refits cast at the forge from the raw materials that drop.
/// </summary>
public class GunslingerTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static GameWorld World(string subclass, int level = 1, params (double X, double Y, CreatureRank Rank, string Creature)[] monsters)
    {
        var zone = new ZoneDefinition("field", "Field", level, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 8],
            monsters.Select(m => new SpawnDefinition([(int)m.X, (int)m.Y], [new PackMember(m.Creature, m.Rank, 1)], Radius: 0, Level: level)).ToList(),
            Shrines: [], Exits: []);
        var hero = TestCharacters.Gunslinger(subclass);
        var world = new GameWorld(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones, abilities: Content.Abilities);
        BalanceReport.Dress(hero, world.Hero.Equipment, $"gunslinger {subclass}");
        BalanceReport.LevelTo(world, level);
        world.Hero.Character.CannotFall = true;
        Run(world, 0.1);
        return world;
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static T Private<T>(GameWorld world, string method, params object?[] args) =>
        (T)typeof(GameWorld).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(world, args)!;

    [Fact]
    public void The_pistolero_starts_with_two_pistols_four_rounds_and_grit()
    {
        var world = World("pistolero");
        var hero = world.Hero;
        Assert.Equal(["pistolero", "sniper"], hero.Character.SubclassOptions.Select(s => s.Id));
        Assert.Equal(ResourceKind.Grit, hero.Resource.Kind);
        Assert.Equal(0, hero.Resource.Current);
        Assert.Equal("flintlock_pistol", hero.Equipment.In(EquipSlot.MainHand)?.Definition.Id);
        Assert.Equal("flintlock_pistol", hero.Equipment.In(EquipSlot.OffHand)?.Definition.Id);   // the second pistol went to the off hand
        Assert.True(world.TwinPistols);
        Assert.Equal(4, world.Capacity);
        Assert.Equal(4, hero.Loaded);                                                            // a gun in hand comes loaded
        Assert.Equal(["gunslinger_strike", "crack_shot", "risky_reload", "hit_the_dirt", "fan_the_hammer"], world.Bar.Values.Select(a => a.Id));
        Assert.True(hero.Character.CritReload);
        hero.Resource.OnCombat();
        Assert.Equal(ClassResource.GritPerHit + 3, hero.Resource.Current);                    // the Pistolero's 3 more a hit
        hero.Resource.OnCombat(taken: true);
        Assert.Equal(ClassResource.GritPerHit + 3, hero.Resource.Current);                    // blows taken build none
    }

    [Fact]
    public void A_shot_spends_a_round_and_an_empty_gun_loads_itself()
    {
        var world = World("sniper", 1, (8, 8, CreatureRank.Elite, "fen_zombie"));
        var hero = world.Hero;
        Assert.Equal(2, world.Capacity);
        Assert.Equal(2, hero.Loaded);
        var zombie = world.Monsters.Single();
        world.Attack(zombie);
        for (var t = 0.0; t < 3 && hero.Loaded == 2; t += 1 / 30.0)
            world.Update(1 / 30.0);
        Assert.Equal(1, hero.Loaded);
        hero.Loaded = 0;                                                                         // as if the last had gone
        var bullet = false;
        for (var t = 0.0; t < 2 && world.ReloadProgress is null; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            bullet |= world.Projectiles.Any(p => p.Shot is { Bullet: true });
        }
        Assert.True(bullet, "no shot flew");
        Assert.NotNull(world.ReloadProgress);                                                    // once the shot is done
        // Loaded again, it fires again at once (the reload ends and the shot goes in the same step).
        var shots = new HashSet<Projectile>();
        for (var t = 0.0; t < world.ReloadSeconds + 1.5; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            shots.UnionWith(world.Projectiles.Where(p => p.Shot is { Bullet: true }));
        }
        Assert.NotEmpty(shots);
        Assert.True(zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints || zombie.IsDead, "two shots at an elite and no hit");
    }

    [Fact]
    public void R_loads_early_and_a_misfire_fouls_the_load()
    {
        var world = World("pistolero");
        var hero = world.Hero;
        hero.Loaded = 1;
        Assert.True(world.Reload());
        Assert.False(world.Reload());                                                            // already loading
        Run(world, world.ReloadSeconds + 0.1);
        Assert.Equal(4, hero.Loaded);
        Assert.False(world.Reload());                                                            // full
        Private<object?>(world, "Misfire");
        Assert.Equal(0, hero.Loaded);
        Assert.Equal("misfire", world.Texts.Last().Text);
    }

    [Fact]
    public void Fan_the_hammer_empties_both_pistols_and_risky_reload_fires_an_empty_gun()
    {
        var world = World("pistolero", 1, (8, 8, CreatureRank.Elite, "fen_zombie"), (8, 11, CreatureRank.Elite, "fen_zombie"));
        var hero = world.Hero;
        var zombie = world.Monsters.MinBy(m => m.Y)!;
        hero.Resource.Gain(100);
        Assert.True(world.UseAbility("fan_the_hammer", zombie));
        Run(world, 0.05);
        Assert.Equal(0, hero.Loaded);                                                            // all four go at once
        var shots = new HashSet<Projectile>();
        for (var t = 0.0; t < 1.5; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            shots.UnionWith(world.Projectiles.Where(p => p.Shot is { Bullet: true } && p.Ability.Id == "fan_the_hammer"));
        }
        Assert.Equal(4, shots.Count);

        world.Stop();
        for (var t = 0.0; t < 3 && hero.IsSwinging; t += 1 / 30.0)
            world.Update(1 / 30.0);
        hero.Loaded = 0;
        hero.ReloadRemaining = 0;
        hero.ReadyAt.Clear();
        Assert.True(world.UseAbility("risky_reload", world.Monsters.First(m => !m.IsDead)));
        Run(world, 0.05);
        Assert.Equal(3, hero.Loaded);                                                            // loaded in an instant, one fired
    }

    [Fact]
    public void The_sniper_hits_harder_far_off_and_a_silvered_round_burns_the_undead()
    {
        var world = World("sniper", 1, (5, 8, CreatureRank.Normal, "fen_zombie"), (12, 8, CreatureRank.Normal, "fen_zombie"));
        var (near, far) = (world.Monsters.MinBy(m => m.X)!, world.Monsters.MaxBy(m => m.X)!);
        Assert.Equal(13, world.Hero.Character.Weapon.Definition.Range + world.Hero.Character.WeaponRangeBonus);   // 11 + 2
        Assert.Equal(0, Private<int>(world, "GunStrikeExtra", null, near, false));
        Assert.Equal(3, Private<int>(world, "GunStrikeExtra", null, far, false));
        var silver = Content.Abilities["silver_round"];
        Assert.True(far.Definition.Undead);
        Assert.InRange(Private<int>(world, "GunStrikeExtra", silver, far, false), 3 + 1, 3 + 12 + 2);   // 2d6 vitality, the dead weak to it
    }

    [Fact]
    public void A_second_pistol_fits_the_off_hand_only_beside_a_pistol()
    {
        var gear = new Equipment(TestCharacters.Gunslinger("sniper"));
        gear.Equip(new Item(Content.Item("arquebus"), Rarity.Common, 1));
        Assert.True(gear.TryAdd(new Item(Content.Item("flintlock_pistol"), Rarity.Common, 1)));
        var cell = gear.Bag.ToList().FindIndex(i => i?.Definition.Id == "flintlock_pistol");
        Assert.False(gear.EquipFromBag(cell, EquipSlot.OffHand));                               // not beside a long gun
        gear.Equip(new Item(Content.Item("dueling_pistol"), Rarity.Common, 4));
        Assert.True(gear.EquipFromBag(cell, EquipSlot.OffHand));
        Assert.Equal("flintlock_pistol", gear.In(EquipSlot.OffHand)?.Definition.Id);
        Assert.True(gear.TryAdd(new Item(Content.Item("arquebus"), Rarity.Common, 1)));
        Assert.True(gear.EquipFromBag(gear.Bag.ToList().FindIndex(i => i?.Definition.Id == "arquebus")));
        Assert.Null(gear.In(EquipSlot.OffHand));                                                 // a long gun takes both hands
    }

    [Fact]
    public void Materials_drop_rarely_in_act_1_and_often_after_and_guns_are_rare_in_act_1()
    {
        var random = new SeededRandom(7);
        double Materials(int act) => Enumerable.Range(0, 3000)
            .Average(_ => Content.Loot.Roll(CreatureRank.Normal, 5, random, act: act).Count(d => d.Item is { Definition.IsMaterial: true }));
        var (one, two) = (Materials(1), Materials(2));
        Assert.InRange(one, 0.015, 0.05);
        Assert.InRange(two, 0.07, 0.13);
        double Guns(int act) => Enumerable.Range(0, 4000).Count(_ => Content.Loot.Generate(8, Rarity.Common, random, act: act).Definition.IsFirearm) / 4000.0;
        Assert.True(Guns(1) * 2.5 < Guns(2), $"Act 1 {Guns(1):0.000} against Act 2 {Guns(2):0.000}");
        Assert.DoesNotContain(Enumerable.Range(0, 2000).Select(_ => Content.Loot.BeltItem(8, random)), i => i?.Definition.Id.EndsWith("_round") == true);
    }

    [Fact]
    public void The_forge_casts_rounds_from_powder_and_shot_and_refits_a_gun_to_hold_one_more()
    {
        var world = new GameWorld(Content.Zone("fort_gallowmere"), Content.Creatures, TestCharacters.Gunslinger("sniper"), 5,
            Content.AbilityBar("gunslinger"), Content.Loot, Content.Zones, Content.Dialogues, Content.Abilities);
        var gear = world.Hero.Equipment;
        gear.Equip(new Item(Content.Item("arquebus"), Rarity.Common, 1));
        gear.AddGold(5000);
        world.TalkTo(world.Vendors.Single(v => v.Definition.Id == "garrow"));
        for (var t = 0.0; t < 20 && world.OpenVendor is null; t += 1 / 30.0)
            world.Update(1 / 30.0);
        Assert.True(world.AtForge);

        var round = Content.Item("incendiary_round");
        Assert.True(world.IsRound(round));
        Assert.Null(world.Craft(CraftRecipe.Brew, brew: round));                                // no powder, no shot
        Assert.Contains("black powder", world.LastMessage);
        gear.TryAdd(new Item(Content.Item("black_powder"), Rarity.Common, 1, count: 40));
        gear.TryAdd(new Item(Content.Item("lead_shot"), Rarity.Common, 1, count: 40));
        gear.TryAdd(new Item(Content.Item("gun_parts"), Rarity.Common, 1, count: 40));
        var dust = gear.Dust;
        for (var i = 0; i < 12 && gear.CountOf(round) == 0; i++)
            world.Craft(CraftRecipe.Brew, brew: round);
        Assert.InRange(gear.CountOf(round), Crafting.RoundsPerCasting, Crafting.RoundsPerCasting + 1);
        Assert.Equal(dust, gear.Dust);                                                           // rounds take no Dust
        Assert.True(gear.CountOf(Content.Item("black_powder")) < 40);

        var gun = gear.In(EquipSlot.MainHand)!;
        for (var i = 0; i < 20 && gun.Refits == 0; i++)
            world.Craft(CraftRecipe.Refit, gun);
        Assert.InRange(gun.Refits, 1, 2);
        Assert.Equal(2 + gun.Refits, world.Capacity);
        var refits = gun.Refits;
        var club = new Item(Content.Item("club"), Rarity.Common, 1);
        Assert.Equal("Only a gun can be refitted.", Crafting.CannotCraft(CraftRecipe.Refit, club));

        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "guns", 3, null).ToJson()).Restore(Content);
        Assert.Equal(refits, loaded.Hero.Equipment.In(EquipSlot.MainHand)!.Refits);
    }

    [Fact]
    public void A_special_round_from_the_belt_is_fired_and_used_up()
    {
        var world = World("pistolero", 1, (7, 8, CreatureRank.Elite, "fen_zombie"));
        var gear = world.Hero.Equipment;
        gear.TryAdd(new Item(Content.Item("scatter_round"), Rarity.Common, 1, count: 2));
        Run(world, 0.1);
        var slot = world.Belt.ToList().FindIndex(i => i?.Id == "scatter_round");
        Assert.True(slot >= 0);
        var zombie = world.Monsters.Single();
        Assert.True(world.UseBelt(slot, zombie));
        Run(world, 1.0);
        Assert.Equal(1, gear.CountOf(Content.Item("scatter_round")));
        Assert.Equal(3, world.Hero.Loaded);
    }

    [Fact]
    public void Hit_the_dirt_never_slips_between_two_blocked_corners()
    {
        // The hero at (3.5, 8.5); trees east and south of it leave only a diagonal gap to the cell beyond.
        var rows = Enumerable.Repeat("..............................", 16).ToArray();
        rows[8] = "....T.........................";
        rows[9] = "...T..........................";
        var zone = new ZoneDefinition("field", "Field", 1, "tileset_grassland", rows,
            new Dictionary<string, LegendEntry> { ["."] = new("grass"), ["T"] = new("grass", Blocks: true) }, PlayerStart: [3, 8], [],
            Shrines: [], Exits: []);
        var hero = TestCharacters.Gunslinger("pistolero");
        var world = new GameWorld(zone, Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot, Content.Zones, abilities: Content.Abilities);
        Assert.True(world.UseAbility("hit_the_dirt", null, (world.Hero.X + 3, world.Hero.Y + 3)));
        Run(world, 1);
        Assert.Equal((3, 8), ((int)world.Hero.X, (int)world.Hero.Y));
    }

    [Theory]
    [InlineData("pistolero", "penetrating_shot", "dead_mans_hand", "hail_of_lead", "twin_thunder")]
    [InlineData("sniper", "penetrating_shot", "vital_shot", "hail_of_lead", "one_shot_one_kill")]
    public void The_gunslinger_gains_its_abilities_at_6_8_11_and_13(string subclass, string six, string eight, string eleven, string thirteen)
    {
        foreach (var (level, id) in new[] { (6, six), (8, eight), (11, eleven), (13, thirteen) })
        {
            Assert.DoesNotContain(World(subclass, level - 1).KnownAbilities, a => a.Id == id);
            Assert.Contains(World(subclass, level).KnownAbilities, a => a.Id == id);
        }
        Assert.Contains(TestCharacters.Gunslinger(subclass).FeatOptions(FeatType.Class, 2), f => f.Subclass == subclass);
    }
}
