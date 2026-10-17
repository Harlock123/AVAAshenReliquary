using AshenReliquary.Core.Content;
using AshenReliquary.Core.World;

namespace AshenReliquary.Core.Tests;

public class SoundTests
{
    private static ContentDatabase Content => ContentDatabase.Embedded;

    private static GameWorld Arena(params SpawnDefinition[] spawns) => new(
        new ZoneDefinition("a", "A", 1, "tileset_grassland", Enumerable.Repeat("..............", 14).ToArray(),
            new Dictionary<string, LegendEntry> { ["."] = new("grass") }, [2, 2], spawns, [], []),
        Content.Creatures, TestCharacters.Fighter(), 9, Content.AbilityBar("fighter"), Content.Loot);

    [Fact]
    public void A_fight_is_heard()
    {
        var world = Arena(new SpawnDefinition([5, 5], [new PackMember("fen_zombie")], 0));
        var zombie = world.Monsters.Single();
        var heard = new List<SoundEvent>();
        world.Attack(zombie);
        for (var i = 0; i < 900 && !zombie.IsDead; i++)
        {
            world.Update(1 / 30.0);
            heard.AddRange(world.DrainSounds());
        }
        Assert.Contains(heard, s => s.Cue == SoundCue.MonsterAggro && s.Variant == "zombie");
        Assert.Contains(heard, s => s.Cue == SoundCue.Swing);
        Assert.Contains(heard, s => s.Cue is SoundCue.Hit or SoundCue.Crit);
        var death = Assert.Single(heard, s => s.Cue == SoundCue.MonsterDeath);
        Assert.Equal("zombie", death.Variant);
        Assert.Equal((zombie.X, zombie.Y), (death.X, death.Y));
    }

    [Fact]
    public void Drinking_and_picking_up_are_heard_and_draining_empties_the_list()
    {
        var world = Arena();
        world.Hero.Character.TakeDamage(5);
        world.DrinkPotion();
        Assert.Contains(world.DrainSounds(), s => s.Cue == SoundCue.Potion);
        Assert.Empty(world.DrainSounds());
    }

    [Fact]
    public void Undrained_sounds_never_grow_without_limit()
    {
        var world = Arena();
        for (var i = 0; i < 600; i++)
        {
            world.Hero.Character.Potion.Refill();
            world.Hero.Character.TakeDamage(1);
            world.Update(9);
            world.DrinkPotion();
        }
        Assert.True(world.DrainSounds().Count <= 256);
    }
}
