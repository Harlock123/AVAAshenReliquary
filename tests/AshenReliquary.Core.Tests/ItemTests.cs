using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class ItemTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static Item Common(string id, int level = 1) => new(Content.Item(id), Rarity.Common, level);

    [Fact]
    public void Item_and_affix_data_load_with_icons_and_slots()
    {
        Assert.True(Content.Items.Count >= 30);
        Assert.All(Content.Items.Values, i => Assert.InRange(i.Icon, 0, 255));
        Assert.Equal(EquipSlot.MainHand, Content.Item("longsword").Slot);
        Assert.Equal(8, Content.Item("longsword").DamageDie);
        Assert.Equal(ArmorCategory.Medium, Content.Item("chain_cuirass").ArmorCategory);
        Assert.True(Content.Affixes.Count >= 15);
    }

    [Fact]
    public void Equipping_sets_weapon_armour_and_gear_bonuses()
    {
        var fighter = TestCharacters.Fighter();
        var gear = new Equipment(fighter);
        gear.Equip(Common("longsword"));
        gear.Equip(Common("leather_chest"));
        Assert.Equal(8, fighter.Weapon.Definition.DamageDie);
        Assert.Equal(10 + 2 + 3 + 1, fighter.ArmorClass().Total);

        var hp = fighter.MaxHitPoints;
        var sturdy = Content.Affixes.Single(a => a.Id == "sturdy");
        var hearth = Content.Affixes.Single(a => a.Id == "hearth");
        gear.Equip(new Item(Content.Item("leather_pants"), Rarity.Uncommon, 1, [new AffixRoll(sturdy, 4), new AffixRoll(hearth, 2)]));
        Assert.Equal(hp + 4, fighter.MaxHitPoints);
        Assert.Equal(2, fighter.Defenses.Resistances[DamageType.Fire]);
        Assert.Equal("Sturdy Leather leggings of the Hearth", gear.In(EquipSlot.Legs)!.Name);
    }

    [Fact]
    public void Unequipping_returns_to_the_bag_and_the_fist()
    {
        var fighter = TestCharacters.Fighter();
        var gear = new Equipment(fighter);
        gear.Equip(Common("longsword"));
        Assert.True(gear.Unequip(EquipSlot.MainHand));
        Assert.Equal("fist", fighter.Weapon.Definition.Id);
        Assert.Equal("Longsword", gear.Bag[0]!.Name);
        Assert.True(gear.EquipFromBag(0));
        Assert.Null(gear.Bag[0]);
        Assert.Equal("longsword", fighter.Weapon.Definition.Id);
    }

    [Fact]
    public void Swapping_from_the_bag_puts_the_old_item_in_the_same_cell()
    {
        var fighter = TestCharacters.Fighter();
        var gear = new Equipment(fighter);
        gear.Equip(Common("club"));
        gear.TryAdd(Common("longsword"));
        gear.EquipFromBag(0);
        Assert.Equal("Club", gear.Bag[0]!.Name);
        Assert.Equal("Longsword", gear.In(EquipSlot.MainHand)!.Name);
    }

    [Fact]
    public void Rings_fill_both_hands()
    {
        var gear = new Equipment(TestCharacters.Fighter());
        gear.Equip(new Item(Content.Item("silver_ring"), Rarity.Uncommon, 1));
        gear.Equip(new Item(Content.Item("gold_ring"), Rarity.Uncommon, 1));
        Assert.NotNull(gear.In(EquipSlot.Ring1));
        Assert.NotNull(gear.In(EquipSlot.Ring2));
    }

    [Fact]
    public void Untrained_armour_gives_no_proficiency_to_ac()
    {
        var wizard = TestCharacters.Wizard(); // DEX +2, trained (3) unarmoured
        var gear = new Equipment(wizard);
        Assert.Equal(10 + 2 + 3, wizard.ArmorClass().Total);
        gear.Equip(Common("chain_cuirass"));
        Assert.Equal(10 + 1 + 0 + 4, wizard.ArmorClass().Total); // DEX capped at +1, untrained, +4 item
        Assert.Contains(wizard.ArmorClass().Applied, m => m.Source == "Untrained in armour" || m.Value == 4);
    }

    [Fact]
    public void Bag_holds_sixty_items()
    {
        var gear = new Equipment(TestCharacters.Fighter());
        for (var i = 0; i < Equipment.BagSize; i++)
            Assert.True(gear.TryAdd(Common("dagger")));
        Assert.False(gear.TryAdd(Common("dagger")));
    }

    [Fact]
    public void Runes_respect_item_level()
    {
        var loot = Content.Loot;
        var random = new SeededRandom(3);
        for (var i = 0; i < 300; i++)
        {
            var low = loot.Generate(1, Rarity.Rare, random);
            Assert.Equal(0, low.Potency);
            Assert.Equal(0, low.Striking);
            var mid = loot.Generate(4, Rarity.Rare, random);
            if (mid.Definition.IsBodyArmor)
                Assert.Equal(0, mid.Potency);
        }
        Assert.Contains(Enumerable.Range(0, 200).Select(_ => loot.Generate(4, Rarity.Uncommon, random)), x => x.Striking == 1);
    }

    [Fact]
    public void Affix_counts_follow_rarity()
    {
        var loot = Content.Loot;
        var random = new SeededRandom(9);
        for (var i = 0; i < 300; i++)
        {
            Assert.Empty(loot.Generate(3, Rarity.Common, random).Affixes);
            var uncommon = loot.Generate(3, Rarity.Uncommon, random);
            Assert.InRange(uncommon.Affixes.Count, 1, 2);
            Assert.True(uncommon.Affixes.Count(a => a.Definition.Prefix) <= 1);
            var rare = loot.Generate(3, Rarity.Rare, random);
            Assert.InRange(rare.Affixes.Count, 2, 4);
            Assert.True(rare.Affixes.Count(a => a.Definition.Prefix) <= 2);
            Assert.Equal(rare.Affixes.Count, rare.Affixes.Select(a => (a.Definition.Stat, a.Definition.Resist)).Distinct().Count());
        }
    }

    [Fact]
    public void Commons_never_roll_jewellery()
    {
        var random = new SeededRandom(5);
        Assert.DoesNotContain(Enumerable.Range(0, 500).Select(_ => Content.Loot.Generate(2, Rarity.Common, random)), i => i.Definition.IsJewelry);
    }

    [Fact]
    public void Drop_rates_by_rank_are_roughly_right()
    {
        var random = new SeededRandom(21);
        double Items(CreatureRank rank) => Enumerable.Range(0, 2000)
            .Average(_ => Content.Loot.Roll(rank, 1, random).Count(d => d.Item is { Definition.IsBeltItem: false }));
        Assert.InRange(Items(CreatureRank.Normal), 0.10, 0.18);
        Assert.InRange(Items(CreatureRank.Champion), 1.4, 1.6);
        Assert.True(Items(CreatureRank.Minion) < 0.07);
    }

    [Fact]
    public void Rarity_shares_match_act_one()
    {
        var random = new SeededRandom(8);
        var rolls = Enumerable.Range(0, 20000).Select(_ => LootGenerator.RollRarity(CreatureRank.Normal, random)).ToList();
        Assert.InRange(rolls.Count(r => r == Rarity.Common) / 20000.0, 0.67, 0.73);
        Assert.InRange(rolls.Count(r => r == Rarity.Uncommon) / 20000.0, 0.22, 0.28);
        Assert.InRange(rolls.Count(r => r == Rarity.Rare) / 20000.0, 0.035, 0.055);
        // The spec's 0.4% Uniques and 0.1% Sets.
        Assert.InRange(rolls.Count(r => r == Rarity.Unique) / 20000.0, 0.002, 0.007);
        Assert.InRange(rolls.Count(r => r == Rarity.Set) / 20000.0, 0.0002, 0.003);
    }

    [Fact]
    public void Equip_preview_shows_changes_without_changing_anything()
    {
        var fighter = TestCharacters.Fighter();
        var gear = new Equipment(fighter);
        gear.Equip(Common("club"));
        gear.Equip(Common("leather_chest"));
        fighter.TakeDamage(5);
        var hp = fighter.HitPoints;
        var vicious = Content.Affixes.Single(a => a.Id == "vicious");
        var better = new Item(Content.Item("longsword"), Rarity.Uncommon, 2, [new AffixRoll(vicious, 1)], potency: 1);

        var preview = gear.PreviewEquip(better);

        var damage = preview.Single(c => c.Name == "Damage per hit");
        Assert.Equal((3.5 + 4, 4.5 + 4 + 1), (damage.Before.Value, damage.After.Value));
        Assert.Equal(1, damage.Direction);
        Assert.Equal(1, preview.Single(c => c.Name == "Weapon attack").Direction);
        Assert.Equal(0, preview.Single(c => c.Name == "AC").Direction);
        Assert.Equal("club", fighter.Weapon.Definition.Id);
        Assert.Equal(hp, fighter.HitPoints);
        Assert.Equal("Club", gear.In(EquipSlot.MainHand)!.Name);
    }

    [Fact]
    public void Unequip_preview_shows_the_loss()
    {
        var fighter = TestCharacters.Fighter();
        var gear = new Equipment(fighter);
        gear.Equip(Common("chain_cuirass"));
        var ac = gear.PreviewUnequip(EquipSlot.Chest).Single(c => c.Name == "AC");
        Assert.Equal(-1, ac.Direction);
        Assert.Equal("chain_cuirass", fighter.Armor.Definition.Id);
    }

    [Fact]
    public void Secondary_stats_only_appear_when_they_change()
    {
        var gear = new Equipment(TestCharacters.Fighter());
        var hearth = Content.Affixes.Single(a => a.Id == "hearth");
        var preview = gear.PreviewEquip(new Item(Content.Item("belt"), Rarity.Uncommon, 1, [new AffixRoll(hearth, 2)]));
        Assert.Contains(preview, c => c.Name == "Fire resistance" && c.Direction == 1);
        Assert.DoesNotContain(preview, c => c.Name == "Cold resistance");
        Assert.Contains(preview, c => c.Name == "Max HP" && c.Delta == 2);
    }

    [Fact]
    public void Gear_speed_and_fury_reach_combat()
    {
        var fighter = TestCharacters.Fighter();
        var gear = new Equipment(fighter);
        var quick = Content.Affixes.Single(a => a.Id == "quick");
        var hungry = Content.Affixes.Single(a => a.Id == "hungry");
        gear.Equip(new Item(Content.Item("longsword"), Rarity.Uncommon, 1, [new AffixRoll(quick, 8), new AffixRoll(hungry, 3)]));
        Assert.Equal(0.08, fighter.Gear.AttackSpeed, 6);
        var fury = new Abilities.ClassResource(fighter);
        fury.OnCombat();
        Assert.Equal(12 + 3, fury.Current);
    }
}

public class LootWorldTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld World(params SpawnDefinition[] spawns) => new(
        new ZoneDefinition("f", "F", 1, "tileset_grassland", Enumerable.Repeat("..................", 18).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, [2, 2], spawns, [], []),
        Content.Creatures, TestCharacters.Fighter(), 4, Content.AbilityBar("fighter"), Content.Loot);

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    [Fact]
    public void A_champion_drops_loot_that_can_be_picked_up()
    {
        var world = World(new SpawnDefinition([5, 5], [new PackMember("fen_zombie", CreatureRank.Champion)], 0));
        var champion = world.Monsters.Single();
        champion.Creature.TakeDamage(champion.Creature.HitPoints - 1);
        world.Attack(champion);
        for (var i = 0; i < 900 && !champion.IsDead; i++)
            world.Update(1 / 30.0);
        Assert.True(champion.IsDead);
        var item = world.GroundItems.First(g => g.Drop.Item is not null);
        world.PickUp(item);
        Run(world, 3);
        Assert.DoesNotContain(item, world.GroundItems);
        Assert.Contains(world.Hero.Equipment.Bag, b => b == item.Drop.Item);
        Assert.True(world.Hero.Equipment.Gold > 0, "champions always drop gold, and walking over it collects it");
    }

    [Fact]
    public void Dropping_from_the_bag_puts_it_on_the_ground()
    {
        var world = World();
        world.Hero.Equipment.TryAdd(new Item(Content.Item("dagger"), Rarity.Common, 1));
        Assert.True(world.DropFromBag(0));
        Assert.Single(world.GroundItems);
        Assert.Null(world.Hero.Equipment.Bag[0]);
    }
}
