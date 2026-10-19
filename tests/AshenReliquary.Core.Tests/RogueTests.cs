using AshenReliquary.Core.Abilities;
using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>
/// M3 step 2: the Rogue. Momentum, sneak attack against off-guard targets, Hide in Shadows, Tumble, versatile weapons,
/// the Cutthroat's Gutting Strike and ambush, the Trickster's Snare and feinting Tumble.
/// </summary>
public class RogueTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static ZoneDefinition Field(params SpawnDefinition[] spawns) => new(
        "field", "Field", 1, "tileset_grassland", Enumerable.Repeat("........................", 16).ToArray(),
        new Dictionary<string, LegendEntry> { ["."] = new("grass") },
        PlayerStart: [3, 8], spawns, Shrines: [], Exits: []);

    private static GameWorld World(Character hero, params SpawnDefinition[] spawns)
    {
        var world = new GameWorld(Field(spawns), Content.Creatures, hero, 5, Content.AbilityBar(hero.Class.Id), Content.Loot,
            abilities: Content.Abilities);
        world.Hero.Equipment.Equip(new Item(Content.Item("shortsword"), Rarity.Common, 1));
        return world;
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    private static SpawnDefinition At(double x, double y, string creature = "fen_zombie", CreatureRank rank = CreatureRank.Elite) =>
        new([(int)x, (int)y], [new PackMember(creature, rank, 1)], Radius: 0);

    [Fact]
    public void The_rogue_is_a_dexterity_class_with_momentum_and_sneak_attack()
    {
        var rogue = TestCharacters.Rogue();
        Assert.Equal(AttributeKind.Dexterity, rogue.KeyAttribute);
        Assert.Equal(["cutthroat", "trickster"], rogue.SubclassOptions.Select(s => s.Id));
        Assert.Equal(1, rogue.SneakAttackDice);
        Assert.Equal(0, TestCharacters.Fighter().SneakAttackDice);
        Assert.Equal(2, TestCharacters.AtLevel(TestCharacters.Rogue(), 5).SneakAttackDice);
        var world = World(rogue);
        Assert.Equal(ResourceKind.Momentum, world.Hero.Resource.Kind);
        Assert.Equal(0, world.Hero.Resource.Current);
        Assert.Equal(["rogue_strike", "sneak_strike", "tumble", "hide"], world.Bar.Values.Select(a => a.Id));
        Assert.Equal(AttributeKind.Dexterity, rogue.AttackAttribute(rogue.Weapon)); // the shortsword is finesse
    }

    [Fact]
    public void Momentum_builds_as_the_rogue_moves_up_to_a_running_start_and_drains_standing_still()
    {
        var world = World(TestCharacters.Rogue());
        world.MoveTo(20, 8);
        Run(world, 3);
        var momentum = world.Hero.Resource.Current;
        Assert.Equal(ClassResource.MomentumRunningStart, momentum, 3);   // out of a fight, only a running start
        world.Stop();
        Run(world, ClassResource.MomentumDecayDelay + 1.5);
        Assert.True(world.Hero.Resource.Current < momentum);
        // In a fight, movement builds past it.
        world.Hero.Resource.OnCombat();
        world.MoveTo(3, 8);
        Run(world, 2);
        Assert.True(world.Hero.Resource.Current > ClassResource.MomentumRunningStart);
    }

    [Fact]
    public void Sneak_attack_lands_on_an_unaware_or_off_guard_target_and_not_on_one_that_sees_the_blow()
    {
        var world = World(TestCharacters.Rogue(), At(10, 8));
        var zombie = world.Monsters.Single();
        var sneak = typeof(GameWorld).GetMethod("SneakAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        (int, int) Check(bool feint) => ((int, int))sneak.Invoke(world, [zombie, feint])!;

        Assert.Equal(MonsterState.Idle, zombie.State);           // hasn't seen the rogue: off-guard, −2 AC, 1d6
        Assert.Equal((-2, 1), Check(false));
        zombie.State = MonsterState.Chase;
        Assert.Equal((0, 0), Check(false));                      // aware: nothing
        Assert.Equal((-2, 1), Check(true));                      // a feint
        zombie.Creature.Conditions.Apply(ConditionKind.Prone, 1, 2);
        Assert.Equal((0, 1), Check(false));                      // prone: the condition already lowers its AC
        world.Hero.Equipment.Equip(new Item(Content.Item("club"), Rarity.Common, 1));
        Assert.Equal((0, 0), Check(false));                      // a club is neither agile nor finesse
    }

    [Fact]
    public void Hiding_lets_the_rogue_walk_up_to_a_pack_unseen_and_its_first_blow_comes_from_the_shadows()
    {
        var world = World(TestCharacters.Rogue("cutthroat"), At(9, 8));
        var zombie = world.Monsters.Single();
        world.UseSlot(world.SlotOf("hide")!, null);
        Assert.True(world.Hero.IsHidden);
        world.MoveTo(zombie.X - 1.3, zombie.Y);
        Run(world, 4);
        Assert.Equal(MonsterState.Idle, zombie.State);           // 1.3 tiles away, inside its 7, and it hasn't noticed
        Assert.True(world.Hero.IsHidden);
        var before = zombie.Creature.HitPoints;
        world.Attack(zombie);
        Run(world, 0.6);
        Assert.False(world.Hero.IsHidden);                       // the blow brings the rogue out
        Assert.NotEqual(MonsterState.Idle, zombie.State);
        Assert.True(zombie.Creature.HitPoints < before || zombie.IsDead);
    }

    [Fact]
    public void A_cutthroat_s_hit_from_hiding_is_a_critical_hit()
    {
        var hits = 0;
        var crits = 0;
        for (var seed = 1; seed <= 20; seed++)
        {
            var hero = TestCharacters.Rogue("cutthroat");
            var world = new GameWorld(Field(At(5, 8)), Content.Creatures, hero, seed, Content.AbilityBar("rogue"), Content.Loot,
                abilities: Content.Abilities);
            world.Hero.Equipment.Equip(new Item(Content.Item("shortsword"), Rarity.Common, 1));
            var zombie = world.Monsters.Single();
            (world.Hero.X, world.Hero.Y) = (zombie.X - 1.2, zombie.Y);
            world.UseSlot(world.SlotOf("hide")!, null);
            world.Attack(zombie);
            Run(world, 0.5);
            var struck = world.Texts.Where(t => t.Kind is FloatingTextKind.DamageDealt or FloatingTextKind.Critical).ToList();
            if (struck.Count > 0)
            {
                hits++;
                if (struck.All(t => t.Kind == FloatingTextKind.Critical))
                    crits++;
            }
        }
        Assert.True(hits > 10);
        Assert.Equal(hits, crits);                               // every hit (not graze) out of hiding was a critical
    }

    [Fact]
    public void Hiding_shakes_off_hunters_further_off_but_never_one_close_by()
    {
        var hero = TestCharacters.Rogue();
        var world = World(hero, At(8, 8, rank: CreatureRank.Normal), At(4, 9, rank: CreatureRank.Normal));
        var far = world.Monsters.MaxBy(m => m.DistanceTo(world.Hero))!;
        var near = world.Monsters.MinBy(m => m.DistanceTo(world.Hero))!;
        Run(world, 0.2);
        Assert.All(world.Monsters, m => Assert.NotEqual(MonsterState.Idle, m.State));
        // Hide repeatedly (its cooldown aside) until the far one has lost the rogue: a Stealth check each time.
        for (var i = 0; i < 20 && far.State != MonsterState.Idle; i++)
        {
            world.Hero.ReadyAt.Clear();
            world.UseSlot(world.SlotOf("hide")!, null);
        }
        Assert.Equal(MonsterState.Idle, far.State);
        Assert.NotEqual(MonsterState.Idle, near.State);
    }

    [Fact]
    public void Tumble_rolls_toward_the_pointer_through_an_enemy_untouchable_and_builds_momentum()
    {
        var world = World(TestCharacters.Rogue(), At(5, 8, rank: CreatureRank.Normal));
        world.Hero.Resource.OnCombat();
        var start = world.Hero.Resource.Current;
        var (x, y) = (world.Hero.X, world.Hero.Y);
        world.UseSlot(world.SlotOf("tumble")!, null, (x + 10, y));
        Assert.True(world.Hero.IsDodging);
        Run(world, 0.2);
        Assert.True(world.Hero.IsDodging);
        Run(world, 0.3);
        Assert.InRange(world.Hero.X - x, 3.0, 3.6);
        Assert.Equal(3.5 * ClassResource.MomentumPerTile, world.Hero.Resource.Current - start, 3); // 3.5 tiles, 4 a tile
        Assert.True(world.CooldownRemaining(world.Bar[world.SlotOf("tumble")!]) > 0);
    }

    [Fact]
    public void A_trickster_s_tumble_leaves_the_enemies_it_passes_off_guard()
    {
        var world = World(TestCharacters.Rogue("trickster"), At(5, 8, rank: CreatureRank.Normal));
        var zombie = world.Monsters.Single();
        world.UseSlot(world.SlotOf("tumble")!, null, (world.Hero.X + 10, world.Hero.Y));
        Run(world, 0.4);
        Assert.True(zombie.Creature.Conditions.Has(ConditionKind.OffGuard));
    }

    [Fact]
    public void A_snare_arms_springs_on_the_first_enemy_to_step_close_and_holds_it()
    {
        var hero = TestCharacters.Rogue("trickster");
        var world = World(hero, At(9, 8, rank: CreatureRank.Normal));
        world.Hero.Resource.Restore(100);
        var slot = world.SlotOf("snare")!;
        world.UseSlot(slot, null);
        Assert.Single(world.Traps);
        var trap = world.Traps[0];
        var zombie = world.Monsters.Single();
        world.MoveTo(1, 8);
        Run(world, 1);
        world.Stop();
        // The zombie comes for the rogue and walks over the snare.
        Run(world, 12);
        Assert.Empty(world.Traps);
        Assert.True(zombie.Creature.HitPoints < zombie.Creature.Stats.HitPoints || zombie.IsDead);
        Assert.True(trap.IsArmed(world.Time));
    }

    [Fact]
    public void Only_two_snares_lie_set_at_once()
    {
        var world = World(TestCharacters.Rogue("trickster"));
        world.Hero.Resource.Restore(100);
        for (var i = 0; i < 3; i++)
        {
            world.Hero.ReadyAt.Clear();
            world.UseSlot(world.SlotOf("snare")!, null);
        }
        Assert.Equal(GameWorld.MaxTraps, world.Traps.Count);
    }

    [Fact]
    public void Gutting_strike_opens_a_bleeding_wound_and_deep_cuts_deepens_it()
    {
        var gutting = Content.Abilities["gutting_strike"];
        Assert.Equal(new Dice(1, 6), gutting.WeaponPersistentAt(1));
        Assert.Equal(new Dice(2, 6), gutting.WeaponPersistentAt(5));
        var hero = TestCharacters.AtLevel(TestCharacters.Rogue("cutthroat"), 2);
        hero.Choose(2, new LevelChoice(ClassFeat: "deep_cuts", SkillFeat: hero.FeatOptions(FeatType.Skill, 2).First().Id));
        Assert.Equal("2d6", hero.Modify(gutting).PersistentDice);

        var world = World(TestCharacters.Rogue("cutthroat"), At(4, 8));
        world.Hero.Resource.Restore(100);
        var zombie = world.Monsters.Single();
        for (var i = 0; i < 10 && zombie.Creature.Persistent.DiceFor(DamageType.Bleed) is null; i++)
        {
            world.UseSlot(world.SlotOf("gutting_strike")!, zombie);
            Run(world, 1);
            world.Hero.Resource.Restore(100);
        }
        Assert.NotNull(zombie.Creature.Persistent.DiceFor(DamageType.Bleed));
    }

    [Fact]
    public void A_rogue_adds_dexterity_to_damage_with_a_finesse_weapon_as_a_thief_does()
    {
        var rogue = TestCharacters.Rogue();
        rogue.Weapon = new Weapon(Content.Item("shortsword").AsWeapon());
        Assert.Equal(4, rogue.DamageBonus().Total);              // DEX +4, not STR +1
        rogue.Weapon = new Weapon(Content.Item("club").AsWeapon());
        Assert.Equal(1, rogue.DamageBonus().Total);              // a club isn't finesse
        var fighter = TestCharacters.Fighter();
        fighter.Weapon = new Weapon(Content.Item("dagger").AsWeapon());
        Assert.Equal(fighter.Attributes[AttributeKind.Strength], fighter.DamageBonus().Total); // only the rogue
    }

    [Fact]
    public void A_versatile_blade_cuts_where_its_point_is_resisted()
    {
        var shortsword = Content.Item("shortsword").AsWeapon();
        var knight = new DamageProfile().Resist(DamageType.Piercing, 3);
        Assert.Equal(DamageType.Slashing, shortsword.TypeAgainst(knight));
        Assert.Equal(DamageType.Piercing, shortsword.TypeAgainst(new DamageProfile()));
        Assert.Equal(DamageType.Piercing, shortsword.TypeAgainst(new DamageProfile().Resist(DamageType.Slashing, 1)));
        Assert.Equal(DamageType.Bludgeoning, Content.Item("club").AsWeapon().TypeAgainst(knight));
    }

    [Fact]
    public void Weapons_without_agile_or_finesse_are_a_poor_fit_for_a_rogue()
    {
        var rogue = TestCharacters.Rogue();
        var gear = new Equipment(rogue);
        Assert.Equal(ItemFit.PoorFit, ItemJudge.Judge(rogue, gear, new Item(Content.Item("greatsword"), Rarity.Common, 1), false).Fit);
        Assert.Equal(ItemFit.PoorFit, ItemJudge.Judge(rogue, gear, new Item(Content.Item("wooden_shield"), Rarity.Common, 1), false).Fit);
        Assert.Equal(ItemFit.Usable, ItemJudge.Judge(rogue, gear, new Item(Content.Item("dagger"), Rarity.Common, 1), false).Fit);
    }
}
