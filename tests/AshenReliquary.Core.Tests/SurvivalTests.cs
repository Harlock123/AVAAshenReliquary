using AshenReliquary.Core.Characters;
using AshenReliquary.Core.Combat;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.Items;
using AshenReliquary.Core.Rules;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class SurvivalTests
{
    [Fact]
    public void Hero_points_are_one_per_zone_up_to_three()
    {
        var points = new HeroPoints();
        for (var i = 0; i < 5; i++)
            points.GainForZone();
        Assert.Equal(3, points.Count);
        Assert.True(points.TrySpend());
        Assert.Equal(2, points.Count);
    }

    [Fact]
    public void Reaching_zero_hp_spends_a_hero_point_to_revive_at_half()
    {
        var c = TestCharacters.Fighter();
        c.HeroPoints.GainForZone();
        Assert.Equal(DeathOutcome.RevivedByHeroPoint, c.TakeDamage(999));
        Assert.Equal(c.MaxHitPoints / 2, c.HitPoints);
        Assert.Equal(0, c.HeroPoints.Count);
    }

    [Fact]
    public void Without_a_hero_point_the_character_dies_until_a_shrine()
    {
        var c = TestCharacters.Fighter();
        Assert.Equal(DeathOutcome.Alive, c.TakeDamage(5));
        Assert.Equal(DeathOutcome.Died, c.TakeDamage(999));
        Assert.True(c.IsDead);
        c.Heal(10);
        Assert.Equal(0, c.HitPoints);
        c.RestAtShrine();
        Assert.False(c.IsDead);
        Assert.Equal(c.MaxHitPoints, c.HitPoints);
    }

    [Fact]
    public void Potions_have_three_charges_an_eight_second_cooldown_and_heal_d8_plus_twice_level()
    {
        var potion = new HealingPotion();
        Assert.Equal(5 + 2 * 3, potion.Drink(0, level: 3, new ScriptedRandom(5)));
        Assert.Null(potion.Drink(7.9, 3, new ScriptedRandom(5)));
        Assert.NotNull(potion.Drink(8.0, 3, new ScriptedRandom(1)));
        Assert.NotNull(potion.Drink(16.0, 3, new ScriptedRandom(1)));
        Assert.Equal(0, potion.Charges);
        Assert.Null(potion.Drink(24.0, 3, new ScriptedRandom(1)));
        potion.Refill();
        Assert.Equal(HealingPotion.MaxCharges, potion.Charges);
    }

    [Fact]
    public void A_draught_fills_an_empty_flask_else_goes_in_the_case_and_the_case_tops_the_flasks_up()
    {
        var potion = new HealingPotion();
        Assert.True(potion.AddCharge());                                       // flasks full: into the case
        Assert.Equal((HealingPotion.MaxCharges, 1), (potion.Charges, potion.Spares));
        Assert.False(potion.TopUp());                                          // no empty flask
        potion.Drink(0, 1, new ScriptedRandom(1));
        Assert.True(potion.AddCharge());                                       // an empty flask first
        Assert.Equal((HealingPotion.MaxCharges, 1), (potion.Charges, potion.Spares));
        potion.Drink(10, 1, new ScriptedRandom(1));
        Assert.True(potion.TopUp());
        Assert.Equal((HealingPotion.MaxCharges, 0), (potion.Charges, potion.Spares));
        for (var i = 0; i < HealingPotion.CaseCapacity; i++)
            Assert.True(potion.AddCharge());
        Assert.True(potion.Full);
        Assert.False(potion.AddCharge());
        potion.Drink(20, 1, new ScriptedRandom(1));
        potion.Refill();                                                       // a shrine fills the flasks, not the case
        Assert.Equal(HealingPotion.CaseCapacity, potion.Spares);
    }

    [Fact]
    public void Sickened_characters_cannot_drink()
    {
        var conditions = new ConditionSet();
        conditions.Apply(ConditionKind.Sickened, 1);
        Assert.Null(new HealingPotion().Drink(0, 1, new ScriptedRandom(4), conditions));
    }

    [Fact]
    public void Taint_gives_minus_one_to_saves_from_fifty_and_whispers_at_a_hundred()
    {
        var taint = new TaintMeter();
        taint.Add(49);
        Assert.Null(taint.SavePenalty);
        taint.Add(1);
        Assert.Equal(-1, taint.SavePenalty?.Value);
        Assert.True(taint.Add(60));
        Assert.Equal(TaintMeter.WhispersSeconds, taint.ReversedMovementRemaining);
        Assert.Equal(TaintMeter.PenaltyThreshold, taint.Value);
        taint.Update(2.0);
        Assert.Equal(0, taint.ReversedMovementRemaining);
    }

    [Fact]
    public void Ashborn_and_constitution_slow_taint_down()
    {
        Assert.Equal(0.75, TaintMeter.RateFor(0.75, 0), 6);
        Assert.Equal(0.9, TaintMeter.RateFor(1.0, 2), 6);
        Assert.Equal(0.5, TaintMeter.RateFor(1.0, 20), 6);
    }

    [Fact]
    public void Taint_penalty_reaches_the_characters_saves()
    {
        var c = TestCharacters.Fighter();
        var will = c.Save(StatisticKind.Will).Total;
        c.Taint.Add(100);
        Assert.Equal(will - 1, c.Save(StatisticKind.Will).Total);
    }

    private static GameWorld Field(params SpawnDefinition[] spawns)
    {
        var content = TestCharacters.Content;
        var zone = new ZoneDefinition("field", "Field", 1, "tileset_grassland", Enumerable.Repeat("....................", 12).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 6], spawns, Shrines: [], Exits: []);
        return new GameWorld(zone, content.Creatures, TestCharacters.Fighter(), 5, content.AbilityBar("fighter"), content.Loot, abilities: content.Abilities);
    }

    private static void Run(GameWorld world, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1 / 30.0)
            world.Update(1 / 30.0);
    }

    [Fact]
    public void Out_of_combat_the_draught_case_refills_empty_flasks_and_a_draught_found_full_goes_in_the_case()
    {
        var world = Field(new SpawnDefinition([16, 6], [new PackMember("fen_zombie")], Radius: 0, Level: 1));
        var potion = world.Hero.Character.Potion;
        potion.Restore(1, 3);
        var zombie = world.Monsters.Single();
        zombie.State = MonsterState.Chase;
        Run(world, GameWorld.RegenDelaySeconds + 3);
        Assert.Equal(1, potion.Charges);                                       // a monster awake: no top-up
        world.Slay(zombie);
        Run(world, GameWorld.RegenDelaySeconds + 0.2);
        Assert.Equal(2, potion.Charges);
        Run(world, GameWorld.TopUpSeconds);
        Assert.Equal((3, 1), (potion.Charges, potion.Spares));
        Run(world, GameWorld.TopUpSeconds * 2);
        Assert.Equal((3, 1), (potion.Charges, potion.Spares));                 // the flasks are full: the spare waits

        typeof(GameWorld).GetMethod("Spill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(world, [world.Hero.X + 1, world.Hero.Y, new[] { new LootDrop(null, Draught: true) }]);
        world.PickUp(world.GroundItems.Single(g => g.Drop.Draught));
        Run(world, 2);
        Assert.Equal((3, 2), (potion.Charges, potion.Spares));
    }

    [Fact]
    public void Draughts_drop_less_often_now_the_case_keeps_spares()
    {
        var loot = TestCharacters.Content.Loot;
        var random = new SeededRandom(11);
        double Share(Func<IReadOnlyList<LootDrop>> roll, int n) => Enumerable.Range(0, n).Count(_ => roll().Any(d => d.Draught)) / (double)n;
        Assert.InRange(Share(() => loot.Roll(CreatureRank.Normal, 3, random), 20000), 0.015, 0.025);
        Assert.InRange(Share(() => loot.RollCache(CacheKind.Cache, 3, random), 4000), 0.12, 0.18);
        Assert.InRange(Share(() => loot.RollCache(CacheKind.Reliquary, 3, random), 4000), 0.31, 0.39);
        Assert.Equal(0, Share(() => loot.Roll(CreatureRank.Minion, 3, random), 2000));
    }
}
