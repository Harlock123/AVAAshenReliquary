using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.Saving;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class SaveTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld NewWorld(int seed = 3)
    {
        var choices = CharacterBuilder.Recommend(Content, "Brunna", "dwarf", "deserter", "fighter", heritageId: "forge-blooded");
        return new GameWorld(Content.Zone("gallows_road"), Content.Creatures, CharacterBuilder.Create(Content, choices), seed,
            Content.AbilityBar("fighter"), Content.Loot);
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    [Fact]
    public void A_game_survives_a_round_trip_through_json()
    {
        var world = NewWorld();
        var hero = world.Hero.Character;
        // Grow the character past a boost level.
        for (var level = 2; level <= 5; level++)
            hero.LevelUp(level == 5 ? [AttributeKind.Strength, AttributeKind.Constitution, AttributeKind.Dexterity, AttributeKind.Charisma] : null);
        // Gear: a rare runed sword worn, an uncommon in the bag, gold.
        var vicious = Content.Affixes.Single(a => a.Id == "vicious");
        var hearth = Content.Affixes.Single(a => a.Id == "hearth");
        world.Hero.Equipment.Equip(new Item(Content.Item("longsword"), Rarity.Rare, 4, [new AffixRoll(vicious, 2), new AffixRoll(hearth, 3)], potency: 1, striking: 1, name: "Ashbite Longsword"));
        world.Hero.Equipment.TryAdd(new Item(Content.Item("leather_boots"), Rarity.Uncommon, 2, [new AffixRoll(Content.Affixes.Single(a => a.Id == "swift"), 7)]));
        world.Hero.Equipment.AddGold(321);
        // State: wounded, tainted, a potion used, some Fury.
        hero.TakeDamage(9);
        hero.Taint.Add(37);
        hero.Potion.Drink(0, hero.Level, new SeededRandom(1));
        world.Hero.Resource.OnCombat();
        // World: two monsters slain, loot on the ground.
        foreach (var m in world.Monsters.Take(2).ToList())
            world.Slay(m);
        Run(world, 0.2);
        Assert.Equal(2, world.Slain.Count);
        var slainBefore = world.Slain.ToList();

        var json = SaveGame.Capture(world, "save-1", 3, "gearhead/dwarf_003", 600).ToJson();
        var loaded = SaveGame.FromJson(json).Restore(Content, seed: 99);
        var c = loaded.Hero.Character;

        Assert.Equal(("Brunna", "dwarf", "forge-blooded", 5), (c.Name, c.Ancestry.Id, c.Heritage!.Id, c.Level));
        Assert.Equal(hero.Attributes.ToString(), c.Attributes.ToString());
        Assert.Equal(hero.MaxHitPoints, c.MaxHitPoints);
        Assert.Equal(hero.HitPoints, c.HitPoints);
        Assert.Equal(hero.ArmorClass().Total, c.ArmorClass().Total);
        Assert.Equal(hero.AttackModifier().Total, c.AttackModifier().Total);
        Assert.Equal(hero.Defenses.Resistances[DamageType.Fire], c.Defenses.Resistances[DamageType.Fire]);
        Assert.Equal(hero.Taint.Value, c.Taint.Value, 6); // 37 slowed by the dwarf's CON
        Assert.Equal(2, c.Potion.Charges);
        Assert.Equal(world.Hero.Resource.Current, loaded.Hero.Resource.Current);
        var sword = loaded.Hero.Equipment.In(EquipSlot.MainHand)!;
        Assert.Equal(("Ashbite Longsword", 1, 1, 2), (sword.Name, sword.Potency, sword.Striking, sword.Affixes.Count));
        Assert.Equal("Swift Leather boots", loaded.Hero.Equipment.Bag[0]!.Name);
        Assert.Equal(321, loaded.Hero.Equipment.Gold);
        Assert.Equal((world.Hero.X, world.Hero.Y), (loaded.Hero.X, loaded.Hero.Y));
        Assert.Equal(slainBefore.Order(), loaded.Slain.Order());
        Assert.Equal(world.Monsters.Count(m => m.SpawnIndex >= 0 && !m.IsDead), loaded.Monsters.Count(m => m.SpawnIndex >= 0));
        Assert.Equal(world.GroundItems.Count, loaded.GroundItems.Count);
    }

    [Fact]
    public void Exploring_reveals_the_map_around_the_hero_and_saves_it()
    {
        var world = NewWorld();
        Run(world, 0.1);
        var (hx, hy) = ((int)world.Hero.X, (int)world.Hero.Y);
        Assert.True(world.IsExplored(hx, hy));
        Assert.True(world.IsExplored(hx + GameWorld.RevealRadius - 1, hy));
        Assert.False(world.IsExplored(hx, hy - 40));
        var version = world.ExploredVersion;
        world.MoveTo(world.Hero.X - 6, world.Hero.Y - 6);
        Run(world, 3);
        Assert.True(world.ExploredVersion > version);

        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        for (var y = 0; y < world.Map.Height; y++)
        for (var x = 0; x < world.Map.Width; x++)
            Assert.Equal(world.IsExplored(x, y), loaded.IsExplored(x, y));
    }

    [Fact]
    public void Shrines_and_the_respawn_point_are_kept()
    {
        var world = NewWorld();
        var shrine = world.Zone.Shrines[0];
        world.MoveTo(shrine.At[0] + 0.5, shrine.At[1] + 0.5);
        Run(world, 8);
        Assert.Contains(shrine.Id, world.ActivatedShrines);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.Contains(shrine.Id, loaded.ActivatedShrines);
        Assert.Equal(world.RespawnPoint, loaded.RespawnPoint);
    }

    [Fact]
    public void Activating_a_shrine_raises_the_autosave_event()
    {
        var world = NewWorld();
        var raised = new List<string>();
        world.ShrineActivated += s => raised.Add(s.Id);
        var shrine = world.Zone.Shrines[0];
        world.MoveTo(shrine.At[0] + 0.5, shrine.At[1] + 0.5);
        Run(world, 8);
        Assert.Equal([shrine.Id], raised);
    }

    [Fact]
    public void An_edited_save_with_an_illegal_build_is_refused()
    {
        var save = SaveGame.Capture(NewWorld(), "s", 3, null);
        var hacked = save with { Choices = save.Choices with { FreeBoosts = [AttributeKind.Strength, AttributeKind.Strength, AttributeKind.Strength, AttributeKind.Strength] } };
        Assert.Throws<RulesException>(() => SaveGame.FromJson(hacked.ToJson()).Restore(Content));
    }

    [Fact]
    public void A_save_from_a_newer_version_is_rejected_cleanly()
    {
        var json = (SaveGame.Capture(NewWorld(), "s", 3, null) with { Version = SaveGame.CurrentVersion + 1 }).ToJson();
        Assert.Throws<InvalidDataException>(() => SaveGame.FromJson(json));
    }

    [Fact]
    public void Saving_while_dead_resumes_at_the_shrine_with_full_hp()
    {
        var world = NewWorld();
        world.Hero.Character.HeroPoints.TrySpend();
        world.Hero.Character.TakeDamage(999);
        var loaded = SaveGame.FromJson(SaveGame.Capture(world, "s", 3, null).ToJson()).Restore(Content);
        Assert.False(loaded.Hero.IsDead);
        Assert.Equal(loaded.Hero.Character.MaxHitPoints, loaded.Hero.Character.HitPoints);
        Assert.Equal(world.RespawnPoint, (loaded.Hero.X, loaded.Hero.Y));
    }
}
