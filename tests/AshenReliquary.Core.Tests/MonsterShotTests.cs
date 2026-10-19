using AshenReliquary.Core.Content;
using AshenReliquary.Core.Creatures;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

/// <summary>A ranged monster's blow flies (an archer's arrow, a caster's bolt) and is rolled when it arrives.</summary>
public class MonsterShotTests
{
    private static ContentDatabase Content => TestCharacters.Content;

    private static GameWorld Field(string creature)
    {
        var zone = new ZoneDefinition("field", "Field", 2, "tileset_grassland", Enumerable.Repeat("..............................", 16).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, PlayerStart: [3, 8],
            [new SpawnDefinition([9, 8], [new PackMember(creature, CreatureRank.Normal, 1)], Radius: 0)], Shrines: [], Exits: []);
        var world = new GameWorld(zone, Content.Creatures, TestCharacters.Fighter(), 5, Content.AbilityBar("fighter"), Content.Loot, Content.Zones,
            abilities: Content.Abilities);
        world.Hero.Character.CannotFall = true;
        return world;
    }

    [Theory]
    [InlineData("bone_archer", false)]
    [InlineData("bog_witch", true)]
    public void A_ranged_monster_s_shot_is_seen_in_flight_before_it_lands(string creature, bool spell)
    {
        var world = Field(creature);
        world.Attack(world.Monsters.Single());
        world.Stop();
        var hp = world.Hero.Character.HitPoints;
        MonsterShot? seen = null;
        for (var t = 0.0; t < 10 && seen is null; t += 1 / 30.0)
        {
            world.Update(1 / 30.0);
            seen = world.MonsterShots.FirstOrDefault();
            if (seen is not null)
                Assert.Equal(hp, world.Hero.Character.HitPoints);              // not yet struck
        }
        Assert.NotNull(seen);
        Assert.Equal(spell, seen!.IsSpell);
        Assert.Equal(world.Hero, seen.Victim);
        for (var t = 0.0; t < 2 && world.MonsterShots.Contains(seen); t += 1 / 30.0)
            world.Update(1 / 30.0);
        Assert.DoesNotContain(seen, world.MonsterShots);                      // it landed (a hit or a miss)
    }

    [Fact]
    public void A_step_timed_against_an_arrow_dodges_it()
    {
        var world = Field("bone_archer");
        world.Attack(world.Monsters.Single());
        world.Stop();
        for (var t = 0.0; t < 10 && world.MonsterShots.Count == 0; t += 1 / 30.0)
            world.Update(1 / 30.0);
        var shot = world.MonsterShots.Single();
        // Step just before it arrives.
        while (Math.Sqrt(Math.Pow(shot.X - world.Hero.X, 2) + Math.Pow(shot.Y - world.Hero.Y, 2)) > 1.5)
            world.Update(1 / 60.0);
        var hp = world.Hero.Character.HitPoints;
        Assert.True(world.Dodge(world.Hero.X, world.Hero.Y + 5));
        for (var t = 0.0; t < 0.3; t += 1 / 60.0)
            world.Update(1 / 60.0);
        Assert.DoesNotContain(shot, world.MonsterShots);
        Assert.Contains(world.Texts, x => x.Text == "dodged");
        Assert.Equal(hp, world.Hero.Character.HitPoints);
    }
}
