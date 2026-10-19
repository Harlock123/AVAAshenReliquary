using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>M3 step 9: a boss fights whoever hurts it most lately (the hero weighted half again), not whoever is nearest.</summary>
public class ThreatTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static readonly System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    [Fact]
    public void A_boss_turns_on_whoever_hurts_it_most_and_forgets_in_time()
    {
        var zone = new ZoneDefinition("field", "Field", 4, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 8],
            [new SpawnDefinition([9, 8], [new PackMember("mother_sallow", CreatureRank.Herald, 1)], Radius: 0)], Shrines: [], Exits: []);
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Ranger("beastmaster"), 5, Content.AbilityBar("ranger"), Content.Loot,
            Content.Zones, abilities: Content.Abilities);
        var sallow = world.Monsters.Single();
        var wolf = world.Companion!;
        (wolf.X, wolf.Y) = (sallow.X - 1, sallow.Y);                    // the wolf at her side, the hero 6 tiles off
        var victimOf = typeof(GameWorld).GetMethod("VictimOf", Private)!;
        var damage = typeof(GameWorld).GetMethod("DamageMonster", Private)!;
        var dealer = typeof(GameWorld).GetField("_dealer", Private)!;
        Actor Victim() => (Actor)victimOf.Invoke(world, [sallow, 1.0])!;

        Assert.Equal(world.Hero, Victim());                                // no threat yet: the hero, however near the wolf
        dealer.SetValue(world, wolf);
        damage.Invoke(world, [sallow, 12]);
        dealer.SetValue(world, null);
        damage.Invoke(world, [sallow, 10]);                                 // 10 × 1.5 beats the wolf's 12
        Assert.Equal(world.Hero, Victim());
        dealer.SetValue(world, wolf);
        damage.Invoke(world, [sallow, 20]);
        dealer.SetValue(world, null);
        Assert.Equal(wolf, Victim());                                       // now the wolf hurts her most
        for (var i = 0; i < 20; i++)
            victimOf.Invoke(world, [sallow, 1.0]);                          // twenty seconds on, all but forgotten
        damage.Invoke(world, [sallow, 1]);
        Assert.Equal(world.Hero, Victim());
    }
}
